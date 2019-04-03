using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace UltimaOnline
{
    public static class Utility
    {
        static Encoding _UTF8, _UTF8WithEncoding;

        public static Encoding UTF8
        {
            get
            {
                if (_UTF8 == null)
                    _UTF8 = new UTF8Encoding(false, false);
                return _UTF8;
            }
        }

        public static Encoding UTF8WithEncoding
        {
            get
            {
                if (_UTF8WithEncoding == null)
                    _UTF8WithEncoding = new UTF8Encoding(true, false);
                return _UTF8WithEncoding;
            }
        }

        public static void Separate(StringBuilder b, string value, string separator)
        {
            if (b.Length > 0)
                b.Append(separator);
            b.Append(value);
        }

        public static string Intern(string str)
        {
            if (str == null)
                return null;
            else if (str.Length == 0)
                return string.Empty;
            return string.Intern(str);
        }

        public static void Intern(ref string str) => str = Intern(str);

        static Dictionary<IPAddress, IPAddress> _ipAddressTable;

        public static IPAddress Intern(IPAddress ipAddress)
        {
            if (_ipAddressTable == null)
                _ipAddressTable = new Dictionary<IPAddress, IPAddress>();
            if (!_ipAddressTable.TryGetValue(ipAddress, out var interned))
            {
                interned = ipAddress;
                _ipAddressTable[ipAddress] = interned;
            }
            return interned;
        }
        public static void Intern(ref IPAddress ipAddress) => ipAddress = Intern(ipAddress);

        public static bool IsValidIP(string text)
        {
            var valid = true;
            IPMatch(text, IPAddress.None, ref valid);
            return valid;
        }

        public static bool IPMatch(string val, IPAddress ip)
        {
            var valid = true;
            return IPMatch(val, ip, ref valid);
        }

        public static string FixHtml(string str)
        {
            if (str == null)
                return string.Empty;
            var hasOpen = str.IndexOf('<') >= 0;
            var hasClose = str.IndexOf('>') >= 0;
            var hasPound = str.IndexOf('#') >= 0;
            if (!hasOpen && !hasClose && !hasPound)
                return str;
            var b = new StringBuilder(str);
            if (hasOpen)
                b.Replace('<', '(');
            if (hasClose)
                b.Replace('>', ')');
            if (hasPound)
                b.Replace('#', '-');
            return b.ToString();
        }

        public static bool IPMatchCIDR(string cidr, IPAddress ip)
        {
            if (ip == null || ip.AddressFamily == AddressFamily.InterNetworkV6)
                return false; // Just worry about IPv4 for now
            /*
			Var str = cidr.Split( '/' );
			if (str.Length != 2)
				return false;
			/* **************************************************
			IPAddress cidrPrefix;
			if (!IPAddress.TryParse(str[0], out cidrPrefix))
				return false;
			*/

            /*
			var dotSplit = str[0].Split( '.' );
			if (dotSplit.Length != 4)		//At this point and time, and for speed sake, we'll only worry about IPv4
				return false;
			var bytes = new byte[4];
			for (var i = 0; i < 4; i++)
				byte.TryParse(dotSplit[i], out bytes[i]);
			var cidrPrefix = OrderedAddressValue(bytes);
			var cidrLength = Utility.ToInt32(str[1]);
			//The below solution is the fastest solution of the three
			*/
            var bytes = new byte[4];
            var split = cidr.Split('.');
            var cidrBits = false;
            var cidrLength = 0;
            for (var i = 0; i < 4; i++)
            {
                var part = 0;
                var partBase = 10;
                var pattern = split[i];
                for (var j = 0; j < pattern.Length; j++)
                {
                    var c = pattern[j];
                    if (c == 'x' || c == 'X')
                        partBase = 16;
                    else if (c >= '0' && c <= '9')
                    {
                        var offset = c - '0';
                        if (cidrBits)
                        {
                            cidrLength *= partBase;
                            cidrLength += offset;
                        }
                        else
                        {
                            part *= partBase;
                            part += offset;
                        }
                    }
                    else if (c >= 'a' && c <= 'f')
                    {
                        var offset = 10 + (c - 'a');
                        if (cidrBits)
                        {
                            cidrLength *= partBase;
                            cidrLength += offset;
                        }
                        else
                        {
                            part *= partBase;
                            part += offset;
                        }
                    }
                    else if (c >= 'A' && c <= 'F')
                    {
                        var offset = 10 + (c - 'A');
                        if (cidrBits)
                        {
                            cidrLength *= partBase;
                            cidrLength += offset;
                        }
                        else
                        {
                            part *= partBase;
                            part += offset;
                        }
                    }
                    else if (c == '/')
                    {
                        if (cidrBits || i != 3) //If there's two '/' or the '/' isn't in the last byte
                            return false;
                        partBase = 10;
                        cidrBits = true;
                    }
                    else return false;
                }
                bytes[i] = (byte)part;
            }
            var cidrPrefix = OrderedAddressValue(bytes);
            return IPMatchCIDR(cidrPrefix, ip, cidrLength);
        }

        public static bool IPMatchCIDR(IPAddress cidrPrefix, IPAddress ip, int cidrLength)
        {
            if (cidrPrefix == null || ip == null || cidrPrefix.AddressFamily == AddressFamily.InterNetworkV6) // Ignore IPv6 for now
                return false;
            var cidrValue = SwapUnsignedInt((uint)GetLongAddressValue(cidrPrefix));
            var ipValue = SwapUnsignedInt((uint)GetLongAddressValue(ip));
            return IPMatchCIDR(cidrValue, ipValue, cidrLength);
        }

        public static bool IPMatchCIDR(uint cidrPrefixValue, IPAddress ip, int cidrLength)
        {
            if (ip == null || ip.AddressFamily == AddressFamily.InterNetworkV6)
                return false;
            var ipValue = SwapUnsignedInt((uint)GetLongAddressValue(ip));
            return IPMatchCIDR(cidrPrefixValue, ipValue, cidrLength);
        }

        public static bool IPMatchCIDR(uint cidrPrefixValue, uint ipValue, int cidrLength)
        {
            if (cidrLength <= 0 || cidrLength >= 32) // if invalid cidr Length, just compare IPs
                return cidrPrefixValue == ipValue;
            var mask = uint.MaxValue << 32 - cidrLength;
            return (cidrPrefixValue & mask) == (ipValue & mask);
        }

        static uint OrderedAddressValue(byte[] bytes)
        {
            if (bytes.Length != 4)
                return 0;
            return (uint)((bytes[0] << 0x18) | (bytes[1] << 0x10) | (bytes[2] << 8) | bytes[3]) & 0xffffffff;
        }

        static uint SwapUnsignedInt(uint source) => ((source & 0x000000FF) << 0x18) | ((source & 0x0000FF00) << 8) | ((source & 0x00FF0000) >> 8) | ((source & 0xFF000000) >> 0x18);

        public static bool TryConvertIPv6toIPv4(ref IPAddress address)
        {
            if (!Socket.OSSupportsIPv6 || address.AddressFamily == AddressFamily.InterNetwork)
                return true;
            var addr = address.GetAddressBytes();
            if (addr.Length == 16) // sanity 0 - 15 //10 11 //12 13 14 15
            {
                if (addr[10] != 0xFF || addr[11] != 0xFF)
                    return false;
                for (var i = 0; i < 10; i++)
                    if (addr[i] != 0)
                        return false;
                var v4Addr = new byte[4];
                for (var i = 0; i < 4; i++)
                    v4Addr[i] = addr[12 + i];
                address = new IPAddress(v4Addr);
                return true;
            }
            return false;
        }

        public static bool IPMatch(string val, IPAddress ip, ref bool valid)
        {
            valid = true;
            var split = val.Split('.');
            for (var i = 0; i < 4; ++i)
            {
                int lowPart, highPart;
                if (i >= split.Length)
                {
                    lowPart = 0;
                    highPart = 255;
                }
                else
                {
                    var pattern = split[i];
                    if (pattern == "*")
                    {
                        lowPart = 0;
                        highPart = 255;
                    }
                    else
                    {
                        lowPart = 0;
                        highPart = 0;
                        var highOnly = false;
                        var lowBase = 10;
                        var highBase = 10;
                        for (var j = 0; j < pattern.Length; ++j)
                        {
                            var c = pattern[j];
                            if (c == '?')
                            {
                                if (!highOnly)
                                {
                                    lowPart *= lowBase;
                                    lowPart += 0;
                                }
                                highPart *= highBase;
                                highPart += highBase - 1;
                            }
                            else if (c == '-')
                            {
                                highOnly = true;
                                highPart = 0;
                            }
                            else if (c == 'x' || c == 'X')
                            {
                                lowBase = 16;
                                highBase = 16;
                            }
                            else if (c >= '0' && c <= '9')
                            {
                                var offset = c - '0';
                                if (!highOnly)
                                {
                                    lowPart *= lowBase;
                                    lowPart += offset;
                                }
                                highPart *= highBase;
                                highPart += offset;
                            }
                            else if (c >= 'a' && c <= 'f')
                            {
                                var offset = 10 + (c - 'a');
                                if (!highOnly)
                                {
                                    lowPart *= lowBase;
                                    lowPart += offset;
                                }
                                highPart *= highBase;
                                highPart += offset;
                            }
                            else if (c >= 'A' && c <= 'F')
                            {
                                var offset = 10 + (c - 'A');
                                if (!highOnly)
                                {
                                    lowPart *= lowBase;
                                    lowPart += offset;
                                }
                                highPart *= highBase;
                                highPart += offset;
                            }
                            else valid = false;  //high & lowpart would be 0 if it got to here.
                        }
                    }
                }
                var b = (byte)(GetAddressValue(ip) >> (i * 8));
                if (b < lowPart || b > highPart)
                    return false;
            }
            return true;
        }

        public static bool IPMatchClassC(IPAddress ip1, IPAddress ip2) => (GetAddressValue(ip1) & 0xFFFFFF) == (GetAddressValue(ip2) & 0xFFFFFF);

        public static int InsensitiveCompare(string first, string second) => Insensitive.Compare(first, second);

        public static bool InsensitiveStartsWith(string first, string second) => Insensitive.StartsWith(first, second);

        #region To[Something]
        public static bool ToBoolean(string value)
        {
            bool.TryParse(value, out bool b);
            return b;
        }

        public static double ToDouble(string value)
        {
            double.TryParse(value, out double d);
            return d;
        }

        public static TimeSpan ToTimeSpan(string value)
        {
            TimeSpan.TryParse(value, out TimeSpan t);
            return t;
        }

        public static int ToInt32(string value)
        {
            int i;
            if (value.StartsWith("0x")) int.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out i);
            else int.TryParse(value, out i);
            return i;
        }
        #endregion

        #region Get[Something]
        public static double GetXMLDouble(string doubleString, double defaultValue)
        {
            try { return XmlConvert.ToDouble(doubleString); }
            catch { return double.TryParse(doubleString, out double val) ? val : defaultValue; }
        }

        public static int GetXMLInt32(string intString, int defaultValue)
        {
            try { return XmlConvert.ToInt32(intString); }
            catch { return int.TryParse(intString, out int val) ? val : defaultValue; }
        }

        public static DateTime GetXMLDateTime(string dateTimeString, DateTime defaultValue)
        {
            try { return XmlConvert.ToDateTime(dateTimeString, XmlDateTimeSerializationMode.Utc); }
            catch { return DateTime.TryParse(dateTimeString, out DateTime d) ? d : defaultValue; }
        }

        public static DateTimeOffset GetXMLDateTimeOffset(string dateTimeOffsetString, DateTimeOffset defaultValue)
        {
            try { return XmlConvert.ToDateTimeOffset(dateTimeOffsetString); }
            catch { return DateTimeOffset.TryParse(dateTimeOffsetString, out DateTimeOffset d) ? d : defaultValue; }
        }

        public static TimeSpan GetXMLTimeSpan(string timeSpanString, TimeSpan defaultValue)
        {
            try { return XmlConvert.ToTimeSpan(timeSpanString); }
            catch { return defaultValue; }
        }

        public static string GetAttribute(XmlElement node, string attributeName, string defaultValue = null)
        {
            if (node == null)
                return defaultValue;
            var attr = node.Attributes[attributeName];
            return attr == null ? defaultValue : attr.Value;
        }

        public static string GetText(XmlElement node, string defaultValue) => node == null ? defaultValue : node.InnerText;

        public static int GetAddressValue(IPAddress address) =>
#pragma warning disable 618
            (int)address.Address;
#pragma warning restore 618

        public static long GetLongAddressValue(IPAddress address) =>
#pragma warning disable 618
            address.Address;
#pragma warning restore 618

        #endregion

        #region In[...]Range
        public static bool InRange(Point3D p1, Point3D p2, int range)
            => p1.X >= (p2.X - range)
            && p1.X <= (p2.X + range)
            && p1.Y >= (p2.Y - range)
            && p1.Y <= (p2.Y + range);

        public static bool InUpdateRange(Point3D p1, Point3D p2)
            => p1.X >= (p2.X - 18)
            && p1.X <= (p2.X + 18)
            && p1.Y >= (p2.Y - 18)
            && p1.Y <= (p2.Y + 18);

        public static bool InUpdateRange(Point2D p1, Point2D p2)
            => p1.X >= (p2.X - 18)
            && p1.X <= (p2.X + 18)
            && p1.Y >= (p2.Y - 18)
            && p1.Y <= (p2.Y + 18);

        public static bool InUpdateRange(IPoint2D p1, IPoint2D p2)
            => p1.X >= (p2.X - 18)
            && p1.X <= (p2.X + 18)
            && p1.Y >= (p2.Y - 18)
            && p1.Y <= (p2.Y + 18);
        #endregion

        public static Direction GetDirection(IPoint2D from, IPoint2D to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var adx = Math.Abs(dx);
            var ady = Math.Abs(dy);
            if (adx >= ady * 3) return dx > 0 ? Direction.East : Direction.West;
            else if (ady >= adx * 3) return dy > 0 ? Direction.South : Direction.North;
            else if (dx > 0) return dy > 0 ? Direction.Down : Direction.Right;
            else return dy > 0 ? Direction.Left : Direction.Up;
        }

        /* Should probably be rewritten to use an ITile interface
		public static bool CanMobileFit( int z, StaticTile[] tiles )
		{
			int checkHeight = 15;
			int checkZ = z;
			for ( int i = 0; i < tiles.Length; ++i )
			{
				StaticTile tile = tiles[i];
				if ( ((checkZ + checkHeight) > tile.Z && checkZ < (tile.Z + tile.Height))*//* || (tile.Z < (checkZ + checkHeight) && (tile.Z + tile.Height) > checkZ)*//* )
					return false;
				else if ( checkHeight == 0 && tile.Height == 0 && checkZ == tile.Z )
					return false;
			}
			return true;
		}

		public static bool IsInContact( StaticTile check, StaticTile[] tiles )
		{
			int checkHeight = check.Height;
			int checkZ = check.Z;
			for ( int i = 0; i < tiles.Length; ++i )
			{
				StaticTile tile = tiles[i];
				if ( ((checkZ + checkHeight) > tile.Z && checkZ < (tile.Z + tile.Height))*//* || (tile.Z < (checkZ + checkHeight) && (tile.Z + tile.Height) > checkZ)*//* )
					return true;
				else if ( checkHeight == 0 && tile.Height == 0 && checkZ == tile.Z )
					return true;
			}
			return false;
		}
		*/

        public static object GetArrayCap(Array array, int index, object emptyValue = null)
        {
            if (array.Length > 0)
            {
                if (index < 0)
                    index = 0;
                else if (index >= array.Length)
                    index = array.Length - 1;
                return array.GetValue(index);
            }
            else return emptyValue;
        }

        #region Random
        //4d6+8 would be: Utility.Dice( 4, 6, 8 )
        public static int Dice(int numDice, int numSides, int bonus)
        {
            int total = 0;

            for (int i = 0; i < numDice; ++i)
                total += RandomImpl.Next(numSides) + 1;

            total += bonus;
            return total;
        }

        public static int RandomList(params int[] list)
        {
            return list[RandomImpl.Next(list.Length)];
        }

        public static bool RandomBool()
        {
            return RandomImpl.NextBool();
        }

        public static int RandomMinMax(int min, int max)
        {
            if (min > max)
            {
                int copy = min;
                min = max;
                max = copy;
            }
            else if (min == max)
            {
                return min;
            }

            return min + RandomImpl.Next((max - min) + 1);
        }

        public static int Random(int from, int count)
        {
            if (count == 0)
            {
                return from;
            }
            else if (count > 0)
            {
                return from + RandomImpl.Next(count);
            }
            else
            {
                return from - RandomImpl.Next(-count);
            }
        }

        public static int Random(int count)
        {
            return RandomImpl.Next(count);
        }

        public static void RandomBytes(byte[] buffer)
        {
            RandomImpl.NextBytes(buffer);
        }

        public static double RandomDouble()
        {
            return RandomImpl.NextDouble();
        }
        #endregion

        #region Random Hues

        /// <summary>
        /// Random pink, blue, green, orange, red or yellow hue
        /// </summary>
        public static int RandomNondyedHue()
        {
            switch (Random(6))
            {
                case 0: return RandomPinkHue();
                case 1: return RandomBlueHue();
                case 2: return RandomGreenHue();
                case 3: return RandomOrangeHue();
                case 4: return RandomRedHue();
                case 5: return RandomYellowHue();
            }
            return 0;
        }

        /// <summary>
        /// Random hue in the range 1201-1254
        /// </summary>
        public static int RandomPinkHue() => Random(1201, 54);

        /// <summary>
        /// Random hue in the range 1301-1354
        /// </summary>
        public static int RandomBlueHue() => Random(1301, 54);

        /// <summary>
        /// Random hue in the range 1401-1454
        /// </summary>
        public static int RandomGreenHue() => Random(1401, 54);

        /// <summary>
        /// Random hue in the range 1501-1554
        /// </summary>
        public static int RandomOrangeHue() => Random(1501, 54);

        /// <summary>
        /// Random hue in the range 1601-1654
        /// </summary>
        public static int RandomRedHue() => Random(1601, 54);

        /// <summary>
        /// Random hue in the range 1701-1754
        /// </summary>
        public static int RandomYellowHue() => Random(1701, 54);

        /// <summary>
        /// Random hue in the range 1801-1908
        /// </summary>
        public static int RandomNeutralHue() => Random(1801, 108);

        /// <summary>
        /// Random hue in the range 2001-2018
        /// </summary>
        public static int RandomSnakeHue() => Random(2001, 18);

        /// <summary>
        /// Random hue in the range 2101-2130
        /// </summary>
        public static int RandomBirdHue() => Random(2101, 30);

        /// <summary>
        /// Random hue in the range 2201-2224
        /// </summary>
        public static int RandomSlimeHue() => Random(2201, 24);

        /// <summary>
        /// Random hue in the range 2301-2318
        /// </summary>
        public static int RandomAnimalHue() => Random(2301, 18);

        /// <summary>
        /// Random hue in the range 2401-2430
        /// </summary>
        public static int RandomMetalHue() => Random(2401, 30);

        public static int ClipDyedHue(int hue)
        {
            if (hue < 2) return 2;
            else if (hue > 1001) return 1001;
            else return hue;
        }

        /// <summary>
        /// Random hue in the range 2-1001
        /// </summary>
        public static int RandomDyedHue() => Random(2, 1000);

        /// <summary>
        /// Random hue from 0x62, 0x71, 0x03, 0x0D, 0x13, 0x1C, 0x21, 0x30, 0x37, 0x3A, 0x44, 0x59
        /// </summary>
        public static int RandomBrightHue() => RandomDouble() < 0.1
            ? RandomList(0x62, 0x71)
            : RandomList(0x03, 0x0D, 0x13, 0x1C, 0x21, 0x30, 0x37, 0x3A, 0x44, 0x59);

        //[Obsolete( "Depreciated, use the methods for the Mobile's race", false )]
        public static int ClipSkinHue(int hue)
        {
            if (hue < 1002) return 1002;
            else if (hue > 1058) return 1058;
            else return hue;
        }

        //[Obsolete( "Depreciated, use the methods for the Mobile's race", false )]
        public static int RandomSkinHue() => Random(1002, 57) | 0x8000;

        //[Obsolete( "Depreciated, use the methods for the Mobile's race", false )]
        public static int ClipHairHue(int hue)
        {
            if (hue < 1102) return 1102;
            else if (hue > 1149) return 1149;
            else return hue;
        }

        //[Obsolete( "Depreciated, use the methods for the Mobile's race", false )]
        public static int RandomHairHue() => Random(1102, 48);

        #endregion

        static SkillName[] _allSkills = new[]
        {
            SkillName.Alchemy,
            SkillName.Anatomy,
            SkillName.AnimalLore,
            SkillName.ItemID,
            SkillName.ArmsLore,
            SkillName.Parry,
            SkillName.Begging,
            SkillName.Blacksmith,
            SkillName.Fletching,
            SkillName.Peacemaking,
            SkillName.Camping,
            SkillName.Carpentry,
            SkillName.Cartography,
            SkillName.Cooking,
            SkillName.DetectHidden,
            SkillName.Discordance,
            SkillName.EvalInt,
            SkillName.Healing,
            SkillName.Fishing,
            SkillName.Forensics,
            SkillName.Herding,
            SkillName.Hiding,
            SkillName.Provocation,
            SkillName.Inscribe,
            SkillName.Lockpicking,
            SkillName.Magery,
            SkillName.MagicResist,
            SkillName.Tactics,
            SkillName.Snooping,
            SkillName.Musicianship,
            SkillName.Poisoning,
            SkillName.Archery,
            SkillName.SpiritSpeak,
            SkillName.Stealing,
            SkillName.Tailoring,
            SkillName.AnimalTaming,
            SkillName.TasteID,
            SkillName.Tinkering,
            SkillName.Tracking,
            SkillName.Veterinary,
            SkillName.Swords,
            SkillName.Macing,
            SkillName.Fencing,
            SkillName.Wrestling,
            SkillName.Lumberjacking,
            SkillName.Mining,
            SkillName.Meditation,
            SkillName.Stealth,
            SkillName.RemoveTrap,
            SkillName.Necromancy,
            SkillName.Focus,
            SkillName.Chivalry,
            SkillName.Bushido,
            SkillName.Ninjitsu,
            SkillName.Spellweaving
        };

        static SkillName[] _combatSkills = new[]
        {
            SkillName.Archery,
            SkillName.Swords,
            SkillName.Macing,
            SkillName.Fencing,
            SkillName.Wrestling
        };

        static SkillName[] _craftSkills = new[]
        {
            SkillName.Alchemy,
            SkillName.Blacksmith,
            SkillName.Fletching,
            SkillName.Carpentry,
            SkillName.Cartography,
            SkillName.Cooking,
            SkillName.Inscribe,
            SkillName.Tailoring,
            SkillName.Tinkering
        };

        public static SkillName RandomSkill() => _allSkills[Random(_allSkills.Length - (Core.ML ? 0 : Core.SE ? 1 : Core.AOS ? 3 : 6))];

        public static SkillName RandomCombatSkill() => _combatSkills[Random(_combatSkills.Length)];

        public static SkillName RandomCraftSkill() => _craftSkills[Random(_craftSkills.Length)];

        public static void FixPoints(ref Point3D top, ref Point3D bottom)
        {
            if (bottom.X < top.X) { var swap = top.X; top.X = bottom.X; bottom.X = swap; }
            if (bottom.Y < top.Y) { var swap = top.Y; top.Y = bottom.Y; bottom.Y = swap; }
            if (bottom.Z < top.Z) { var swap = top.Z; top.Z = bottom.Z; bottom.Z = swap; }
        }

        public static ArrayList BuildArrayList(IEnumerable enumerable)
        {
            var e = enumerable.GetEnumerator();
            var list = new ArrayList();
            while (e.MoveNext())
                list.Add(e.Current);
            return list;
        }

        public static bool RangeCheck(IPoint2D p1, IPoint2D p2, int range)
            => p1.X >= (p2.X - range)
            && p1.X <= (p2.X + range)
            && p1.Y >= (p2.Y - range)
            && p2.Y <= (p2.Y + range);

        public static void FormatBuffer(TextWriter o, Stream input, int length)
        {
            o.WriteLine("        0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");
            o.WriteLine("       -- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --");
            var byteIndex = 0;
            var whole = length >> 4;
            var rem = length & 0xF;
            for (var i = 0; i < whole; ++i, byteIndex += 16)
            {
                var bytes = new StringBuilder(49);
                var chars = new StringBuilder(16);
                for (var j = 0; j < 16; ++j)
                {
                    var c = input.ReadByte();
                    bytes.Append(c.ToString("X2"));
                    if (j != 7) bytes.Append(' '); else bytes.Append("  ");
                    chars.Append(c >= 0x20 && c < 0x7F ? (char)c : '.');
                }
                o.Write(byteIndex.ToString("X4"));
                o.Write("   ");
                o.Write(bytes.ToString());
                o.Write("  ");
                o.WriteLine(chars.ToString());
            }
            if (rem != 0)
            {
                var bytes = new StringBuilder(49);
                var chars = new StringBuilder(rem);
                for (var j = 0; j < 16; ++j)
                    if (j < rem)
                    {
                        var c = input.ReadByte();
                        bytes.Append(c.ToString("X2"));
                        if (j != 7) bytes.Append(' '); else bytes.Append("  ");
                        chars.Append(c >= 0x20 && c < 0x7F ? (char)c : '.');
                    }
                    else bytes.Append("   ");
                o.Write(byteIndex.ToString("X4"));
                o.Write("   ");
                o.Write(bytes.ToString());
                o.Write("  ");
                o.WriteLine(chars.ToString());
            }
        }

        static Stack<ConsoleColor> _consoleColors = new Stack<ConsoleColor>();

        public static void PushColor(ConsoleColor color)
        {
            try
            {
                _consoleColors.Push(Console.ForegroundColor);
                Console.ForegroundColor = color;
            }
            catch { }
        }

        public static void PopColor()
        {
            try { Console.ForegroundColor = _consoleColors.Pop(); }
            catch { }
        }

        public static bool NumberBetween(double num, int bound1, int bound2, double allowance)
        {
            if (bound1 > bound2) { var i = bound1; bound1 = bound2; bound2 = i; }
            return num < bound2 + allowance && num > bound1 - allowance;
        }

        public static void AssignRandomHair(Mobile m, int hue)
        {
            m.HairItemID = m.Race.RandomHair(m);
            m.HairHue = hue;
        }

        public static void AssignRandomHair(Mobile m, bool randomHue = true)
        {
            m.HairItemID = m.Race.RandomHair(m);
            if (randomHue)
                m.HairHue = m.Race.RandomHairHue();
        }

        public static void AssignRandomFacialHair(Mobile m, int hue)
        {
            m.FacialHairItemID = m.Race.RandomFacialHair(m);
            m.FacialHairHue = hue;
        }

        public static void AssignRandomFacialHair(Mobile m, bool randomHue = true)
        {
            m.FacialHairItemID = m.Race.RandomFacialHair(m);
            if (randomHue)
                m.FacialHairHue = m.Race.RandomHairHue();
        }

        public static List<TOutput> CastConvertList<TInput, TOutput>(List<TInput> list) where TOutput : TInput
            => list.ConvertAll(new Converter<TInput, TOutput>(delegate (TInput value) { return (TOutput)value; }));
        public static List<TOutput> SafeConvertList<TInput, TOutput>(List<TInput> list) where TOutput : class
        {
            var output = new List<TOutput>(list.Capacity);
            for (var i = 0; i < list.Count; i++)
            {
                TOutput t = list[i] as TOutput;
                if (t != null)
                    output.Add(t);
            }
            return output;
        }
    }
}