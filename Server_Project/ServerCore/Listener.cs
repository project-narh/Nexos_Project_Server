using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    //이걸 분리하는건 그냥 분리하는 습관을 들이기 위함
    //이제 전부 public 다른 프로젝트에서도 사용할 수 있어야함
    public class Listener
    {
        Socket _listenSocket;
        Func<Session> _sessionFactory; // 연결이 완료되었음을 알려주기 위한 액션

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory, int register = 10, int backlog = 100)
        {
            _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // TCP로 통신 (문지기가 들고 있는 휴대폰)
            _sessionFactory += sessionFactory;
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(backlog); // backlog - 최대 대기수
            //문지기가 하나라서 사람이 많으면 더 많이 만들면 된다.
            //500명이 동시에 접근한다면 그중 에러가 많이 뜰거야 왜냐면 백로그는 10개이기에

            //문지기 역할로 한개지만 이를 10개로 수정 (동시다발적을 위해서)
            for (int i = 0; i < register; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();//한번만들면 계속 재사용 가능
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted); // 델리게이트로 이벤트 핸들러 등록                                                                       //최초로 낚시대를 던진것
                RegisterAccept(args);
            }
            /*for(int i = 0; i < 10; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();//한번만들면 계속 재사용 가능
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted); // 델리게이트로 이벤트 핸들러 등록                                                                       //최초로 낚시대를 던진것
                RegisterAccept(args);
            } // 낚시대를 10개정도 만들어놔도 상관 없다*/
        }

        void RegisterAccept(SocketAsyncEventArgs args) // 접속시도 이제 비동기로 예약
        {
            //재사용을 하고 있기 때문에 지워야한다
            args.AcceptSocket = null;
            bool pending = _listenSocket.AcceptAsync(args); // 바로될수도 있고 아닐 수도 있구
            if (pending == false)//운 좋게 바로 접속이 된경우
                OnAcceptCompleted(null, args);
            //유저들이 작정하고 공격하지 않는 이상 스택 오버플로어 일어날일 없다
        }

        void OnAcceptCompleted(object sender, SocketAsyncEventArgs args) // 접속 완료
        {
            if(args.SocketError == SocketError.Success)
            {
                //유저가 오면 해야할거
                //이벤트가 완료되었다고 콜백을 사용
                //TODO
                //SocketAsyncEventArgs가 일꾼 느낌
                Session session = _sessionFactory.Invoke();
                session.Start(args.AcceptSocket);
                session.OnConnected(args.AcceptSocket.RemoteEndPoint);
                //이렇게 바꾸면 문제가 되는 건
                //게임 세션은 콘텐츠 딴에 빠져있다
                //즉 이게 엔진 파트에 들어가는게 아니라 다른곳에 있어야 한다
                //밖에서 정의해야 한다
                //이를 위해 Action을 Func로 바꾼다
            }
            else
            {
                Console.WriteLine(args.SocketError.ToString());
            }

            RegisterAccept(args); // 연결이 되면 다음 들어올 사람을 위해 미리 예약
        }

        public Socket Accept()
        {
            _listenSocket.AcceptAsync();//Async를 붙여 비동기로 넣는다 성공하든 아니든 바로 리턴하고 본다
            //바로 리턴하면 유저가 접속하지 않아도 나온다는 소리 유저가 접속요청 하면 따로 알려줘야 한다는 이야기가 된다
            //비동기 계열을 쓰면 더 어려워지긴 한다
            //그러면  accept 요청 부분과 실제로 처리되는 2개로 분리되야 한다
            return _listenSocket.Accept();
            //(만약 안들어오면 어떻게 되나? 그건 블로킹 논블로킹이라는 개념이 존재 안들어오면 이 아래는 진행이 안되는 상태)
            //입출력 계열의 함수들은 비동기(논블로킹)방식으로 바꿔야 한다 (하나를 위하 계속 기다리는건 좀 아니자너)
        }
    }
}
