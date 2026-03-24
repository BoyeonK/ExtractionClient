using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Google.Protobuf; // Protobuf 네임스페이스
using GameProtocol; // protoc로 뽑아낸 C# 클래스들이 있는 네임스페이스

public class PacketHandler {
    private ushort _sessionId = 0; // TODO : 실제 세션 관리 로직 필요
    private UInt32 _sendSequence = 0;
    private UInt32 _securityKey = 0; // TODO : 실제 보안 키 관리 로직 필요

    // ==========================================
    // [수신] 패킷 분해 및 라우팅
    // ==========================================

    public void ProcessReceivedPacket(byte[] rawData) {
        if (rawData.Length < UDPHeader.Size) {
            Util.LogWarning("[PacketHandler] 유효하지 않은 패킷 (헤더 크기 미달)");
            return;
        }

        ReadOnlySpan<byte> packetSpan = rawData;
        UDPHeader header = MemoryMarshal.Read<UDPHeader>(packetSpan.Slice(0, UDPHeader.Size));
        ReadOnlySpan<byte> payloadSpan = packetSpan.Slice(UDPHeader.Size);
        ushort id = header.packetId;

        try {
            // TODO : packetId에 따른 패킷 핸들러 호출
        }
        catch (Exception e) {
            Util.LogError($"[PacketHandler] 패킷 파싱/처리 중 에러 발생 (ID: {id}) - {e.Message}");
        }
    }

    // ====================
    // [송신] 패킷 생성
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
