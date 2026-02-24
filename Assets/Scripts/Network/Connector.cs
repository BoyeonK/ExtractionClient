using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace ServerCore {
	public class Connector {
		Func<Session> _sessionFactory;

		public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count = 1) {
			for (int i = 0; i < count; i++) {
				Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				_sessionFactory = sessionFactory;

				SocketAsyncEventArgs args = new SocketAsyncEventArgs();
				args.Completed += OnConnectCompleted;
				args.RemoteEndPoint = endPoint;
				args.UserToken = socket;

				RegisterConnect(args);
			}
		}

		void RegisterConnect(SocketAsyncEventArgs args)	{
			Socket socket = args.UserToken as Socket;
			if (socket == null)
				return;

			//비 동기적 연결.
			bool pending = socket.ConnectAsync(args);
			//즉시 완료되었을 경우 (false인 경우) 직접 Completed에 들어갔을 함수를 호출해야함.
			//즉시 완료되지 않았을 경우(true인 경우) Completed가 실행되도록 내부적으로 작성되있음.
			if (pending == false)
				OnConnectCompleted(null, args);
		}

		void OnConnectCompleted(object sender, SocketAsyncEventArgs args) {
			if (args.SocketError == SocketError.Success) {
				//최초 Connect함수에 인자로 들어온 Factory함수로 Session을 만들고
				Session session = _sessionFactory.Invoke();
				//해당 Session으로서 socket handle을 관리 및 Recv시작.
				session.Start(args.ConnectSocket);
				//해당 Session의 OnConnected함수 실행 (HandShake과정 수행 시작)
				session.OnConnected(args.RemoteEndPoint);

				Managers.ExecuteAtMainThread(() => {
                    //Managers.Network.ConnectCompleted(true);
                }); 
			}
			else {
				Debug.Log($"OnConnectCompleted Fail: {args.SocketError}");
                Managers.ExecuteAtMainThread(() => {
                    //Managers.Network.ConnectCompleted(false);
                }); 
			}
		}
	}
}
