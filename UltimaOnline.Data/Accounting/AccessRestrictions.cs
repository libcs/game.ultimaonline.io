using System;
using System.IO;
using System.Net;
using UltimaOnline.Misc;

namespace UltimaOnline
{
    public class AccessRestrictions
    {
        public static void Initialize()
        {
            EventSink.SocketConnect += new SocketConnectEventHandler(EventSink_SocketConnect);
        }

        private static void EventSink_SocketConnect(SocketConnectEventArgs e)
        {
            try
            {
                var ip = ((IPEndPoint)e.Socket.RemoteEndPoint).Address;
                if (Firewall.IsBlocked(ip))
                {
                    Console.WriteLine($"Client: {ip}: Firewall blocked connection attempt.");
                    e.AllowConnection = false;
                    return;
                }
                else if (IPLimiter.SocketBlock && !IPLimiter.Verify(ip))
                {
                    Console.WriteLine($"Client: {ip}: Past IP limit threshold");
                    using (var op = new StreamWriter("ipLimits.log", true))
                        op.WriteLine($"{ip}\tPast IP limit threshold\t{DateTime.UtcNow}");
                    e.AllowConnection = false;
                    return;
                }
            }
            catch { e.AllowConnection = false; }
        }
    }
}