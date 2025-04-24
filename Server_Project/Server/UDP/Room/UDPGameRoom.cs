using Server.UDP.Session;
using ServerCore;

namespace Server.UDP.Room
{
    public class UDPGameRoom
    {
        private HashSet<UDPSession> sessions = new HashSet<UDPSession>();
        object _lock = new object();

        Vector3 Spawn_Pos = new Vector3(-118.981f, 17.977f, -31.193f);
        Quaternion Spawn_Rot = new Quaternion(0f, 0f, 0f, 1f);

        public async Task Enter(UDPSession session)
        {
            Console.WriteLine($"............................................................................");
            session.position = Spawn_Pos;
            session.rotation = Spawn_Rot;
            lock (_lock)
            {
                if (!sessions.Contains(session))
                {
                    sessions.Add(session);
                }
                else return;
            }
            Console.WriteLine($"[UDP] Player {session.UID} 게임 접속.");

            session.room = this;


            S_PlayerList players = new S_PlayerList();
            lock(_lock)
            {
                foreach (UDPSession s in sessions)
                {
                    players.players.Add(new S_PlayerList.Player()
                    {
                        isSelf = (s == session),
                        uid = s.UID,
                        playerId = s.sessionID,
                        position = s.position,
                        rotation = s.rotation,
                    });
                }
            }
            S_BroadcastEnterGame enterGame = new S_BroadcastEnterGame();
            enterGame.position = session.position;
            enterGame.rotation = session.rotation;
            enterGame.uid = session.UID;
            enterGame.playerId = session.sessionID;

            Console.WriteLine($"[UDP] Player {session.sessionID} 게임 접속 완료.");
            await Broadcast(enterGame.Write(), (ushort)PacketID.S_BroadcastEnterGame);
          //  await session.SendPacketAsync(enterGame.Write(), (ushort)PacketID.S_BroadcastEnterGame);
            await session.SendPacketAsync(players.Write(),(ushort)PacketID.S_PlayerList);
        }

        public async Task Leave(UDPSession session)
        {
            lock(_lock)
            {
                sessions.Remove(session);
            }
            //if (sessions.Remove(session)) { }
            S_BroadcastLeaveGame leave = new S_BroadcastLeaveGame();
            leave.playerId = session.sessionID;
            await Broadcast(leave.Write(), (ushort)PacketID.S_BroadcastLeaveGame);
        }

        public async Task Move(UDPSession session, C_Move packet)
        {
            //좌표를 바꿔주고
            session.position = packet.position;
            session.rotation = packet.rotation;

            //모두에게 알린다
            S_BroadcastMove move = new S_BroadcastMove();
            move.playerId = session.sessionID;
            move.position = packet.position;
            move.rotation = packet.rotation;
            await Broadcast(move.Write(),(ushort)PacketID.S_BroadcastMove);
        }

        public async Task Broadcast(ArraySegment<byte> segment, ushort protocol)
        {
            List<Task> sendTasks = new List<Task>();
            foreach (var session in sessions)
            {
                sendTasks.Add(session.SendPacketAsync(segment, protocol));
            }
            await Task.WhenAll(sendTasks);
        }
    }
}
