using System.Threading.Tasks;
using System.Collections.Concurrent;
using UnityEngine;
using System;

namespace Server.UDP.Packet
{
    public class ResendManager
    {
        private ConcurrentDictionary<ushort, (byte[], int)> pending = new ConcurrentDictionary<ushort, (byte[], int)>();
        private readonly UDPSession session;
        private static readonly int maxRetries = 3;
        private static readonly int DelayMs = 500;
        private static readonly int MaxDelayMs = 2000;
        private System.Timers.Timer resendTimer;
        private readonly object timerLock = new object();
        private volatile bool _disposed = false;

        public ResendManager(UDPSession session)
        {
            this.session = session;
            resendTimer = new System.Timers.Timer(DelayMs); // 1초마다 체크
            resendTimer.Elapsed += CheckPendingPackets;
            resendTimer.AutoReset = true;
            resendTimer.Start();
        }

        public void Add(ushort seq, byte[] packet)
        {
            if (_disposed) return;
            pending[seq] = (packet, 0);
            // Debug.Log($"[ResendManager] 처리할 데이터 : {pending.Count}");
        }

        private void CheckPendingPackets(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_disposed) return;
            try
            {
                if (session == null || session._disposed == 1) return;

                foreach (var kvp in pending)
                {
                    var seq = kvp.Key;
                    var (packet, retries) = kvp.Value;
                    //int delay = Mathf.Min(DelayMs * (1 << retries), MaxDelayMs);

                    if (retries >= maxRetries)
                    {
                        pending.TryRemove(seq, out _);
                        continue;
                    }

                    try
                    {
                        session.ResendPacket(seq, packet);
                        pending[seq] = (packet, retries + 1);
                    }
                    catch (ObjectDisposedException)
                    {
                        _disposed = true;
                    }

                }
            }
            catch (Exception ex) 
            {
                Debug.LogError($"[UDP] 재전송 오류: {ex.Message}");
            }
        }

        public void RemoveAckedPacket(ushort seq)
        {
            if (_disposed) return;
            if (pending.TryRemove(seq, out _))
            {
                //Debug.Log($"[UDP] ResendManager: ACK 확인됨, 재전송 목록에서 제거 | Seq: {seq}");
                //Debug.Log($"[ResendManager] 삭제 이후 처리할 데이터 : {pending.Count}");

            }
            else
            {
                //Debug.LogWarning($"[UDP] ResendManager: ACK 제거 실패 | Seq: {seq}가 존재하지 않음");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var tempTimer = resendTimer;
            resendTimer = null;

            if (tempTimer != null)
            {
                tempTimer.Stop();
                tempTimer.Elapsed -= CheckPendingPackets;
                tempTimer.Dispose();
            }

            pending.Clear();
        }
    }
}
