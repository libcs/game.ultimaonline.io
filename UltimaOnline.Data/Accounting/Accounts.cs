using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace UltimaOnline.Accounting
{
    public class Accounts
    {
        static Dictionary<string, IAccount> _Accounts = new Dictionary<string, IAccount>();

        public static void Configure()
        {
            EventSink.WorldLoad += new WorldLoadEventHandler(Load);
            EventSink.WorldSave += new WorldSaveEventHandler(Save);
        }

        static Accounts() { }

        public static int Count => _Accounts.Count;

        public static ICollection<IAccount> GetAccounts() => _Accounts.Values;

        public static IAccount GetAccount(string username)
        {
            _Accounts.TryGetValue(username, out IAccount a);
            return a;
        }

        public static void Add(IAccount a) => _Accounts[a.Username] = a;

        public static void Remove(string username) => _Accounts.Remove(username);

        public static void Load()
        {
            _Accounts = new Dictionary<string, IAccount>(32, StringComparer.OrdinalIgnoreCase);
            var filePath = Path.Combine("Saves/Accounts", "accounts.xml");
            if (!File.Exists(filePath))
                return;
            var doc = new XmlDocument();
            doc.Load(filePath);
            var root = doc["accounts"];
            foreach (XmlElement account in root.GetElementsByTagName("account"))
                try { var acct = new Account(account); }
                catch { Console.WriteLine("Warning: Account instance load failed"); }
        }

        public static void Save(WorldSaveEventArgs e)
        {
            if (!Directory.Exists("Saves/Accounts"))
                Directory.CreateDirectory("Saves/Accounts");
            var filePath = Path.Combine("Saves/Accounts", "accounts.xml");
            using (StreamWriter op = new StreamWriter(filePath))
            {
                var w = new XmlTextWriter(op)
                {
                    Formatting = Formatting.Indented,
                    IndentChar = '\t',
                    Indentation = 1
                };
                w.WriteStartDocument(true);
                w.WriteStartElement("accounts");
                w.WriteAttributeString("count", _Accounts.Count.ToString());
                foreach (Account a in GetAccounts())
                    a.Save(w);
                w.WriteEndElement();
                w.Close();
            }
        }
    }
}