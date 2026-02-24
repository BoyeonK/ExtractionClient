using ServerCore;
using System;
using System.Collections.Generic;

class PacketManager {
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } }
	#endregion

	PacketManager()	{
		Register();
	}

	Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();
    /*
	Dictionary<ushort, Action<PacketSession, IMessage>> _handler = new Dictionary<ushort, Action<PacketSession, IMessage>>();
	Dictionary<ushort, Func<IMessage>> _msgFactories = new Dictionary<ushort, Func<IMessage>>();
	*/

	//PacketManager 초기화 시, 각 패킷을 다룰 델리게이트들을 초기화해준다.
	//_onRecv는 서버로부터 받은 바이너리에서 추출한 패킷 헤더의 msgId를 토대로
	// 바이너리를 헤더와 protobuf부분으로 나누고, protobuf부분을 알맞게 캐스팅한다.
	//_handler는 캐스팅된 protobuf내용에 따라 호출할 handler함수의 집합이다.
	//_msgFactories는 SEncrypted를 통해 복호화된 바이너리를 알맞은 protobuf로 parse하기 위한
	//factory함수이다.
	public void Register() {
        /*
		_onRecv.Add((ushort)MsgId.SEncrypted, UnpackPacket<S_Encrypted>);
		_handler.Add((ushort)MsgId.SEncrypted, PacketHandler.S_EncryptedHandler);
        */
    }

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer) {
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Action<PacketSession, ArraySegment<byte>, ushort> action = null;
		if (_onRecv.TryGetValue(id, out action))
			action?.Invoke(session, buffer, id);
	}

	/*
	void UnpackPacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()	{
		T pkt = new T();
		pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

		// 방법1. 모든 핸들러 함수를 메인스레드에서 실행되도록 유도.
		// 이런 푸쉬짓을 내가 할 리가 없지 ㅋㅋㅋ
		//CustomHandler.Invoke(session, pkt, id);

		// 방법2. 받은 즉시 그 스레드에서 핸들러 함수 실행
		// 이 핸들러에 영향을 받는 모든 object는 이제 멀티스레드를 고려해서 설계되어야함.
		// 메인스레드에서만 정상 동작을 보장하는 메서드는 따로 메인스레드에서 동작하도록
		// 설계 해 주어야함.
		Action<PacketSession, IMessage> act = GetPacketHandler(id);
		act?.Invoke(session, pkt);
	}

	public Action<PacketSession, IMessage> GetPacketHandler(ushort id) {
		Action<PacketSession, IMessage> action = null;
		if (_handler.TryGetValue(id, out action))
			return action;
		return null;
	}

	public bool ByteToIMessage(PacketSession session, byte[] plaintext, ushort msgId) {
		//유효성검사 X. 나중에 유효성 검사 로직 필요.
		if (_msgFactories.TryGetValue(msgId, out Func<IMessage> factory)) {
			IMessage pkt = factory.Invoke();
			pkt.MergeFrom(plaintext);
			Action<PacketSession, IMessage> act = GetPacketHandler(msgId);
			act?.Invoke(session, pkt);
			return true;
		}
		return false;
    }
	*/
}