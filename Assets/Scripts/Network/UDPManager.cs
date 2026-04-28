using GameProtocol;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class UDPManager {
    private Socket _socket;
    private IPEndPoint _serverEndPoint;

    private Thread _receiveThread;
    private volatile bool _isRunning;

    // 수신 전용 사전 할당 버퍼 — Receive 호출마다 new byte[] 생성을 방지
    private readonly byte[] _recvBuf = new byte[1500];

    // 송신 큐 — 메인 스레드에서 삽입, 워커 스레드에서 소진
    private readonly ConcurrentQueue<(byte[] data, int length)> _sendQueue
        = new ConcurrentQueue<(byte[], int)>();

    public PacketHandler Handler { get; private set; } = new PacketHandler();

    public void RegisterEndPointAndStart(string ip, int port) {
        try {
            Disconnect(); // 기존 연결 및 스레드 정리

            if (IPAddress.TryParse(ip, out IPAddress ipAddr) == false) {
                Managers.ExecuteAtMainThread(() => Util.LogError($"RegisterServerEndPoint : 올바르지 않은 IP 형식 ({ip})"));
                return;
            }

            _serverEndPoint = new IPEndPoint(ipAddr, port);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Connect를 호출해 목적지 IP/Port 고정 (OS 커널 필터링)
            _socket.Connect(_serverEndPoint);

            var localEp = (IPEndPoint)_socket.LocalEndPoint;

            _isRunning = true;

            // 전용 워커 스레드 생성 및 실행 (수신 + 큐 송신 담당)
            _receiveThread = new Thread(NetworkLoopSync);
            _receiveThread.IsBackground = true;
            _receiveThread.Name = "UDP_Network_Thread";
            _receiveThread.Start();
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => { Util.LogError($"RegisterServerEndPoint : {e.Message}"); });
        }
    }

    // 워커 스레드: Poll(1ms) 기반으로 수신 처리 + 송신 큐 소진을 반복
    private void NetworkLoopSync() {
        while (_isRunning) {
            try {
                // 최대 1ms 대기 후 수신 데이터 유무 확인 (블로킹 없이 루프 유지)
                if (_socket.Poll(1000, SelectMode.SelectRead)) {
                    int len = _socket.Receive(_recvBuf);
                    Handler.ProcessReceivedPacket(_recvBuf, len);
                }
                DrainSendQueue();
            }
            catch (SocketException e) {
                if (_isRunning) {
                    Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP] 소켓 에러: {e.Message}"));
                }
                break;
            }
            catch (ObjectDisposedException) {
                // Disconnect() 후 Poll이 소켓 해제 상태에서 호출된 경우 — 정상 종료
                break;
            }
            catch (ThreadAbortException) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("[UDP] 워커 스레드가 강제 종료되었습니다."));
                break;
            }
            catch (Exception e) {
                Managers.ExecuteAtMainThread(() => Util.LogError($"[UDP 루프 에러] {e.Message}"));
            }
        }
        Managers.ExecuteAtMainThread(() => { Util.Log("[UDP] 워커 스레드 안전하게 종료됨"); });
    }

    // 워커 스레드 전용 — 큐에 쌓인 송신 요청을 모두 처리
    private void DrainSendQueue() {
        Socket socket = _socket;
        if (socket == null) return;
        while (_sendQueue.TryDequeue(out var item)) {
            try {
                socket.Send(item.data, 0, item.length, SocketFlags.None);
            }
            catch (ObjectDisposedException) { break; } // Disconnect와 동시 처리 방어
            catch (Exception e) {
                Managers.ExecuteAtMainThread(() => { Util.LogError($"[UDP 전송 에러] {e.Message}"); });
            }
        }
    }

    // 메인 스레드 호출 — 송신 큐에 삽입만 하고 즉시 반환
    public void SendPacket(byte[] data, int length) {
        if (_socket == null) return;
        _sendQueue.Enqueue((data, length));
    }

    public void SendReliable(ushort packetId, IMessage proto) {
        var (data, length) = Handler.MakeReliablePacket(packetId, proto);
        SendPacket(data, length);
    }

    public void SendUnreliable(ushort packetId, IMessage proto) {
        var (data, length) = Handler.MakeUnreliablePacket(packetId, proto);
        SendPacket(data, length);
    }

    // 메인 스레드 — 재전송 대상을 큐에 삽입 (실제 송신은 워커 스레드)
    public void OnUpdate() {
        if (_socket == null) return;
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
        _isRunning = false; // 루프 종료 플래그 — Poll 루프가 최대 1ms 안에 자연 종료

        if (_receiveThread != null && _receiveThread.IsAlive) {
            _receiveThread.Join(TimeSpan.FromSeconds(2));
            _receiveThread = null;
        }

        if (_socket != null) {
            _socket.Close();
            _socket = null;
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