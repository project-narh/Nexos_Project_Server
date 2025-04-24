using ServerCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;



// 수동으로 관리하며 무엇을 호출할지
class PacketHandler
{
    public static event Action<S_LoginResponse> OnLogin;
    public static event Action<S_RegisterResponse> OnRegister;

    public static void S_BroadcastEnterGameHandler(UDPSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;
        //PlayerManager.Instance.PlayerEnter(pkt);
    }

    public static void S_BroadcastLeaveGameHandler(UDPSession session, IPacket packet)
    {
        S_BroadcastLeaveGame pkt = packet as S_BroadcastLeaveGame;
        //PlayerManager.Instance.PlayerLeave(pkt);

    }

    public static void S_BroadcastMoveHandler(UDPSession session, IPacket packet)
    {
        S_BroadcastMove pkt = packet as S_BroadcastMove;
        //PlayerManager.Instance.Move(pkt);
    }

    public static void S_PlayerListHandler(UDPSession session, IPacket packet)
    {
        //NetworkManager.Instance.Get_UDPconnect().Enqueue(() =>
        //{
            S_PlayerList pkt = packet as S_PlayerList;
            //PlayerManager.Instance.Player_List_wait(pkt);
        //});
    }

    public static void S_LoginResponseHandler(UDPSession session, IPacket packet)
    {
        S_LoginResponse pack = packet as S_LoginResponse;

        //if(pack != null) OnLogin?.Invoke(pack);
        //else UnityEngine.Debug.Log("Login Response Error");
    }

    public static void S_RegisterResponseHandler(UDPSession session, IPacket packet)
    {
        S_RegisterResponse pack = packet as S_RegisterResponse;

        //if (pack != null) OnRegister?.Invoke(pack);
        //else UnityEngine.Debug.Log("Register Response Error");
    }

    public static void S_AckHandler(UDPSession session, IPacket packet)
    {
        S_Ack pkt = packet as S_Ack;
        if (pkt.Protocol == (ushort)PacketID.S_Ack)
        {
            //UnityEngine.Debug.Log("S_ACK 처리");
            if (pkt.type)
            {
                if(session.sendTimestamps.TryGetValue(pkt.sequenceNumber, out long sendTime))
                {

                    long now = DateTime.UtcNow.Ticks;
                    long rttMs = (now - sendTime) / TimeSpan.TicksPerMillisecond;

                    //NetworkManager.Instance.rtt.Add(rttMs);
                    //if(NetworkManager.Instance.rtt.Count > 100)
                    //{
                    //    //NetworkManager.Instance.rtt.RemoveAt(0);
                    //}
                    //double avg = NetworkManager.Instance.rtt.Average();
                    //UnityEngine.Debug.Log($"[UDP] ACK 수신 | Seq={pkt.sequenceNumber}, RTT={rttMs}ms 평균 RTT = {avg:F2}");
                    //session.sendTimestamps.Remove(pkt.sequenceNumber);
                }
                //UnityEngine.Debug.Log("S_ACK 받음 제거");
                session.resendManager.RemoveAckedPacket(pkt.sequenceNumber);
            }
            else
            {
                //UnityEngine.Debug.Log("S_ACK 요청 재송신");

                //NetworkManager.Instance.Get_UDPconnect().Enqueue(() =>
                //{
                //    Task.Run(() => session.SendACKAsync(pkt.sequenceNumber, true));
                //});
            }
        }
    }

    public static void KeepAliveHandler(UDPSession session, IPacket packet)
    {
        KeepAlive pkt = packet as KeepAlive;

        if (pkt.Protocol == (ushort)PacketID.KeepAlive)
        {
            //구조
            //서버에서 연결된 클라이언트들에 Alive 요청
            //살이있다면 반환 받고 이로 인해 Timer 시간 문제 없어짐
        }
    }
}