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
    public PacketHandler Handler { get; private set; } = new PacketHandler();

    public void RegisterEndPointAndStart(string ip, int port, string roomToken) {
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

            Util.Log($"RegisterServerEndPoint : 서버({ip}:{port}) 접속 및 전용 수신 스레드 시작");
        }
        catch (Exception e) {
            Util.LogError($"RegisterServerEndPoint : {e.Message}");
        }
    }

    // Worker thread에서 실행되는 수신 루프 함수.
    private void ReceiveLoopSync() {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

        while (_isRunning) {
            try {
                byte[] receivedData = _udpClient.Receive(ref sender);

                // TODO : 패킷 핸들러 실행
            }
            catch (SocketException e) {
                // 팁: Disconnect()에서 _udpClient.Close()를 호출하면, 
                // 대기 중이던 Receive()가 강제로 깨어나며 SocketException을 던집니다.
                if (_isRunning) {
                    Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP 소켓 에러] {e.Message}"));
                }
                break;
            }
            catch (ThreadAbortException) {
                break; // 스레드 강제 종료 시 탈출
            }
            catch (Exception e) {
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