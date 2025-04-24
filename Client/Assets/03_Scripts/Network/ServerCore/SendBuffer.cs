using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * send는 외부에서 바이트를 만들어줘서 전달하고 있다
현재까지는 간단한 경우라 문제가 안되지만

게임을 복잡하게 만든다면 패킷으로 보내야 하기 때문에 별도로 관리를 해야 한다

public Knight
 int hp
 attack 
이런 정보가 있다면 이걸 
Knight knight = new Knight(100,10)
byte[] send = new byte[1024]
byte[] buffer = BitConverter.GetBytes(Knight.hp)
byte[] buffer2 = BitConverter.GetBytes(Knight.attack)
array.copy(buffer, 0, sendbuffer, 0, buffer.Length);_
array.copy(buffer2, 0, sendbuffer, buffer.Length, buffer2.Length);_
 이런식으로 정보를 정보를 보내야 하기에 이를 관리하는 클래스를 만들어야 한다
그냥 위처럼 하면 성능적인 이슈가 발생 유저 100명이면 각각의 정보를 받아야 하므로

그냥 개별적으로 보내면
100 * 100 1만 데이터를 보내야한다
이때문에 관리를 위해서

버퍼 사이즈를 몇으로 할지가 애매하다
큰 덩어리를 만들어서 차츰차츰 사용하는 방식
     */

    public class SendBufferHelper // 사용하기 쉽게
    {
        //ThreadLocal로 한 이유는 쓰레드끼리의 경합을 제거하기 위해서
        public static ThreadLocal<SendBuffer> CurrentBuffer = new ThreadLocal<SendBuffer>(() => { return null; }); //전역은 전역인데 나만의 쓰레드에서만 사용할 수 있다
        
        //쓰레드마다 청크를 크게 나누고 이를 쪼개서 사용한다.
        //SendBuffer도 4096에서 65535로 변경
        public static int chunkSize { get; set; } = 65535; // 이제 유니티에서 관리하니 너무 크게 잡을필요 없다 오히려 크면 문제 생기니 *100 제거

        public static ArraySegment<byte> Open ( int reserveSize )
        {
            if (CurrentBuffer.Value == null) // 한번도 사용 안한것
                CurrentBuffer.Value = new SendBuffer(chunkSize);

            if(CurrentBuffer.Value.FreeSize < reserveSize) // 사이즈보다 작다면 기존의 청크를 대체 기존 청크 버리는거 다만 C++이면 참조하는게 없으면 메모리 풀에 반환하여 재사용할 수 있음 c#은 빡시다
                CurrentBuffer.Value = new SendBuffer(chunkSize);
            return CurrentBuffer.Value.Open(reserveSize);
        }
        public static ArraySegment<byte> Close(int usedSize)
        {
            return CurrentBuffer.Value.Close(usedSize);
        }
    }
    //그런데 help에서 관리 자체는 ThreadLocal 사용하여 괜찮지만 문제는 버퍼 자체는 락이 없다 다수의 쓰레드에서 참조해서 사용할거야
    // 그렇지만 문제가 되지 않는 이유는 직접 수정하는게 아니라 읽기만 하기 떄문
    // 정보를 쓰는건 초창기 한 번만 해주고 읽기만 한다.


    public class SendBuffer
    {
        byte[] _buffer;
        int _usedSize = 0; //recvbuffer에서 w와 동일한 역할
        //이번에는 읽고 쓰고 이런게 없다 왜냐 최대한 얼마나 쓸지 때어 쓰는 방식이며 크기가 가변적이라서 여유 공간을 많이 두고 보냄
        //recv와 방식이 다르다 이전에는 클린이 있어서 초기화 했는데
        //샌드는 한명한테만 보내는게 아니라 여러명한테 보내는거라 나는 보냈어도 큐에 남아있을 수 있어서 버리기 애매하다 ()
        //
        public SendBuffer(int chunkSize)
        {
            _buffer = new byte[chunkSize];
        }

        public int FreeSize { get { return _buffer.Length - _usedSize; } }
        public ArraySegment<byte> Open(int reserveSize)//예상하는 최대 크기를 예약
        {
            if (reserveSize > FreeSize) return null;
            return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
        }

        public ArraySegment<byte> Close(int usedSize) // 실제로 사용한 크기
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
            _usedSize += usedSize;
            return segment;
        }
    }
}
