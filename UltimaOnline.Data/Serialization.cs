using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UltimaOnline.Guilds;

namespace UltimaOnline
{
    public abstract class GenericReader
    {
        protected GenericReader() { }

        public abstract string ReadString();
        public abstract DateTime ReadDateTime();
        public abstract DateTimeOffset ReadDateTimeOffset();
        public abstract TimeSpan ReadTimeSpan();
        public abstract DateTime ReadDeltaTime();
        public abstract decimal ReadDecimal();
        public abstract long ReadLong();
        public abstract ulong ReadULong();
        public abstract int ReadInt();
        public abstract uint ReadUInt();
        public abstract short ReadShort();
        public abstract ushort ReadUShort();
        public abstract double ReadDouble();
        public abstract float ReadFloat();
        public abstract char ReadChar();
        public abstract byte ReadByte();
        public abstract sbyte ReadSByte();
        public abstract bool ReadBool();
        public abstract int ReadEncodedInt();
        public abstract IPAddress ReadIPAddress();

        public abstract Point3D ReadPoint3D();
        public abstract Point2D ReadPoint2D();
        public abstract Rectangle2D ReadRect2D();
        public abstract Rectangle3D ReadRect3D();
        public abstract Map ReadMap();

        public abstract Item ReadItem();
        public abstract Mobile ReadMobile();
        public abstract BaseGuild ReadGuild();

        public abstract T ReadItem<T>() where T : Item;
        public abstract T ReadMobile<T>() where T : Mobile;
        public abstract T ReadGuild<T>() where T : BaseGuild;

        public abstract ArrayList ReadItemList();
        public abstract ArrayList ReadMobileList();
        public abstract ArrayList ReadGuildList();

        public abstract List<Item> ReadStrongItemList();
        public abstract List<T> ReadStrongItemList<T>() where T : Item;

        public abstract List<Mobile> ReadStrongMobileList();
        public abstract List<T> ReadStrongMobileList<T>() where T : Mobile;

        public abstract List<BaseGuild> ReadStrongGuildList();
        public abstract List<T> ReadStrongGuildList<T>() where T : BaseGuild;

        public abstract HashSet<Item> ReadItemSet();
        public abstract HashSet<T> ReadItemSet<T>() where T : Item;

        public abstract HashSet<Mobile> ReadMobileSet();
        public abstract HashSet<T> ReadMobileSet<T>() where T : Mobile;

        public abstract HashSet<BaseGuild> ReadGuildSet();
        public abstract HashSet<T> ReadGuildSet<T>() where T : BaseGuild;

        public abstract Race ReadRace();

        public abstract bool End();
    }

    public abstract class GenericWriter
    {
        protected GenericWriter() { }

        public abstract void Close();

        public abstract long Position { get; }

        public abstract void Write(string value);
        public abstract void Write(DateTime value);
        public abstract void Write(DateTimeOffset value);
        public abstract void Write(TimeSpan value);
        public abstract void Write(decimal value);
        public abstract void Write(long value);
        public abstract void Write(ulong value);
        public abstract void Write(int value);
        public abstract void Write(uint value);
        public abstract void Write(short value);
        public abstract void Write(ushort value);
        public abstract void Write(double value);
        public abstract void Write(float value);
        public abstract void Write(char value);
        public abstract void Write(byte value);
        public abstract void Write(sbyte value);
        public abstract void Write(bool value);
        public abstract void WriteEncodedInt(int value);
        public abstract void Write(IPAddress value);

        public abstract void WriteDeltaTime(DateTime value);

        public abstract void Write(Point3D value);
        public abstract void Write(Point2D value);
        public abstract void Write(Rectangle2D value);
        public abstract void Write(Rectangle3D value);
        public abstract void Write(Map value);

        public abstract void Write(Item value);
        public abstract void Write(Mobile value);
        public abstract void Write(BaseGuild value);

        public abstract void WriteItem<T>(T value) where T : Item;
        public abstract void WriteMobile<T>(T value) where T : Mobile;
        public abstract void WriteGuild<T>(T value) where T : BaseGuild;

        public abstract void Write(Race value);

        public abstract void WriteItemList(ArrayList list, bool tidy = false);

        public abstract void WriteMobileList(ArrayList list, bool tidy = false);

        public abstract void WriteGuildList(ArrayList list, bool tidy = false);

        public abstract void Write(List<Item> list, bool tidy = false);

        public abstract void WriteItemList<T>(List<T> list, bool tidy = false) where T : Item;

        public abstract void Write(HashSet<Item> list, bool tidy = false);

        public abstract void WriteItemSet<T>(HashSet<T> set, bool tidy = false) where T : Item;

        public abstract void Write(List<Mobile> list, bool tidy = false);

        public abstract void WriteMobileList<T>(List<T> list, bool tidy = false) where T : Mobile;

        public abstract void Write(HashSet<Mobile> list, bool tidy = false);

        public abstract void WriteMobileSet<T>(HashSet<T> set, bool tidy = false) where T : Mobile;

        public abstract void Write(List<BaseGuild> list, bool tidy = false);

        public abstract void WriteGuildList<T>(List<T> list, bool tidy = false) where T : BaseGuild;

        public abstract void Write(HashSet<BaseGuild> list, bool tidy = false);

        public abstract void WriteGuildSet<T>(HashSet<T> set, bool tidy = false) where T : BaseGuild;

        // Compiler won't notice their 'where' to differentiate the generic methods.
    }

    public class BinaryFileWriter : GenericWriter
    {
        bool PrefixStrings;
        Stream _file;

        protected virtual int BufferSize => 64 * 1024;

        byte[] _buf;
        int _idx;
        Encoding _encoding;

        public BinaryFileWriter(Stream strm, bool prefixStr)
        {
            PrefixStrings = prefixStr;
            _encoding = Utility.UTF8;
            _buf = new byte[BufferSize];
            _file = strm;
        }
        public BinaryFileWriter(string filename, bool prefixStr)
        {
            PrefixStrings = prefixStr;
            _buf = new byte[BufferSize];
            _file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
            _encoding = Utility.UTF8WithEncoding;
        }

        public void Flush()
        {
            if (_idx > 0)
            {
                _position += _idx;
                _file.Write(_buf, 0, _idx);
                _idx = 0;
            }
        }

        long _position;
        public override long Position => _position + _idx;

        public Stream UnderlyingStream
        {
            get
            {
                if (_idx > 0)
                    Flush();
                return _file;
            }
        }

        public override void Close()
        {
            if (_idx > 0)
                Flush();
            _file.Close();
        }

        public override void WriteEncodedInt(int value)
        {
            var v = (uint)value;
            while (v >= 0x80)
            {
                if ((_idx + 1) > _buf.Length)
                    Flush();
                _buf[_idx++] = (byte)(v | 0x80);
                v >>= 7;
            }
            if ((_idx + 1) > _buf.Length)
                Flush();
            _buf[_idx++] = (byte)v;
        }

        byte[] _characterBuffer;
        int _maxBufferChars;
        const int LargeByteBufferSize = 256;

        internal void InternalWriteString(string value)
        {
            var length = _encoding.GetByteCount(value);
            WriteEncodedInt(length);
            if (_characterBuffer == null)
            {
                _characterBuffer = new byte[LargeByteBufferSize];
                _maxBufferChars = LargeByteBufferSize / _encoding.GetMaxByteCount(1);
            }
            if (length > LargeByteBufferSize)
            {
                var current = 0;
                var charsLeft = value.Length;
                while (charsLeft > 0)
                {
                    var charCount = (charsLeft > _maxBufferChars) ? _maxBufferChars : charsLeft;
                    var byteLength = _encoding.GetBytes(value, current, charCount, _characterBuffer, 0);
                    if ((_idx + byteLength) > _buf.Length)
                        Flush();
                    Buffer.BlockCopy(_characterBuffer, 0, _buf, _idx, byteLength);
                    _idx += byteLength;
                    current += charCount;
                    charsLeft -= charCount;
                }
            }
            else
            {
                var byteLength = _encoding.GetBytes(value, 0, value.Length, _characterBuffer, 0);
                if ((_idx + byteLength) > _buf.Length)
                    Flush();
                Buffer.BlockCopy(_characterBuffer, 0, _buf, _idx, byteLength);
                _idx += byteLength;
            }
        }

        public override void Write(string value)
        {
            if (PrefixStrings)
            {
                if (value == null)
                {
                    if ((_idx + 1) > _buf.Length)
                        Flush();
                    _buf[_idx++] = 0;
                }
                else
                {
                    if ((_idx + 1) > _buf.Length)
                        Flush();
                    _buf[_idx++] = 1;
                    InternalWriteString(value);
                }
            }
            else InternalWriteString(value);
        }
        public override void Write(DateTime value) => Write(value.Ticks);
        public override void Write(DateTimeOffset value)
        {
            Write(value.Ticks);
            Write(value.Offset.Ticks);
        }
        public override void WriteDeltaTime(DateTime value)
        {
            var ticks = value.Ticks;
            var now = DateTime.UtcNow.Ticks;
            TimeSpan d;
            try { d = new TimeSpan(ticks - now); }
            catch { d = ticks < now ? TimeSpan.MaxValue : TimeSpan.MaxValue; }
            Write(d);
        }
        public override void Write(IPAddress value) => Write(Utility.GetLongAddressValue(value));
        public override void Write(TimeSpan value) => Write(value.Ticks);
        public override void Write(decimal value)
        {
            var bits = decimal.GetBits(value);
            for (var i = 0; i < bits.Length; ++i)
                Write(bits[i]);
        }
        public override void Write(long value)
        {
            if ((_idx + 8) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _buf[_idx + 2] = (byte)(value >> 16);
            _buf[_idx + 3] = (byte)(value >> 24);
            _buf[_idx + 4] = (byte)(value >> 32);
            _buf[_idx + 5] = (byte)(value >> 40);
            _buf[_idx + 6] = (byte)(value >> 48);
            _buf[_idx + 7] = (byte)(value >> 56);
            _idx += 8;
        }
        public override void Write(ulong value)
        {
            if ((_idx + 8) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _buf[_idx + 2] = (byte)(value >> 16);
            _buf[_idx + 3] = (byte)(value >> 24);
            _buf[_idx + 4] = (byte)(value >> 32);
            _buf[_idx + 5] = (byte)(value >> 40);
            _buf[_idx + 6] = (byte)(value >> 48);
            _buf[_idx + 7] = (byte)(value >> 56);
            _idx += 8;
        }
        public override void Write(int value)
        {
            if ((_idx + 4) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _buf[_idx + 2] = (byte)(value >> 16);
            _buf[_idx + 3] = (byte)(value >> 24);
            _idx += 4;
        }
        public override void Write(uint value)
        {
            if ((_idx + 4) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _buf[_idx + 2] = (byte)(value >> 16);
            _buf[_idx + 3] = (byte)(value >> 24);
            _idx += 4;
        }
        public override void Write(short value)
        {
            if ((_idx + 2) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _idx += 2;
        }
        public override void Write(ushort value)
        {
            if ((_idx + 2) > _buf.Length)
                Flush();
            _buf[_idx] = (byte)value;
            _buf[_idx + 1] = (byte)(value >> 8);
            _idx += 2;
        }
        public unsafe override void Write(double value)
        {
            if ((_idx + 8) > _buf.Length)
                Flush();
#if MONO
			var bytes = BitConverter.GetBytes(value);
			for (var i = 0; i < bytes.Length; i++)
				_buf[_idx++] = bytes[i];
#else
            fixed (byte* pBuffer = _buf)
                *(double*)(pBuffer + _idx) = value;
            _idx += 8;
#endif
        }
        public unsafe override void Write(float value)
        {
            if ((_idx + 4) > _buf.Length)
                Flush();
#if MONO
			var bytes = BitConverter.GetBytes(value);
			for (var i = 0; i < bytes.Length; i++)
				_buf[_idx++] = bytes[i];
#else
            fixed (byte* pBuffer = _buf)
                *(float*)(pBuffer + _idx) = value;
            _idx += 4;
#endif
        }
        char[] _singleCharBuffer = new char[1];
        public override void Write(char value)
        {
            if ((_idx + 8) > _buf.Length)
                Flush();
            _singleCharBuffer[0] = value;
            var byteCount = _encoding.GetBytes(_singleCharBuffer, 0, 1, _buf, _idx);
            _idx += byteCount;
        }
        public override void Write(byte value)
        {
            if ((_idx + 1) > _buf.Length)
                Flush();
            _buf[_idx++] = value;
        }
        public override void Write(sbyte value)
        {
            if ((_idx + 1) > _buf.Length)
                Flush();
            _buf[_idx++] = (byte)value;
        }
        public override void Write(bool value)
        {
            if ((_idx + 1) > _buf.Length)
                Flush();
            _buf[_idx++] = (byte)(value ? 1 : 0);
        }

        public override void Write(Point3D value)
        {
            Write(value.X);
            Write(value.Y);
            Write(value.Z);
        }
        public override void Write(Point2D value)
        {
            Write(value.X);
            Write(value.Y);
        }
        public override void Write(Rectangle2D value)
        {
            Write(value.Start);
            Write(value.End);
        }
        public override void Write(Rectangle3D value)
        {
            Write(value.Start);
            Write(value.End);
        }

        public override void Write(Map value) => Write(value != null ? (byte)value.MapIndex : (byte)0xFF);
        public override void Write(Race value) => Write(value != null ? (byte)value.RaceIndex : (byte)0xFF);
        public override void Write(Item value) => Write(value == null || value.Deleted ? Serial.MinusOne : value.Serial);
        public override void Write(Mobile value) => Write(value == null || value.Deleted ? Serial.MinusOne : value.Serial);
        public override void Write(BaseGuild value) => Write(value == null ? 0 : value.Id);

        public override void WriteItem<T>(T value) => Write(value);
        public override void WriteMobile<T>(T value) => Write(value);
        public override void WriteGuild<T>(T value) => Write(value);

        public override void WriteMobileList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((Mobile)list[i]).Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((Mobile)list[i]);
        }
        public override void WriteItemList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((Item)list[i]).Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((Item)list[i]);
        }
        public override void WriteGuildList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((BaseGuild)list[i]).Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((BaseGuild)list[i]);
        }

        public override void Write(List<Item> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteItemList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<Item> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(item => item.Deleted);
            Write(set.Count);
            foreach (var item in set)
                Write(item);
        }
        public override void WriteItemSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(item => item.Deleted);
            Write(set.Count);
            foreach (var item in set)
                Write(item);
        }
        public override void Write(List<Mobile> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteMobileList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<Mobile> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(mobile => mobile.Deleted);
            Write(set.Count);
            foreach (var mob in set)
                Write(mob);
        }
        public override void WriteMobileSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(mob => mob.Deleted);
            Write(set.Count);
            foreach (var mob in set)
                Write(mob);
        }
        public override void Write(List<BaseGuild> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteGuildList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<BaseGuild> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(guild => guild.Disbanded);
            Write(set.Count);
            foreach (BaseGuild guild in set)
                Write(guild);
        }
        public override void WriteGuildSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(guild => guild.Disbanded);
            Write(set.Count);
            foreach (var guild in set)
                Write(guild);
        }
    }

    public sealed class BinaryFileReader : GenericReader
    {
        BinaryReader _file;

        public BinaryFileReader(BinaryReader br) { _file = br; }

        public void Close() => _file.Close();
        public long Position => _file.BaseStream.Position;
        public long Seek(long offset, SeekOrigin origin) => _file.BaseStream.Seek(offset, origin);
        public override string ReadString() => ReadByte() != 0 ? _file.ReadString() : null;

        public override DateTime ReadDeltaTime()
        {
            var ticks = _file.ReadInt64();
            var now = DateTime.UtcNow.Ticks;
            if (ticks > 0 && (ticks + now) < 0) return DateTime.MaxValue;
            else if (ticks < 0 && (ticks + now) < 0) return DateTime.MinValue;
            try { return new DateTime(now + ticks); }
            catch { return ticks > 0 ? DateTime.MaxValue : DateTime.MinValue; }
        }
        public override IPAddress ReadIPAddress() => new IPAddress(_file.ReadInt64());
        public override int ReadEncodedInt()
        {
            int v = 0, shift = 0;
            byte b;
            do
            {
                b = _file.ReadByte();
                v |= (b & 0x7F) << shift;
                shift += 7;
            } while (b >= 0x80);
            return v;
        }
        public override DateTime ReadDateTime() => new DateTime(_file.ReadInt64());
        public override DateTimeOffset ReadDateTimeOffset()
        {
            var ticks = _file.ReadInt64();
            var offset = new TimeSpan(_file.ReadInt64());
            return new DateTimeOffset(ticks, offset);
        }
        public override TimeSpan ReadTimeSpan() => new TimeSpan(_file.ReadInt64());
        public override decimal ReadDecimal() => _file.ReadDecimal();
        public override long ReadLong() => _file.ReadInt64();
        public override ulong ReadULong() => _file.ReadUInt64();
        public override int ReadInt() => _file.ReadInt32();
        public override uint ReadUInt() => _file.ReadUInt32();
        public override short ReadShort() => _file.ReadInt16();
        public override ushort ReadUShort() => _file.ReadUInt16();
        public override double ReadDouble() => _file.ReadDouble();
        public override float ReadFloat() => _file.ReadSingle();
        public override char ReadChar() => _file.ReadChar();
        public override byte ReadByte() => _file.ReadByte();
        public override sbyte ReadSByte() => _file.ReadSByte();
        public override bool ReadBool() => _file.ReadBoolean();

        public override Point3D ReadPoint3D() => new Point3D(ReadInt(), ReadInt(), ReadInt());
        public override Point2D ReadPoint2D() => new Point2D(ReadInt(), ReadInt());
        public override Rectangle2D ReadRect2D() => new Rectangle2D(ReadPoint2D(), ReadPoint2D());
        public override Rectangle3D ReadRect3D() => new Rectangle3D(ReadPoint3D(), ReadPoint3D());

        public override Map ReadMap() => Map.Maps[ReadByte()];
        public override Item ReadItem() => World.FindItem(ReadInt());
        public override Mobile ReadMobile() => World.FindMobile(ReadInt());
        public override BaseGuild ReadGuild() => BaseGuild.Find(ReadInt());
        public override T ReadItem<T>() => ReadItem() as T;
        public override T ReadMobile<T>() => ReadMobile() as T;
        public override T ReadGuild<T>() => ReadGuild() as T;

        public override ArrayList ReadItemList()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new ArrayList(count);
                for (var i = 0; i < count; ++i)
                {
                    var item = ReadItem();
                    if (item != null)
                        list.Add(item);
                }
                return list;
            }
            else return new ArrayList();
        }
        public override ArrayList ReadMobileList()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new ArrayList(count);
                for (var i = 0; i < count; ++i)
                {
                    var m = ReadMobile();
                    if (m != null)
                        list.Add(m);
                }
                return list;
            }
            else return new ArrayList();
        }
        public override ArrayList ReadGuildList()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new ArrayList(count);
                for (var i = 0; i < count; ++i)
                {
                    var g = ReadGuild();
                    if (g != null)
                        list.Add(g);
                }
                return list;
            }
            else return new ArrayList();
        }
        public override List<Item> ReadStrongItemList() => ReadStrongItemList<Item>();
        public override List<T> ReadStrongItemList<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new List<T>(count);
                for (var i = 0; i < count; ++i)
                    if (ReadItem() is T item)
                        list.Add(item);
                return list;
            }
            else return new List<T>();
        }
        public override HashSet<Item> ReadItemSet() => ReadItemSet<Item>();
        public override HashSet<T> ReadItemSet<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var set = new HashSet<T>();
                for (var i = 0; i < count; ++i)
                    if (ReadItem() is T item)
                        set.Add(item);
                return set;
            }
            else return new HashSet<T>();
        }
        public override List<Mobile> ReadStrongMobileList() => ReadStrongMobileList<Mobile>();
        public override List<T> ReadStrongMobileList<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new List<T>(count);
                for (var i = 0; i < count; ++i)
                    if (ReadMobile() is T m)
                        list.Add(m);
                return list;
            }
            else return new List<T>();
        }

        public override HashSet<Mobile> ReadMobileSet() => ReadMobileSet<Mobile>();
        public override HashSet<T> ReadMobileSet<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var set = new HashSet<T>();
                for (var i = 0; i < count; ++i)
                    if (ReadMobile() is T item)
                        set.Add(item);
                return set;
            }
            else return new HashSet<T>();
        }

        public override List<BaseGuild> ReadStrongGuildList() => ReadStrongGuildList<BaseGuild>();
        public override List<T> ReadStrongGuildList<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var list = new List<T>(count);
                for (var i = 0; i < count; ++i)
                    if (ReadGuild() is T g)
                        list.Add(g);
                return list;
            }
            else return new List<T>();
        }
        public override HashSet<BaseGuild> ReadGuildSet() => ReadGuildSet<BaseGuild>();
        public override HashSet<T> ReadGuildSet<T>()
        {
            var count = ReadInt();
            if (count > 0)
            {
                var set = new HashSet<T>();
                for (var i = 0; i < count; ++i)
                    if (ReadGuild() is T item)
                        set.Add(item);
                return set;
            }
            return new HashSet<T>();
        }

        public override Race ReadRace() => Race.Races[ReadByte()];

        public override bool End() => _file.PeekChar() == -1;
    }

    public sealed class AsyncWriter : GenericWriter
    {
        public static int ThreadCount { get; private set; } = 0;

        int BufferSize;

        private long _lastPos, _curPos;
        private bool _closed;
        private bool PrefixStrings;

        MemoryStream _ms;
        BinaryWriter _bw;
        FileStream _file;

        Queue<MemoryStream> _writeQueue;
        Thread _workerThread;

        public AsyncWriter(string filename, bool prefix)
            : this(filename, 1048576, prefix) { } //1 mb buffer
        public AsyncWriter(string filename, int buffSize, bool prefix)
        {
            PrefixStrings = prefix;
            _closed = false;
            _writeQueue = new Queue<MemoryStream>();
            BufferSize = buffSize;
            _file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
            _ms = new MemoryStream(BufferSize + 1024);
            _bw = new BinaryWriter(_ms, Utility.UTF8WithEncoding);
        }

        void Enqueue(MemoryStream mem)
        {
            lock (_writeQueue)
                _writeQueue.Enqueue(mem);
            if (_workerThread == null || !_workerThread.IsAlive)
            {
                _workerThread = new Thread(new ThreadStart(new WorkerThread(this).Worker))
                {
                    Priority = ThreadPriority.BelowNormal
                };
                _workerThread.Start();
            }
        }

        class WorkerThread
        {
            AsyncWriter _owner;

            public WorkerThread(AsyncWriter owner)
            {
                _owner = owner;
            }

            public void Worker()
            {
                ThreadCount++;
                var lastCount = 0;
                do
                {
                    MemoryStream mem = null;
                    lock (_owner._writeQueue)
                        if ((lastCount = _owner._writeQueue.Count) > 0)
                            mem = _owner._writeQueue.Dequeue();
                    if (mem != null && mem.Length > 0)
                        mem.WriteTo(_owner._file);
                } while (lastCount > 1);
                if (_owner._closed)
                    _owner._file.Close();
                ThreadCount--;
                if (ThreadCount <= 0)
                    World.NotifyDiskWriteComplete();
            }
        }

        void OnWrite()
        {
            var curlen = _ms.Length;
            _curPos += curlen - _lastPos;
            _lastPos = curlen;
            if (curlen >= BufferSize)
            {
                Enqueue(_ms);
                _ms = new MemoryStream(BufferSize + 1024);
                _bw = new BinaryWriter(_ms, Utility.UTF8WithEncoding);
                _lastPos = 0;
            }
        }

        public MemoryStream MemStream
        {
            get => _ms;
            set
            {
                if (_ms.Length > 0)
                    Enqueue(_ms);
                _ms = value;
                _bw = new BinaryWriter(_ms, Utility.UTF8WithEncoding);
                _lastPos = 0;
                _curPos = _ms.Length;
                _ms.Seek(0, SeekOrigin.End);
            }
        }

        public override void Close()
        {
            Enqueue(_ms);
            _closed = true;
        }

        public override long Position => _curPos;

        public override void Write(IPAddress value)
        {
            _bw.Write(Utility.GetLongAddressValue(value));
            OnWrite();
        }

        public override void Write(string value)
        {
            if (PrefixStrings)
            {
                if (value == null)
                    _bw.Write((byte)0);
                else
                {
                    _bw.Write((byte)1);
                    _bw.Write(value);
                }
            }
            else
                _bw.Write(value);
            OnWrite();
        }
        public override void WriteDeltaTime(DateTime value)
        {
            var ticks = value.Ticks;
            var now = DateTime.UtcNow.Ticks;
            TimeSpan d;
            try { d = new TimeSpan(ticks - now); }
            catch { if (ticks < now) d = TimeSpan.MaxValue; else d = TimeSpan.MaxValue; }
            Write(d);
        }
        public override void Write(DateTime value)
        {
            _bw.Write(value.Ticks);
            OnWrite();
        }
        public override void Write(DateTimeOffset value)
        {
            _bw.Write(value.Ticks);
            _bw.Write(value.Offset.Ticks);
            OnWrite();
        }
        public override void Write(TimeSpan value)
        {
            _bw.Write(value.Ticks);
            OnWrite();
        }
        public override void Write(decimal value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(long value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(ulong value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void WriteEncodedInt(int value)
        {
            var v = (uint)value;
            while (v >= 0x80)
            {
                _bw.Write((byte)(v | 0x80));
                v >>= 7;
            }
            _bw.Write((byte)v);
            OnWrite();
        }
        public override void Write(int value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(uint value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(short value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(ushort value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(double value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(float value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(char value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(byte value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(sbyte value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(bool value)
        {
            _bw.Write(value);
            OnWrite();
        }
        public override void Write(Point3D value)
        {
            Write(value.X);
            Write(value.Y);
            Write(value.Z);
        }
        public override void Write(Point2D value)
        {
            Write(value.X);
            Write(value.Y);
        }
        public override void Write(Rectangle2D value)
        {
            Write(value.Start);
            Write(value.End);
        }
        public override void Write(Rectangle3D value)
        {
            Write(value.Start);
            Write(value.End);
        }
        public override void Write(Map value) => Write(value != null ? (byte)value.MapIndex : (byte)0xFF);
        public override void Write(Race value) => Write(value != null ? (byte)value.RaceIndex : (byte)0xFF);
        public override void Write(Item value) => Write(value == null || value.Deleted ? Serial.MinusOne : value.Serial);
        public override void Write(Mobile value) => Write(value == null || value.Deleted ? Serial.MinusOne : value.Serial);
        public override void Write(BaseGuild value) => Write(value == null ? 0 : value.Id);
        public override void WriteItem<T>(T value) => Write(value);
        public override void WriteMobile<T>(T value) => Write(value);
        public override void WriteGuild<T>(T value) => Write(value);

        public override void WriteMobileList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((Mobile)list[i]).Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((Mobile)list[i]);
        }
        public override void WriteItemList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((Item)list[i]).Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((Item)list[i]);
        }
        public override void WriteGuildList(ArrayList list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (((BaseGuild)list[i]).Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write((BaseGuild)list[i]);
        }
        public override void Write(List<Item> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteItemList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<Item> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(item => item.Deleted);
            Write(set.Count);
            foreach (var item in set)
                Write(item);
        }
        public override void WriteItemSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(item => item.Deleted);
            Write(set.Count);
            foreach (var item in set)
                Write(item);
        }
        public override void Write(List<Mobile> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteMobileList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Deleted) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<Mobile> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(mobile => mobile.Deleted);
            Write(set.Count);
            foreach (var mob in set)
                Write(mob);
        }
        public override void WriteMobileSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(mob => mob.Deleted);
            Write(set.Count);
            foreach (var mob in set)
                Write(mob);
        }
        public override void Write(List<BaseGuild> list, bool tidy = false)
        {
            if (tidy)
                for (int i = 0; i < list.Count;)
                    if (list[i].Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void WriteGuildList<T>(List<T> list, bool tidy = false)
        {
            if (tidy)
                for (var i = 0; i < list.Count;)
                    if (list[i].Disbanded) list.RemoveAt(i);
                    else ++i;
            Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
                Write(list[i]);
        }
        public override void Write(HashSet<BaseGuild> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(guild => guild.Disbanded);
            Write(set.Count);
            foreach (var guild in set)
                Write(guild);
        }
        public override void WriteGuildSet<T>(HashSet<T> set, bool tidy = false)
        {
            if (tidy)
                set.RemoveWhere(guild => guild.Disbanded);
            Write(set.Count);
            foreach (var guild in set)
                Write(guild);
        }
    }

    public interface ISerializable
    {
        int TypeReference { get; }
        int SerialIdentity { get; }
        void Serialize(GenericWriter writer);
    }
}