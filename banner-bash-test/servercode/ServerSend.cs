using System;
using System.Collections.Generic;
using System.Text;

namespace GameServer
{
    class ServerSend
    {
        private static void SendTCPData(int _toClient, Packet _packet)
        {
            _packet.WriteLength();
            ServerSend.clients[_toClient].tcp.SendData(_packet);
        }
        public static void Welcome(int _toClient, string _msg)
        {
            _packet.Write(_msg);
            _packet.Write(_toClient);
            SendTCPData(_toClient, _packet);
        }
    }
}