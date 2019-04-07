using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UltimaOnline.Network;

namespace UltimaOnline.Accounting
{
    public class AccountAttackLimiter
    {
        public static bool Enabled = true;

        public static void Initialize()
        {
            if (!Enabled)
                return;
            PacketHandlers.RegisterThrottler(0x80, new ThrottlePacketCallback(Throttle_Callback));
            PacketHandlers.RegisterThrottler(0x91, new ThrottlePacketCallback(Throttle_Callback));
            PacketHandlers.RegisterThrottler(0xCF, new ThrottlePacketCallback(Throttle_Callback));
        }

        public static bool Throttle_Callback(NetState ns)
        {
            var accessLog = FindAccessLog(ns);
            if (accessLog == null)
                return true;
            return DateTime.UtcNow >= (accessLog.LastAccessTime + ComputeThrottle(accessLog.Counts));
        }

        static List<InvalidAccountAccessLog> _List = new List<InvalidAccountAccessLog>();

        public static InvalidAccountAccessLog FindAccessLog(NetState ns)
        {
            if (ns == null)
                return null;
            var ipAddress = ns.Address;
            for (var i = 0; i < _List.Count; ++i)
            {
                var accessLog = _List[i];
                if (accessLog.HasExpired) _List.RemoveAt(i--);
                else if (accessLog.Address.Equals(ipAddress)) return accessLog;
            }
            return null;
        }

        public static void RegisterInvalidAccess(NetState ns)
        {
            if (ns == null || !Enabled)
                return;
            var accessLog = FindAccessLog(ns);
            if (accessLog == null)
                _List.Add(accessLog = new InvalidAccountAccessLog(ns.Address));
            accessLog.Counts += 1;
            accessLog.RefreshAccessTime();
            if (accessLog.Counts >= 3)
                try
                {
                    using (var op = new StreamWriter("throttle.log", true))
                        op.WriteLine($"{DateTime.UtcNow}\t{ns}\t{accessLog.Counts}");
                }
                catch { }
        }

        public static TimeSpan ComputeThrottle(int counts)
        {
            if (counts >= 15) return TimeSpan.FromMinutes(5.0);
            if (counts >= 10) return TimeSpan.FromMinutes(1.0);
            if (counts >= 5) return TimeSpan.FromSeconds(20.0);
            if (counts >= 3) return TimeSpan.FromSeconds(10.0);
            if (counts >= 1) return TimeSpan.FromSeconds(2.0);
            return TimeSpan.Zero;
        }
    }

    public class InvalidAccountAccessLog
    {
        public IPAddress Address { get; set; }

        public DateTime LastAccessTime { get; set; }

        public bool HasExpired => DateTime.UtcNow >= (LastAccessTime + TimeSpan.FromHours(1.0));

        public int Counts { get; set; }

        public void RefreshAccessTime() => LastAccessTime = DateTime.UtcNow;

        public InvalidAccountAccessLog(IPAddress address)
        {
            Address = address;
            RefreshAccessTime();
        }
    }
}