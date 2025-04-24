using Server;
using Server.UDP.Room;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// 수동으로 관리하며 무엇을 호출할지
namespace UDP
{
    class PacketHandler
    {
        public static async Task C_EnterGameHandler(UDPSession session, IPacket packet)
        {
            C_EnterGame pkt = packet as C_EnterGame;
            Console.WriteLine($"UID : {pkt.uid}");
            session.UID = pkt.uid;
            Console.WriteLine($"UID(Session) : {session.UID}");
            if(session.room == null) Console.WriteLine($"없어");
            await session.room.Enter(session);

            
            //TCPServer.Room.Push(() => TCPServer.Room.Enter(clientSession));
        }

        public static async Task C_LeaveGameHandler(UDPSession session, IPacket packet)
        {
            C_LeaveGame pkt = packet as C_LeaveGame;

            Console.WriteLine("C_Leave 실행됨");
            UDPGameRoom room = session.room;
            await room.Leave(session);
            session.OnDisconnected();
        }

        public static async Task C_MoveHandler(UDPSession session, IPacket packet)
        {
            C_Move pkt = packet as C_Move;

            await session.SendACKAsync(pkt.sequenceNumber);

            if (session.room == null)
                return;

            //Console.WriteLine($"{movePacket.posX}, {movePacket.posY}, {movePacket.posZ}");

            await session.room.Move(session, pkt);
        }

        public static async Task C_AccountLoginHandler(UDPSession session, IPacket packet)
        {
            C_AccountLogin login = packet as C_AccountLogin;
            await session.SendACKAsync(login.sequenceNumber);
            //C_AccountLogin loginPacket = packet as C_AccountLogin;
            //ClientSession clientSession = session as ClientSession;

            //LoginRoom room = clientSession.lg_Room;
            //room.Push(() => room.Login(clientSession, loginPacket));
        }

        public static async Task C_RegisterHandler(UDPSession session, IPacket packet)
        {
            C_Register pkt = packet as C_Register;
            await session.SendACKAsync(pkt.sequenceNumber);

        }

        public static async Task C_AckHandler(UDPSession session, IPacket packet)
        {
            C_Ack pkt = packet as C_Ack;
            if (pkt.Protocol == (ushort)PacketID.C_Ack)
            {
                if (pkt.type)
                {
                    session.resendManager.RemoveAckedPacket(pkt.sequenceNumber);
                }
                else
                {
                    Console.WriteLine("S_ACK 요청 재송신");

                    await session.SendACKAsync(pkt.sequenceNumber, true);
                }
            }
            //Console.WriteLine($"[UDP] ASK 받음 : {pkt.sequenceNumber} 송신자 : {session.clientEP}");
        }

        public static async Task S_AliveHandler(UDPSession session, IPacket packet)
        {
            KeepAlive pkt = packet as KeepAlive;

            if (pkt.Protocol == (ushort)PacketID.KeepAlive)
            {
                
            }
            //Console.WriteLine($"[UDP] alive 받음 : ID : {session.sessionID} 송신자 : {session.clientEP}");
        }
    }
}

