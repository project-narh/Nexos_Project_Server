using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    //c#을 최대한 이용하는 방법과 수동적으로 만드는 방법 둘다 할거
    public interface IJobQueue
    {
        void Push(Action job);
    }


    public class JobQueue : IJobQueue
    {
        Queue<Action> _jobQueue = new Queue<Action>();
        object _lock = new object();
        bool _flush = false;

        public void Push(Action job)
        {
            bool flush = false; // 실행하는동한 다른 애들이 접근할 수 없으니 단계적으로 풀어주기 위해서
            lock (_lock)
            {
                _jobQueue.Enqueue(job);
                if (_flush == false)
                    flush = _flush = true;
            }
            if (flush)
                Flush();
        }

        //누가 이걸 실행시키지?
        /*
         * 1번 방법 - 메인이나 다른 스레드가 순차적으로 돌게 하는 방법 
         * 2번 푸쉬를 할때 처음으로 일을 넣은 사람이 실행하고 아니라면 바로 빠져나오는 방법
         * 서버 코어의 세션에서 Send에서와 유사
         */

        //GameRoom에서 사용하는 메서드에서 Lock을 사용할 이유가 없어짐 왜냐면 어차피 혼자 실행되니까

        void Flush() // 하나의 쓰레드만 가능
        {
            while (true)
            {
                Action action = Pop();
                if (action == null) return;

                action.Invoke();
            }
        }

        Action Pop()
        {
            lock (_lock) // Flush는 나 혼자 하고 있지만 Pop에 왜 락을 거냐면 하나씩 꺼내는 동안 다른애가 Push를 통해 넣을 수 있기 때문
            {
                if (_jobQueue.Count == 0)
                {
                    _flush = false;
                    return null;
                }
                return _jobQueue.Dequeue();
            }
        }
    }
}
