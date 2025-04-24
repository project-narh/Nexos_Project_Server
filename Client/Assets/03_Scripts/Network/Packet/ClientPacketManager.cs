using ServerCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PacketManager
{
    #region Singleton (이전 방식은 매번 실행될때 래지스터 호출해줘야 해서 그런 작업 안하게 수정)
    static PacketManager _instance = new PacketManager();
    public static PacketManager Instance { get { return _instance; } }
    #endregion
    
    PacketManager() 
    {
        Register();
    }
    Dictionary<ushort, Func<UDPSession, ArraySegment<byte>, IPacket>> _makeFunc = new Dictionary<ushort, Func<UDPSession, ArraySegment<byte>, IPacket>>();
    ConcurrentDictionary<ushort, Action<UDPSession, IPacket>> _handler = new ConcurrentDictionary<ushort, Action<UDPSession, IPacket>>();

    public void Register()
    {
        //패킷을 받는도중 Register를 하면 문제 발생 (먼저 등록하고 패킷이 들어오면 문제가 안된다)

        _makeFunc.Add((ushort)PacketID.S_BroadcastEnterGame, MakePacket<S_BroadcastEnterGame>);
        _handler[(ushort)PacketID.S_BroadcastEnterGame] =  PacketHandler.S_BroadcastEnterGameHandler;

        _makeFunc.Add((ushort)PacketID.S_BroadcastLeaveGame, MakePacket<S_BroadcastLeaveGame>);
        _handler[(ushort)PacketID.S_BroadcastLeaveGame]= PacketHandler.S_BroadcastLeaveGameHandler;

        _makeFunc.Add((ushort)PacketID.S_PlayerList, MakePacket<S_PlayerList>);
        _handler[(ushort)PacketID.S_PlayerList] = PacketHandler.S_PlayerListHandler;

        _makeFunc.Add((ushort)PacketID.S_BroadcastMove, MakePacket<S_BroadcastMove>);
        _handler[(ushort)PacketID.S_BroadcastMove]=PacketHandler.S_BroadcastMoveHandler;

        _makeFunc.Add((ushort)PacketID.S_LoginResponse, MakePacket<S_LoginResponse>);
        _handler[(ushort)PacketID.S_LoginResponse]= PacketHandler.S_LoginResponseHandler;

        _makeFunc.Add((ushort)PacketID.S_RegisterResponse, MakePacket<S_RegisterResponse>);
        _handler[(ushort)PacketID.S_RegisterResponse]= PacketHandler.S_RegisterResponseHandler;

        _makeFunc.Add((ushort)PacketID.S_Ack, MakePacket<S_Ack>);
        _handler[(ushort)PacketID.S_Ack] = PacketHandler.S_AckHandler;

        _makeFunc.Add((ushort)PacketID.KeepAlive, MakePacket<KeepAlive>);
        _handler[(ushort)PacketID.KeepAlive] = PacketHandler.KeepAliveHandler;

    }

    T MakePacket<T>(UDPSession session, ArraySegment<byte> buffer) where T : IPacket, new()
    {
        /*long playerId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
                    count += 8;*/
        T pkt = new T();
        pkt.Read(buffer);
        return pkt;
    }

    // 핸들러로 보내는 부분 분리
    public void HandlePacket(UDPSession session, ArraySegment<byte> buffer)
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

            if (_makeFunc.TryGetValue(PacketID, out func))
            {
                IPacket packet = func.Invoke(session, buffer);
                if (_handler.TryGetValue(PacketID, out var action))
                {
                    NetworkManager.Instance.Get_UDPconnect().Enqueue(() => { action.Invoke(session, packet);});
                }
                else
                    Debug.Log("[UDP] 등록된 메서드가 존재하지 않습니다.");
            }
            else
            {
                Debug.Log("[UDP] 등록된 메서드가 존재하지 않습니다.");
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[UDP] " + ex.ToString());
        }
    }
}
