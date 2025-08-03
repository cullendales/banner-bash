using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;


namespace GameServer
{
    class ServerSend
    {
        public static void SendTCPData(int _toClient, SendPacketsElement _packet)
        {
            _packet.WriteLength();
            Server.clients[_toClient].tcp.SendData(_packet);
        }

        private static void SendTCPDataToALL(int _exceptClient, SendPacketsElement _packet)
        {
            _packet.WriteLength();

            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                Server.clients[i].tcp.SendData(_packet);
            }
        }
        public static void Begin(int _toClient, string _msg)
        {
            using (Packet _packet = new Packet((int)ServerPackets.welcome))
            {
                _packet.Write(_msg);
                _packet.Write(_toClient);
                SendTCPData(_toClient, _packet);
            }



        }
    }
}