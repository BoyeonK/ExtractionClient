using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ServerCore {
	public abstract class PacketSession : Session {
		public static readonly int HeaderSize = 2;

		//public bool IsConnected { get; private set; } = false;와 동일.
		private bool _isConnected = false;
		public bool IsConnected	{
			get => _isConnected;
			set { _isConnected = value; }
		}

		private byte[] _aesKey;
		//_aesKey는, 사실상 public으로 열어 놓은 것과 같아 보이지만,
		//이렇게 작성함으로서 _aesKey의 변화는 반드시 setter함수를 통해 이루어지며,
		//나중에 생길 요구사항 변화에 좀 더 유연하게 대응할 수 있다. (유효성 검사 등의 로직 추가 등)
		public byte[] AESKey {
			get => _aesKey;
			set	{
				if (value == null || value.Length != 32)
					throw new ArgumentException("AES 키는 반드시 32바이트여야 합니다.");
				_aesKey = value;
			}
		}

		public sealed override int OnRecv(ArraySegment<byte> buffer) {
			int processLen = 0;

			while (true) {
				// 최소한 헤더는 파싱할 수 있는지 확인
				if (buffer.Count < HeaderSize)
					break;

				// 패킷이 완전체로 도착했는지 확인
				ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
				if (buffer.Count < dataSize)
					break;

				// 여기까지 왔으면 패킷 조립 가능
				OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));

				processLen += dataSize;
				buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
			}

			return processLen;
		}

		public abstract void OnRecvPacket(ArraySegment<byte> buffer);
	}

	public abstract class Session {
		Socket _socket;
		int _disconnected = 0;

		RecvBuffer _recvBuffer = new RecvBuffer(65535);

		object _lock = new object();
		Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
		List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
		SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
		SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

		public abstract void OnConnected(EndPoint endPoint);
		public abstract int  OnRecv(ArraySegment<byte> buffer);
		public abstract void OnSend(int numOfBytes);
		public abstract void OnDisconnected(EndPoint endPoint);

		void Clear() {
			lock (_lock) {
				_sendQueue.Clear();
				_pendingList.Clear();
			}
		}

		public void Start(Socket socket) {
			_socket = socket;

			_recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
			_sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

			RegisterRecv();
		}

		public void Send(List<ArraySegment<byte>> sendBuffList) {
			if (sendBuffList.Count == 0)
				return;

			lock (_lock) {
				foreach (ArraySegment<byte> sendBuff in sendBuffList)
					_sendQueue.Enqueue(sendBuff);

				if (_pendingList.Count == 0)
					RegisterSend();
			}
		}

		public void Send(ArraySegment<byte> sendBuff) {
			lock (_lock) {
				_sendQueue.Enqueue(sendBuff);
				if (_pendingList.Count == 0)
					RegisterSend();
			}
		}

		public void Disconnect() {
			if (Interlocked.Exchange(ref _disconnected, 1) == 1)
				return;

			OnDisconnected(_socket.RemoteEndPoint);
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
			Clear();
		}

		#region 네트워크 통신

		void RegisterSend() {
			if (_disconnected == 1)
				return;

			while (_sendQueue.Count > 0) {
				ArraySegment<byte> buff = _sendQueue.Dequeue();
				_pendingList.Add(buff);
			}
			_sendArgs.BufferList = _pendingList;

			try {
				bool pending = _socket.SendAsync(_sendArgs);
				if (pending == false)
					OnSendCompleted(null, _sendArgs);
			}
			catch (Exception e) {
				Debug.Log($"RegisterSend Failed {e}");
			}
		}

		void OnSendCompleted(object sender, SocketAsyncEventArgs args) {
			lock (_lock) {
				if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success) {
					try	{
						_sendArgs.BufferList = null;
						_pendingList.Clear();

						OnSend(_sendArgs.BytesTransferred);

						if (_sendQueue.Count > 0)
							RegisterSend();
					}
					catch (Exception e) {
						Debug.Log($"OnSendCompleted Failed {e}");
					}
				}
				else {
					Disconnect();
				}
			}
		}

		void RegisterRecv()	{
			if (_disconnected == 1)
				return;

			_recvBuffer.Clean();
			ArraySegment<byte> segment = _recvBuffer.WriteSegment;
			_recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

			try	{
				bool pending = _socket.ReceiveAsync(_recvArgs);
				if (pending == false)
					OnRecvCompleted(null, _recvArgs);
			}
			catch (Exception e)	{
				Debug.Log($"RegisterRecv Failed {e}");
			}
		}

		void OnRecvCompleted(object sender, SocketAsyncEventArgs args) {
			if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success) {
				try	{
					// Write 커서 이동
					if (_recvBuffer.OnWrite(args.BytesTransferred) == false) {
						Disconnect();
						return;
					}

					// 컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리했는지 받는다
					int processLen = OnRecv(_recvBuffer.ReadSegment);
					if (processLen < 0 || _recvBuffer.DataSize < processLen) {
						Disconnect();
						return;
					}

					// Read 커서 이동
					if (_recvBuffer.OnRead(processLen) == false) {
						Disconnect();
						return;
					}

					RegisterRecv();
				}
				catch (Exception e) {
					Debug.Log($"OnRecvCompleted Failed {e}");
				}
			} else {
				Disconnect();
			}
		}
		#endregion
	}
}
