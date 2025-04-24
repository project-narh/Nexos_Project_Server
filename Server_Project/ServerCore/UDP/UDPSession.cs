//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Numerics;
//using System.Text;
//using System.Threading.Tasks;

//public class UDPSession //송수신 담당
//{
//    EndPoint clientEP;
//    Socket socket;
//    int sequnce; // 패킷 순서를 보장하기 위한 숫자
//    DateTime lastTime; // 마지막 패킷 받은 시간
//    static TimeSpan timeOut = TimeSpan.FromSeconds(60);
//    private HashSet<int> recivSeq = new HashSet<int>(); // 중복 방지
//    public int UID = -1;
//    public Vector3 position { get; set; }
//    public Quaternion rotation { get; set; }

//    public UDPSession(EndPoint clientEP, Socket socket)
//    {
//        this.clientEP = clientEP;
//        this.socket = socket;
//        lastTime = DateTime.UtcNow;
//        StartTimeOut();
//    }

//    public void Onconnected()
//    {
//        Console.WriteLine($"[UDP] 현재 {clientEP}가 연결되었습니다.");
//    }

//    public void OnDisconnected()
//    {
//        Console.WriteLine($"[UDP] 현재 {clientEP}가 타임아웃으로 연결이 종료되었습니다.");
//    }

//    private void StartTimeOut()
//    {
//        Task.Run(async () =>
//        {
//            while (true)
//            {
//                await Task.Delay(10000);
//                if(DateTime.UtcNow - lastTime > timeOut)
//                {
//                    OnDisconnected();
//                    break;
//                }
//            }
//        });
//    }

//    //public void OnReceive(ArraySegment<byte> buffer)
//    //{
//    //    lastTime = DateTime.UtcNow;
//    //    int recivs = BitConverter.ToInt32(buffer.Array, buffer.Offset); // 시퀀스
//    //    if (recivSeq.Contains(recivs)) return;
//    //    recivSeq.Add(recivs); // 시퀀스 넘버를 확인해 중복 방지
//    //    Console.WriteLine($"[UDP] 수신된 데이터 : {buffer} 송신자 : {clientEP}");
//    //    PacketManager.Instance.HandlePack(this, buffer);
//    //    SendACK(recivs);
//    //}

//    public void Send(ArraySegment<byte> buffer)
//    {
//        lastTime = DateTime.UtcNow;
//        byte[] data = new byte[buffer.Count + sizeof(int)]; // 시퀀스 포함하기 위해서 +
//        BitConverter.GetBytes(sequnce++).CopyTo(data, 0);
//        Array.Copy(buffer.Array, buffer.Offset, data, sizeof(int), buffer.Count);
//        socket.SendTo(data, clientEP);
//    }

//    public void SendACK(int seqNumber) 
//    {
//        byte[] ack = BitConverter.GetBytes(seqNumber);
//        socket.SendTo(ack, clientEP);
//    }

//    public int GetSeq(ArraySegment<byte> buffer)
//    {
//        return BitConverter.ToInt32(buffer.Array, buffer.Offset);
//    }
//}

