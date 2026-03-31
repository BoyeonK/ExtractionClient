using GameProtocol;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

//TODO : Span과 ArraySegment 차이 이해하기
public delegate void HandlerFunc(ReadOnlySpan<byte> payloadSpan);

public class PacketHandler {
    private ushort _sessionId = 0; // TODO : 실제 세션 관리 로직 필요
    private UInt32 _sendSequence = 0;
    private UInt32 _securityKey = 0; // TODO : 실제 보안 키 관리 로직 필요
    private Dictionary<ushort, HandlerFunc> _handlers = new Dictionary<ushort, HandlerFunc>();

    public PacketHandler()  {
        _handlers.Add((ushort)PktId.D2CTestPkt, Handle_D2CTestPkt);
    }

    // ==========================================
    // 패킷 분해 및 라우팅
    // ==========================================

    public void SetSessionVariable(ushort sessionId, UInt32 securityKey) {
        _sessionId = sessionId;
        _securityKey = securityKey;
        _sendSequence = 1;
    }

    public void ProcessReceivedPacket(byte[] receivedBytes) {
        if (receivedBytes.Length < UDPHeader.Size) {
            // 헤더 크기보다 작은 패킷 무시
            return;
        }

        ReadOnlySpan<byte> packetSpan = receivedBytes;
        UDPHeader header = MemoryMarshal.Read<UDPHeader>(packetSpan.Slice(0, UDPHeader.Size));
        ReadOnlySpan<byte> payloadSpan = packetSpan.Slice(UDPHeader.Size);
        ushort id = header.packetId;

        try {
            if (_handlers.TryGetValue(id, out HandlerFunc handler)) {
                handler(payloadSpan);
            }
            else {
                Util.LogWarning($"[PacketHandler] 등록되지 않은 패킷 ID: {id}");
            }
        }
        catch (Exception e) {
            Util.LogError($"[PacketHandler] 패킷 파싱/처리 중 에러 발생 (ID: {id}) - {e.Message}");
        }
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
            Util.LogError($"D2CTestPkt 파싱 실패: {e.Message}");
            return;
        }
        catch (Exception e) {
            Util.LogError($"D2CTestPkt 처리 중 알 수 없는 에러: {e.Message}");
            return;
        }

        // MainThread에서 실행되어야 할 내용
        Managers.ExecuteAtMainThread(() => {

        });
    }


    // ====================
    // 송신 패킷 생성
    // ====================
    private byte[] CreatePacketInternal(ushort packetId, ushort sessionId, UInt32 sequenceNum, UInt32 securityKey, byte flag, IMessage protobufMessage) {
        byte[] payload = protobufMessage != null ? protobufMessage.ToByteArray() : Array.Empty<byte>();

        // 13바이트 헤더 세팅
        UDPHeader header = new UDPHeader {
            packetId = packetId,
            sessionId = sessionId,
            sequenceNum = sequenceNum,
            securityKey = securityKey,
            flags = flag,
        };

        // 최종 패킷 버퍼 할당 (헤더 크기 + 페이로드 크기)
        byte[] finalPacket = new byte[UDPHeader.Size + payload.Length];
        Span<byte> packetSpan = finalPacket;

        // Zero-Allocation 메모리 복사 (구조체 덮어쓰기)
        MemoryMarshal.Write(packetSpan.Slice(0, UDPHeader.Size), ref header);

        // 뒷부분에 페이로드 복사
        if (payload.Length > 0) {
            payload.CopyTo(packetSpan.Slice(UDPHeader.Size));
        }

        return finalPacket;
    }

    public byte[] MakeSendBuffer(GameProtocol.C2DTestPkt packet) {
        return CreatePacketInternal(
            packetId: (ushort)GameProtocol.PktId.C2DTestPkt,
            sessionId: 0,
            sequenceNum: _sendSequence++,
            securityKey: 0,
            flag: 0x01,
            protobufMessage: packet
        );
    }
}
