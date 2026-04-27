using GameProtocol;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public delegate void HandlerFunc(ReadOnlySpan<byte> payloadSpan);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UDPHeader {
    public ushort packetId;       // 2B
    public ushort sessionId;      // 2B
    public uint   rSeqNum;        // 4B - reliable 채널 시퀀스 (FLAG_RELIABLE 패킷에만 유효)
    public ushort uSeqNum;        // 2B - unreliable 채널 시퀀스 (나머지 패킷에만 유효)
    public uint   securityKey;    // 4B
    public byte   flags;          // 1B
    public uint   ackRSeqNum;     // 4B - 수신 확인한 가장 최신 reliable 시퀀스 번호
    public uint   ackBitfield;    // 4B - 이전 32개 reliable 패킷 수신 여부
    public uint   timestamp;      // 4B - 송신 시각 (ms)
    public uint   timestampEcho;  // 4B - 상대방 timestamp 반사 (RTT 계산용)

    public const int Size = 31;
}

public static class UDPFlags {
    public const byte FLAG_HAS_ACK    = 0x01;  // ackRSeqNum / ackBitfield 유효
    public const byte FLAG_RELIABLE   = 0x02;  // 이 패킷은 ACK를 요구함 (재전송 대상)
    public const byte FLAG_FRAGMENTED = 0x04;  // 예약 - 미사용
}

public class PacketHandler {
    // ── 세션 ───────────────────────────────────────────
    private ushort _sessionId   = 0;
    private uint   _securityKey = 0;

    // ── 송신 시퀀스 ────────────────────────────────────
    private uint   _rSeqNum = 0;  // reliable 채널
    private ushort _uSeqNum = 0;  // unreliable 채널

    // ── 수신 ACK 상태 (서버 reliable 패킷에 대한 우리 측 ACK, 다음 송신에 piggybacked) ──
    private uint _recvAckRSeqNum  = 0;
    private uint _recvAckBitfield = 0;
    private bool _hasReceivedReliable = false;

    // ── 타임스탬프 에코 (서버의 timestamp를 그대로 돌려줌) ──
    private uint _timestampEcho = 0;

    // ── Reliable 재전송 큐 ─────────────────────────────
    private const float RTO_MS    = 100f;  // 재전송 타임아웃 (ms)
    private const int   MAX_RETRY = 10;    // 최대 재전송 횟수
    private struct PendingEntry {
        public byte[] data;
        public float  sentAtMs;
        public int    retryCount;
    }
    private Dictionary<uint, PendingEntry> _pendingReliable = new Dictionary<uint, PendingEntry>();

    // ── 핸들러 ─────────────────────────────────────────
    private Dictionary<ushort, HandlerFunc> _handlers = new Dictionary<ushort, HandlerFunc>();

    public PacketHandler() {
        _handlers.Add((ushort)PktId.D2CTestPkt, Handle_D2CTestPkt);
    }

    // ==========================================
    // 세션 초기화
    // ==========================================

    public void SetSessionVariable(ushort sessionId, uint securityKey) {
        _sessionId   = sessionId;
        _securityKey = securityKey;
        _rSeqNum     = 1;
        _uSeqNum     = 1;
    }

    // ==========================================
    // 송신 패킷 생성
    // ==========================================

    private byte[] BuildPacket(ushort packetId, bool reliable, IMessage proto) {
        byte[] payload = proto != null ? proto.ToByteArray() : Array.Empty<byte>();
        uint   nowMs   = (uint)(Time.realtimeSinceStartup * 1000f);

        byte flags = reliable ? UDPFlags.FLAG_RELIABLE : (byte)0;
        if (_hasReceivedReliable) flags |= UDPFlags.FLAG_HAS_ACK;

        UDPHeader hdr = new UDPHeader {
            packetId      = packetId,
            sessionId     = _sessionId,
            rSeqNum       = reliable ? _rSeqNum : 0,
            uSeqNum       = reliable ? (ushort)0 : _uSeqNum,
            securityKey   = _securityKey,
            flags         = flags,
            ackRSeqNum    = _hasReceivedReliable ? _recvAckRSeqNum  : 0,
            ackBitfield   = _hasReceivedReliable ? _recvAckBitfield : 0,
            timestamp     = nowMs,
            timestampEcho = _timestampEcho,
        };

        byte[] packet = new byte[UDPHeader.Size + payload.Length];
        Span<byte> span = packet;
        MemoryMarshal.Write(span.Slice(0, UDPHeader.Size), ref hdr);
        if (payload.Length > 0) payload.CopyTo(span.Slice(UDPHeader.Size));

        return packet;
    }

    public byte[] MakeReliablePacket(ushort packetId, IMessage proto) {
        byte[] data  = BuildPacket(packetId, reliable: true, proto);
        float  nowMs = Time.realtimeSinceStartup * 1000f;
        _pendingReliable[_rSeqNum] = new PendingEntry { data = data, sentAtMs = nowMs, retryCount = 0 };
        _rSeqNum++;
        return data;
    }

    public byte[] MakeUnreliablePacket(ushort packetId, IMessage proto) {
        byte[] data = BuildPacket(packetId, reliable: false, proto);
        _uSeqNum++;
        return data;
    }

    // ==========================================
    // 재전송 큐 관리 (메인 스레드에서 호출)
    // ==========================================

    public List<byte[]> CollectRetransmits(float nowMs, out bool shouldDisconnect) {
        shouldDisconnect = false;
        var result = new List<byte[]>();

        foreach (var key in new List<uint>(_pendingReliable.Keys)) {
            var entry = _pendingReliable[key];
            if (nowMs - entry.sentAtMs < RTO_MS) continue;

            if (entry.retryCount >= MAX_RETRY) {
                shouldDisconnect = true;
                return result;
            }

            entry.retryCount++;
            entry.sentAtMs = nowMs;
            _pendingReliable[key] = entry;
            result.Add(entry.data);
        }
        return result;
    }

    // ==========================================
    // 수신 패킷 처리
    // ==========================================

    public void ProcessReceivedPacket(byte[] receivedBytes) {
        if (receivedBytes.Length < UDPHeader.Size) return;

        ReadOnlySpan<byte> packetSpan = receivedBytes;
        UDPHeader header = MemoryMarshal.Read<UDPHeader>(packetSpan.Slice(0, UDPHeader.Size));
        ReadOnlySpan<byte> payloadSpan = packetSpan.Slice(UDPHeader.Size);

        // 서버가 우리 reliable 패킷을 확인한 ACK → pending 큐 정리
        if ((header.flags & UDPFlags.FLAG_HAS_ACK) != 0) {
            uint ack = header.ackRSeqNum;
            uint bf  = header.ackBitfield;
            Managers.ExecuteAtMainThread(() => ProcessAck(ack, bf));
        }

        // 서버가 보낸 reliable 패킷 → 우리 측 ACK 상태 갱신 (다음 송신에 piggybacked)
        if ((header.flags & UDPFlags.FLAG_RELIABLE) != 0) {
            uint serverRSeq = header.rSeqNum;
            uint serverTs   = header.timestamp;
            Managers.ExecuteAtMainThread(() => UpdateRecvAckState(serverRSeq, serverTs));
        }

        ushort id = header.packetId;
        try {
            if (_handlers.TryGetValue(id, out HandlerFunc handler)) {
                handler(payloadSpan);
            }
            else {
                Managers.ExecuteAtMainThread(() => { Util.LogWarning($"[PacketHandler] 등록되지 않은 패킷 ID: {id}"); });
            }
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"[PacketHandler] 패킷 파싱/처리 중 에러 발생 (ID: {id}) - {e.Message}"); });
        }
    }

    // 메인 스레드 전용: 서버가 확인한 우리 reliable 패킷 제거
    private void ProcessAck(uint ackSeq, uint bitfield) {
        _pendingReliable.Remove(ackSeq);

        for (int i = 0; i < 32; i++) {
            if ((bitfield & (1u << i)) != 0) {
                uint seq = ackSeq - (uint)(i + 1);
                _pendingReliable.Remove(seq);
            }
        }
    }

    // 메인 스레드 전용: 서버 reliable 패킷 수신 기록 → 다음 송신에 piggybacked할 ACK 갱신
    private void UpdateRecvAckState(uint serverRSeq, uint serverTs) {
        _timestampEcho       = serverTs;
        _hasReceivedReliable = true;

        if (serverRSeq > _recvAckRSeqNum) {
            uint diff = serverRSeq - _recvAckRSeqNum;
            _recvAckBitfield = diff < 32
                ? (_recvAckBitfield << (int)diff) | (1u << ((int)diff - 1))
                : 0;
            _recvAckRSeqNum = serverRSeq;
        }
        else if (serverRSeq < _recvAckRSeqNum) {
            uint diff = _recvAckRSeqNum - serverRSeq;
            if (diff <= 32) _recvAckBitfield |= (1u << ((int)diff - 1));
        }
        // serverRSeq == _recvAckRSeqNum: 중복 수신, 무시
    }

    // ====================
    // 핸들러 함수
    // ====================

    private void Handle_D2CTestPkt(ReadOnlySpan<byte> payloadSpan) {
        D2CTestPkt pkt = null;

        try {
            pkt = D2CTestPkt.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CTestPkt 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CTestPkt 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        Managers.ExecuteAtMainThread(() => {
            Util.Log($"[PacketHandler] D2CTestPkt 수신 - Message: {pkt.Echo}");
        });
    }
}
