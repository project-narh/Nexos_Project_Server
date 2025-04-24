using Server;
using Server.UDP.Packet;
using Server.UDP.Room;
using Server.UDP.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UDP;

public class UDPSession //송수신 담당
{
    public EndPoint clientEP;
    Socket socket;
    ushort sequnce; // 패킷 순서를 보장하기 위한 숫자
    public DateTime lastTime; // 마지막 패킷 받은 시간
    static TimeSpan timeOut = TimeSpan.FromSeconds(60);
    private HashSet<ushort> recivSeq = new HashSet<ushort>(); // 중복 방지
    private HashSet<ushort> recivSeq_Ack = new HashSet<ushort>();
    private static readonly object _lock = new object();
    private static readonly int MaxStoredSeq = 1000; // 최대 시퀀스
    private static readonly int keepMs = 10000; // 연결 호출 간격
    private CancellationTokenSource timeoutCts;
    public ResendManager resendManager { get; }
    private System.Timers.Timer keepTimer;
    public UDPGameRoom room;
    public int sessionID = -1;
    public int UID = -1;
    private bool isDisposed = false;
    public int _disposed = 0;
    private readonly object socketLcok = new object();

    public Vector3 position = new Vector3(5.702278f, 0f, 11.80618f);
    public Quaternion rotation = new Quaternion(0f, 0.866f, 0f, -0.5f);
    public UDPSession(EndPoint clientEP, Socket socket)
    {
        this.clientEP = clientEP;
        this.socket = socket;
        lastTime = DateTime.UtcNow;
        timeoutCts = new CancellationTokenSource();
        StartTimeOut();
        KeepAlive_Start();
        resendManager = new ResendManager(this);
        Onconnected();
    }

    public UDPSession(EndPoint clientEP, Socket socket, int session)
    {
        this.clientEP = clientEP;
        this.socket = socket;
        this.sessionID = session;
        lastTime = DateTime.UtcNow;
        timeoutCts = new CancellationTokenSource();
        StartTimeOut();
        KeepAlive_Start();
        resendManager = new ResendManager(this);
        Onconnected();
    }

    public void KeepAlive_Start()
    {
        keepTimer = new System.Timers.Timer(keepMs);
        keepTimer.Elapsed += async (sender, e) => await SendKeepAliveAsync();
        keepTimer.AutoReset = true;
        keepTimer.Start();

    }

    public void Onconnected()
    {
        //JobTimer.Instance.Push(() => UDPServer.room.Enter(this).Wait());
    }

    public void OnClientDisconnect(UDPSession session)
    {
        if (session != null)
        {
        }
    }

    public void OnDisconnected()
    {

        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;
        try
        {
            var tempCts = Interlocked.Exchange(ref timeoutCts, null);
            if (tempCts != null)
            {
                tempCts.Cancel();
                tempCts.Dispose();
            }

            var tempTimer = Interlocked.Exchange(ref keepTimer, null);
            if (tempTimer != null)
            {
                tempTimer.Stop();
                tempTimer.Dispose();
            }

            resendManager?.Dispose();

            //var tempSocket = Interlocked.Exchange(ref socket, null);
            //if (tempSocket != null)
            //{
            //    try
            //    {
            //        tempSocket.Close();
            //        tempSocket.Dispose();
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"[UDP] 소켓 정리 중 오류: {ex.Message}");
            //    }
            //}
            if(sessionID > 0)
                SessionManager.Instance.RemoveSession(sessionID);

            if(room != null)
            {
                Task.Run(async () => await room.Leave(this)).Wait();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 연결 정리중 에러 발생 : {ex.Message}");
        }
    }

    //public async Task DisconnectAsync()
    //{
    //    OnDisconnected();
    //}

    private void StartTimeOut()
    {
        Task.Run(async () =>
        {
            while (!timeoutCts.Token.IsCancellationRequested)
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

    public async Task SendPacketAsync(ArraySegment<byte> sendBuffer, ushort packetID)
    {
        if(_disposed == 1 || socket == null) return;

        byte[] packetWithHeader = new byte[sendBuffer.Count];
        Array.Copy(sendBuffer.Array, sendBuffer.Offset, packetWithHeader, 0, sendBuffer.Count);

        ushort currentSeq;

        lock (_lock)
        {
            //Console.WriteLine($"시퀀스 증가");
            currentSeq = sequnce++;
        }
        BitConverter.GetBytes(currentSeq).CopyTo(packetWithHeader, 4);

        try
        {
            await socket.SendToAsync(new ArraySegment<byte>(packetWithHeader), SocketFlags.None, clientEP);
            resendManager.Add(currentSeq, packetWithHeader);
        }
        catch (ObjectDisposedException){ }// 소켓이 이미 닫힌 경우 무시
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 패킷 전송 중 오류: {ex.Message}");
        }
    }

    public async Task<bool> SendACKAsync(ushort seqNumber,bool isSend = false)
    {
        try
        {
            byte[] ack = new byte[7];
            BitConverter.GetBytes((ushort)7).CopyTo(ack, 0);
            BitConverter.GetBytes((ushort)PacketID.S_Ack).CopyTo(ack, 2);
            BitConverter.GetBytes((ushort)seqNumber).CopyTo(ack, 4);
            ack[6] = (byte)(isSend ? 1 : 0);
            //Console.WriteLine($"[UDP] ACK 전송: Seq {seqNumber} 수신자: {clientEP} 요청(false가 요청) {0}");
            
            await socket.SendToAsync(new ArraySegment<byte>(ack), SocketFlags.None, clientEP);

            return true;
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"[UDP] 데이터 송신 ACK 에러: {ex}");
            return false;
        }
    }

    public async Task ResendPacketAsync(ushort seq, byte[] buffer)
    {
        if (_disposed == 1 || socket == null) return;
        try
        {
            Socket currentSocket = socket;
            if (currentSocket == null)
                return;

            await currentSocket.SendToAsync(new ArraySegment<byte>(buffer), SocketFlags.None, clientEP);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 패킷 재전송 오류: {ex.Message}");
        }
    }

    public async Task ReceivePacketAsync(ArraySegment<byte> buffer)
    {
        try
        {
            //Console.WriteLine("[UDP] 클라이언트 수신 데이터 확인");
            if (buffer.Array == null || buffer.Count < sizeof(ushort) * 3)
            {
                //Console.WriteLine("[UDP] 잘못된 패킷 수신 (크기가 너무 작음)");
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
               // Console.WriteLine($"[UDP] 패킷 크기 불일치: 기대값 {size}, 실제값 {buffer.Count}");

                return;
            }

            //if (packetID == (ushort)PacketID.KeepAlive)
            //{
            //    Console.WriteLine($"[UDP - 수신] KeepAlive 패킷 수신 (Seq: {receivedSeq})");
            //    await SendKeepAliveAsync();
            //    await PacketManager.Instance.HandlePacket(this, buffer);
            //    return;
            //}

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
                            resendManager.RemoveAckedPacket(receivedSeq);
                           // Console.WriteLine($"[UDP - 수신] ACK데이터 추가 (Seq: {receivedSeq}) PacketID: {packetID}");
                        }
                        else
                        {
                           // Console.WriteLine($"[UDP - 수신] ACK 중복 (Seq: {receivedSeq}) PacketID: {packetID}");
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
            if(isSendAck)
            {
                bool ackSent = await SendACKAsync(receivedSeq);
                if (!ackSent)
                {
                    // Console.WriteLine($"[UDP-수신] ACK 전송 실패: {receivedSeq} → 재전송 목록에서 삭제하지 않음");
                }
            }

            await PacketManager.Instance.HandlePacket(this, buffer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP-수신] 패킷 처리 중 오류 발생: {ex}");
        }
    }

    public async Task SendKeepAliveAsync()
    {
        try
        {
            if (socket == null) return;

            byte[] keepAlivePacket = new byte[10];
            ushort currentSeq;

            lock (_lock) // 
            {
                currentSeq = sequnce++;
            }

            BitConverter.GetBytes((ushort)10).CopyTo(keepAlivePacket, 0);
            BitConverter.GetBytes((ushort)PacketID.KeepAlive).CopyTo(keepAlivePacket, 2);
            BitConverter.GetBytes((ushort)currentSeq).CopyTo(keepAlivePacket, 4);
            BitConverter.GetBytes(sessionID).CopyTo(keepAlivePacket, 6);

            try
            {
                await socket.SendToAsync(new ArraySegment<byte>(keepAlivePacket), SocketFlags.None, clientEP);
            }
            catch(ObjectDisposedException)
            {
                
            }
            //Console.WriteLine($"[UDP] KeepAlive 전송: {clientEP}, Seq: {currentSeq}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] KeepAlive 전송 오류: {ex}");
        }
    }

}

