using GameProtocol;
using Google.Protobuf;
using K4os.Hash.xxHash;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public delegate void HandlerFunc(ReadOnlySpan<byte> payloadSpan);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UDPHeader {
    // ── 보안 서명 ────────────────────── 8B
    public ulong signature;    // 해싱 전에는 반드시 0
    // ── 기본 필드 ────────────────────── 11B
    public ushort packetId;       // 2B
    public ushort sessionId;      // 2B
    public uint   rSeqNum;        // 4B - reliable 채널 시퀀스 (FLAG_RELIABLE 패킷에만 유효)
    public ushort uSeqNum;        // 2B - unreliable 채널 시퀀스 (나머지 패킷에만 유효)
    public byte   flags;          // 1B
    // ── ACK 필드 ─────────────────────── 8B
    public uint   ackRSeqNum;     // 4B - 수신 확인한 가장 최신 reliable 시퀀스 번호
    public uint   ackBitfield;    // 4B - 이전 32개 reliable 패킷 수신 여부
    // ── 타임스탬프 ───────────────────── 8B
    public uint   timestamp;      // 4B - 송신 시각 (ms)
    public uint   timestampEcho;  // 4B - 상대방 timestamp 반사 (RTT 계산용)

    public const int Size = 35;
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

    // ── Reliable 재전송 링 버퍼 ────────────────────────
    private const float MIN_RTO_MS     = 300f;   // RTO 하한 (ms)
    private const int   MAX_RETRY      = 10;     // 최대 재전송 횟수
    private const int   WINDOW_SIZE    = 32;     // ACK bitfield 32비트와 일치
    private const int   MAX_PACKET_SIZE = 1400;  // 이더넷 MTU 기준 안전 최대치

    // ── RTT 추정 (RFC 6298 EWMA) ──────────────────────
    private float _srtt            = 0f;
    private float _rttvar          = 0f;
    private bool  _rttInitialized  = false;
    private float CurrentRto => _rttInitialized
        ? Mathf.Max(MIN_RTO_MS, _srtt + 4f * _rttvar)
        : MIN_RTO_MS;

    private struct PendingSlot {
        public bool   inUse;
        public uint   seqNum;
        public float  sentAtMs;
        public int    retryCount;
        public int    dataLength;
        public byte[] data;  // 생성자에서 new byte[MAX_PACKET_SIZE] 할당
    }
    private PendingSlot[] _pendingSlots;
    private byte[]        _unreliableScratch;
    private List<(byte[] data, int length)> _retransmitCache = new List<(byte[], int)>(WINDOW_SIZE);

    // ── 핸들러 ─────────────────────────────────────────
    private Dictionary<ushort, HandlerFunc> _handlers = new Dictionary<ushort, HandlerFunc>();

    public PacketHandler() {
        _pendingSlots = new PendingSlot[WINDOW_SIZE];
        for (int i = 0; i < WINDOW_SIZE; i++)
            _pendingSlots[i].data = new byte[MAX_PACKET_SIZE];
        _unreliableScratch = new byte[MAX_PACKET_SIZE];

        _handlers.Add((ushort)PktId.D2CResponseChannelOpen, Handle_D2CResponseChannelOpen);
        _handlers.Add((ushort)PktId.D2CHeartBeat, Handle_D2CHeartBeat);
        _handlers.Add((ushort)PktId.D2CResponseBlueprintStaticObjects, Handle_D2CResponseBlueprintStaticObjects);
        _handlers.Add((ushort)PktId.D2CResponseSpawnMeSpawnSpot, Handle_D2CResponseSpawnMeSpawnSpot);
        _handlers.Add((ushort)PktId.D2CResponseSpawnMeDynamicObjects, Handle_D2CResponseSpawnMeDynamicObjects);
        _handlers.Add((ushort)PktId.D2CResponseSpawnByObjectId, Handle_D2CResponseSpawnByObjectId);
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

    public void Reset() {
        _sessionId           = 0;
        _securityKey         = 0;
        _rSeqNum             = 0;
        _uSeqNum             = 0;
        _recvAckRSeqNum      = 0;
        _recvAckBitfield     = 0;
        _hasReceivedReliable = false;
        _timestampEcho       = 0;
        _srtt                = 0f;
        _rttvar              = 0f;
        _rttInitialized      = false;

        for (int i = 0; i < WINDOW_SIZE; i++)
            _pendingSlots[i].inUse = false;
    }

    // ==========================================
    // 송신 패킷 생성
    // ==========================================

    private int BuildPacketInto(ushort packetId, bool reliable, IMessage proto, Span<byte> dest) {
        uint nowMs = (uint)(Time.realtimeSinceStartup * 1000f);

        byte flags = reliable ? UDPFlags.FLAG_RELIABLE : (byte)0;
        flags |= UDPFlags.FLAG_HAS_ACK;

        UDPHeader hdr = new UDPHeader {
            signature     = 0, 
            packetId      = packetId,
            sessionId     = (ushort)_sessionId,
            rSeqNum       = reliable ? _rSeqNum : 0u,
            uSeqNum       = reliable ? (ushort)0 : _uSeqNum,
            flags         = flags,
            ackRSeqNum    = _hasReceivedReliable ? _recvAckRSeqNum  : 0u,
            ackBitfield   = _hasReceivedReliable ? _recvAckBitfield : 0u,
            timestamp     = nowMs,
            timestampEcho = _timestampEcho,
        };

        MemoryMarshal.Write(dest.Slice(0, UDPHeader.Size), ref hdr);

        int payloadLen = 0;

        payloadLen = proto.CalculateSize();
        if (payloadLen > 0)
            proto.WriteTo(dest.Slice(UDPHeader.Size, payloadLen));

        int totalSize = UDPHeader.Size + payloadLen;

        Span<uint> secKeySpan = stackalloc uint[1] { _securityKey };
        Span<byte> secKeyBytes = MemoryMarshal.AsBytes(secKeySpan);
        Span<byte> packetData = dest.Slice(0, totalSize);

        XXH64.State hashState = default;
        XXH64.Reset(ref hashState, 0); // Seed: 0

        // 해시 믹서에 데이터 투입
        XXH64.Update(ref hashState, packetData);
        XXH64.Update(ref hashState, secKeyBytes);

        // 최종 서명 추출
        ulong calculatedSignature = XXH64.Digest(in hashState);

        // 생성된 서명을 헤더 영역에 직접 덮어쓰기
        MemoryMarshal.Write(dest.Slice(0, 8), ref calculatedSignature);

        return totalSize;
    }

    public (byte[] data, int length) MakeReliablePacket(ushort packetId, IMessage proto) {
        int slotIndex = (int)(_rSeqNum % WINDOW_SIZE);
        ref PendingSlot slot = ref _pendingSlots[slotIndex];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (slot.inUse)
            Util.LogWarning($"[PacketHandler] 슬롯 {slotIndex} 덮어쓰기 — in-flight 패킷 32개 초과");
#endif

        slot.inUse      = true;
        slot.seqNum     = _rSeqNum;
        slot.sentAtMs   = Time.realtimeSinceStartup * 1000f;
        slot.retryCount = 0;
        slot.dataLength = BuildPacketInto(packetId, true, proto, slot.data);

        _rSeqNum++;
        return (slot.data, slot.dataLength);
    }

    public (byte[] data, int length) MakeUnreliablePacket(ushort packetId, IMessage proto) {
        int len = BuildPacketInto(packetId, false, proto, _unreliableScratch);
        _uSeqNum++;
        return (_unreliableScratch, len);
    }

    // ==========================================
    // 재전송 큐 관리 (메인 스레드에서 호출)
    // ==========================================

    public List<(byte[] data, int length)> CollectRetransmits(float nowMs, out bool shouldDisconnect) {
        shouldDisconnect = false;
        _retransmitCache.Clear();

        float rto = CurrentRto;
        for (int i = 0; i < WINDOW_SIZE; i++) {
            ref PendingSlot slot = ref _pendingSlots[i];
            if (!slot.inUse) continue;
            if (nowMs - slot.sentAtMs < rto) continue;

            if (slot.retryCount >= MAX_RETRY) {
                shouldDisconnect = true;
                return _retransmitCache;
            }

            slot.retryCount++;
            slot.sentAtMs = nowMs;
            _retransmitCache.Add((slot.data, slot.dataLength));
        }
        return _retransmitCache;
    }

    // ==========================================
    // 수신 패킷 처리
    // ==========================================

    private bool VerifySignature(byte[] buf, int length) {
        Span<byte> packetSpan = buf.AsSpan(0, length);
        Span<byte> sigSpan = packetSpan.Slice(0, 8);

        ulong originalSig = MemoryMarshal.Read<ulong>(sigSpan);

        ulong zero = 0;
        MemoryMarshal.Write(sigSpan, ref zero);

        Span<uint> secKeySpan = stackalloc uint[1] { _securityKey };
        Span<byte> secKeyBytes = MemoryMarshal.AsBytes(secKeySpan);

        XXH64.State hashState = default;
        XXH64.Reset(ref hashState, 0);
        XXH64.Update(ref hashState, packetSpan); 
        XXH64.Update(ref hashState, secKeyBytes);
        ulong expected = XXH64.Digest(hashState);

        MemoryMarshal.Write(sigSpan, ref originalSig);

        return originalSig == expected;
    }

    public void ProcessReceivedPacket(byte[] buf, int length) {
        if (length < UDPHeader.Size) return;

        if (!VerifySignature(buf, length)) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogWarning($"[PacketHandler] signature 검증 실패 — 패킷 드롭 (length: {length})");
            });
            return;
        }

        ReadOnlySpan<byte> packetSpan = buf.AsSpan(0, length);
        UDPHeader header = MemoryMarshal.Read<UDPHeader>(packetSpan.Slice(0, UDPHeader.Size));
        ReadOnlySpan<byte> payloadSpan = packetSpan.Slice(UDPHeader.Size);

        // 서버가 우리 reliable 패킷을 확인한 ACK → pending 슬롯 정리
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

        // 서버가 우리 timestamp를 에코해 준 경우 → RTT 샘플 수집
        if (header.timestampEcho != 0) {
            uint echoed = header.timestampEcho;
            Managers.ExecuteAtMainThread(() => UpdateRtt(echoed));
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

    // 메인 스레드 전용: 서버가 확인한 우리 reliable 패킷 슬롯 해제
    private void ProcessAck(uint ackSeq, uint bitfield) {
        ClearSlot(ackSeq);
        for (int i = 0; i < 32; i++) {
            if ((bitfield & (1u << i)) != 0)
                ClearSlot(ackSeq - (uint)(i + 1));
        }
    }

    private void ClearSlot(uint seqNum) {
        int idx = (int)(seqNum % WINDOW_SIZE);
        ref PendingSlot slot = ref _pendingSlots[idx];
        if (slot.inUse && slot.seqNum == seqNum)
            slot.inUse = false;
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

    // 메인 스레드 전용: RTT 샘플 반영 (RFC 6298 EWMA)
    private void UpdateRtt(uint echoedTs) {
        uint nowMs = (uint)(Time.realtimeSinceStartup * 1000f);
    
        // 비정상적인 시간 역전 방어 (클라이언트 렉 등)
        if (nowMs < echoedTs) return; 

        float rtt = (float)(nowMs - echoedTs);

        if (!_rttInitialized) {
            _srtt = rtt;
            _rttvar = rtt / 2f;
            _rttInitialized = true;
        } else {
            // RFC 6298 EWMA 공식 (alpha = 0.125, beta = 0.25)
            float delta = rtt - _srtt;
            _srtt += 0.125f * delta;
            _rttvar = 0.75f * _rttvar + 0.25f * Mathf.Abs(delta);
        }
    }

    // worker thread 전용: 
    public Quaternion DecompressQuaternion(uint compressedQuat) {
        uint maxIndex = (compressedQuat >> 30) & 0x03;
        float a = UnpackFloat((compressedQuat >> 20) & 0x3FF);
        float b = UnpackFloat((compressedQuat >> 10) & 0x3FF);
        float c = UnpackFloat(compressedQuat & 0x3FF);

        float d = Mathf.Sqrt(Mathf.Max(1.0f - (a * a + b * b + c * c), 0.0f));

        float x = 0f, y = 0f, z = 0f, w = 0f;
        if (maxIndex == 0) { x = d; y = a; z = b; w = c; }
        else if (maxIndex == 1) { x = a; y = d; z = b; w = c; }
        else if (maxIndex == 2) { x = a; y = b; z = d; w = c; }
        else { x = a; y = b; z = c; w = d; }

        return new Quaternion(x, y, z, w);
    }

    private float UnpackFloat(uint val) {
        float mapped = (val / 511.5f) - 1.0f;
        return mapped * 0.70710678f;
    }

    // ====================
    // 핸들러 함수
    // ====================

    private void Handle_D2CResponseChannelOpen(ReadOnlySpan<byte> payloadSpan) {
        D2CResponseChannelOpen pkt = null;

        try {
            pkt = D2CResponseChannelOpen.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseChannelOpen 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseChannelOpen 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        Managers.ExecuteAtMainThread(() => {
            Util.Log($"[PacketHandler] D2CResponseChannelOpen 수신 - Message: {pkt.Echo}");
        });
    }

    private void Handle_D2CHeartBeat(ReadOnlySpan<byte> payloadSpan) {
        // 서버가 heartbeat에 응답함 — 연결 살아있음 확인. 별도 처리 없음.
    }

    private void Handle_D2CResponseBlueprintStaticObjects(ReadOnlySpan<byte> payloadSpan) {
        D2CResponseBlueprintStaticObjects pkt = null;

        try {
            pkt = D2CResponseBlueprintStaticObjects.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseBlueprintStaticObjects 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseBlueprintStaticObjects 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        var objects = new List<ObjectData>(pkt.IngameObjects.Count);
        foreach (var obj in pkt.IngameObjects) {
            TransformInfo tf = obj.Transform;
            GameProtocol.Vector3 pos = tf?.Position;
            
            Quaternion finalRotation = Quaternion.identity;

            if (tf != null) {
                if (tf.RotationCase == TransformInfo.RotationOneofCase.CompressedQuat) {
                    finalRotation = DecompressQuaternion(tf.CompressedQuat);
                } 
                else if (tf.RotationCase == TransformInfo.RotationOneofCase.YawAngle) {
                    finalRotation = Quaternion.Euler(0f, tf.YawAngle, 0f);
                }
            }

            objects.Add(new ObjectData {
                ObjectId   = obj.ObjectId,
                ObjectType = obj.ObjectType,
                Position   = new UnityEngine.Vector3(pos?.X ?? 0f, pos?.Y ?? 0f, pos?.Z ?? 0f),
                Rotation   = finalRotation
            });
        }

        Managers.ExecuteAtMainThread(() => {
            BaseScene scene = Managers.Scene.CurrentScene;
            if (scene is LoadingScene loadingScene) {
                Managers.Scene.NextSceneStaticContext.AddObjectDatas(pkt.Index, pkt.IsLast, objects);
                Util.Log($"[PacketHandler] D2CResponseBlueprintStaticObjects 수신 - index: {pkt.Index}, isLast: {pkt.IsLast}, count: {pkt.IngameObjects.Count}");
                loadingScene.TryCompleteBlueprint();
            }
        });
    }

    private void Handle_D2CResponseSpawnMeSpawnSpot(ReadOnlySpan<byte> payloadSpan) {
        D2CResponseSpawnMeSpawnSpot pkt = null;

        try {
            pkt = D2CResponseSpawnMeSpawnSpot.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnMeSpawnSpot 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnMeSpawnSpot 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        Managers.ExecuteAtMainThread(() => {
            BaseScene scene = Managers.Scene.CurrentScene;
            if (scene is IngameScene ingameScene) {
                UnityEngine.Vector3 spawnPoint = new() {
                    x = pkt.SpawnPoint?.X ?? 0f,
                    y = pkt.SpawnPoint?.Y ?? 0f,
                    z = pkt.SpawnPoint?.Z ?? 0f
                };
                ingameScene.HandleSpawnSpot(spawnPoint, pkt.CharacterType, pkt.ObjectId);
            }
        });
    }

    private void Handle_D2CResponseSpawnMeDynamicObjects(ReadOnlySpan<byte> payloadSpan) {
        D2CResponseSpawnMeDynamicObjects pkt = null;

        try {
            pkt = D2CResponseSpawnMeDynamicObjects.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnMeDynamicObjects 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnMeDynamicObjects 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        var objects = new List<ObjectData>(pkt.IngameObjects.Count);
        foreach (var obj in pkt.IngameObjects) {
            TransformInfo tf = obj.Transform;
            GameProtocol.Vector3 pos = tf?.Position;
            
            Quaternion finalRotation = Quaternion.identity;

            if (tf != null) {
                if (tf.RotationCase == TransformInfo.RotationOneofCase.CompressedQuat) {
                    finalRotation = DecompressQuaternion(tf.CompressedQuat);
                } 
                else if (tf.RotationCase == TransformInfo.RotationOneofCase.YawAngle) {
                    finalRotation = Quaternion.Euler(0f, tf.YawAngle, 0f);
                }
            }

            objects.Add(new ObjectData {
                ObjectId   = obj.ObjectId,
                ObjectType = obj.ObjectType,
                Position   = new UnityEngine.Vector3(pos?.X ?? 0f, pos?.Y ?? 0f, pos?.Z ?? 0f),
                Rotation   = finalRotation
            });
        }

        Managers.ExecuteAtMainThread(() => {
            BaseScene scene = Managers.Scene.CurrentScene;
            if (scene is IngameScene ingameScene) {
                Managers.Scene.SceneDynamicContext.AddObjectDatas(pkt.Index, pkt.IsLast, objects);
                Util.Log($"[PacketHandler] D2CResponseSpawnMeDynamicObjects 수신 - index: {pkt.Index}, isLast: {pkt.IsLast}, count: {pkt.IngameObjects.Count}");
                ingameScene.TryCompleteSpawnMe();
            }
        });
    }

    private void Handle_D2CResponseSpawnByObjectId(ReadOnlySpan<byte> payloadSpan) {
        D2CResponseSpawnByObjectId pkt = null;

        try {
            pkt = D2CResponseSpawnByObjectId.Parser.ParseFrom(payloadSpan);
        }
        catch (InvalidProtocolBufferException e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnByObjectId 파싱 실패: {e.Message}"); });
            return;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"D2CResponseSpawnByObjectId 처리 중 알 수 없는 에러: {e.Message}"); });
            return;
        }

        UnityGameObject obj = pkt.GameObject;
        TransformInfo tf = obj?.Transform;
        GameProtocol.Vector3 pos = tf?.Position;

        Quaternion rotation = Quaternion.identity;
        if (tf != null) {
            if (tf.RotationCase == TransformInfo.RotationOneofCase.CompressedQuat)
                rotation = DecompressQuaternion(tf.CompressedQuat);
            else if (tf.RotationCase == TransformInfo.RotationOneofCase.YawAngle)
                rotation = Quaternion.Euler(0f, tf.YawAngle, 0f);
        }

        ObjectData data = new ObjectData {
            ObjectId   = obj?.ObjectId ?? 0,
            ObjectType = obj?.ObjectType ?? 0,
            Position   = new UnityEngine.Vector3(pos?.X ?? 0f, pos?.Y ?? 0f, pos?.Z ?? 0f),
            Rotation   = rotation
        };

        Managers.ExecuteAtMainThread(() => {
            if (Managers.Scene.CurrentScene is not IngameScene) return;
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        });
    }
}
