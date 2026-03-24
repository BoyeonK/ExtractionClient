using ServerCore;
using System;
using System.Collections.Generic;

class PacketHandler {
    Dictionary<ushort, Action<ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<ArraySegment<byte>, ushort>>();

}

