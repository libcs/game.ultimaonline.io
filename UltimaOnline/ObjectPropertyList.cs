using System.Text;
using UltimaOnline.Network;

namespace UltimaOnline
{
    public sealed class ObjectPropertyList : Packet
    {
        int _Hash;
        int _Strings;

        public IEntity Entity { get; }
        public int Hash { get { return 0x40000000 + _Hash; } }

        public int Header { get; set; }
        public string HeaderArgs { get; set; }

        public static bool Enabled { get; set; } = false;

        public ObjectPropertyList(IEntity e) : base(0xD6)
        {
            EnsureCapacity(128);
            Entity = e;
            Stream.Write((short)1);
            Stream.Write(e.Serial);
            Stream.Write((byte)0);
            Stream.Write((byte)0);
            Stream.Write(e.Serial);
        }

        public void Add(int number)
        {
            if (number == 0)
                return;
            AddHash(number);
            if (Header == 0)
            {
                Header = number;
                HeaderArgs = string.Empty;
            }
            Stream.Write(number);
            Stream.Write((short)0);
        }

        public void Terminate()
        {
            Stream.Write(0);
            Stream.Seek(11, System.IO.SeekOrigin.Begin);
            Stream.Write(_Hash);
        }

        static byte[] _Buffer = new byte[1024];
        static Encoding _Encoding = Encoding.Unicode;

        public void AddHash(int val)
        {
            _Hash ^= val & 0x3FFFFFF;
            _Hash ^= (val >> 26) & 0x3F;
        }

        public void Add(int number, string arguments)
        {
            if (number == 0)
                return;
            if (arguments == null)
                arguments = string.Empty;
            if (Header == 0)
            {
                Header = number;
                HeaderArgs = arguments;
            }
            AddHash(number);
            AddHash(arguments.GetHashCode());
            Stream.Write(number);
            var byteCount = _Encoding.GetByteCount(arguments);
            if (byteCount > _Buffer.Length)
                _Buffer = new byte[byteCount];
            byteCount = _Encoding.GetBytes(arguments, 0, arguments.Length, _Buffer, 0);
            Stream.Write((short)byteCount);
            Stream.Write(_Buffer, 0, byteCount);
        }

        public void Add(int number, string format, object arg0) => Add(number, string.Format(format, arg0));
        public void Add(int number, string format, object arg0, object arg1) => Add(number, string.Format(format, arg0, arg1));
        public void Add(int number, string format, object arg0, object arg1, object arg2) => Add(number, string.Format(format, arg0, arg1, arg2));
        public void Add(int number, string format, params object[] args) => Add(number, string.Format(format, args));

        // Each of these are localized to "~1_NOTHING~" which allows the string argument to be used
        static int[] _StringNumbers = new int[]
        {
            1042971,
            1070722
        };

        int GetStringNumber() => _StringNumbers[_Strings++ % _StringNumbers.Length];

        public void Add(string text) => Add(GetStringNumber(), text);
        public void Add(string format, string arg0) => Add(GetStringNumber(), string.Format(format, arg0));
        public void Add(string format, string arg0, string arg1) => Add(GetStringNumber(), string.Format(format, arg0, arg1));
        public void Add(string format, string arg0, string arg1, string arg2) => Add(GetStringNumber(), string.Format(format, arg0, arg1, arg2));
        public void Add(string format, params object[] args) => Add(GetStringNumber(), string.Format(format, args));
    }

    public sealed class OPLInfo : Packet
    {
        /*public OPLInfo(ObjectPropertyList list) : base(0xBF)
		{
			EnsureCapacity(13);
			_Stream.Write((short) 0x10);
			_Stream.Write(list.Entity.Serial);
			_Stream.Write(list.Hash);
		}*/
        public OPLInfo(ObjectPropertyList list) : base(0xDC, 9)
        {
            Stream.Write(list.Entity.Serial);
            Stream.Write(list.Hash);
        }
    }
}