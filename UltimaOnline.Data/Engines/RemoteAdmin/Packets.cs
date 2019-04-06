using System;
using System.Collections;
using UltimaOnline;
using UltimaOnline.Items;
using UltimaOnline.Network;
using UltimaOnline.Accounting;
using UltimaOnline.Commands;

namespace UltimaOnline.RemoteAdmin
{
    public enum LoginResponse : byte
    {
        NoUser = 0,
        BadIP,
        BadPass,
        NoAccess,
        OK
    }

    public sealed class AdminCompressedPacket : Packet
    {
        public AdminCompressedPacket(byte[] CompData, int CDLen, int unCompSize) : base(0x01)
        {
            EnsureCapacity(1 + 2 + 2 + CDLen);
            Stream.Write((ushort)unCompSize);
            Stream.Write(CompData, 0, CDLen);
        }
    }

    public sealed class Login : Packet
    {
        public Login(LoginResponse resp) : base(0x02, 2)
        {
            Stream.Write((byte)resp);
        }
    }

    public sealed class ConsoleData : Packet
    {
        public ConsoleData(string str) : base(0x03)
        {
            EnsureCapacity(1 + 2 + 1 + str.Length + 1);
            Stream.Write((byte)2);

            Stream.WriteAsciiNull(str);
        }

        public ConsoleData(char ch) : base(0x03)
        {
            EnsureCapacity(1 + 2 + 1 + 1);
            Stream.Write((byte)3);

            Stream.Write((byte)ch);
        }
    }

    public sealed class ServerInfo : Packet
    {
        public ServerInfo() : base(0x04)
        {
            string netVer = Environment.Version.ToString();
            string os = Environment.OSVersion.ToString();

            EnsureCapacity(1 + 2 + (10 * 4) + netVer.Length + 1 + os.Length + 1);
            int banned = 0;
            int active = 0;

            foreach (Account acct in Accounts.GetAccounts())
            {
                if (acct.Banned)
                    ++banned;
                else
                    ++active;
            }

            Stream.Write((int)active);
            Stream.Write((int)banned);
            Stream.Write((int)Firewall.List.Count);
            Stream.Write((int)NetState.Instances.Count);

            Stream.Write((int)World.Mobiles.Count);
            Stream.Write((int)Core.ScriptMobiles);
            Stream.Write((int)World.Items.Count);
            Stream.Write((int)Core.ScriptItems);

            Stream.Write((uint)(DateTime.UtcNow - Clock.ServerStart).TotalSeconds);
            Stream.Write((uint)GC.GetTotalMemory(false));                        // TODO: uint not sufficient for TotalMemory (long). Fix protocol.
            Stream.WriteAsciiNull(netVer);
            Stream.WriteAsciiNull(os);
        }
    }

    public sealed class AccountSearchResults : Packet
    {
        public AccountSearchResults(ArrayList results) : base(0x05)
        {
            EnsureCapacity(1 + 2 + 2);

            Stream.Write((byte)results.Count);

            foreach (Account a in results)
            {
                Stream.WriteAsciiNull(a.Username);

                string pwToSend = a.PlainPassword;

                if (pwToSend == null)
                    pwToSend = "(hidden)";

                Stream.WriteAsciiNull(pwToSend);
                Stream.Write((byte)a.AccessLevel);
                Stream.Write(a.Banned);
                unchecked { Stream.Write((uint)a.LastLogin.Ticks); } // TODO: This doesn't work, uint.MaxValue is only 7 minutes of ticks. Fix protocol.

                Stream.Write((ushort)a.LoginIPs.Length);
                for (int i = 0; i < a.LoginIPs.Length; i++)
                    Stream.WriteAsciiNull(a.LoginIPs[i].ToString());

                Stream.Write((ushort)a.IPRestrictions.Length);
                for (int i = 0; i < a.IPRestrictions.Length; i++)
                    Stream.WriteAsciiNull(a.IPRestrictions[i]);
            }
        }
    }

    public sealed class CompactServerInfo : Packet
    {
        public CompactServerInfo() : base(0x51)
        {
            EnsureCapacity(1 + 2 + (4 * 4) + 8);

            Stream.Write((int)NetState.Instances.Count - 1);                      // Clients
            Stream.Write((int)World.Items.Count);                                 // Items
            Stream.Write((int)World.Mobiles.Count);                               // Mobiles
            Stream.Write((uint)(DateTime.UtcNow - Clock.ServerStart).TotalSeconds);  // Age (seconds)

            long memory = GC.GetTotalMemory(false);
            Stream.Write((uint)(memory >> 32));                                   // Memory high bytes
            Stream.Write((uint)memory);                                           // Memory low bytes
        }
    }

    public sealed class UOGInfo : Packet
    {
        public UOGInfo(string str) : base(0x52, str.Length + 6) // 'R'
        {
            Stream.WriteAsciiFixed("unUO", 4);
            Stream.WriteAsciiNull(str);
        }
    }

    public sealed class MessageBoxMessage : Packet
    {
        public MessageBoxMessage(string msg, string caption) : base(0x08)
        {
            EnsureCapacity(1 + 2 + msg.Length + 1 + caption.Length + 1);

            Stream.WriteAsciiNull(msg);
            Stream.WriteAsciiNull(caption);
        }
    }
}
