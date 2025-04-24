using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Server.UDP.Session
{
    public class SessionManager
    {
        private static readonly object _lock = new object();
        public static SessionManager Instance { get; private set; } = new SessionManager();
        public static event Action<EndPoint> OnSessionRemoved;

        //현재는 빠른 개발을 위해 일단 세션 ID를 발급받고 추후 IPEndPoint로 재변경
        //private ConcurrentDictionary<IPEndPoint, UDPSession> sessions = new ConcurrentDictionary<IPEndPoint, UDPSession>();
        public ConcurrentDictionary<int, UDPSession> sessions = new ConcurrentDictionary<int, UDPSession>();

        int _sessionId = 0;

        public UDPSession GetOrCreateSession(EndPoint clientEndPoint, Socket udpSocket)
        {
            if (clientEndPoint is IPEndPoint ipEndPoint)
            {
                lock(_lock)
                {
                    var existSession = sessions.Values.FirstOrDefault(s => ((IPEndPoint)s.clientEP).Address.Equals(((IPEndPoint)clientEndPoint).Address) &&
                ((IPEndPoint)s.clientEP).Port == ((IPEndPoint)clientEndPoint).Port);

                    if (existSession != null)
                    {
                        sessions.TryRemove(existSession.sessionID, out _);
                        existSession.OnDisconnected();
                    }
                    int id = ++_sessionId;
                    UDPSession session = new UDPSession(ipEndPoint, udpSocket,id);
                    sessions[id] = session;
                    Console.WriteLine($"[UDP] 클라이언트 세션 접속 {id} ");
                    session.room = UDPServer.room;

                    return session;
                }

                //if (!sessions.TryGetValue(ipEndPoint, out UDPSession session))
                //{
                //    session = new UDPSession(ipEndPoint, udpSocket);
                //    sessions[ipEndPoint] = session;
                //}
            }
            else
            {
                throw new ArgumentException("[UDP] 잘못된 타입입니다", nameof(clientEndPoint));
            }
        }

        public bool RemoveSession(int sessionId)
        {
            lock (_lock)
            {
                if (sessions.TryGetValue(sessionId, out UDPSession session))
                {
                    sessions.TryRemove(sessionId, out _);
                    OnSessionRemoved?.Invoke(session.clientEP);


                    Console.WriteLine($"[UDP] 클라이언트 세션 종료: {sessionId}");
                    return true;
                }
                return false;
            }
        }
        public UDPSession GetSession(int sessionId)
        {
            lock (_lock)
            {
                sessions.TryGetValue(sessionId, out UDPSession session);
                return session;
            }
        }

        //public void RemoveSession(UDPSession clientEndPoint)
        //{
        //    lock (_lock)
        //    {
        //        sessions.Remove(clientEndPoint.sessionID);
        //    }

        //    //if (clientEndPoint is IPEndPoint ipEndPoint)
        //    //{
        //    //    sessions.TryRemove(ipEndPoint, out _);
        //    //    Console.WriteLine($"[UDP] {clientEndPoint}가 세션에서 나갔습니다.");
        //    //}
        //    //else
        //    //{
        //    //    throw new ArgumentException("[UDP] 잘못된 타입입니다", nameof(clientEndPoint));
        //    //}
        //}
    }
}

