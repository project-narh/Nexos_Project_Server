using Server.UDP.Packet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;
using Unity.VisualScripting;
using ServerCore;
using static UnityEngine.Rendering.DebugUI;

public class UDPSession //송수신 담당
{
    public EndPoint clientEP;
    Socket socket;
    ushort sequnce; // 패킷 순서를 보장하기 위한 숫자
    DateTime lastTime; // 마지막 패킷 받은 시간
    static TimeSpan timeOut = TimeSpan.FromSeconds(60);
    private HashSet<ushort> recivSeq = new HashSet<ushort>(); // 중복 방지
    private HashSet<ushort> recivSeq_Ack = new HashSet<ushort>(); // 중복 방지
    private static readonly object _lock = new object();
    private static readonly int MaxStoredSeq = 1000; // 최대 시퀀스
    private static readonly int keepMs = 10000; // 연결 호출 간격
    public Dictionary<ushort, long> sendTimestamps = new Dictionary<ushort, long>();
    public  int _disposed = 0;
    public ResendManager resendManager { get; }
    private System.Timers.Timer keepTimer;
    public int sessionID = -1;
    public int UID = -1;
    public Vector3 position = new Vector3(5.702278f, 0f, 11.80618f);
    public Quaternion rotation = new Quaternion(0f, 0.866f, 0f, -0.5f);

    public CancellationToken token;
    
    public static byte[] GetValidBytes(ArraySegment<byte> segment)
{
    byte[] validBytes = new byte[segment.Count];
    Array.Copy(segment.Array, segment.Offset, validBytes, 0, segment.Count);
    return validBytes;
}
    public UDPSession(EndPoint clientEP, Socket socket)
    {
        this.clientEP = clientEP;
        this.socket = socket;
        lastTime = DateTime.UtcNow;
        //StartTimeOut();
        //KeepAlive_Start();
        resendManager = new ResendManager(this);
        Onconnected();
        if (socket == null) Debug.Log("소켓 비었음");
        if (this.socket == null) Debug.Log("소켓2 비었음");
    }

    public UDPSession(EndPoint clientEP, Socket socket, int session)
    {
        this.clientEP = clientEP;
        this.socket = socket;
        this.sessionID = session;
        lastTime = DateTime.UtcNow;
        //StartTimeOut();
        //KeepAlive_Start();
        resendManager = new ResendManager(this);
        Onconnected();
    }

    public void KeepAlive_Start()
    {
        keepTimer = new System.Timers.Timer(keepMs);
        //keepTimer.Elapsed += async (sender, e) => await KeepAlive();
        keepTimer.AutoReset = true;
        keepTimer.Start();

    }

    public void Onconnected()
    {
        Console.WriteLine($"[UDP] 현재 {sessionID}가 연결되었습니다.");
    }

    public void OnDisconnected()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;
        try
        {
            if (socket != null)
            {
                try
                {
                    C_LeaveGame leaveGame = new C_LeaveGame();
                    SendPacket(leaveGame.Write(), (ushort)PacketID.C_LeaveGame);
                }
                catch(Exception ex) { Debug.Log("[UDP] 나가기 패킷 전송 실패"); }

            }

            var tempTimer = Interlocked.Exchange(ref keepTimer, null);
            if (tempTimer != null)
            {
                try
                {
                    tempTimer.Stop();
                    tempTimer.Dispose();
                    Debug.Log("[UDP] 타이머 정리 ");
                }
                catch (Exception ex) { Debug.Log("[UDP] 타이머 실패"); }
            }

            var tempManager = resendManager;
            if (tempManager != null)
            {
                try { tempManager.Dispose();
                    Debug.Log("[UDP] 재전송 매니저 정리");
                } catch (Exception ex) { Debug.Log("[UDP] 재전송 매니저 실패"); }
            }

            // 소켓 닫기
            var tempSocket = Interlocked.Exchange(ref socket, null);
            if (tempSocket != null)
            {
                try
                {
                    tempSocket.Close();
                    tempSocket.Dispose();
                    Debug.Log("[UDP] 패킷 정리");
                }
                catch (Exception ex) { Debug.Log("[UDP] 패킷 정리 실패"); }
            }

        }
        catch(Exception ex)
        {
            Debug.LogError($"[UDP] 연결 해제중 에러 : {ex.Message}");
        }
        Console.WriteLine($"[UDP] {sessionID} 연결이 종료되었습니다.");
    }


    private void StartTimeOut()
    {

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000);
                    if (DateTime.UtcNow - lastTime > timeOut)
                    {
                        Console.WriteLine($"[UDP] {sessionID} 타임아웃");
                        OnDisconnected();
                        break;
                    }
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP] 타임아웃 실행 중 에러 : {ex.Message}");
                }
            }
        });
    }

    public void SendPacket(ArraySegment<byte> sendBuffer, ushort packetID)
    {
        if (_disposed == 1 || socket == null) return;
        byte[] packetWithHeader = new byte[sendBuffer.Count];
        Array.Copy(sendBuffer.Array, sendBuffer.Offset, packetWithHeader, 0, sendBuffer.Count);

        ushort currentSeq;

        lock (_lock)
        {
            currentSeq = sequnce++;
        }
        // 시퀀스 번호 설정
        BitConverter.GetBytes(currentSeq).CopyTo(packetWithHeader, 4);

        try
        {
            Socket currentSocket = socket;
            if (currentSocket == null || _disposed == 1)
                return;
            currentSocket.SendTo(packetWithHeader, clientEP);

            if (packetID != (ushort)PacketID.C_Ack && packetID != (ushort)PacketID.S_Ack && _disposed == 0)
            {
                resendManager?.Add(currentSeq, packetWithHeader);
            }
        }
        catch (ObjectDisposedException) { }
        catch (SocketException sex)
        {
            Debug.LogError($"[UDP] 소켓 예외 발생: {sex.SocketErrorCode} - {sex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP] 전송중 오류 발생 : {ex.Message} ");
        }
    }


    public async Task<bool> SendACKAsync(ushort seqNumber, bool isSend = false)
    {
        try
        {
            sendTimestamps[seqNumber] = DateTime.UtcNow.Ticks;
            byte[] ack = new byte[7];
            BitConverter.GetBytes((ushort)(7)).CopyTo(ack, 0);
            BitConverter.GetBytes((ushort)PacketID.C_Ack).CopyTo(ack, 2);
            BitConverter.GetBytes((ushort)seqNumber).CopyTo(ack, 4);
            ack[6] = (byte)(isSend ? 1 : 0);
            //Console.WriteLine($"[UDP] ACK 전송: Seq {seqNumber} 수신자: {clientEP} 요청(false가 요청) {0}");
            await socket.SendToAsync(new ArraySegment<byte>(ack), SocketFlags.None, clientEP);

            return true; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 데이터 송신 ACK 에러: {ex}");
            return false;
        }
    }

    public void ResendPacket(ushort seq, byte[] buffer)
    {
        try
        {
            socket.SendTo(buffer, SocketFlags.None, clientEP);
            // Debug.Log($"[UDP] 패킷 재전송 성공  시퀀스 :  {seq}  크기 {buffer.Length} ");
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            //Debug.Log($"[UDP] 패킷 재전송 오류 발생  시퀀스 :  {seq}  크기 {buffer.Length} ");
            //Debug.Log($"[UDP] 오류 내용 : {ex}") ;
        }
    }

    public async Task ReceivePacket(ArraySegment<byte> buffer)
    {
        if (_disposed == 1) return;
        try
        {
            if (buffer.Array == null || buffer.Count < sizeof(ushort) * 3)
            {
                //Debug.Log("[UDP] 잘못된 패킷 수신 (크기가 너무 작음)");
                return;
            }

            ushort count = 0;
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            count += sizeof(ushort);
            ushort packetID = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += sizeof(ushort);
            ushort receivedSeq = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += sizeof(ushort);

            if (size != buffer.Count)
            {
                //Debug.Log($"[UDP - 수신] 패킷 크기 불일치: 기대값 {size}, 실제값 {buffer.Count}");
                return;
            }

            if (packetID == (ushort)PacketID.KeepAlive)
            {
                Debug.Log($"[UDP - 수신] KeepAlive 패킷 수신 (Seq: {receivedSeq})");
                Task.Run(() => KeepAlive());
                PacketManager.Instance.HandlePacket(this, buffer);
                return;
            }

            bool isSendAck = false;

            lock (_lock)
            {
                if (packetID == (ushort)PacketID.S_Ack || packetID == (ushort)PacketID.C_Ack)
                {
                    bool ackType = BitConverter.ToBoolean(buffer.Array, buffer.Offset + count);
                    count += sizeof(bool);

                    if (!ackType)
                    {
                        if (!recivSeq_Ack.Contains(receivedSeq))
                        {
                            recivSeq_Ack.Add(receivedSeq);
                            //Debug.LogWarning($"[UDP - 수신] ACK데이터 추가 (Seq: {receivedSeq}) PacketID: {packetID}");
                        }
                        else
                        {
                            //Debug.LogWarning($"[UDP - 수신] ACK 중복 (Seq: {receivedSeq}) PacketID: {packetID}");
                        }
                    }
                    PacketManager.Instance.HandlePacket(this, buffer);
                    return;
                }
                else
                {
                    if (!recivSeq.Contains(receivedSeq))
                    {
                        recivSeq.Add(receivedSeq);
                        if (recivSeq.Count > MaxStoredSeq)
                            recivSeq.Remove(recivSeq.First());
                        isSendAck = true;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            lastTime = DateTime.UtcNow;
            //TODO : Ack는 일정시간후 다시 보내게
            //Debug.Log($"[UDP] 정상 패킷 처리 완료 (Seq: {receivedSeq}, ID: {packetID}, Size: {size})");
            if(isSendAck)  PacketManager.Instance.HandlePacket(this, buffer);
        }
        catch (Exception ex)
        {
            if(_disposed==0)
            Debug.LogError($"[UDP] 패킷 처리 중 오류 발생: {ex}");
        }
    }

    public async Task KeepAlive()
    {
        Debug.Log($"[UDP] KeepAlive 실행");
        if (_disposed == 1 || socket == null)
        {
            Debug.Log("[UDP] KeepAlive 중단");
            return;
        }
        try
        {
            //Debug.Log($"[UDP] KeepAlive 여기까진 접근");
            //if (!token.IsCancellationRequested)
                //Debug.Log($"[UDP] 여기도 오긴 해");
                //sessionID 추가
                byte[] keepAlivePacket = new byte[10];
                ushort currentSeq;
                lock (_lock) // 
                {
                    sequnce++;
                    currentSeq = sequnce;
                }
                BitConverter.GetBytes((ushort)10).CopyTo(keepAlivePacket, 0);
                BitConverter.GetBytes((ushort)PacketID.KeepAlive).CopyTo(keepAlivePacket, 2);
                BitConverter.GetBytes((ushort)currentSeq).CopyTo(keepAlivePacket, 4);
                BitConverter.GetBytes((int)sessionID).CopyTo(keepAlivePacket, 6);
                socket.SendTo(keepAlivePacket, SocketFlags.None, clientEP);
                //Console.WriteLine($"[UDP] KeepAlive 전송: 세션 : {sessionID}, Seq: {currentSeq}");

        }
        catch (ObjectDisposedException ex) { Console.WriteLine($"[UDP] KeepAlive ObjectDisposed전송 오류: {ex}"); }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] KeepAlive 전송 오류: {ex}");
        }
    }
}
