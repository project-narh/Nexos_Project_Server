using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using System.Buffers;

public class UDPConnect : MonoBehaviour {

    private Socket socket;
    private string addresss = "localhost";
    private int port = 8888;
    private UDPSession session;
    private IPEndPoint endPoint;
    public static CancellationTokenSource cancel = new CancellationTokenSource(); // 원할때 취소할 수 있게 하는 토큰
    public Dictionary<ushort, long> sendTimestamps = new Dictionary<ushort, long>();
    private int errorCount = 0;
    private int _disposed = 0;

    ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>(); // 유니티를 위한 작업 저장

    bool isConnected = false;

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    public class IPAddressUtility
    {
        public static string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1"; // 기본값 (로컬호스트)
            try
            {
                foreach (var netInterface in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (netInterface.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = netInterface.ToString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] 내부 IP 가져오기 실패: {ex}");
            }
            return localIP;
        }
    }

    public void SetUDP(string address, int port)
    {
        if (address == null || address.Equals("localhost")) this.addresss = "127.0.0.1";
        else this.addresss = address;
        this.port = port;

        endPoint = new IPEndPoint(IPAddress.Parse(this.addresss),port);
        UDPStart();
    }

    public void SendToServer(ArraySegment<byte> sendBuffer, ushort packetID)
    {
        if(sendBuffer.Array == null || sendBuffer.Count == 0)
        {
            Debug.Log("[UDP] 송신할 데이터가 존재하지 않습니다.");
            return;
        }
        //Debug.LogError($"[UDP] SendToServer 진입 | session null? {(session == null)}, disposed = {_disposed}");
        if (session == null || session._disposed == 1)
        {
            Debug.LogError("[UDP] SendPacket 불가 | 세션이 없거나 disposed 됨");
            return;
        }

        //Debug.Log($"[UDP] SendToServer 호출됨 - 패킷 ID: {packetID}, Size: {sendBuffer.Count}");
        session.SendPacket(sendBuffer,packetID);
    }

    public void Close()
    {
        if (!isConnected) return;

        isConnected = false;
        try
        {
            if (session != null)
            {
                session.OnDisconnected();
                session = null;
            }

            if (socket != null)
            {
                try
                {
                    socket.Close();
                    socket.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UDP] 소켓 해제중 에러 : {ex.Message}");
                }
                finally
                {
                    socket = null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP] 클라이언트 연결 해제중 에러 : {ex.Message}");
        }
        finally
        {

        }
    }

    public void UDPStart()
    {

        if (isConnected && session != null && socket != null)
        {
            Debug.Log("[UDP] 이미 연결되어 있습니다.");
            Close();
            System.Threading.Thread.Sleep(100);

            Interlocked.Exchange(ref _disposed, 0);

            session = new UDPSession(endPoint, socket);

            session.token = cancel.Token;

            Task.Run(() => OnReceive());

            return;
        }
        try
        {
            if (endPoint == null)
            {
                endPoint = new IPEndPoint(IPAddress.Parse(IPAddressUtility.GetLocalIPAddress()), 8888);
            }
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            IPEndPoint socketPort = new IPEndPoint(IPAddress.Parse(IPAddressUtility.GetLocalIPAddress()), 0);
            socket.Bind(socketPort);

            IPEndPoint localEndPoint = (IPEndPoint)socket.LocalEndPoint;
            Debug.Log($"[UDP] 클라이언트에서 사용 중인 포트: {localEndPoint.Port}");
            Debug.Log($"[UDP] UDPStart 호출됨. 서버 주소: {endPoint}");
            Debug.Log($"[UDP] 생성된 로컬 포트: {((IPEndPoint)socket.LocalEndPoint).Port}");
            session = new UDPSession(endPoint, socket);
            // cancel = new CancellationTokenSource();
            session.token = cancel.Token;
            isConnected = true;

            Task.Run(() => OnReceive());

            C_EnterGame enter = new C_EnterGame();
            enter.uid = 0; // 이후 로그인도입될때 대비 UID 남겨두기 일단 모든 유저 0

            SendToServer(enter.Write(), (ushort)PacketID.C_EnterGame);
        }
        catch(Exception ex)
        {
            Debug.LogError($"[UDP] 클라이언트 연결중 에러 : {ex.Message}");
            isConnected = false;
        }
    }

    private async Task OnReceive()
    {
        byte[] buffer = new byte[1024 * 1];
        EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isConnected)
        {
            if(cancel.Token.IsCancellationRequested || socket == null)
            {
                await Task.Delay(100);
                continue;
            }

            try
            {
                Socket currentSocket = socket; // 스냅샷
                if (currentSocket == null) continue;

                SocketReceiveFromResult receivedBytes = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEP);
                int receive = receivedBytes.ReceivedBytes;
                if (receive > 0)
                {
                    //byte[] receivedData = new byte[receive];
                    //Buffer.BlockCopy(buffer, 0, receivedData, 0, receive);
                    var receivedData = new ArraySegment<byte>(buffer, 0, receivedBytes.ReceivedBytes);
                    Enqueue(() => { session.ReceivePacket(receivedData); });
                }
            }
            catch (ObjectDisposedException) { }
            //catch (SocketException ex)
            //{
            //    Debug.LogWarning($"[UDP] 소켓 에러 : {ex.SocketErrorCode}");
            //    if (ex.SocketErrorCode == SocketError.ConnectionReset)
            //    {
            //        Debug.LogWarning("[UDP] 연결 재설정 감지 - 잠시후 서버에 재연결 시도");
            //        isConnected = false;

            //        Enqueue(() => {
            //            System.Threading.Thread.Sleep(500);
            //            UDPStart();
            //        });

            //        break; 
            //    }
            //    else
            //    {
            //        Debug.LogError($"[UDP] 소켓 에러 발생: {ex.SocketErrorCode}");
            //    }
            //}
            catch (Exception ex)
            {
                Debug.LogError($"[UDP] 수신 오류: {ex}");
            }
        }
    }


    public void Enqueue(Action action)
    {
        mainThread.Enqueue(action);
    }

    private void Update()
    {
        while (mainThread.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }
}
