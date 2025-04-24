using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    public class RecvBuffer
    {
        //[][][][][][][][][][] 10byte 배열이라고 가정 
        //도중 도중 보낼 크기가 완성되면 보내주고 r 과 w를 왔다 갔다 조절한다.
        //이때 만일 1byte만 와서 패킷이 완성 안되었다고 할때 그러면 가장 처음으로 보내서 용량을 조절한다
        ArraySegment<byte> _buffer;
        int _readPos;
        int _writePos;

        public RecvBuffer(int bufferSize)
        {
            _buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
        }

        public int DataSize { get { return _writePos - _readPos; } } // 유효범위 데이터가 얼마나 쌓였나
        //처음에는 r과 w가 동일하지만 데이터가 쌓이면 w가 r보다 앞
        public int FreeSize { get { return _buffer.Count - _writePos; } } // 버퍼의 남은 공간

        public ArraySegment<byte> ReadSegment // (DataSegment)유효 범위 세그먼트 (어디부터 데이터를 읽으면 되나)
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize); }
        }
        public ArraySegment<byte> WriteSegment //(RecvSegment)리시브를 할때 어디부터 어디까지가 유효범위인지
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize); }
        }

        public void Clean() // 버퍼 끝까지 도착하기 전에 처음으로 정리해줘야 한다
        {
            //데이터 유효범위가 존재한다면 그냥 처음으로 가는게 아니라 유효 데이터도 함께 처음으로 옮기면서 정리해야 한다
            int dataSize = DataSize;
            if(dataSize == 0) // 유효 데이터가 없고 다 처리된 상태 
            {
                _readPos = _writePos = 0; // 남은 데이터가 없으면 복사하지 않고 커서 위치만 리셋  
            }
            else // 처리되지 않은 데이터가 존재
            {
                Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
                _readPos = 0;
                _writePos = dataSize;
            }
        }

        public bool OnRead(int numOfBytes) // 컨텐츠 코드에서 이 데이터를 가공할텐데 성공적으로 처리하면 커서 위치 이동
        {
            if(numOfBytes > DataSize) // 지금 더 읽는다고 할때
                return false;
            _readPos += numOfBytes;
            return true;
        }

        public bool OnWrite(int numOfBytes)// 반대로 클라이언트에서 데이터를 쏴서 리시브를 할때
        {
            if (numOfBytes > FreeSize) // 현재 가능 데이터보다 초과
                return false;
            _writePos += numOfBytes;
            return true;
        }

    }
}
