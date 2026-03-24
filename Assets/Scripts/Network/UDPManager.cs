using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
// using Google.Protobuf;
// using GameProtocol;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UDPHeader {
    public ushort packetId;
    public ushort sessionId;
    public uint sequenceNum;
    public uint securityKey;
    public byte flags;

    public const int Size = 13;
}

public class UdpManager {
    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;

    private Thread _receiveThread;
    private volatile bool _isRunning;

    // 1. 서버 접속 및 수신 대기 시작
    public void ConnectToServer(string ip, int port, string roomToken) {
        try {
            Disconnect(); // 기존 연결 및 스레드 정리

            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            // Connect를 호출해 목적지 IP/Port 고정 (OS 커널 필터링)
            _udpClient.Connect(_serverEndPoint);

            _isRunning = true;

            // 전용 수신 스레드 생성 및 실행
            _receiveThread = new Thread(ReceiveLoopSync);
            _receiveThread.IsBackground = true; // 유니티 메인 스레드 종료 시 함께 강제 종료됨
            _receiveThread.Name = "UDP_Receive_Thread";
            _receiveThread.Start();

            Util.Log($"[UDP] 서버({ip}:{port}) 접속 및 전용 수신 스레드 시작");

            // 입장 패킷 전송
            //SendJoinPacket(roomToken);
        }
        catch (Exception e) {
            Util.LogError($"[UDP] 접속 실패: {e.Message}");
        }
    }

    private void ProcessReceivedPacket(byte[] rawData) {
        // 최소 헤더 사이즈 검증
        if (rawData.Length < UDPHeader.Size) {
            Util.LogWarning("[UDP] 유효하지 않은 패킷 (크기 미달)");
            return;
        }

        ReadOnlySpan<byte> packetSpan = rawData;

        // reinterpret_cast 처럼 바이트 배열을 그대로 구조체로 읽기
        UDPHeader header = MemoryMarshal.Read<UDPHeader>(packetSpan.Slice(0, UDPHeader.Size));

        // 페이로드 추출
        ReadOnlySpan<byte> payloadSpan = packetSpan.Slice(UDPHeader.Size);

        Util.Log($"[UDP 수신] PacketID: {header.packetId}, Seq: {header.sequenceNum}, Payload 크기: {payloadSpan.Length} bytes");

        // 4. Protobuf 역직렬화 (예시)
        // var message = MyProtoMessage.Parser.ParseFrom(payloadSpan.ToArray()); 
        // (주의: Protobuf-net이나 Google.Protobuf 최신 버전은 ReadOnlySpan/ReadOnlySequence 파싱을 지원하여 ToArray() 복사마저 없앨 수 있습니다)
    }

    // ==========================================
    // [송신] 구조체 & Protobuf -> 바이트 배열 조립
    // ==========================================
    private byte[] CreatePacket(UDPHeader header, byte[] protobufPayload) {
        // 헤더 크기(13) + 페이로드 크기만큼 배열 할당
        byte[] packetBuffer = new byte[UDPHeader.Size + protobufPayload.Length];
        Span<byte> packetSpan = packetBuffer;

        // 1. 헤더 구조체를 바이트 배열 앞부분에 메모리 복사 (No GC)
        MemoryMarshal.Write(packetSpan.Slice(0, UDPHeader.Size), ref header);

        // 2. Protobuf 페이로드를 그 뒷부분에 복사
        if (protobufPayload.Length > 0) {
            protobufPayload.CopyTo(packetSpan.Slice(UDPHeader.Size));
        }

        return packetBuffer;
    }

    // 3. 비동기 데이터 수신 루프 (백그라운드에서 계속 돎)
    private void ReceiveLoopSync() {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

        while (_isRunning) {
            try {
                // 핵심: Receive()는 패킷이 도착할 때까지 스레드를 그 자리에 멈춰 세웁니다 (Block).
                // CPU 점유율을 0%로 유지하다가, 패킷이 닿는 즉시 0.001초의 딜레이도 없이 깨어납니다!
                byte[] receivedData = _udpClient.Receive(ref sender);

                // No-GC 초고속 파싱 함수로 전달 (이 작업 역시 전용 스레드에서 진행됨)
                ProcessReceivedPacket(receivedData);
            }
            catch (SocketException e) {
                // 팁: Disconnect()에서 _udpClient.Close()를 호출하면, 
                // 대기 중이던 Receive()가 강제로 깨어나며 SocketException을 던집니다.
                // 이것이 블로킹 소켓 루프를 가장 우아하게 종료하는 정석적인 방법입니다.
                if (_isRunning)
                {
                    Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP 소켓 에러] {e.Message}"));
                }
                break; // while 루프 탈출
            }
            catch (ThreadAbortException)
            {
                break; // 스레드 강제 종료 시 탈출
            }
            catch (Exception e)
            {
                Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP 수신 루프 에러] {e.Message}"));
            }
        }

        // 루프를 빠져나오면 스레드는 자연스럽게 수명을 다하고 소멸합니다.
        Util.Log("[UDP] 전용 수신 스레드 안전하게 종료됨");
    }

    public void SendPacket(byte[] data) {
        if (_udpClient == null) return;

        try {
            _udpClient.SendAsync(data, data.Length);
        }
        catch (Exception e) {
            Util.LogError($"[UDP 전송 에러] {e.Message}");
        }
    }

    public void Disconnect() {
        _isRunning = false; // 루프 종료 플래그

        if (_udpClient != null) {
            // 기존 thread가 SocketException을 던지며 깨어남.
            _udpClient.Close();
            _udpClient = null;
        }

        if (_receiveThread != null && _receiveThread.IsAlive) {
            _receiveThread = null;
        }

        Util.Log("[UDP] 소켓 닫힘 및 자원 정리 완료");
    }
}