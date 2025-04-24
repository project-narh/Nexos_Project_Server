using Org.BouncyCastle.Asn1.Ocsp;
using Server.UDP.Room;
using ServerCore;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UDP;

namespace Server
{
    public class UDPServer
    {
        private Socket socket;
        private int port;
        private IPEndPoint endPoint;
        public static int timeTick = 100;

        public static UDPGameRoom room { get; } = new UDPGameRoom();
        private UDPListner _listener = new UDPListner();
        private static readonly int MaxPacketSize = 30000;

        public UDPServer(string address, int port, int _timeTick)
        {
            this.port = port;
            timeTick = _timeTick;
            IPAddress ipAddr;

            if (address != null) ipAddr = IPAddress.Parse(address);
            else
            {
                string host = Dns.GetHostName();
                IPHostEntry ipHost = Dns.GetHostEntry(host);
                ipAddr = ipHost.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            }
            endPoint = new IPEndPoint(ipAddr, port);
        }

        static void FlushRoom()
        {
            try
            {
                //room.Push(() => room.Flush()); // 해당 방식은 UDP에 적합하지 않음
                JobTimer.Instance.Push(FlushRoom, timeTick);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UDP] 실행중 에러 발생 : " +ex.ToString());
            }
        }

        public async Task UDP_Start()
        {
            Console.WriteLine($"[UDP] Listening... {endPoint}");
            _listener.Init(endPoint, MaxPacketSize);
            //JobTimer.Instance.Push(FlushRoom);

            await Task.Delay(-1);// 무한대기

        }

        private IPAddress ResolveIPAddress(string address)
        {
            if (!string.IsNullOrEmpty(address))
                return IPAddress.Parse(address);

            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            return ipHost.AddressList[0];
        }

    }
}
