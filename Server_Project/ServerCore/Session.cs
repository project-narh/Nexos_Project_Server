using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    //패킷 세션을 만들어준다
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;

        //패킷이 왔다는건 [size(2)][packetId(2)][추가적인 내용]... [size(2)][packetId(2)][추가적인 내용]이런식으로 올거
        //운나쁘게 1바이트만 오면 다른게 올때 까지 기다리고
        //2바이트가 왔다고 하면 지금 분석하는 패킷이 몇바이트짜리인지 보고 해당하는 데이터가 올때까지 기다렸다가 처리하는 방식으로 이뤄질 것
        
        // 패킷 내용의 사이즈를 패킷 크기의 사이즈를 보내줄지, 아니면 헤더를 포함한 패킷 내용의 사이즈를 포함할지는 정해주면 된다
        //전체를 추천한다.
        
        public sealed override int OnRecv(ArraySegment<byte> buffer) // sealed는 다른 클래스에서 패킷세션을 상속받아서 OnRecv를 오버라이드하려면 에러가 뜬다
        {
            int processLen = 0; // 몇바이트를 처리했는지
            int packetCount = 0; // 패킷 몇개 처리했느지

            while (true) // 패킷을 계속 처리할 수 있을대 까지 처리
            {
                if (buffer.Count < HeaderSize) //2보다 작으면 말이 안된다 왜냐 ushort로 받기 때문 (최소한 헤더는 파싱할 수 있는지 확인)
                    break;
                //패킷이 완전체로 도착했는지 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize) //패킷이 완전체가 아닌 부분적으로 온 경우
                    break;

                //여기까지 왔다면 패킷을 조집하는게 가능하다
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));//여기에 들어갈건 패킷을 만들어서 보내줘도 되고 해당 영역을 찝어줘도 된다 지금할건 패킷 영역 찝어주기
                                                                                            //패킷의 범위를 보내주는거야
                packetCount++;
                processLen += dataSize;

                //데이터가 패킷 세트 하나가 끝난다면 다른 세트를 해줘야 한다 이동해야 하니
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize); // 힙 영역 아님 스택이라 상관없음
                //buffer.Slice(); 이런 함수로 잘라서 보내줘도 된다
            }
            if (packetCount > 1)
                Console.WriteLine($"패킷 모아보내기 : {packetCount}");

            return processLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer); // 이제 페킷 세션을 사용하는 사람들은 OnRecvPacket로 받으라고 바꿔치기
        //이를 컨텐츠 딴에서 분기해서 진행하면 된다.
    }


    // 이번 시간은 이벤트 핸들러
    //패킷을 보낼때 send로 보낸다 그런데 반대로 받는건 그냥 하드코딩으로 콘솔에다만 로그를 찍고 끝냈다
    //콜백이든 뭐든 메시지를 받았다는걸 연동해야 한다
    //이걸 리스너에서 한적이 있다
    //접속이 완료되었을때 Action으로 만든 콜백을 돌려보내 접속이 완료되었다는걸 확인했다
    //즉 이걸 세션에서도 해야줘야 한다.
    //서버 코어는 서버 엔진같은 존재라서 나중에 콘텐츠는 서버에서 만들어주게 될 것
    //이벤트를 받아주는 방식 1. 이벤트 핸들러를 만들어 연결 2. 아에 세션을 상속받아 만드는 방법
    //필요한걸 보면 4가지 종류 OnConnected, OnRecv() OnSend OnDisconnected
/*    class SessionHandler
    {
        public void OnConnected(EndPoint endPoint) { }
        public void OnRecv(ArraySegment<byte> buffer) { }
        public void OnSend(int numOfBytes) { }
        public void OnDisconnected(EndPoint endPoint) { }
    }*///이렇게 만들어주는게 첫번째 방법 

    //함수를 추상화하기 위해 클래스를 추상화로 변경 즉 무조건 상속을 하게 만든다
    //마찬 가지로 클래스 라이브러화로 다른 프로젝트에서 사용할 수 있게 public으로 변경
    public abstract class Session
    {
        Socket _socket;
        int _disconnected = 0;

        //리시브 버퍼 추가
        //패킷을 100개씩 보내는데 막상 보면 100씩 안가는거 같다 이는 버퍼 크기 때문일 가능성이 크다 현재 1021라서 손실이를 65535로 변경
        RecvBuffer _recvBuffer = new RecvBuffer(65535);

        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs(); // _sendArgs처럼 빼도 무관 다만 C++서버는 이런식
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs(); // 재사용 해야지 계속 생성하는건 말이 안되고 의미도 없어
        List<ArraySegment<byte>> _Pendinglist = new List<ArraySegment<byte>>(); // 말그대로 대기중인 목록
        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        object _lock = new object();

        public abstract void OnConnected(EndPoint endPoint); // 이건 리스너에서 해주고 있었다
        //데이터를 
        public abstract int OnRecv(ArraySegment<byte> buffer); //데이터를 얼마나 처리했는지 확인하기 위해 void에서 int로 변경
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);

        void Clear()
        {
            lock(_lock)
            {
                _sendQueue.Clear();
                _Pendinglist.Clear();
            }
        }

        public void Start(Socket socket)
        {
            _socket = socket;
            //SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs(); // _sendArgs처럼 빼도 무관 다만 C++서버는 이런식
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            //이제 이 위치에서 하는게 아니다
            //recvArgs.SetBuffer(new byte[1024], 0, 1024);
           
            
            //버퍼의 offset은 0이고 크기는 1024까지 가능한 버퍼
            //TCP는 100를 보낸다고 전부 100으로 온다는 보장이 없다.
            //그러면 넘어온 바이트를 파악하고 전부다 안오면 리시브 곱하기 보간만 하고 있다가 추가로 오면
            //이를 조립해서 완성시켜 한번에 처리가 되도록 수정해야 한다
            //지금까지의 코드는 완벽하게 왔다는 가정하에 만들고 있다
            //만약 80의 데이터가 오면 오프셋을 조정해서 더 받아야 할거야


            //이렇게 하는 이유는 버퍼를 엄청 크게 나눠서 세션끼리 나눠쓰는 경우가 있기 때문
            //이렇게 하면 세션을 만들때 마다 버퍼를 만듬
            //추가 정보를 주고 싶으면
            //recvArgs.UserToken = 어떤 정보든 상관 없음; 식별자나 연동하고 싶은 데이터가 있을대
            //SetBuffer로 해줘야한다 기존에 리슨때는 args.AcceptSocket = null;
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted); // 수정 
            RegisterRecv();

            //낚시대를 쓰고 올려서 다시 던지고하는것과 같다

        }
        //send는 async가 아직 아님 그리고 그렇다고 해도 문제가 있다.
        /*public void Send(byte[] sendBuff)
        {
            //_socket.Send(sendBuff);
            SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            sendArgs.SetBuffer(sendBuff,0,sendBuff.Length);
            Registersend(sendArgs);
            //MMO를 만들면 유저가 많은데 조금만 움직여도 모두에게 정보를 보내야 하는데
            //이걸 계속한다는건 말이 안된다.
            //데이터를 뭉쳐서 보내면 좋겠다.
            //그리고 이전에 만든 SocketAsyncEventArgs를 재사용하는 방법으로
        }*/

        /*        public void Send(byte[] sendBuff)
                {
                    _sendArgs.SetBuffer(sendBuff, 0, sendBuff.Length);
                    Registersend();
                    //MMO를 만들면 유저가 많은데 조금만 움직여도 모두에게 정보를 보내야 하는데
                    //이걸 계속한다는건 말이 안된다.
                    //데이터를 뭉쳐서 보내면 좋겠다.
                    //그리고 이전에 만든 SocketAsyncEventArgs를 재사용하는 방법으로
                }
                //이렇게 고친 방법은 더욱 멀티 쓰레드 환경에서 문제가 된다
                //버퍼 전달이 안되도 버퍼를 맘대로 바꿀 수 있기 때문
                //매번 RegisterSend로 보내는게 아닌 전송이 끝날때 까지 모든 데이터를 큐에 쌓아두고 완료되면 보내는 방식*/

        public void Send(ArraySegment<byte> sendBuff)
        {
            lock (_lock) //락을 잡아 한번에 한명씩만
            {
                _sendQueue.Enqueue(sendBuff); // 큐도 변경
                //즉 리스트를 따로 관리하고 있으니 이것도 이렇게 리스트로 확인이 가능
                if (_Pendinglist.Count == 0 ) Registersend(); // 이대로 사용해도 무관하나 좀 더 최적화하기 위해선 해당 메소드를 건드려야한다 

                //만일 이걸 이렇게 안하고 어느정도 모이면 보내게 한다면 패킷 모아보내기를 엔진에서 할 수 있다

            }
        }

        public void Send(List<ArraySegment<byte>> sendBuffList)
        {
            if (sendBuffList.Count == 0) return; //문제 생길거 방지

            lock (_lock) //락을 잡아 한번에 한명씩만
            {
                foreach(ArraySegment<byte> sendBuff in sendBuffList)
                    _sendQueue.Enqueue(sendBuff); // 큐도 변경

                //즉 리스트를 따로 관리하고 있으니 이것도 이렇게 리스트로 확인이 가능
                if (_Pendinglist.Count == 0) Registersend(); // 이대로 사용해도 무관하나 좀 더 최적화하기 위해선 해당 메소드를 건드려야한다 

                //만일 이걸 이렇게 안하고 어느정도 모이면 보내게 한다면 패킷 모아보내기를 엔진에서 할 수 있다

            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            OnDisconnected(_socket.RemoteEndPoint);
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            Clear();
        }
        //동시다발적 혹은 동시에 2개가 실행이 되면?

        #region 네트워크
        // 이대로 사용해도 무관하나 좀 더 최적화하기 위해선 해당 메소드를 건드려야한다 
        void Registersend() // 어차피 전역으로 선언되었기 때문에 매개변수로 넣어줄 필요가 없다
        {
            if (_disconnected == 1) return;

            /*_pending = true;
            byte[] buff = _sendQueue.Dequeue();
            _sendArgs.SetBuffer(buff, 0, buff.Length);*/
            //args를 확인하면 버퍼 리스트가 존재 리스트에 연결된 모든 애들을 한번에 다 보내주는 기능을 한다
            //즉 리스트를 만들어 사용하면 더 효율적
            //조심할 점으로는 SetBuffer와 BufferList를 동시에 사용하면 에러 즉 한개만 선태 

            //이제 이쪽은 멀티쓰레드를 고려해야한다
            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue(); //이도 동일하게 ArraySegment로 변경
                _Pendinglist.Add(buff);
                //_sendArgs.BufferList.Add(buff, 0, buff.Length); 이렇게 넣으면 안된다
                //_sendArgs.BufferList.Add(new ArraySegment<byte>(buff, 0, buff.Length));
                //byte[] 배열이 아닌 Arraysement를 원하기 때문
                //따라 들어가면 Struct 형태 즉 힙이 아닌 스택에 데이터 저장
                //이는 즉 할당이 아닌 복제형태로 들어갈 것
                //C#은 배열을 볼때 C++은 포인터로 주소를 주면 되지만 C#은 그게 불가능
                //무조건 버퍼의 첫 주소만 알 수 있어서 인덱스를 넘겨줘서 시작과 사용할 크기를 알려주는 것

                //MSDN에도 안나왔지만 버퍼 리스트는 완성을 따로 만들어서 넘겨줘야 한다 그래야 문제가 안생김
                //즉 _sendArgs.BufferList.Add(new ArraySegment<byte>(buff, 0, buff.Length)); 이렇게 추가하면 문제 생김
                //위 처럼list.Add(new ArraySegment<byte>(buff, 0,buff.Length)); 이렇게 하고 완성본 넘겨야함


                //이렇게 너무 한번에 보내버리면 안된다 버퍼를 추적해서 상대방이 받을 수 있는지 양이 많은지도 확인해야함
                //상대방이 못받는데 너무 어거지로 보내버리면 안되기 때문
                //너무 양이 이상하게 많이 오고(DDOS)하면 차단 하는 기능이 아직 없다

                //프로젝트 마다 다르지만 엔진은 그냥 서로 보내기를 하다 컨텐츠를 만들대 모든 행위를 기록을 하고 한번에 보내는게 좋다고 생각한다고 말씀
            }
            _sendArgs.BufferList = _Pendinglist;

            try
            {
                bool pendig = _socket.SendAsync(_sendArgs);
                if (!pendig) OnSendCompleted(null, _sendArgs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RegisterSend Failed : {ex}");
            }
            
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock(_lock)// 콜백 방식으로 다른 쓰레드에서 사용할 수 있기 때문에 lock을 사용
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {

                    //이제 애매한 부분 Send하는 부분이 정해져있지 않다
                    //다른 방법으로 접근해야 한다
                    try
                    {
                        //예약한 리스트가 완료되었을때니
                        _sendArgs.BufferList = null;
                        _Pendinglist.Clear();

                        OnSend(_sendArgs.BytesTransferred);

                        //이전과 같이 하면 버퍼가 2개가 되버리는 문제가 발생한다
                        //똑같은 정보를 2번 보내는 일
                        if (_sendQueue.Count > 0)
                        {
                            Registersend();
                        }
     
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OnSendCompleted Failed {ex}");
                    }
                }
                else
                {
                    // 상대방에게 문제가 있다는거니까
                    Disconnect();
                }
            }
        }
        /*void RegisterRecv(SocketAsyncEventArgs args)
        {
            //이제 여기에서 현재 사용할 수 있는 버퍼를 찝어줘야 한다
            ArraySegment<byte> segment =  _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            bool pending = _socket.ReceiveAsync(args);
            if (!pending) OnRecvCompleted(null, args);
        }*/
        void RegisterRecv()
        {
            if (_disconnected == 1) return;
            //이제 여기에서 현재 사용할 수 있는 버퍼를 찝어줘야 한다
            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
            try
            {
                bool pending = _socket.ReceiveAsync(_recvArgs);
                if (!pending) OnRecvCompleted(null, _recvArgs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RegisterRecv Failed {ex}");
            }
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            //이부분은 좀 다름
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success) // 몇 바이트를 받았느냐
            {
                //TODO
                try
                {
                    //write 커서를 이동해야 한다
                    if(_recvBuffer.OnWrite(args.BytesTransferred) == false)// 이런 상황이 일어난다면이건 버그
                    {
                        Disconnect();
                        return;
                    }

                    //컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리했는지 받는다.
                    int processLen = OnRecv(_recvBuffer.ReadSegment);
                    if(processLen < 0 || _recvBuffer.DataSize < processLen) // 문제가 있는 상황
                    {
                        Disconnect();
                        return;
                    }

                    //Read 커서 이동
                    if(_recvBuffer.OnRead(processLen) == false)
                    {
                        Disconnect();
                        return;
                    }

                    //OnRecv(new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred)); //처음부터 끝까지 유효범위였다
                    RegisterRecv();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            else
            {
                Disconnect();
            }
        }
    }
    #endregion
}
