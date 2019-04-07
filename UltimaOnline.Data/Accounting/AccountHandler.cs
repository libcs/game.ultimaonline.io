using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UltimaOnline.Accounting;
using UltimaOnline.Commands;
using UltimaOnline.Engines.Help;
using UltimaOnline.Network;
using UltimaOnline.Regions;

namespace UltimaOnline.Misc
{
    public enum PasswordProtection
    {
        None,
        Crypt,
        NewCrypt
    }

    public class AccountHandler
    {
        static int MaxAccountsPerIP = 1;
        static bool AutoAccountCreation = true;
        static bool RestrictDeletion = !TestCenter.Enabled;
        static TimeSpan DeleteDelay = TimeSpan.FromDays(7.0);
        public static PasswordProtection ProtectPasswords = PasswordProtection.NewCrypt;
        public static AccessLevel LockdownLevel { get; set; }

        static CityInfo[] StartingCities = new CityInfo[]
        {
            new CityInfo( "New Haven",  "New Haven Bank",   1150168, 3667,  2625,   0  ),
            new CityInfo( "Yew",        "The Empath Abbey", 1075072, 633,   858,    0  ),
            new CityInfo( "Minoc",      "The Barnacle",     1075073, 2476,  413,    15 ),
            new CityInfo( "Britain",    "The Wayfarer's Inn",   1075074, 1602,  1591,   20 ),
            new CityInfo( "Moonglow",   "The Scholars Inn", 1075075, 4408,  1168,   0  ),
            new CityInfo( "Trinsic",    "The Traveler's Inn",   1075076, 1845,  2745,   0  ),
            new CityInfo( "Jhelom",     "The Mercenary Inn",    1075078, 1374,  3826,   0  ),
            new CityInfo( "Skara Brae", "The Falconer's Inn",   1075079, 618,   2234,   0  ),
            new CityInfo( "Vesper",     "The Ironwood Inn", 1075080, 2771,  976,    0  )
        };

        /* Old Haven/Magincia Locations
			new CityInfo( "Britain", "Sweet Dreams Inn", 1496, 1628, 10 );
			// ..
			// Trinsic
			new CityInfo( "Magincia", "The Great Horns Tavern", 3734, 2222, 20 ),
			// Jhelom
			// ..
			new CityInfo( "Haven", "Buckler's Hideaway", 3667, 2625, 0 )

			if ( Core.AOS )
			{
				//CityInfo haven = new CityInfo( "Haven", "Uzeraan's Mansion", 3618, 2591, 0 );
				CityInfo haven = new CityInfo( "Haven", "Uzeraan's Mansion", 3503, 2574, 14 );
				StartingCities[StartingCities.Length - 1] = haven;
			}
		*/

        static bool PasswordCommandEnabled = false;

        public static void Initialize()
        {
            EventSink.DeleteRequest += new DeleteRequestEventHandler(EventSink_DeleteRequest);
            EventSink.AccountLogin += new AccountLoginEventHandler(EventSink_AccountLogin);
            EventSink.GameLogin += new GameLoginEventHandler(EventSink_GameLogin);
            if (PasswordCommandEnabled)
                CommandSystem.Register("Password", AccessLevel.Player, new CommandEventHandler(Password_OnCommand));
        }

        [Usage("Password <newPassword> <repeatPassword>"), Description("Changes the password of the commanding players account. Requires the same C-class IP address as the account's creator.")]
        public static void Password_OnCommand(CommandEventArgs e)
        {
            var from = e.Mobile;
            if (!(from.Account is Account acct))
                return;
            var accessList = acct.LoginIPs;
            if (accessList.Length == 0)
                return;
            var ns = from.NetState;
            if (ns == null)
                return;
            if (e.Length == 0)
            {
                from.SendMessage("You must specify the new password.");
                return;
            }
            else if (e.Length == 1)
            {
                from.SendMessage("To prevent potential typing mistakes, you must type the password twice. Use the format:");
                from.SendMessage("Password \"(newPassword)\" \"(repeated)\"");
                return;
            }
            var pass = e.GetString(0);
            var pass2 = e.GetString(1);
            if (pass != pass2)
            {
                from.SendMessage("The passwords do not match.");
                return;
            }
            var isSafe = true;
            for (var i = 0; isSafe && i < pass.Length; ++i)
                isSafe = pass[i] >= 0x20 && pass[i] < 0x7F;
            if (!isSafe)
            {
                from.SendMessage("That is not a valid password.");
                return;
            }
            try
            {
                var ipAddress = ns.Address;
                if (Utility.IPMatchClassC(accessList[0], ipAddress))
                {
                    acct.SetPassword(pass);
                    from.SendMessage("The password to your account has changed.");
                }
                else
                {
                    var entry = PageQueue.GetEntry(from);
                    if (entry != null)
                        from.SendMessage(entry.Message.StartsWith("[Automated: Change Password]") ? "You already have a password change request in the help system queue." : "Your IP address does not match that which created this account.");
                    else if (PageQueue.CheckAllowedToPage(from))
                    {
                        from.SendMessage("Your IP address does not match that which created this account.  A page has been entered into the help system on your behalf.");
                        from.SendLocalizedMessage(501234, string.Empty, 0x35); /* The next available Counselor/Game Master will respond as soon as possible. Please check your Journal for messages every few minutes. */
                        PageQueue.Enqueue(new PageEntry(from, String.Format("[Automated: Change Password]<br>Desired password: {0}<br>Current IP address: {1}<br>Account IP address: {2}", pass, ipAddress, accessList[0]), PageType.Account));
                    }
                }
            }
            catch { }
        }

        static void EventSink_DeleteRequest(DeleteRequestEventArgs e)
        {
            var state = e.State;
            var index = e.Index;
            if (!(state.Account is Account acct))
                state.Dispose();
            else if (index < 0 || index >= acct.Length)
            {
                state.Send(new DeleteResult(DeleteResultType.BadRequest));
                state.Send(new CharacterListUpdate(acct));
            }
            else
            {
                var m = acct[index];
                if (m == null)
                {
                    state.Send(new DeleteResult(DeleteResultType.CharNotExist));
                    state.Send(new CharacterListUpdate(acct));
                }
                else if (m.NetState != null)
                {
                    state.Send(new DeleteResult(DeleteResultType.CharBeingPlayed));
                    state.Send(new CharacterListUpdate(acct));
                }
                else if (RestrictDeletion && DateTime.UtcNow < (m.CreationTime + DeleteDelay))
                {
                    state.Send(new DeleteResult(DeleteResultType.CharTooYoung));
                    state.Send(new CharacterListUpdate(acct));
                }
                else if (m.AccessLevel == AccessLevel.Player && Region.Find(m.LogoutLocation, m.LogoutMap).GetRegion(typeof(Jail)) != null) //Don't need to check current location, if netstate is null, they're logged out
                {
                    state.Send(new DeleteResult(DeleteResultType.BadRequest));
                    state.Send(new CharacterListUpdate(acct));
                }
                else
                {
                    Console.WriteLine($"Client: {state}: Deleting character {index} (0x{m.Serial.Value:X})");
                    acct.Comments.Add(new AccountComment("System", $"Character #{index + 1} {m} deleted by {state}"));
                    m.Delete();
                    state.Send(new CharacterListUpdate(acct));
                }
            }
        }

        public static bool CanCreate(IPAddress ip) => !IPTable.ContainsKey(ip) ? true : IPTable[ip] < MaxAccountsPerIP;

        static Dictionary<IPAddress, int> _IPTable;

        public static Dictionary<IPAddress, int> IPTable
        {
            get
            {
                if (_IPTable == null)
                {
                    _IPTable = new Dictionary<IPAddress, int>();
                    foreach (Account a in Accounts.GetAccounts())
                        if (a.LoginIPs.Length > 0)
                        {
                            var ip = a.LoginIPs[0];
                            if (_IPTable.ContainsKey(ip)) _IPTable[ip]++;
                            else _IPTable[ip] = 1;
                        }
                }
                return _IPTable;
            }
        }

        static readonly char[] _ForbiddenChars = new char[]
        {
            '<', '>', ':', '"', '/', '\\', '|', '?', '*'
        };

        static bool IsForbiddenChar(char c)
        {
            for (var i = 0; i < _ForbiddenChars.Length; ++i)
                if (c == _ForbiddenChars[i])
                    return true;
            return false;
        }

        static Account CreateAccount(NetState state, string un, string pw)
        {
            if (un.Length == 0 || pw.Length == 0)
                return null;
            var isSafe = !(un.StartsWith(" ") || un.EndsWith(" ") || un.EndsWith("."));
            for (var i = 0; isSafe && i < un.Length; ++i)
                isSafe = un[i] >= 0x20 && un[i] < 0x7F && !IsForbiddenChar(un[i]);
            for (var i = 0; isSafe && i < pw.Length; ++i)
                isSafe = pw[i] >= 0x20 && pw[i] < 0x7F;
            if (!isSafe)
                return null;
            if (!CanCreate(state.Address))
            {
                Console.WriteLine($"Login: {state}: Account '{un}' not created, ip already has {MaxAccountsPerIP} account{(MaxAccountsPerIP == 1 ? string.Empty : "s")}.");
                return null;
            }
            Console.WriteLine($"Login: {state}: Creating new account '{un}'");
            var a = new Account(un, pw);
            return a;
        }

        public static void EventSink_AccountLogin(AccountLoginEventArgs e)
        {
            if (!IPLimiter.SocketBlock && !IPLimiter.Verify(e.State.Address))
            {
                e.Accepted = false;
                e.RejectReason = ALRReason.InUse;
                Console.WriteLine($"Login: {e.State}: Past IP limit threshold");
                using (var op = new StreamWriter("ipLimits.log", true))
                    op.WriteLine($"{e.State}\tPast IP limit threshold\t{DateTime.UtcNow}");
                return;
            }
            var un = e.Username;
            var pw = e.Password;
            e.Accepted = false;
            if (!(Accounts.GetAccount(un) is Account acct))
            {
                if (AutoAccountCreation && un.Trim().Length > 0) // To prevent someone from making an account of just '' or a bunch of meaningless spaces
                {
                    e.State.Account = acct = CreateAccount(e.State, un, pw);
                    e.Accepted = acct == null ? false : acct.CheckAccess(e.State);
                    if (!e.Accepted)
                        e.RejectReason = ALRReason.BadComm;
                }
                else
                {
                    Console.WriteLine($"Login: {e.State}: Invalid username '{un}'");
                    e.RejectReason = ALRReason.Invalid;
                }
            }
            else if (!acct.HasAccess(e.State))
            {
                Console.WriteLine($"Login: {e.State}: Access denied for '{un}'");
                e.RejectReason = LockdownLevel > AccessLevel.Player ? ALRReason.BadComm : ALRReason.BadPass;
            }
            else if (!acct.CheckPassword(pw))
            {
                Console.WriteLine($"Login: {e.State}: Invalid password for '{un}'");
                e.RejectReason = ALRReason.BadPass;
            }
            else if (acct.Banned)
            {
                Console.WriteLine($"Login: {e.State}: Banned account '{un}'");
                e.RejectReason = ALRReason.Blocked;
            }
            else
            {
                Console.WriteLine($"Login: {e.State}: Valid credentials for '{un}'");
                e.State.Account = acct;
                e.Accepted = true;
                acct.LogAccess(e.State);
            }
            if (!e.Accepted)
                AccountAttackLimiter.RegisterInvalidAccess(e.State);
        }

        public static void EventSink_GameLogin(GameLoginEventArgs e)
        {
            if (!IPLimiter.SocketBlock && !IPLimiter.Verify(e.State.Address))
            {
                e.Accepted = false;
                Console.WriteLine($"Login: {e.State}: Past IP limit threshold");
                using (var op = new StreamWriter("ipLimits.log", true))
                    op.WriteLine($"{e.State}\tPast IP limit threshold\t{DateTime.UtcNow}");
                return;
            }
            var un = e.Username;
            var pw = e.Password;
            if (!(Accounts.GetAccount(un) is Account acct))
                e.Accepted = false;
            else if (!acct.HasAccess(e.State))
            {
                Console.WriteLine($"Login: {e.State}: Access denied for '{un}'");
                e.Accepted = false;
            }
            else if (!acct.CheckPassword(pw))
            {
                Console.WriteLine($"Login: {e.State}: Invalid password for '{un}'");
                e.Accepted = false;
            }
            else if (acct.Banned)
            {
                Console.WriteLine($"Login: {e.State}: Banned account '{un}'");
                e.Accepted = false;
            }
            else
            {
                acct.LogAccess(e.State);
                Console.WriteLine($"Login: {e.State}: Account '{un}' at character list");
                e.State.Account = acct;
                e.Accepted = true;
                e.CityInfo = StartingCities;
            }
            if (!e.Accepted)
                AccountAttackLimiter.RegisterInvalidAccess(e.State);
        }

        public static bool CheckAccount(Mobile mobCheck, Mobile accCheck)
        {
            if (accCheck != null && accCheck.Account is Account a)
                for (var i = 0; i < a.Length; ++i)
                    if (a[i] == mobCheck)
                        return true;
            return false;
        }
    }
}