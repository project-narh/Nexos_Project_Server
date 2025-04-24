using ServerCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using UDP;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

public class PacketManager
{

    //    [2바이트] 패킷 길이 | [2바이트] 패킷 ID | [2바이트] 시퀀스 번호 | [데이터]
    #region Singleton (이전 방식은 매번 실행될때 래지스터 호출해줘야 해서 그런 작업 안하게 수정)
    static PacketManager instance = new PacketManager();
    public static PacketManager Instance { get { return instance; } }
    #endregion

    PacketManager()
    {
        Register();
    }

    Dictionary<ushort, Func<UDPSession, ArraySegment<byte>, IPacket>> _makeFunc = new Dictionary<ushort, Func<UDPSession, ArraySegment<byte>, IPacket>>();
    ConcurrentDictionary<ushort, Func<UDPSession, IPacket, Task>> _handler = new ConcurrentDictionary<ushort, Func<UDPSession, IPacket, Task>>();

    public void Register()
    {
        //패킷을 받는도중 Register를 하면 문제 발생(먼저 등록하고 패킷이 들어오면 문제가 안된다)
            Register_UDP();
    }

    T MakePacket<T>(UDPSession session, ArraySegment<byte> buffer) where T : IPacket, new()
    {
        T pkt = new T();
        pkt.Read(buffer);
        return pkt;
    }

    private void Register_UDP()
    {
        _makeFunc.Add((ushort)PacketID.C_EnterGame, MakePacket<C_EnterGame>);
        _handler[(ushort)PacketID.C_EnterGame] = PacketHandler.C_EnterGameHandler;
        
        _makeFunc.Add((ushort)PacketID.C_Move, MakePacket<C_Move>);
        _handler[(ushort)PacketID.C_Move] = PacketHandler.C_MoveHandler;
        
        _makeFunc.Add((ushort)PacketID.C_AccountLogin, MakePacket<C_AccountLogin>);
        _handler[(ushort)PacketID.C_AccountLogin] = PacketHandler.C_AccountLoginHandler;

        _makeFunc.Add((ushort)PacketID.C_LeaveGame, MakePacket<C_LeaveGame>);
        _handler[(ushort)PacketID.C_LeaveGame] = PacketHandler.C_LeaveGameHandler;

        _makeFunc.Add((ushort)PacketID.C_Register, MakePacket<C_Register>);
        _handler[(ushort)PacketID.C_Register] = PacketHandler.C_RegisterHandler;

        _makeFunc.Add((ushort)PacketID.C_Ack, MakePacket<C_Ack>);
        _handler[(ushort)PacketID.C_Ack] = PacketHandler.C_AckHandler;

        _makeFunc.Add((ushort)PacketID.KeepAlive, MakePacket<KeepAlive>);
        _handler[(ushort)PacketID.KeepAlive] = PacketHandler.S_AliveHandler;

    }



    //핸들러로 보내는 부분 분리
        public async Task HandlePacket(UDPSession session, ArraySegment<byte> buffer)
    {
        try
        {
            ushort count = 0;
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            count += sizeof(ushort);
            ushort PacketID = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += sizeof(ushort);
            ushort sequence = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += sizeof(ushort);

            Func<UDPSession, ArraySegment<byte>, IPacket> func = null;

            if(_makeFunc.TryGetValue(PacketID, out func))
            {
                IPacket packet = func.Invoke(session, buffer);

                if (_handler.TryGetValue(PacketID, out var action))
                {
                    await action.Invoke(session, packet);
                }
                else
                    Console.WriteLine("[UDP] 등록된 메서드가 존재하지 않습니다.");
            }
            else
            {
                Console.WriteLine("[UDP] 등록된 메서드가 존재하지 않습니다.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[UDP] " + ex.ToString());
        }
    }
}

