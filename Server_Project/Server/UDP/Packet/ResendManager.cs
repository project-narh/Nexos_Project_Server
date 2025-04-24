using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;

namespace Server.UDP.Packet
{
    public class ResendManager
    {
        private ConcurrentDictionary<ushort, (byte[], int)> pending = new ConcurrentDictionary<ushort, (byte[], int)>();
        private int _disposed = 0;
        private readonly UDPSession session;
        private static readonly int maxRetries = 3;
        private static readonly int DelayMs = 1000;
        private System.Timers.Timer resendTimer;
        private readonly object timerLock = new object();

        public ResendManager(UDPSession session)
        {
            this.session = session;

            //기존 방식은 추가 되면 그에 맞는 Task를 할당하는 방식이지만 이제 계속 돌면서 확인
            resendTimer = new System.Timers.Timer(DelayMs);
            resendTimer.Elapsed += CheckPendingPackets;
            resendTimer.AutoReset = true;
            resendTimer.Start();
        }

        public void Add(ushort seq, byte[] packet)
        {
            if (pending.TryAdd(seq, (packet, 0)))
            {
                
            }
            else
            {
                //Console.WriteLine($"[UDP] ResendManager: {seq} 패킷이 이미 존재합니다.");
            }
        }

        private async void CheckPendingPackets(object sender, System.Timers.ElapsedEventArgs e) // 기존 방식에서 변경 기존 방식 (패킷마다 Task 생성 - 비효율적)
        {
            if (_disposed == 1) return;

            try
            {
                if (session == null || session._disposed == 1) return;

                List<(ushort seq, byte[] packet)> packetsToResend = new List<(ushort, byte[])>();

                foreach (var kvp in pending)
                {
                    var (packet, retries) = kvp.Value;

                    if (retries >= maxRetries)
                    {
                        // 재시도 횟수 초과한 패킷 제거
                        pending.TryRemove(kvp.Key, out _);
                        //Console.WriteLine($"[UDP] 패킷 {kvp.Key}가 최대 재시도 횟수를 초과했습니다.");
                        continue;
                    }

                    // 재전송 목록에 추가
                    packetsToResend.Add((kvp.Key, packet));

                    // 재시도 횟수 증가
                    pending.TryUpdate(kvp.Key, (packet, retries + 1), (packet, retries));
                }

                foreach (var (seq, packet) in packetsToResend)
                {
                    if (_disposed == 1) break;
                    await session.ResendPacketAsync(seq, packet).ConfigureAwait(false);
                    //Console.WriteLine($"[UDP] {seq} 패킷 {session.clientEP} 재전송 (시도: {pending[seq].Item2})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] 패킷 오류 : {ex}");
            }
        }
        public void RemoveAckedPacket(ushort seq)
        {
            if (pending.TryRemove(seq, out _))
            {
                //Console.WriteLine($"[UDP] ResendManager: ACK 확인됨, 재전송 목록에서 제거 | Seq: {seq}");
            }
            else
            {
                //Console.WriteLine($"[UDP] ResendManager: ACK 제거 실패 | Seq: {seq}가 존재하지 않음");
            }
        }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) // 0이면 1로 바꾸고 진행 아니면 종료
                return;
            try
            {
                if (resendTimer != null)
                {
                    resendTimer.Stop();
                    resendTimer.Elapsed -= CheckPendingPackets;
                    resendTimer.Dispose();
                    resendTimer = null;
                }

                pending.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] ResendManager 정리 중 오류 : {ex}");
            }
        }
    }
}

//private async Task MonitorAck(ushort seq)
//        {
//            while (true)
//            {
//                await Task.Delay(DelayMs);

//                if (!pending.TryGetValue(seq, out var packetInfo))
//                    return; // 패킷이 삭제된 경우 안전하게 종료

//                if (packetInfo.Item2 >= maxRetries)
//                {
//                   // Console.WriteLine($"[UDP] 패킷이 최대 횟수 재전송에도 Ack를 받지 못했습니다. {seq} 패킷 송신자 : {session.clientEP}");
//                    if (pending.TryRemove(seq, out _))
//                    {
//                        //Console.WriteLine($"[UDP] {seq} 패킷 삭제 완료.");
//                    }
//                    return;
//                }

//                //Console.WriteLine($"[UDP] {seq} 패킷 {session.clientEP} 재전송");
//                await session.ResendPacketAsync(seq, packetInfo.Item1);
//                pending.TryUpdate(seq, (packetInfo.Item1, packetInfo.Item2 + 1), packetInfo);
//            }
//        }