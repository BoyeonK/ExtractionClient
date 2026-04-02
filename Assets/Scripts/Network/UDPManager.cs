using GameProtocol;
using System;
using System.Net;
using System.Net.Sockets;
// using Google.Protobuf;
// using GameProtocol;
using System.Runtime.InteropServices;
using System.Threading;
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

public class UDPManager {
    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;

    private Thread _receiveThread;
    private volatile bool _isRunning;
    public PacketHandler Handler { get; private set; } = new PacketHandler();

    public void RegisterEndPointAndStart(string ip, int port) {
        try {
            Disconnect(); // 기존 연결 및 스레드 정리

            // 💡 수정: port는 이미 int이므로 검사 불필요
            if (IPAddress.TryParse(ip, out IPAddress ipAddr) == false) {
                Util.LogWarning($"RegisterServerEndPoint : 올바르지 않은 IP 형식 ({ip})");
                return;
            }

            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(ipAddr, port);

            // Connect를 호출해 목적지 IP/Port 고정 (OS 커널 필터링)
            _udpClient.Connect(_serverEndPoint);

            _isRunning = true;

            // 전용 수신 스레드 생성 및 실행
            _receiveThread = new Thread(ReceiveLoopSync);
            _receiveThread.IsBackground = true;
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

                Handler.ProcessReceivedPacket(receivedData);
            }
            catch (SocketException e) {
                // 팁: Disconnect()에서 _udpClient.Close()를 호출하면, 
                // 대기 중이던 Receive()가 강제로 깨어나며 SocketException을 던집니다.
                if (_isRunning) {
                    Managers.ExecuteAtMainThread(() => Util.LogError($"Disconnect: {e.Message}"));
                }
                break;
            }
            catch (ThreadAbortException) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning($"[UDP 수신 루프] 스레드가 강제 종료되었습니다."));
                break;
            }
            catch (Exception e) {
                Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP 수신 루프 에러] {e.Message}"));
            }
        }

        Util.Log("[UDP] 전용 수신 스레드 안전하게 종료됨");
    }


    public void SendPacket(byte[] data) {
        if (_udpClient == null) return;

        try {
            _udpClient.Send(data, data.Length);
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
    }

    // ====================
    // 게임 로직 함수
    // ====================
    public void RequestSessionIdAndSecurityKey() {
        // 1. Protobuf 페이로드 생성
        C2DTestPkt testPkt = new C2DTestPkt {
            Echo = "Hello, UDP Server! Let me in!"
        };

        // 2. 패킷 핸들러에게 조립(헤더 씌우기)을 부탁함
        byte[] sendBuffer = Handler.MakeSendBuffer(testPkt);

        // 3. 만들어진 바이트 배열을 전송
        SendPacket(sendBuffer);
    }
}