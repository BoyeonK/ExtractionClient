using GameProtocol;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class UDPManager {
    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;

    private Thread _receiveThread;
    private volatile bool _isRunning;
    public PacketHandler Handler { get; private set; } = new PacketHandler();

    public void RegisterEndPointAndStart(string ip, int port) {
        try {
            Disconnect(); // 기존 연결 및 스레드 정리

            if (IPAddress.TryParse(ip, out IPAddress ipAddr) == false) {
                Managers.ExecuteAtMainThread(() => Util.LogError($"RegisterServerEndPoint : 올바르지 않은 IP 형식 ({ip})"));
                return;
            }

            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(ipAddr, port);

            // Connect를 호출해 목적지 IP/Port 고정 (OS 커널 필터링)
            _udpClient.Connect(_serverEndPoint);

            var localEp = (IPEndPoint)_udpClient.Client.LocalEndPoint;

            _isRunning = true;

            // 전용 수신 스레드 생성 및 실행
            _receiveThread = new Thread(ReceiveLoopSync);
            _receiveThread.IsBackground = true;
            _receiveThread.Name = "UDP_Receive_Thread";
            _receiveThread.Start();
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"RegisterServerEndPoint : {e.Message}"); });
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
                // Disconnect()에서 _udpClient.Close()를 호출하면, 대기 중이던 Receive()가 강제로 깨어나며 SocketException을 던집니다.
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
        Managers.ExecuteAtMainThread(() => { Util.Log("[UDP] 전용 수신 스레드 안전하게 종료됨"); });
    }


    public void SendPacket(byte[] data, int length) {
        UdpClient client = _udpClient; // 로컬 변수에 캡처
        if (client == null) return;
        try {
            client.Send(data, length);
        }
        catch (ObjectDisposedException) { } // Disconnect와 동시 호출 방어
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"[UDP 전송 에러] {e.Message}"); });
        }
    }

    public void SendReliable(ushort packetId, IMessage proto) {
        var (data, length) = Handler.MakeReliablePacket(packetId, proto);
        SendPacket(data, length);
    }

    public void SendUnreliable(ushort packetId, IMessage proto) {
        var (data, length) = Handler.MakeUnreliablePacket(packetId, proto);
        SendPacket(data, length);
    }

    public void OnUpdate() {
        if (_udpClient == null) return;
        float nowMs = Time.realtimeSinceStartup * 1000f;
        var retransmits = Handler.CollectRetransmits(nowMs, out bool shouldDisconnect);
        if (shouldDisconnect) {
            Util.LogError("[UDP] 재전송 한도 초과. 연결 종료.");
            Disconnect();
            return;
        }
        foreach (var (data, length) in retransmits) SendPacket(data, length);
    }

    public void Disconnect() {
        _isRunning = false; // 루프 종료 플래그

        if (_udpClient != null) {
            // 기존 thread가 SocketException을 던지며 깨어남.
            _udpClient.Close();
            _udpClient = null;
        }

        if (_receiveThread != null && _receiveThread.IsAlive) {
            _receiveThread.Join(TimeSpan.FromSeconds(2));
            _receiveThread = null;
        }
    }

    // ====================
    // 게임 로직 함수
    // ====================
    public void RequestSessionIdAndSecurityKey() {
        C2DTestPkt testPkt = new C2DTestPkt {
            Echo = "Hello, UDP Server! Let me in!"
        };
        SendReliable((ushort)GameProtocol.PktId.C2DTestPkt, testPkt);
    }
}