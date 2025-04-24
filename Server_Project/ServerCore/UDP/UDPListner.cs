//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading.Tasks;
//using Server.UDP;

//namespace ServerCore.UDP
//{
//    public class UDPListner // 패킷 수신 세션 관리
//    {
//        private Socket socket;
//        private EndPoint endPoint; // 이거로 구분
//        private byte[] recvBuffer;
//        private Dictionary<EndPoint, UDPSession> sessions;

//        public void Init(EndPoint localEnd, int bufferSize = 65535)//초기화 및 수신 시작 UDP 최대 크기 65535
//        {
//            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
//            socket.Bind(localEnd);
//            endPoint = new IPEndPoint(IPAddress.Any, 0); // 모든 패킷
//            recvBuffer = new byte[bufferSize];
//            sessions = new Dictionary<EndPoint, UDPSession>();
//            Start(); // 수신 시작
//        }

//        private void Start() // 비동기 수신 작업
//        {
//            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
//            args.SetBuffer(recvBuffer,0,recvBuffer.Length);
//            args.RemoteEndPoint = endPoint;
//            args.Completed += OnReceiveCompleted;
//            socket.ReceiveFromAsync(args);
//        }

//        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
//        {
//            if(args.BytesTransferred > 0 && args.SocketError == SocketError.Success) // BytesTransferred 수신된 바이트
//            {
//                EndPoint client = args.RemoteEndPoint;
//                if(!sessions.ContainsKey(client))
//                {
//                    sessions[client] = new UDPSession(client,socket);
//                }
//                sessions[client].OnReceive(new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred));
//            }
//        }
//    }
//}
