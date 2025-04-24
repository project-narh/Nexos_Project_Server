using System;
using System.Net;
using ServerCore;
using System.Threading.Tasks;
using Server.Web;
using Server.Manager;
using Server.Database;
using Server.BlockChain;
using dotenv.net;

//using Server.Packet.Protocol; // 추가 서버 코어를 라이브러리화하였기 때문에

//이로서 서버 코어는 엔진    서버는 콘텐츠를 관리할 수 있게 되었다
// 세션 인터페이스만 사용하고 이벤트만 지정해서 사용하고 있다 현재

namespace Server
{
    /*    패킷을 구분하는 방법은 뭘까?
    ID로 1 이동 2 채팅 이런식으로 하는 방법이 있을 수 있다

    다만 문제는 경우에 따라 유동적으로 사이즈가 달라질 수 있다는 점이다
    그래서 첫 인자로 size 두번째로 ID를 넘겨주는 경우가 대다수이다. (int short 둘 중 ushort로 충분히 사용하긴 한다)
     */


    class Program
    {

        static async Task Main(string[] args)
        {
            DotEnv.Load();
            ManagerInitializer.InitializeAll();
            Console.WriteLine("=============================================================");
            //TCPServer tcpServer = new TCPServer(null,7777,250);
            UDPServer udpServer = new UDPServer(null, 8888, 100);
            var webServer = CreateHostBuilder(args).Build();
            //_=Task.Run(()=>tcpServer.TCP_Start());
            Task webTask = Task.Run(() => webServer.Run());
            Task udpTask = Task.Run(() => udpServer.UDP_Start());
            //Task tcpTask = Task.Run(() => tcpServer.TCP_Start());
            await Task.WhenAll(udpTask, webTask);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>().UseKestrel(options =>
                {
                    options.ListenAnyIP(8259);
                    options.ListenAnyIP(443, listenOptions =>
                    {
                        listenOptions.UseHttps(@"C:\certs\cert.pfx", Environment.GetEnvironmentVariable("CERT_PASSWARD"));
                    });
                });
            });
    }

}