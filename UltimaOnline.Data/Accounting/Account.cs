using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UltimaOnline.Commands;
using UltimaOnline.Items;
using UltimaOnline.Misc;
using UltimaOnline.Mobiles;
using UltimaOnline.Multis;
using UltimaOnline.Network;

namespace UltimaOnline.Accounting
{
    public class Account : IAccount, IComparable, IComparable<Account>
    {
        public static readonly TimeSpan YoungDuration = TimeSpan.FromHours(40.0);
        public static readonly TimeSpan InactiveDuration = TimeSpan.FromDays(180.0);
        public static readonly TimeSpan EmptyInactiveDuration = TimeSpan.FromDays(30.0);

        public static void Configure()
        {
            CommandSystem.Register("ConvertCurrency", AccessLevel.Owner, ConvertCurrency);
        }

        private static void ConvertCurrency(CommandEventArgs e)
        {
            e.Mobile.SendMessage($"Converting All Banked Gold from {(AccountGold.Enabled ? "checks and coins" : "account treasury")} to {(AccountGold.Enabled ? "account treasury" : "checks and coins")}.  Please wait...");
            NetState.Pause();
            double found = 0.0, converted = 0.0;
            try
            {
                BankBox box;
                List<Gold> gold;
                List<BankCheck> checks;
                long share = 0, shared;
                int diff;
                foreach (var a in Accounts.GetAccounts().OfType<Account>().Where(a => a.Count > 0))
                {
                    try
                    {
                        if (!AccountGold.Enabled)
                        {
                            share = (int)Math.Truncate((a.TotalCurrency / a.Count) * CurrencyThreshold);
                            found += a.TotalCurrency * CurrencyThreshold;
                        }
                        foreach (var m in a._Mobiles.Where(m => m != null))
                        {
                            box = m.FindBankNoCreate();
                            if (box == null)
                                continue;
                            if (AccountGold.Enabled)
                            {
                                foreach (var o in checks = box.FindItemsByType<BankCheck>())
                                {
                                    found += o.Worth;
                                    if (!a.DepositGold(o.Worth))
                                        break;
                                    converted += o.Worth;
                                    o.Delete();
                                }
                                checks.Clear();
                                checks.TrimExcess();
                                foreach (var o in gold = box.FindItemsByType<Gold>())
                                {
                                    found += o.Amount;
                                    if (!a.DepositGold(o.Amount))
                                        break;
                                    converted += o.Amount;
                                    o.Delete();
                                }
                                gold.Clear();
                                gold.TrimExcess();
                            }
                            else
                            {
                                shared = share;
                                while (shared > 0)
                                {
                                    if (shared > 60000)
                                    {
                                        diff = (int)Math.Min(10000000, shared);
                                        if (a.WithdrawGold(diff))
                                            box.DropItem(new BankCheck(diff));
                                        else break;
                                    }
                                    else
                                    {
                                        diff = (int)Math.Min(60000, shared);
                                        if (a.WithdrawGold(diff))
                                            box.DropItem(new Gold(diff));
                                        else break;
                                    }
                                    converted += diff;
                                    shared -= diff;
                                }
                            }
                            box.UpdateTotals();
                        }
                    }
                    catch { }
                }
            }
            catch { }
            NetState.Resume();
            e.Mobile.SendMessage($"Operation complete: {converted:#,0} of {found:#,0} Gold has been converted in total.");
        }

        AccessLevel _AccessLevel;
        TimeSpan _TotalGameTime;
        List<AccountComment> _Comments;
        List<AccountTag> _Tags;
        Mobile[] _Mobiles;

        /// <summary>
        /// Deletes the account, all characters of the account, and all houses of those characters
        /// </summary>
        public void Delete()
        {
            for (var i = 0; i < Length; ++i)
            {
                var m = this[i];
                if (m == null)
                    continue;
                var list = BaseHouse.GetHouses(m);
                for (var j = 0; j < list.Count; ++j)
                    list[j].Delete();
                m.Delete();
                m.Account = null;
                _Mobiles[i] = null;
            }
            if (LoginIPs.Length != 0 && AccountHandler.IPTable.ContainsKey(LoginIPs[0]))
                --AccountHandler.IPTable[LoginIPs[0]];
            Accounts.Remove(Username);
        }

        /// <summary>
        /// Object detailing information about the hardware of the last person to log into this account
        /// </summary>
        public HardwareInfo HardwareInfo { get; set; }

        /// <summary>
        /// List of IP addresses for restricted access. '*' wildcard supported. If the array contains zero entries, all IP addresses are allowed.
        /// </summary>
        public string[] IPRestrictions { get; set; }

        /// <summary>
        /// List of IP addresses which have successfully logged into this account.
        /// </summary>
        public IPAddress[] LoginIPs { get; set; }

        /// <summary>
        /// List of account comments. Type of contained objects is AccountComment.
        /// </summary>
        public List<AccountComment> Comments
        {
            get { if (_Comments == null) _Comments = new List<AccountComment>(); return _Comments; }
        }

        /// <summary>
        /// List of account tags. Type of contained objects is AccountTag.
        /// </summary>
        public List<AccountTag> Tags
        {
            get { if (_Tags == null) _Tags = new List<AccountTag>(); return _Tags; }
        }

        /// <summary>
        /// Account username. Case insensitive validation.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Account email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Account password. Plain text. Case sensitive validation. May be null.
        /// </summary>
        public string PlainPassword { get; set; }

        /// <summary>
        /// Account password. Hashed with MD5. May be null.
        /// </summary>
        public string CryptPassword { get; set; }

        /// <summary>
        /// Account username and password hashed with SHA1. May be null.
        /// </summary>
        public string NewCryptPassword { get; set; }

        /// <summary>
        /// Initial AccessLevel for new characters created on this account.
        /// </summary>
        public AccessLevel AccessLevel
        {
            get => _AccessLevel;
            set => _AccessLevel = value;
        }

        /// <summary>
        /// Internal bitfield of account flags. Consider using direct access properties (Banned, Young), or GetFlag/SetFlag methods
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// Gets or sets a flag indiciating if this account is banned.
        /// </summary>
        public bool Banned
        {
            get
            {
                var isBanned = GetFlag(0);
                if (!isBanned)
                    return false;
                if (GetBanTags(out var banTime, out var banDuration) && banDuration != TimeSpan.MaxValue && DateTime.UtcNow >= (banTime + banDuration))
                {
                    SetUnspecifiedBan(null); // clear
                    Banned = false;
                    return false;
                }
                return true;
            }
            set => SetFlag(0, value);
        }

        /// <summary>
        /// Gets or sets a flag indicating if the characters created on this account will have the young status.
        /// </summary>
        public bool Young
        {
            get => !GetFlag(1);
            set
            {
                SetFlag(1, !value);
                if (_YoungTimer != null)
                {
                    _YoungTimer.Stop();
                    _YoungTimer = null;
                }
            }
        }

        /// <summary>
        /// The date and time of when this account was created.
        /// </summary>
        public DateTime Created { get; }

        /// <summary>
        /// Gets or sets the date and time when this account was last accessed.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// An account is considered inactive based upon LastLogin and InactiveDuration.  If the account is empty, it is based upon EmptyInactiveDuration
        /// </summary>
        public bool Inactive
        {
            get
            {
                if (AccessLevel != AccessLevel.Player)
                    return false;
                var inactiveLength = DateTime.UtcNow - LastLogin;
                return inactiveLength > ((Count == 0) ? EmptyInactiveDuration : InactiveDuration);
            }
        }

        /// <summary>
        /// Gets the total game time of this account, also considering the game time of characters
        /// that have been deleted.
        /// </summary>
        public TimeSpan TotalGameTime
        {
            get
            {
                for (var i = 0; i < _Mobiles.Length; i++)
                    if (_Mobiles[i] is PlayerMobile m && m.NetState != null)
                        return _TotalGameTime + (DateTime.UtcNow - m.SessionStart);
                return _TotalGameTime;
            }
        }

        /// <summary>
        /// Gets the value of a specific flag in the Flags bitfield.
        /// </summary>
        /// <param name="index">The zero-based flag index.</param>
        public bool GetFlag(int index) => (Flags & (1 << index)) != 0;

        /// <summary>
        /// Sets the value of a specific flag in the Flags bitfield.
        /// </summary>
        /// <param name="index">The zero-based flag index.</param>
        /// <param name="value">The value to set.</param>
        public void SetFlag(int index, bool value)
        {
            if (value) Flags |= 1 << index;
            else Flags &= ~(1 << index);
        }

        /// <summary>
        /// Adds a new tag to this account. This method does not check for duplicate names.
        /// </summary>
        /// <param name="name">New tag name.</param>
        /// <param name="value">New tag value.</param>
        public void AddTag(string name, string value) => Tags.Add(new AccountTag(name, value));

        /// <summary>
        /// Removes all tags with the specified name from this account.
        /// </summary>
        /// <param name="name">Tag name to remove.</param>
        public void RemoveTag(string name)
        {
            for (var i = Tags.Count - 1; i >= 0; --i)
            {
                if (i >= Tags.Count)
                    continue;
                var tag = Tags[i];
                if (tag.Name == name)
                    Tags.RemoveAt(i);
            }
        }

        /// <summary>
        /// Modifies an existing tag or adds a new tag if no tag exists.
        /// </summary>
        /// <param name="name">Tag name.</param>
        /// <param name="value">Tag value.</param>
        public void SetTag(string name, string value)
        {
            for (var i = 0; i < Tags.Count; ++i)
            {
                var tag = Tags[i];
                if (tag.Name == name)
                {
                    tag.Value = value;
                    return;
                }
            }
            AddTag(name, value);
        }

        /// <summary>
        /// Gets the value of a tag -or- null if there are no tags with the specified name.
        /// </summary>
        /// <param name="name">Name of the desired tag value.</param>
        public string GetTag(string name)
        {
            for (var i = 0; i < Tags.Count; ++i)
            {
                var tag = Tags[i];
                if (tag.Name == name)
                    return tag.Value;
            }
            return null;
        }

        public void SetUnspecifiedBan(Mobile from) => SetBanTags(from, DateTime.MinValue, TimeSpan.Zero);

        public void SetBanTags(Mobile from, DateTime banTime, TimeSpan banDuration)
        {
            if (from == null) RemoveTag("BanDealer");
            else SetTag("BanDealer", from.ToString());
            if (banTime == DateTime.MinValue) RemoveTag("BanTime");
            else SetTag("BanTime", XmlConvert.ToString(banTime, XmlDateTimeSerializationMode.Utc));
            if (banDuration == TimeSpan.Zero) RemoveTag("BanDuration");
            else SetTag("BanDuration", banDuration.ToString());
        }

        public bool GetBanTags(out DateTime banTime, out TimeSpan banDuration)
        {
            var tagTime = GetTag("BanTime");
            var tagDuration = GetTag("BanDuration");
            banTime = tagTime != null ? Utility.GetXMLDateTime(tagTime, DateTime.MinValue) : DateTime.MinValue;
            if (tagDuration == "Infinite") banDuration = TimeSpan.MaxValue;
            else if (tagDuration != null) banDuration = Utility.ToTimeSpan(tagDuration);
            else banDuration = TimeSpan.Zero;
            return banTime != DateTime.MinValue && banDuration != TimeSpan.Zero;
        }

        static MD5CryptoServiceProvider _MD5HashProvider;
        static SHA1CryptoServiceProvider _SHA1HashProvider;
        static byte[] _HashBuffer;

        public static string HashMD5(string phrase)
        {
            if (_MD5HashProvider == null)
                _MD5HashProvider = new MD5CryptoServiceProvider();
            if (_HashBuffer == null)
                _HashBuffer = new byte[256];
            var length = Encoding.ASCII.GetBytes(phrase, 0, phrase.Length > 256 ? 256 : phrase.Length, _HashBuffer, 0);
            var hashed = _MD5HashProvider.ComputeHash(_HashBuffer, 0, length);
            return BitConverter.ToString(hashed);
        }

        public static string HashSHA1(string phrase)
        {
            if (_SHA1HashProvider == null)
                _SHA1HashProvider = new SHA1CryptoServiceProvider();
            if (_HashBuffer == null)
                _HashBuffer = new byte[256];
            var length = Encoding.ASCII.GetBytes(phrase, 0, phrase.Length > 256 ? 256 : phrase.Length, _HashBuffer, 0);
            var hashed = _SHA1HashProvider.ComputeHash(_HashBuffer, 0, length);
            return BitConverter.ToString(hashed);
        }

        public void SetPassword(string plainPassword)
        {
            switch (AccountHandler.ProtectPasswords)
            {
                case PasswordProtection.None:
                    {
                        PlainPassword = plainPassword;
                        CryptPassword = null;
                        NewCryptPassword = null;
                        break;
                    }
                case PasswordProtection.Crypt:
                    {
                        PlainPassword = null;
                        CryptPassword = HashMD5(plainPassword);
                        NewCryptPassword = null;
                        break;
                    }
                default: // PasswordProtection.NewCrypt
                    {
                        PlainPassword = null;
                        CryptPassword = null;
                        NewCryptPassword = HashSHA1(Username + plainPassword);
                        break;
                    }
            }
        }

        public bool CheckPassword(string plainPassword)
        {
            bool ok;
            PasswordProtection curProt;
            if (PlainPassword != null) { ok = PlainPassword == plainPassword; curProt = PasswordProtection.None; }
            else if (CryptPassword != null) { ok = CryptPassword == HashMD5(plainPassword); curProt = PasswordProtection.Crypt; }
            else { ok = NewCryptPassword == HashSHA1(Username + plainPassword); curProt = PasswordProtection.NewCrypt; }
            if (ok && curProt != AccountHandler.ProtectPasswords) SetPassword(plainPassword);
            return ok;
        }

        Timer _YoungTimer;

        public static void Initialize()
        {
            EventSink.Connected += new ConnectedEventHandler(EventSink_Connected);
            EventSink.Disconnected += new DisconnectedEventHandler(EventSink_Disconnected);
            EventSink.Login += new LoginEventHandler(EventSink_Login);
        }

        static void EventSink_Connected(ConnectedEventArgs e)
        {
            if (!(e.Mobile.Account is Account acc))
                return;
            if (acc.Young && acc._YoungTimer == null)
            {
                acc._YoungTimer = new YoungTimer(acc);
                acc._YoungTimer.Start();
            }
        }

        private static void EventSink_Disconnected(DisconnectedEventArgs e)
        {
            if (!(e.Mobile.Account is Account acc))
                return;
            if (acc._YoungTimer != null)
            {
                acc._YoungTimer.Stop();
                acc._YoungTimer = null;
            }
            if (!(e.Mobile is PlayerMobile m))
                return;
            acc._TotalGameTime += DateTime.UtcNow - m.SessionStart;
        }

        private static void EventSink_Login(LoginEventArgs e)
        {

            if (!(e.Mobile is PlayerMobile m))
                return;
            if (!(m.Account is Account acc))
                return;
            if (m.Young && acc.Young)
            {
                var ts = YoungDuration - acc.TotalGameTime;
                var hours = Math.Max((int)ts.TotalHours, 0);
                m.SendAsciiMessage($"You will enjoy the benefits and relatively safe status of a young player for {hours} more hour{(hours != 1 ? "s" : string.Empty)}.");
            }
        }

        public void RemoveYoungStatus(int message)
        {
            Young = false;
            for (var i = 0; i < _Mobiles.Length; i++)
                if (_Mobiles[i] is PlayerMobile m && m.Young)
                {
                    m.Young = false;
                    if (m.NetState != null)
                        m.SendLocalizedMessage(message > 0 ? message : 1019039); // 1019039 - You are no longer considered a young player of Ultima Online, and are no longer subject to the limitations and benefits of being in that caste.
                }
        }

        public void CheckYoung()
        {
            if (TotalGameTime >= YoungDuration)
                RemoveYoungStatus(1019038); // You are old enough to be considered an adult, and have outgrown your status as a young player!
        }

        class YoungTimer : Timer
        {
            Account _Account;

            public YoungTimer(Account account)
                : base(TimeSpan.FromMinutes(1.0), TimeSpan.FromMinutes(1.0))
            {
                _Account = account;
                Priority = TimerPriority.FiveSeconds;
            }

            protected override void OnTick() => _Account.CheckYoung();
        }

        public Account(string username, string password)
        {
            Username = username;
            SetPassword(password);
            _AccessLevel = AccessLevel.Player;
            Created = LastLogin = DateTime.UtcNow;
            _TotalGameTime = TimeSpan.Zero;
            _Mobiles = new Mobile[7];
            IPRestrictions = new string[0];
            LoginIPs = new IPAddress[0];
            Accounts.Add(this);
        }

        public Account(XmlElement node)
        {
            Username = Utility.GetText(node["username"], "empty");
            var plainPassword = Utility.GetText(node["password"], null);
            var cryptPassword = Utility.GetText(node["cryptPassword"], null);
            var newCryptPassword = Utility.GetText(node["newCryptPassword"], null);
            switch (AccountHandler.ProtectPasswords)
            {
                case PasswordProtection.None:
                    {
                        if (plainPassword != null) SetPassword(plainPassword);
                        else if (newCryptPassword != null) NewCryptPassword = newCryptPassword;
                        else if (cryptPassword != null) CryptPassword = cryptPassword;
                        else SetPassword("empty");
                        break;
                    }
                case PasswordProtection.Crypt:
                    {
                        if (cryptPassword != null) CryptPassword = cryptPassword;
                        else if (plainPassword != null) SetPassword(plainPassword);
                        else if (newCryptPassword != null) NewCryptPassword = newCryptPassword;
                        else SetPassword("empty");
                        break;
                    }
                default: // PasswordProtection.NewCrypt
                    {
                        if (newCryptPassword != null) NewCryptPassword = newCryptPassword;
                        else if (plainPassword != null) SetPassword(plainPassword);
                        else if (cryptPassword != null) CryptPassword = cryptPassword;
                        else SetPassword("empty");
                        break;
                    }
            }
            Enum.TryParse(Utility.GetText(node["accessLevel"], "Player"), true, out _AccessLevel);
            Flags = Utility.GetXMLInt32(Utility.GetText(node["flags"], "0"), 0);
            Created = Utility.GetXMLDateTime(Utility.GetText(node["created"], null), DateTime.UtcNow);
            LastLogin = Utility.GetXMLDateTime(Utility.GetText(node["lastLogin"], null), DateTime.UtcNow);
            TotalCurrency = Utility.GetXMLDouble(Utility.GetText(node["totalCurrency"], "0"), 0);
            _Mobiles = LoadMobiles(node);
            _Comments = LoadComments(node);
            _Tags = LoadTags(node);
            LoginIPs = LoadAddressList(node);
            IPRestrictions = LoadAccessCheck(node);
            for (var i = 0; i < _Mobiles.Length; ++i)
                if (_Mobiles[i] != null)
                    _Mobiles[i].Account = this;
            var totalGameTime = Utility.GetXMLTimeSpan(Utility.GetText(node["totalGameTime"], null), TimeSpan.Zero);
            if (totalGameTime == TimeSpan.Zero)
                for (var i = 0; i < _Mobiles.Length; i++)
                    if (_Mobiles[i] is PlayerMobile m)
                        totalGameTime += m.GameTime;
            _TotalGameTime = totalGameTime;
            if (Young)
                CheckYoung();
            Accounts.Add(this);
        }

        /// <summary>
        /// Deserializes a list of string values from an xml element. Null values are not added to the list.
        /// </summary>
        /// <param name="node">The XmlElement from which to deserialize.</param>
        /// <returns>String list. Value will never be null.</returns>
        public static string[] LoadAccessCheck(XmlElement node)
        {
            string[] stringList;
            var accessCheck = node["accessCheck"];
            if (accessCheck != null)
            {
                var list = new List<string>();
                foreach (XmlElement ip in accessCheck.GetElementsByTagName("ip"))
                {
                    var text = Utility.GetText(ip, null);
                    if (text != null)
                        list.Add(text);
                }
                stringList = list.ToArray();
            }
            else stringList = new string[0];
            return stringList;
        }

        /// <summary>
        /// Deserializes a list of IPAddress values from an xml element.
        /// </summary>
        /// <param name="node">The XmlElement from which to deserialize.</param>
        /// <returns>Address list. Value will never be null.</returns>
        public static IPAddress[] LoadAddressList(XmlElement node)
        {
            IPAddress[] list;
            var addressList = node["addressList"];
            if (addressList != null)
            {
                var count = Utility.GetXMLInt32(Utility.GetAttribute(addressList, "count", "0"), 0);
                list = new IPAddress[count];
                count = 0;
                foreach (XmlElement ip in addressList.GetElementsByTagName("ip"))
                    if (count < list.Length && IPAddress.TryParse(Utility.GetText(ip, null), out IPAddress address))
                    {
                        list[count] = Utility.Intern(address);
                        count++;
                    }
                if (count != list.Length)
                {
                    var old = list;
                    list = new IPAddress[count];
                    for (var i = 0; i < count && i < old.Length; ++i)
                        list[i] = old[i];
                }
            }
            else list = new IPAddress[0];
            return list;
        }

        /// <summary>
        /// Deserializes a list of Mobile instances from an xml element.
        /// </summary>
        /// <param name="node">The XmlElement instance from which to deserialize.</param>
        /// <returns>Mobile list. Value will never be null.</returns>
        public static Mobile[] LoadMobiles(XmlElement node)
        {
            var list = new Mobile[7];
            var chars = node["chars"];
            //var length = Accounts.GetInt32( Accounts.GetAttribute( chars, "length", "6" ), 6 );
            //list = new Mobile[length];
            //Above is legacy, no longer used
            if (chars != null)
                foreach (XmlElement ele in chars.GetElementsByTagName("char"))
                    try
                    {
                        var index = Utility.GetXMLInt32(Utility.GetAttribute(ele, "index", "0"), 0);
                        var serial = Utility.GetXMLInt32(Utility.GetText(ele, "0"), 0);
                        if (index >= 0 && index < list.Length)
                            list[index] = World.FindMobile(serial);
                    }
                    catch { }
            return list;
        }

        /// <summary>
        /// Deserializes a list of AccountComment instances from an xml element.
        /// </summary>
        /// <param name="node">The XmlElement from which to deserialize.</param>
        /// <returns>Comment list. Value will never be null.</returns>
        public static List<AccountComment> LoadComments(XmlElement node)
        {
            List<AccountComment> list = null;
            var comments = node["comments"];
            if (comments != null)
            {
                list = new List<AccountComment>();
                foreach (XmlElement comment in comments.GetElementsByTagName("comment"))
                    try { list.Add(new AccountComment(comment)); }
                    catch { }
            }
            return list;
        }

        /// <summary>
        /// Deserializes a list of AccountTag instances from an xml element.
        /// </summary>
        /// <param name="node">The XmlElement from which to deserialize.</param>
        /// <returns>Tag list. Value will never be null.</returns>
        public static List<AccountTag> LoadTags(XmlElement node)
        {
            List<AccountTag> list = null;
            var tags = node["tags"];
            if (tags != null)
            {
                list = new List<AccountTag>();
                foreach (XmlElement tag in tags.GetElementsByTagName("tag"))
                    try { list.Add(new AccountTag(tag)); }
                    catch { }
            }
            return list;
        }

        /// <summary>
        /// Checks if a specific NetState is allowed access to this account.
        /// </summary>
        /// <param name="ns">NetState instance to check.</param>
        /// <returns>True if allowed, false if not.</returns>
        public bool HasAccess(NetState ns) => ns != null && HasAccess(ns.Address);

        public bool HasAccess(IPAddress ipAddress)
        {
            var level = AccountHandler.LockdownLevel;
            if (level > AccessLevel.Player)
            {
                var hasAccess = false;
                if (_AccessLevel >= level)
                    hasAccess = true;
                else
                    for (var i = 0; !hasAccess && i < Length; ++i)
                    {
                        var m = this[i];
                        if (m != null && m.AccessLevel >= level)
                            hasAccess = true;
                    }
                if (!hasAccess)
                    return false;
            }
            var accessAllowed = (IPRestrictions.Length == 0 || IPLimiter.IsExempt(ipAddress));
            for (var i = 0; !accessAllowed && i < IPRestrictions.Length; ++i)
                accessAllowed = Utility.IPMatch(IPRestrictions[i], ipAddress);
            return accessAllowed;
        }

        /// <summary>
        /// Records the IP address of 'ns' in its 'LoginIPs' list.
        /// </summary>
        /// <param name="ns">NetState instance to record.</param>
        public void LogAccess(NetState ns)
        {
            if (ns != null)
                LogAccess(ns.Address);
        }

        public void LogAccess(IPAddress ipAddress)
        {
            if (IPLimiter.IsExempt(ipAddress))
                return;
            if (LoginIPs.Length == 0)
                if (AccountHandler.IPTable.ContainsKey(ipAddress)) AccountHandler.IPTable[ipAddress]++;
                else AccountHandler.IPTable[ipAddress] = 1;
            var contains = false;
            for (var i = 0; !contains && i < LoginIPs.Length; ++i)
                contains = LoginIPs[i].Equals(ipAddress);
            if (contains)
                return;
            var old = LoginIPs;
            LoginIPs = new IPAddress[old.Length + 1];
            for (var i = 0; i < old.Length; ++i)
                LoginIPs[i] = old[i];
            LoginIPs[old.Length] = ipAddress;
        }

        /// <summary>
        /// Checks if a specific NetState is allowed access to this account. If true, the NetState IPAddress is added to the address list.
        /// </summary>
        /// <param name="ns">NetState instance to check.</param>
        /// <returns>True if allowed, false if not.</returns>
        public bool CheckAccess(NetState ns) => ns != null && CheckAccess(ns.Address);

        public bool CheckAccess(IPAddress ipAddress)
        {
            var hasAccess = HasAccess(ipAddress);
            if (hasAccess)
                LogAccess(ipAddress);
            return hasAccess;
        }

        /// <summary>
        /// Serializes this Account instance to an XmlTextWriter.
        /// </summary>
        /// <param name="w">The XmlTextWriter instance from which to serialize.</param>
        public void Save(XmlTextWriter w)
        {
            w.WriteStartElement("account");
            w.WriteStartElement("username"); w.WriteString(Username); w.WriteEndElement();
            if (PlainPassword != null) { w.WriteStartElement("password"); w.WriteString(PlainPassword); w.WriteEndElement(); }
            if (CryptPassword != null) { w.WriteStartElement("cryptPassword"); w.WriteString(CryptPassword); w.WriteEndElement(); }
            if (NewCryptPassword != null) { w.WriteStartElement("newCryptPassword"); w.WriteString(NewCryptPassword); w.WriteEndElement(); }
            if (_AccessLevel != AccessLevel.Player) { w.WriteStartElement("accessLevel"); w.WriteString(_AccessLevel.ToString()); w.WriteEndElement(); }
            if (Flags != 0) { w.WriteStartElement("flags"); w.WriteString(XmlConvert.ToString(Flags)); w.WriteEndElement(); }
            w.WriteStartElement("created"); w.WriteString(XmlConvert.ToString(Created, XmlDateTimeSerializationMode.Utc)); w.WriteEndElement();
            w.WriteStartElement("lastLogin"); w.WriteString(XmlConvert.ToString(LastLogin, XmlDateTimeSerializationMode.Utc)); w.WriteEndElement();
            w.WriteStartElement("totalGameTime"); w.WriteString(XmlConvert.ToString(TotalGameTime)); w.WriteEndElement();
            w.WriteStartElement("chars");
            //xml.WriteAttributeString( "length", m_Mobiles.Length.ToString() );	//Legacy, Not used anymore
            for (var i = 0; i < _Mobiles.Length; ++i)
            {
                var m = _Mobiles[i];
                if (m != null && !m.Deleted) { w.WriteStartElement("char"); w.WriteAttributeString("index", i.ToString()); w.WriteString(m.Serial.Value.ToString()); w.WriteEndElement(); }
            }
            w.WriteEndElement();
            if (_Comments != null && _Comments.Count > 0)
            {
                w.WriteStartElement("comments");
                for (var i = 0; i < _Comments.Count; ++i)
                    _Comments[i].Save(w);
                w.WriteEndElement();
            }
            if (_Tags != null && _Tags.Count > 0)
            {
                w.WriteStartElement("tags");
                for (var i = 0; i < _Tags.Count; ++i)
                    _Tags[i].Save(w);
                w.WriteEndElement();
            }
            if (LoginIPs.Length > 0)
            {
                w.WriteStartElement("addressList");
                w.WriteAttributeString("count", LoginIPs.Length.ToString());
                for (var i = 0; i < LoginIPs.Length; ++i) { w.WriteStartElement("ip"); w.WriteString(LoginIPs[i].ToString()); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            if (IPRestrictions.Length > 0)
            {
                w.WriteStartElement("accessCheck");
                for (var i = 0; i < IPRestrictions.Length; ++i) { w.WriteStartElement("ip"); w.WriteString(IPRestrictions[i]); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            w.WriteStartElement("totalCurrency"); w.WriteString(XmlConvert.ToString(TotalCurrency)); w.WriteEndElement();
            w.WriteEndElement();
        }

        /// <summary>
        /// Gets the current number of characters on this account.
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Length; ++i)
                    if (this[i] != null)
                        ++count;
                return count;
            }
        }

        /// <summary>
        /// Gets the maximum amount of characters allowed to be created on this account. Values other than 1, 5, 6, or 7 are not supported by the client.
        /// </summary>
        public int Limit => Core.SA ? 7 : Core.AOS ? 6 : 5;

        /// <summary>
        /// Gets the maxmimum amount of characters that this account can hold.
        /// </summary>
        public int Length => _Mobiles.Length;

        /// <summary>
        /// Gets or sets the character at a specified index for this account. Out of bound index values are handled; null returned for get, ignored for set.
        /// </summary>
        public Mobile this[int index]
        {
            get
            {
                if (index >= 0 && index < _Mobiles.Length)
                {
                    var m = _Mobiles[index];
                    if (m != null && m.Deleted)
                    {
                        m.Account = null;
                        _Mobiles[index] = m = null;
                    }
                    return m;
                }
                return null;
            }
            set
            {
                if (index >= 0 && index < _Mobiles.Length)
                {
                    if (_Mobiles[index] != null)
                        _Mobiles[index].Account = null;
                    _Mobiles[index] = value;
                    if (_Mobiles[index] != null)
                        _Mobiles[index].Account = this;
                }
            }
        }

        public override string ToString() => Username;

        public int CompareTo(Account other) => other == null ? 1 : Username.CompareTo(other.Username);

        public int CompareTo(IAccount other) => other == null ? 1 : Username.CompareTo(other.Username);

        public int CompareTo(object obj)
        {
            if (obj is Account)
                return CompareTo((Account)obj);
            throw new ArgumentException();
        }

        #region Gold Account

        /// <summary>
        ///     This amount specifies the value at which point Gold turns to Platinum.
        ///     By default, when 1,000,000,000 Gold is accumulated, it will transform
        ///     into 1 Platinum.
        /// </summary>
        public static int CurrencyThreshold
        {
            get => AccountGold.CurrencyThreshold;
            set => AccountGold.CurrencyThreshold = value;
        }

        /// <summary>
        ///     This amount represents the total amount of currency owned by the player.
        ///     It is cumulative of both Gold and Platinum, the absolute total amount of
        ///     Gold owned by the player can be found by multiplying this value by the
        ///     CurrencyThreshold value.
        /// </summary>
        [CommandProperty(AccessLevel.Administrator, true)]
        public double TotalCurrency { get; private set; }

        /// <summary>
        ///     This amount represents the current amount of Gold owned by the player.
        ///     The value does not include the value of Platinum and ranges from
        ///     0 to 999,999,999 by default.
        /// </summary>
        [CommandProperty(AccessLevel.Administrator)]
        public int TotalGold => (int)Math.Floor((TotalCurrency - Math.Truncate(TotalCurrency)) * Math.Max(1.0, CurrencyThreshold));

        /// <summary>
        ///     This amount represents the current amount of Platinum owned by the player.
        ///     The value does not include the value of Gold and ranges from
        ///     0 to 2,147,483,647 by default.
        ///     One Platinum represents the value of CurrencyThreshold in Gold.
        /// </summary>
        [CommandProperty(AccessLevel.Administrator)]
        public int TotalPlat => (int)Math.Truncate(TotalCurrency);

        /// <summary>
        ///     Attempts to deposit the given amount of Gold and Platinum into this account.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <returns>True if successful, false if amount given is less than or equal to zero.</returns>
        public bool DepositCurrency(double amount)
        {
            if (amount <= 0)
                return false;
            TotalCurrency += amount;
            return true;
        }

        /// <summary>
        ///     Attempts to deposit the given amount of Gold into this account.
        ///     If the given amount is greater than the CurrencyThreshold,
        ///     Platinum will be deposited to offset the difference.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <returns>True if successful, false if amount given is less than or equal to zero.</returns>
        public bool DepositGold(int amount) => DepositCurrency(amount / Math.Max(1.0, CurrencyThreshold));

        /// <summary>
        ///     Attempts to deposit the given amount of Gold into this account.
        ///     If the given amount is greater than the CurrencyThreshold,
        ///     Platinum will be deposited to offset the difference.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <returns>True if successful, false if amount given is less than or equal to zero.</returns>
        public bool DepositGold(long amount) => DepositCurrency(amount / Math.Max(1.0, CurrencyThreshold));

        /// <summary>
        ///     Attempts to deposit the given amount of Platinum into this account.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <returns>True if successful, false if amount given is less than or equal to zero.</returns>
        public bool DepositPlat(int amount) => DepositCurrency(amount);

        /// <summary>
        ///     Attempts to deposit the given amount of Platinum into this account.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <returns>True if successful, false if amount given is less than or equal to zero.</returns>
        public bool DepositPlat(long amount) => DepositCurrency(amount);

        /// <summary>
        ///     Attempts to withdraw the given amount of Platinum and Gold from this account.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <returns>True if successful, false if balance was too low.</returns>
        public bool WithdrawCurrency(double amount)
        {
            if (amount <= 0)
                return true;
            if (amount > TotalCurrency)
                return false;
            TotalCurrency -= amount;
            return true;
        }

        /// <summary>
        ///     Attempts to withdraw the given amount of Gold from this account.
        ///     If the given amount is greater than the CurrencyThreshold,
        ///     Platinum will be withdrawn to offset the difference.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <returns>True if successful, false if balance was too low.</returns>
        public bool WithdrawGold(int amount) => WithdrawCurrency(amount / Math.Max(1.0, CurrencyThreshold));

        /// <summary>
        ///     Attempts to withdraw the given amount of Gold from this account.
        ///     If the given amount is greater than the CurrencyThreshold,
        ///     Platinum will be withdrawn to offset the difference.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <returns>True if successful, false if balance was too low.</returns>
        public bool WithdrawGold(long amount) => WithdrawCurrency(amount / Math.Max(1.0, CurrencyThreshold));

        /// <summary>
        ///     Attempts to withdraw the given amount of Platinum from this account.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <returns>True if successful, false if balance was too low.</returns>
        public bool WithdrawPlat(int amount) => WithdrawCurrency(amount);

        /// <summary>
        ///     Attempts to withdraw the given amount of Platinum from this account.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <returns>True if successful, false if balance was too low.</returns>
        public bool WithdrawPlat(long amount) => WithdrawCurrency(amount);

        /// <summary>
        ///     Gets the total balance of Gold for this account.
        /// </summary>
        /// <param name="gold">Gold value, Platinum exclusive</param>
        /// <param name="totalGold">Gold value, Platinum inclusive</param>
        public void GetGoldBalance(out int gold, out double totalGold)
        {
            gold = TotalGold;
            totalGold = TotalCurrency * Math.Max(1.0, CurrencyThreshold);
        }

        /// <summary>
        ///     Gets the total balance of Gold for this account.
        /// </summary>
        /// <param name="gold">Gold value, Platinum exclusive</param>
        /// <param name="totalGold">Gold value, Platinum inclusive</param>
        public void GetGoldBalance(out long gold, out double totalGold)
        {
            gold = TotalGold;
            totalGold = TotalCurrency * Math.Max(1.0, CurrencyThreshold);
        }

        /// <summary>
        ///     Gets the total balance of Platinum for this account.
        /// </summary>
        /// <param name="plat">Platinum value, Gold exclusive</param>
        /// <param name="totalPlat">Platinum value, Gold inclusive</param>
        public void GetPlatBalance(out int plat, out double totalPlat)
        {
            plat = TotalPlat;
            totalPlat = TotalCurrency;
        }

        /// <summary>
        ///     Gets the total balance of Platinum for this account.
        /// </summary>
        /// <param name="plat">Platinum value, Gold exclusive</param>
        /// <param name="totalPlat">Platinum value, Gold inclusive</param>
        public void GetPlatBalance(out long plat, out double totalPlat)
        {
            plat = TotalPlat;
            totalPlat = TotalCurrency;
        }

        /// <summary>
        ///     Gets the total balance of Gold and Platinum for this account.
        /// </summary>
        /// <param name="gold">Gold value, Platinum exclusive</param>
        /// <param name="totalGold">Gold value, Platinum inclusive</param>
        /// <param name="plat">Platinum value, Gold exclusive</param>
        /// <param name="totalPlat">Platinum value, Gold inclusive</param>
        public void GetBalance(out int gold, out double totalGold, out int plat, out double totalPlat)
        {
            GetGoldBalance(out gold, out totalGold);
            GetPlatBalance(out plat, out totalPlat);
        }

        /// <summary>
        ///     Gets the total balance of Gold and Platinum for this account.
        /// </summary>
        /// <param name="gold">Gold value, Platinum exclusive</param>
        /// <param name="totalGold">Gold value, Platinum inclusive</param>
        /// <param name="plat">Platinum value, Gold exclusive</param>
        /// <param name="totalPlat">Platinum value, Gold inclusive</param>
        public void GetBalance(out long gold, out double totalGold, out long plat, out double totalPlat)
        {
            GetGoldBalance(out gold, out totalGold);
            GetPlatBalance(out plat, out totalPlat);
        }

        #endregion
    }
}