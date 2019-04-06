using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UltimaOnline.Guilds;
using UltimaOnline.Network;

namespace UltimaOnline
{
    public static class World
    {
        readonly static ManualResetEvent _diskWriteHandle = new ManualResetEvent(true);

        static Queue<IEntity> _addQueue, _deleteQueue;

        public static bool Saving { get; private set; }
        public static bool Loaded { get; private set; }
        public static bool Loading { get; private set; }

        public readonly static string MobileIndexPath = Path.Combine("Saves/Mobiles/", "Mobiles.idx");
        public readonly static string MobileTypesPath = Path.Combine("Saves/Mobiles/", "Mobiles.tdb");
        public readonly static string MobileDataPath = Path.Combine("Saves/Mobiles/", "Mobiles.bin");

        public readonly static string ItemIndexPath = Path.Combine("Saves/Items/", "Items.idx");
        public readonly static string ItemTypesPath = Path.Combine("Saves/Items/", "Items.tdb");
        public readonly static string ItemDataPath = Path.Combine("Saves/Items/", "Items.bin");

        public readonly static string GuildIndexPath = Path.Combine("Saves/Guilds/", "Guilds.idx");
        public readonly static string GuildDataPath = Path.Combine("Saves/Guilds/", "Guilds.bin");

        public static void NotifyDiskWriteComplete()
        {
            if (_diskWriteHandle.Set())
                Console.WriteLine("Closing Save Files. ");
        }

        public static void WaitForWriteCompletion() => _diskWriteHandle.WaitOne();

        public static Dictionary<Serial, Mobile> Mobiles { get; private set; }

        public static Dictionary<Serial, Item> Items { get; private set; }

        public static bool OnDelete(IEntity entity)
        {
            if (Saving || Loading)
            {
                if (Saving)
                    AppendSafetyLog("delete", entity);
                _deleteQueue.Enqueue(entity);
                return false;
            }
            return true;
        }

        public static void Broadcast(int hue, bool ascii, string text)
        {
            var p = ascii
                ? (Packet)new AsciiMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "System", text)
                : new UnicodeMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "ENU", "System", text);
            var list = NetState.Instances;
            p.Acquire();
            for (var i = 0; i < list.Count; ++i)
                if (list[i].Mobile != null)
                    list[i].Send(p);
            p.Release();
            NetState.FlushAll();
        }

        public static void Broadcast(int hue, bool ascii, string format, params object[] args) => Broadcast(hue, ascii, string.Format(format, args));

        interface IEntityEntry
        {
            Serial Serial { get; }
            int TypeID { get; }
            long Position { get; }
            int Length { get; }
        }

        sealed class GuildEntry : IEntityEntry
        {
            public GuildEntry(BaseGuild g, long pos, int length)
            {
                Guild = g;
                Position = pos;
                Length = length;
            }

            public BaseGuild Guild { get; }
            public Serial Serial => Guild == null ? 0 : Guild.Id;
            public int TypeID => 0;
            public long Position { get; }
            public int Length { get; }
        }

        sealed class ItemEntry : IEntityEntry
        {
            public ItemEntry(Item item, int typeID, string typeName, long pos, int length)
            {
                Item = item;
                TypeID = typeID;
                TypeName = typeName;
                Position = pos;
                Length = length;
            }

            public Item Item { get; }
            public Serial Serial => Item == null ? Serial.MinusOne : Item.Serial;
            public int TypeID { get; }
            public string TypeName { get; }
            public long Position { get; }
            public int Length { get; }
        }

        sealed class MobileEntry : IEntityEntry
        {
            public MobileEntry(Mobile mobile, int typeID, string typeName, long pos, int length)
            {
                Mobile = mobile;
                TypeID = typeID;
                TypeName = typeName;
                Position = pos;
                Length = length;
            }

            public Mobile Mobile { get; }
            public Serial Serial => Mobile == null ? Serial.MinusOne : Mobile.Serial;
            public int TypeID { get; }
            public string TypeName { get; }
            public long Position { get; }
            public int Length { get; }
        }

        public static string LoadingType { get; private set; }

        static readonly Type[] _serialTypeArray = new[] { typeof(Serial) };

        static List<Tuple<ConstructorInfo, string>> ReadTypes(BinaryReader r)
        {
            var count = r.ReadInt32();
            var types = new List<Tuple<ConstructorInfo, string>>(count);
            for (var i = 0; i < count; ++i)
            {
                var typeName = r.ReadString();
                var t = ScriptCompiler.FindTypeByFullName(typeName);
                if (t == null)
                {
                    Console.WriteLine("failed");
                    if (!Core.Service)
                    {
                        Console.WriteLine($"Error: Type '{typeName}' was not found. Delete all of those types? (y/n)");
                        if (Console.ReadKey(true).Key == ConsoleKey.Y)
                        {
                            types.Add(null);
                            Console.Write("World: Loading...");
                            continue;
                        }
                        Console.WriteLine("Types will not be deleted. An exception will be thrown.");
                    }
                    else Console.WriteLine($"Error: Type '{typeName}' was not found.");
                    throw new Exception($"Bad type '{typeName}'");
                }
                var ctor = t.GetConstructor(_serialTypeArray);
                if (ctor != null)
                    types.Add(new Tuple<ConstructorInfo, string>(ctor, typeName));
                else throw new Exception($"Type '{t}' does not have a serialization constructor");
            }
            return types;
        }

        public static void Load()
        {
            if (Loaded)
                return;
            Loaded = true;
            LoadingType = null;
            Console.Write("World: Loading...");
            var watch = Stopwatch.StartNew();
            Loading = true;
            _addQueue = new Queue<IEntity>();
            _deleteQueue = new Queue<IEntity>();
            var ctorArgs = new object[1];
            // MOBILE
            var mobiles = new List<MobileEntry>();
            if (File.Exists(MobileIndexPath) && File.Exists(MobileTypesPath))
                using (var idx = new FileStream(MobileIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var idxR = new BinaryReader(idx))
                using (var tdb = new FileStream(MobileTypesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var tdbR = new BinaryReader(tdb))
                {
                    var types = ReadTypes(tdbR);
                    var mobileCount = idxR.ReadInt32();
                    Mobiles = new Dictionary<Serial, Mobile>(mobileCount);
                    for (var i = 0; i < mobileCount; ++i)
                    {
                        var typeID = idxR.ReadInt32();
                        var serial = idxR.ReadInt32();
                        var pos = idxR.ReadInt64();
                        var length = idxR.ReadInt32();
                        var objs = types[typeID];
                        if (objs == null)
                            continue;
                        Mobile m = null;
                        var ctor = objs.Item1;
                        var typeName = objs.Item2;
                        try
                        {
                            ctorArgs[0] = (Serial)serial;
                            m = (Mobile)(ctor.Invoke(ctorArgs));
                        }
                        catch { }
                        if (m != null)
                        {
                            mobiles.Add(new MobileEntry(m, typeID, typeName, pos, length));
                            AddMobile(m);
                        }
                    }
                }
            else Mobiles = new Dictionary<Serial, Mobile>();
            // ITEM
            var items = new List<ItemEntry>();
            if (File.Exists(ItemIndexPath) && File.Exists(ItemTypesPath))
                using (var idx = new FileStream(ItemIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var idxR = new BinaryReader(idx))
                using (var tdb = new FileStream(ItemTypesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var tdbR = new BinaryReader(tdb))
                {
                    var types = ReadTypes(tdbR);
                    var itemCount = idxR.ReadInt32();
                    Items = new Dictionary<Serial, Item>(itemCount);
                    for (var i = 0; i < itemCount; ++i)
                    {
                        var typeID = idxR.ReadInt32();
                        var serial = idxR.ReadInt32();
                        var pos = idxR.ReadInt64();
                        var length = idxR.ReadInt32();
                        var objs = types[typeID];
                        if (objs == null)
                            continue;
                        Item item = null;
                        var ctor = objs.Item1;
                        var typeName = objs.Item2;
                        try
                        {
                            ctorArgs[0] = (Serial)serial;
                            item = (Item)(ctor.Invoke(ctorArgs));
                        }
                        catch { }
                        if (item != null)
                        {
                            items.Add(new ItemEntry(item, typeID, typeName, pos, length));
                            AddItem(item);
                        }
                    }
                }
            else Items = new Dictionary<Serial, Item>();
            // GUILD
            var guilds = new List<GuildEntry>();
            if (File.Exists(GuildIndexPath))
                using (var idx = new FileStream(GuildIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var idxReader = new BinaryReader(idx))
                {
                    var guildCount = idxReader.ReadInt32();
                    var createEventArgs = new CreateGuildEventArgs(-1);
                    for (var i = 0; i < guildCount; ++i)
                    {
                        idxReader.ReadInt32(); // no typeid for guilds
                        var id = idxReader.ReadInt32();
                        var pos = idxReader.ReadInt64();
                        var length = idxReader.ReadInt32();
                        createEventArgs.Id = id;
                        EventSink.InvokeCreateGuild(createEventArgs);
                        var guild = createEventArgs.Guild;
                        if (guild != null)
                            guilds.Add(new GuildEntry(guild, pos, length));
                    }
                }
            //
            bool failedMobiles = false, failedItems = false, failedGuilds = false;
            Type failedType = null;
            var failedSerial = Serial.Zero;
            Exception failed = null;
            var failedTypeID = 0;
            // MOBILE DATA
            if (File.Exists(MobileDataPath))
                using (var fs = new FileStream(MobileDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var r = new BinaryFileReader(new BinaryReader(fs));
                    for (var i = 0; i < mobiles.Count; ++i)
                    {
                        var entry = mobiles[i];
                        var m = entry.Mobile;
                        if (m != null)
                        {
                            r.Seek(entry.Position, SeekOrigin.Begin);
                            try
                            {
                                LoadingType = entry.TypeName;
                                m.Deserialize(r);
                                if (r.Position != (entry.Position + entry.Length))
                                    throw new Exception($"***** Bad serialize on {m.GetType()} *****");
                            }
                            catch (Exception e)
                            {
                                mobiles.RemoveAt(i);
                                failed = e;
                                failedMobiles = true;
                                failedType = m.GetType();
                                failedTypeID = entry.TypeID;
                                failedSerial = m.Serial;
                                break;
                            }
                        }
                    }
                    r.Close();
                }
            // ITEM DATA
            if (!failedMobiles && File.Exists(ItemDataPath))
                using (var fs = new FileStream(ItemDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var r = new BinaryFileReader(new BinaryReader(fs));
                    for (var i = 0; i < items.Count; ++i)
                    {
                        var entry = items[i];
                        var item = entry.Item;
                        if (item != null)
                        {
                            r.Seek(entry.Position, SeekOrigin.Begin);
                            try
                            {
                                LoadingType = entry.TypeName;
                                item.Deserialize(r);
                                if (r.Position != (entry.Position + entry.Length))
                                    throw new Exception($"***** Bad serialize on {item.GetType()} *****");
                            }
                            catch (Exception e)
                            {
                                items.RemoveAt(i);
                                failed = e;
                                failedItems = true;
                                failedType = item.GetType();
                                failedTypeID = entry.TypeID;
                                failedSerial = item.Serial;
                                break;
                            }
                        }
                    }
                    r.Close();
                }
            LoadingType = null;
            // GUILD DATA
            if (!failedMobiles && !failedItems && File.Exists(GuildDataPath))
                using (var fs = new FileStream(GuildDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var r = new BinaryFileReader(new BinaryReader(fs));
                    for (var i = 0; i < guilds.Count; ++i)
                    {
                        var entry = guilds[i];
                        var g = entry.Guild;
                        if (g != null)
                        {
                            r.Seek(entry.Position, SeekOrigin.Begin);
                            try
                            {
                                g.Deserialize(r);
                                if (r.Position != (entry.Position + entry.Length))
                                    throw new Exception("***** Bad serialize on Guild {g.Id} *****");
                            }
                            catch (Exception e)
                            {
                                guilds.RemoveAt(i);
                                failed = e;
                                failedGuilds = true;
                                failedType = typeof(BaseGuild);
                                failedTypeID = g.Id;
                                failedSerial = g.Id;
                                break;
                            }
                        }
                    }
                    r.Close();
                }

            if (failedItems || failedMobiles || failedGuilds)
            {
                Console.WriteLine("An error was encountered while loading a saved object");
                Console.WriteLine($" - Type: {failedType}");
                Console.WriteLine($" - Serial: {failedSerial}");
                if (!Core.Service)
                {
                    Console.WriteLine("Delete the object? (y/n)");
                    if (Console.ReadKey(true).Key == ConsoleKey.Y)
                    {
                        if (failedType != typeof(BaseGuild))
                        {
                            Console.WriteLine("Delete all objects of that type? (y/n)");
                            if (Console.ReadKey(true).Key == ConsoleKey.Y)
                            {
                                if (failedMobiles)
                                    for (var i = 0; i < mobiles.Count;)
                                        if (mobiles[i].TypeID == failedTypeID) mobiles.RemoveAt(i);
                                        else ++i;
                                else if (failedItems)
                                    for (var i = 0; i < items.Count;)
                                        if (items[i].TypeID == failedTypeID) items.RemoveAt(i);
                                        else ++i;
                            }
                        }
                        SaveIndex(mobiles, MobileIndexPath);
                        SaveIndex(items, ItemIndexPath);
                        SaveIndex(guilds, GuildIndexPath);
                    }
                    Console.WriteLine("After pressing return an exception will be thrown and the server will terminate.");
                    Console.ReadLine();
                }
                else Console.WriteLine("An exception will be thrown and the server will terminate.");
                throw new Exception($"Load failed (items={failedItems}, mobiles={failedMobiles}, guilds={failedGuilds}, type={failedType}, serial={failedSerial})", failed);
            }
            EventSink.InvokeWorldLoad();
            Loading = false;
            ProcessSafetyQueues();
            foreach (var item in Items.Values)
            {
                if (item.Parent == null)
                    item.UpdateTotals();
                item.ClearProperties();
            }
            foreach (var m in Mobiles.Values)
            {
                m.UpdateRegion(); // Is this really needed?
                m.UpdateTotals();
                m.ClearProperties();
            }
            watch.Stop();
            Console.WriteLine($"done ({watch.Elapsed.TotalSeconds} items, {Items.Count} mobiles) ({Mobiles.Count:F2} seconds)");
        }

        static void ProcessSafetyQueues()
        {
            while (_addQueue.Count > 0)
            {
                var entity = _addQueue.Dequeue();
                if (entity is Item item) AddItem(item);
                else if (entity is Mobile mob) AddMobile(mob);
            }
            while (_deleteQueue.Count > 0)
            {
                var entity = _deleteQueue.Dequeue();
                if (entity is Item item) item.Delete();
                else if (entity is Mobile mob) mob.Delete();
            }
        }

        static void AppendSafetyLog(string action, IEntity entity)
        {
            var message = $"Warning: Attempted to {action} {entity} during world save." +
                $"{Environment.NewLine}This action could cause inconsistent state." +
                $"{Environment.NewLine}It is strongly advised that the offending scripts be corrected.";
            Console.WriteLine(message);
            try
            {
                using (var op = new StreamWriter("world-save-errors.log", true))
                {
                    op.WriteLine("{0}\t{1}", DateTime.UtcNow, message);
                    op.WriteLine(new StackTrace(2).ToString());
                    op.WriteLine();
                }
            }
            catch { }
        }

        static void SaveIndex<T>(List<T> list, string path) where T : IEntityEntry
        {
            if (!Directory.Exists("Saves/Mobiles/"))
                Directory.CreateDirectory("Saves/Mobiles/");
            if (!Directory.Exists("Saves/Items/"))
                Directory.CreateDirectory("Saves/Items/");
            if (!Directory.Exists("Saves/Guilds/"))
                Directory.CreateDirectory("Saves/Guilds/");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var w = new BinaryWriter(fs);
                w.Write(list.Count);
                for (var i = 0; i < list.Count; ++i)
                {
                    var e = list[i];
                    w.Write(e.TypeID);
                    w.Write(e.Serial);
                    w.Write(e.Position);
                    w.Write(e.Length);
                }
                w.Close();
            }
        }

        internal static int m_Saves;

        public static void Save()
        {
            Save(true, false);
        }

        public static void Save(bool message, bool permitBackgroundWrite)
        {
            if (Saving)
                return;

            ++m_Saves;

            NetState.FlushAll();
            NetState.Pause();

            World.WaitForWriteCompletion();//Blocks Save until current disk flush is done.

            Saving = true;

            _diskWriteHandle.Reset();

            if (message)
                Broadcast(0x35, true, "The world is saving, please wait.");

            SaveStrategy strategy = SaveStrategy.Acquire();
            Console.WriteLine("Core: Using {0} save strategy", strategy.Name.ToLowerInvariant());

            Console.Write("World: Saving...");

            Stopwatch watch = Stopwatch.StartNew();

            if (!Directory.Exists("Saves/Mobiles/"))
                Directory.CreateDirectory("Saves/Mobiles/");
            if (!Directory.Exists("Saves/Items/"))
                Directory.CreateDirectory("Saves/Items/");
            if (!Directory.Exists("Saves/Guilds/"))
                Directory.CreateDirectory("Saves/Guilds/");


            /*using ( SaveMetrics metrics = new SaveMetrics() ) {*/
            strategy.Save(null, permitBackgroundWrite);
            /*}*/

            try
            {
                EventSink.InvokeWorldSave(new WorldSaveEventArgs(message));
            }
            catch (Exception e)
            {
                throw new Exception("World Save event threw an exception.  Save failed!", e);
            }

            watch.Stop();

            Saving = false;

            if (!permitBackgroundWrite)
                World.NotifyDiskWriteComplete();    //Sets the DiskWriteHandle.  If we allow background writes, we leave this upto the individual save strategies.

            ProcessSafetyQueues();

            strategy.ProcessDecay();

            Console.WriteLine("Save done in {0:F2} seconds.", watch.Elapsed.TotalSeconds);

            if (message)
                Broadcast(0x35, true, "World save complete. The entire process took {0:F1} seconds.", watch.Elapsed.TotalSeconds);

            NetState.Resume();
        }

        internal static List<Type> _ItemTypes = new List<Type>();
        internal static List<Type> m_MobileTypes = new List<Type>();

        public static IEntity FindEntity(Serial serial)
        {
            if (serial.IsItem)
                return FindItem(serial);
            else if (serial.IsMobile)
                return FindMobile(serial);

            return null;
        }

        public static Mobile FindMobile(Serial serial)
        {
            Mobile mob;

            Mobiles.TryGetValue(serial, out mob);

            return mob;
        }

        public static void AddMobile(Mobile m)
        {
            if (Saving)
            {
                AppendSafetyLog("add", m);
                _addQueue.Enqueue(m);
            }
            else
            {
                Mobiles[m.Serial] = m;
            }
        }

        public static Item FindItem(Serial serial)
        {
            Item item;

            Items.TryGetValue(serial, out item);

            return item;
        }

        public static void AddItem(Item item)
        {
            if (Saving)
            {
                AppendSafetyLog("add", item);
                _addQueue.Enqueue(item);
            }
            else
            {
                Items[item.Serial] = item;
            }
        }

        public static void RemoveMobile(Mobile m)
        {
            Mobiles.Remove(m.Serial);
        }

        public static void RemoveItem(Item item)
        {
            Items.Remove(item.Serial);
        }
    }
}