using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServerCore;

namespace Server
{
    struct JobTimerElem : IComparable<JobTimerElem>
    {
        public int execTick; // 실행 시간
        public Action action;

        public int CompareTo(JobTimerElem other)
        {
            return other.execTick - this.execTick;
        }
    }

    //잡을 예약하는 시스템
    //더 진지하게 관리할거면
    //5분이상 한참 남은잡은 우선순위 큐에서 관리하고 시간이 임박하면 버킷단위로 생각해서 리스트로 관리하는 방법이 있다
    //클라이언트는 유니티의 Coroutine의 wait for seconds 같이 중앙에서 관리 하는 기능이 있으니 또 만들 필요 없지만,
    //서버 쪽에서는 이렇게 중앙 관리 시스템이 있으면 굉장히 좋다
    internal class JobTimer
    {
        PriorityQueue<JobTimerElem> _pq = new PriorityQueue<JobTimerElem>(); // 다른 스세드에서도 사용 가능한 공용 데이터라 락 걸어
        object _lock = new object();
        
        public static JobTimer Instance { get; } = new JobTimer();
        public void Push(Action action, int tickAfter = 0)
        {
            JobTimerElem job;
            job.execTick = System.Environment.TickCount + tickAfter; // 언제?
            job.action = action;

            lock (_lock)
            {
                _pq.Push(job);
            }
        }

        public void Flush()
        {
            while(true)
            {
                int now = System.Environment.TickCount;
                JobTimerElem job;
                lock (_lock)
                {
                    if (_pq.Count == 0) break;

                    job = _pq.Peek();
                    if (job.execTick > now) break;

                    _pq.Pop(); // job으로 안받아도 된다 왜냐면 그게 peek으로 이미 받았거든
                }
                job.action.Invoke();
            }
        }
    }
}
