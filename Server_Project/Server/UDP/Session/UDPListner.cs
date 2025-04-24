using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Server.UDP;
using Server.UDP.Session;

namespace UDP
{
    public class UDPListner // 패킷 수신 세션 관리
    {
        private Socket socket;
        private EndPoint endPoint;
        private byte[] recvBuffer;
        private Dictionary<EndPoint, UDPSession> sessions;
        private SocketAsyncEventArgs args;
        private int _disposed = 0;

        public void Init(EndPoint localEnd, int bufferSize = 65535)
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(localEnd);
                endPoint = new IPEndPoint(IPAddress.Any, 8888);
                recvBuffer = new byte[bufferSize];
                sessions = new Dictionary<EndPoint, UDPSession>();

                // SocketAsyncEventArgs 인스턴스를 한 번만 생성하고 재사용
                args = new SocketAsyncEventArgs();
                args.SetBuffer(recvBuffer, 0, recvBuffer.Length);
                args.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                args.Completed += OnReceiveCompleted;
                SessionManager.OnSessionRemoved += OnSessionRemoved;
                Console.WriteLine($"[UDP] 서버 소켓 바인딩됨: {socket.LocalEndPoint}");
                Console.WriteLine($"[UDP] 서버 준비 완료. 바인딩 주소: {socket.LocalEndPoint}");
                StartReceiving();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[URP] Init Error : {ex}");
            }
        }

        private void StartReceiving()
        {
            Console.WriteLine("[UDP] 서버 시작");
            try
            {
                if (_disposed == 1 || socket == null)
                    return;

                Socket currentSocket = socket;
                if (currentSocket == null)
                    return;

                try
                {
                    bool pending = socket.ReceiveFromAsync(args);
                    Console.WriteLine($"[UDP] ReceiveFromAsync 호출됨 → pending: {pending}");
                    if (!pending)
                    {
                        Console.WriteLine("[UDP] ReceiveFromAsync 완료 → 수동 호출 시작");
                        OnReceiveCompleted(this, args);
                    }

                }
                catch (ObjectDisposedException) { }
                catch(SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        if (socket != null)
                        {
                            try
                            {
                                socket.Close();
                                socket.Dispose();
                            }
                            catch { }

                            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            socket.Bind((IPEndPoint)endPoint);
                        }

                        StartReceiving();
                    }
                    else Console.WriteLine($"[UDP] 소켓 {ex.Message} 코드 : {ex.ErrorCode}");
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP] StartReceiving 진행중 에러: {ex}");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[UDP] StartReceiving 에러: {ex}");
            }
        }

        private async void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
        {
            ushort packetID = BitConverter.ToUInt16(args.Buffer, args.Offset + 2);

            try
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    EndPoint client = args.RemoteEndPoint;
                    ArraySegment<byte> data = new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred); // 새롭게 배열 만들지 않고 ArraySegment (가비지 컬렉션 부담 저하)
                    if (sessions.TryGetValue(client, out var session))
                    {
                        await session.ReceivePacketAsync(data);
                    }
                    else
                    {
                        // 동일 IP지만 포트 변경 등으로 인한 새 연결 처리
                        session = SessionManager.Instance.GetOrCreateSession(client, socket);
                        sessions[client] = session;
                        await session.ReceivePacketAsync(data);
                    }

                    //byte[] data = new byte[args.BytesTransferred];
                    //Console.WriteLine($"[UDP] 수신: {client} : 데이터 크기 {args.BytesTransferred} 바이트");

                    //await sessions[client].ReceivePacketAsync(data);
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Console.WriteLine($"[UDP] 소켓 {ex.Message} 코드 : {ex.ErrorCode} ㅇㅇㅇㅇㅇㅇㅇ");
                    if (args.RemoteEndPoint is IPEndPoint clientEp && sessions.TryGetValue(clientEp, out UDPSession session))
                    {
                        sessions.Remove(clientEp);
                        session.OnDisconnected();
                    }
                }
                else Console.WriteLine($"[UDP] 소켓 {ex.Message} 코드 : {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] 수신 에러: {ex}");
            }
            finally
            {
                if (_disposed == 0)
                {
                    try
                    {
                        if (socket == null)
                        {
                            Console.WriteLine("[UDP] 소켓이 null이라 재생성 시도 중...");
                            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            socket.Bind((IPEndPoint)endPoint);
                            Console.WriteLine($"[UDP] 소켓 재생성 완료: {socket.LocalEndPoint}");
                        }

                        if (!socket.ReceiveFromAsync(args))
                        {
                            OnReceiveCompleted(this, args);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("[UDP] 재수신 실패: 소켓이 종료된 상태(ObjectDisposed)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UDP] 재수신 실패: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[UDP] 수신 루프 종료: _disposed == 1");
                }
            }
        }

        private void OnSessionRemoved(EndPoint clientEP)
        {
            sessions.Remove(clientEP);
        }
    }
}
