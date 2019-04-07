using System.Collections.Generic;
using System.IO;
using System.Net;

namespace UltimaOnline
{
    public class Firewall
    {
        #region Firewall Entries

        public interface IFirewallEntry
        {
            bool IsBlocked(IPAddress address);
        }

        public class IPFirewallEntry : IFirewallEntry
        {
            IPAddress _Address;
            public IPFirewallEntry(IPAddress address) => _Address = address;
            public bool IsBlocked(IPAddress address) => _Address.Equals(address);
            public override string ToString() => _Address.ToString();
            public override bool Equals(object obj)
            {
                if (obj is IPAddress) return obj.Equals(_Address);
                else if (obj is string s)
                {
                    if (IPAddress.TryParse(s, out IPAddress otherAddress))
                        return otherAddress.Equals(_Address);
                }
                else if (obj is IPFirewallEntry ife) return _Address.Equals(ife._Address);
                return false;
            }
            public override int GetHashCode() => _Address.GetHashCode();
        }

        public class CIDRFirewallEntry : IFirewallEntry
        {
            IPAddress _CIDRPrefix;
            int _CIDRLength;
            public CIDRFirewallEntry(IPAddress cidrPrefix, int cidrLength)
            {
                _CIDRPrefix = cidrPrefix;
                _CIDRLength = cidrLength;
            }
            public bool IsBlocked(IPAddress address) => Utility.IPMatchCIDR(_CIDRPrefix, address, _CIDRLength);
            public override string ToString() => $"{_CIDRPrefix}/{_CIDRLength}";
            public override bool Equals(object obj)
            {
                if (obj is string entry)
                {
                    var str = entry.Split('/');
                    if (str.Length == 2 && IPAddress.TryParse(str[0], out IPAddress cidrPrefix) && int.TryParse(str[1], out int cidrLength))
                        return _CIDRPrefix.Equals(cidrPrefix) && _CIDRLength.Equals(cidrLength);
                }
                else if (obj is CIDRFirewallEntry cfe)
                    return _CIDRPrefix.Equals(cfe._CIDRPrefix) && _CIDRLength.Equals(cfe._CIDRLength);
                return false;
            }
            public override int GetHashCode() => _CIDRPrefix.GetHashCode() ^ _CIDRLength.GetHashCode();
        }

        public class WildcardIPFirewallEntry : IFirewallEntry
        {
            string _Entry;
            bool _Valid = true;
            public WildcardIPFirewallEntry(string entry)
            {
                _Entry = entry;
            }
            public bool IsBlocked(IPAddress address) => !_Valid ? false : Utility.IPMatch(_Entry, address, ref _Valid); //Why process if it's invalid?  it'll return false anyway after processing it.
            public override string ToString() => _Entry.ToString();
            public override bool Equals(object obj)
            {
                if (obj is string) return obj.Equals(_Entry);
                else if (obj is WildcardIPFirewallEntry entry) return _Entry.Equals(entry._Entry);
                return false;
            }
            public override int GetHashCode() => _Entry.GetHashCode();
        }

        #endregion

        static Firewall()
        {
            List = new List<IFirewallEntry>();
            var path = "firewall.cfg";
            string line;
            if (File.Exists(path))
                using (var ip = new StreamReader(path))
                    while ((line = ip.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0)
                            continue;
                        List.Add(ToFirewallEntry(line));
                        /*
						var toAdd = IPAddress.TryParse(line, out var addr) ? addr : line;
						_Blocked.Add(toAdd.ToString());
						 * */
                    }
        }

        public static List<IFirewallEntry> List { get; private set; }

        public static IFirewallEntry ToFirewallEntry(object entry)
        {
            if (entry is IFirewallEntry e) return e;
            else if (entry is IPAddress ia) return new IPFirewallEntry(ia);
            else if (entry is string s) return ToFirewallEntry(s);
            return null;
        }

        public static IFirewallEntry ToFirewallEntry(string entry)
        {
            if (IPAddress.TryParse(entry, out IPAddress addr))
                return new IPFirewallEntry(addr);
            //Try CIDR parse
            var str = entry.Split('/');
            return str.Length == 2 && IPAddress.TryParse(str[0], out IPAddress cidrPrefix) && int.TryParse(str[1], out int cidrLength)
                ? new CIDRFirewallEntry(cidrPrefix, cidrLength)
                : (IFirewallEntry)new WildcardIPFirewallEntry(entry);
        }

        public static void RemoveAt(int index)
        {
            List.RemoveAt(index);
            Save();
        }

        public static void Remove(object obj)
        {
            var entry = ToFirewallEntry(obj);
            if (entry != null)
            {
                List.Remove(entry);
                Save();
            }
        }

        public static void Add(object obj)
        {
            if (obj is IPAddress ia) Add(ia);
            else if (obj is string s) Add(s);
            else if (obj is IFirewallEntry fe) Add(fe);
        }

        public static void Add(IFirewallEntry entry)
        {
            if (!List.Contains(entry))
                List.Add(entry);
            Save();
        }

        public static void Add(string pattern)
        {
            var entry = ToFirewallEntry(pattern);
            if (!List.Contains(entry))
                List.Add(entry);
            Save();
        }

        public static void Add(IPAddress ip)
        {
            var entry = new IPFirewallEntry(ip);
            if (!List.Contains(entry))
                List.Add(entry);
            Save();
        }

        public static void Save()
        {
            var path = "firewall.cfg";
            using (var op = new StreamWriter(path))
                for (var i = 0; i < List.Count; ++i)
                    op.WriteLine(List[i]);
        }

        public static bool IsBlocked(IPAddress ip)
        {
            for (var i = 0; i < List.Count; i++)
                if (List[i].IsBlocked(ip))
                    return true;
            return false;
            /*
			bool contains = false;
			for (var i = 0; !contains && i < m_Blocked.Count; ++i)
			{
				if (m_Blocked[i] is IPAddress)
					contains = ip.Equals(m_Blocked[i]);
                else if (m_Blocked[i] is String)
                {
                    var s = (string)m_Blocked[i];
                    contains = Utility.IPMatchCIDR(s, ip);
                    if (!contains)
                        contains = Utility.IPMatch(s, ip);
                }
			}
			return contains;
			 * */
        }
    }
}