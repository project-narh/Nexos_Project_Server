using System;
using System.Net;
using ServerCore;
using System.Threading.Tasks;

namespace Server
{ 
    class Program
    {

        static async Task Main(string[] args)
        {
            Console.WriteLine("=============================================================");
            UDPServer udpServer = new UDPServer(null, 8888, 100);
            Task udpTask = Task.Run(() => udpServer.UDP_Start());
            try
            {
                await udpTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 실행 중 오류 발생: {ex.Message}");
            }

            Console.WriteLine("프로그램이 종료되었습니다.");
        }
    }

}