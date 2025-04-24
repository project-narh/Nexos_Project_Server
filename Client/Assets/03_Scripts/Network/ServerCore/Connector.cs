using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    public class Connector
    {
        //게임에서는 블로킹함수 사용을 최대한 지향한다
        //그래서 기존 방법 말고 리슨의 반대로 접속 요청하는 클래스
        //서버라는 건 리스너로 손님을 기다리는게 있는데 왜 접속하는걸 때로 만들어줘야 할까?
        // 서버코어를 서버 메인으로 만들고 있지만 연결하고 리스브랑 샌드하는걸 공용으로 하는게 큰 이유고
        //서버를 콘텐츠로 올릴때 서버를 한개짜리로 할지 분산처리할지 (캐릭터, 몬스터, 아이템 전부 분산하는 경우 있다)
        // 다른 애들이랑 연결하여 통신하기 위해서 커넥트로 연결하긴 해야 한다.
        //한쪽은 듣고 한쪽은 연결하고 이를 진행해야해서 만들게 됨

        //설정에서 서버 코어를 콘솔 > 클래스 라이브러리로 변경

        Func<Session> _sessionFactory;

        public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count = 1) // 소캣을 만들고 하는 작업을 한번만 해서 여러번 하도록 카운트 추가
        {
            for (int i = 0; i < count; i++)
            {
                
                Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                _sessionFactory = sessionFactory;
                args.Completed += OnConnectCompleted;
                args.RemoteEndPoint = endPoint;
                //이번에는 유저토큰 사용
                args.UserToken = socket;
                //이번에는 이벤트를 왜 전역으로 선언안하냐면 Connect를 한개가 아니라 여러명일 수 있기 때문에
                RegisterConnect(args);
            }

        }

        void RegisterConnect(SocketAsyncEventArgs args)
        {
            Socket socket =  args.UserToken as Socket;
            if (socket == null) return;

            bool pending = socket.ConnectAsync(args);
            if (!pending) OnConnectCompleted(null, args);
        }

        void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if(args.SocketError == SocketError.Success )
            {
                //현재 세션은 추상화이며 리슨에서는 펙토리로 받아서 했었음 여기서도 동일
                Session session = _sessionFactory.Invoke();
                session.Start(args.ConnectSocket);
                //세션을 시작하기 위해서는 소켓을 넣어야 하기 때문에 이를 기반으로 사용하기 때문에 ConnectSocket으로 넘겨준다
                session.OnConnected(args.RemoteEndPoint);
            }
            else
            {
                Console.WriteLine($"OnConnectCompleted Fail : {args.SocketError}");
            }
        }
    }
}
