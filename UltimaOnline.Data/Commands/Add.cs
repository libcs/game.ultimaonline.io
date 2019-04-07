using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UltimaOnline.Items;
using UltimaOnline.Targeting;
using CPA = UltimaOnline.CommandPropertyAttribute;

namespace UltimaOnline.Commands
{
    public class Add
    {
        public static void Initialize()
        {
            CommandSystem.Register("Tile", AccessLevel.GameMaster, new CommandEventHandler(Tile_OnCommand));
            CommandSystem.Register("TileRXYZ", AccessLevel.GameMaster, new CommandEventHandler(TileRXYZ_OnCommand));
            CommandSystem.Register("TileXYZ", AccessLevel.GameMaster, new CommandEventHandler(TileXYZ_OnCommand));
            CommandSystem.Register("TileZ", AccessLevel.GameMaster, new CommandEventHandler(TileZ_OnCommand));
            CommandSystem.Register("TileAvg", AccessLevel.GameMaster, new CommandEventHandler(TileAvg_OnCommand));
            CommandSystem.Register("Outline", AccessLevel.GameMaster, new CommandEventHandler(Outline_OnCommand));
            CommandSystem.Register("OutlineRXYZ", AccessLevel.GameMaster, new CommandEventHandler(OutlineRXYZ_OnCommand));
            CommandSystem.Register("OutlineXYZ", AccessLevel.GameMaster, new CommandEventHandler(OutlineXYZ_OnCommand));
            CommandSystem.Register("OutlineZ", AccessLevel.GameMaster, new CommandEventHandler(OutlineZ_OnCommand));
            CommandSystem.Register("OutlineAvg", AccessLevel.GameMaster, new CommandEventHandler(OutlineAvg_OnCommand));
        }

        public static void Invoke(Mobile from, Point3D start, Point3D end, string[] args, List<Container> packs = null, bool outline = false, bool mapAvg = false)
        {
            var b = new StringBuilder();
            b.AppendFormat("{0} {1} building ", from.AccessLevel, CommandLogging.Format(from));
            if (start == end)
                b.AppendFormat("at {0} in {1}", start, from.Map);
            else
                b.AppendFormat("from {0} to {1} in {2}", start, end, from.Map);
            b.Append(":");
            for (var i = 0; i < args.Length; ++i)
                b.AppendFormat(" \"{0}\"", args[i]);
            CommandLogging.WriteLine(from, b.ToString());
            var name = args[0];
            FixArgs(ref args);
            string[,] props = null;
            for (var i = 0; i < args.Length; ++i)
                if (Insensitive.Equals(args[i], "set"))
                {
                    var remains = args.Length - i - 1;
                    if (remains >= 2)
                    {
                        props = new string[remains / 2, 2];
                        remains /= 2;
                        for (var j = 0; j < remains; ++j)
                        {
                            props[j, 0] = args[i + (j * 2) + 1];
                            props[j, 1] = args[i + (j * 2) + 2];
                        }
                        FixSetString(ref args, i);
                    }
                    break;
                }
            var type = ScriptCompiler.FindTypeByName(name);
            if (!IsEntity(type))
            {
                from.SendMessage("No type with that name was found.");
                return;
            }
            var time = DateTime.UtcNow;
            var built = BuildObjects(from, type, start, end, args, props, packs, outline, mapAvg);
            if (built > 0) from.SendMessage("{0} object{1} generated in {2:F1} seconds.", built, built != 1 ? "s" : string.Empty, (DateTime.UtcNow - time).TotalSeconds);
            else SendUsage(type, from);
        }

        public static void FixSetString(ref string[] args, int index)
        {
            var old = args;
            args = new string[index];
            Array.Copy(old, 0, args, 0, index);
        }

        public static void FixArgs(ref string[] args)
        {
            var old = args;
            args = new string[args.Length - 1];
            Array.Copy(old, 1, args, 0, args.Length);
        }

        public static int BuildObjects(Mobile from, Type type, Point3D start, Point3D end, string[] args, string[,] props, List<Container> packs, bool outline = false, bool mapAvg = false)
        {
            Utility.FixPoints(ref start, ref end);
            PropertyInfo[] realProps = null;
            if (props != null)
            {
                realProps = new PropertyInfo[props.GetLength(0)];
                var allProps = type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
                for (var i = 0; i < realProps.Length; ++i)
                {
                    PropertyInfo thisProp = null;
                    var propName = props[i, 0];
                    for (var j = 0; thisProp == null && j < allProps.Length; ++j)
                        if (Insensitive.Equals(propName, allProps[j].Name))
                            thisProp = allProps[j];
                    if (thisProp == null)
                        from.SendMessage("Property not found: {0}", propName);
                    else
                    {
                        var attr = Properties.GetCPA(thisProp);
                        if (attr == null) from.SendMessage("Property ({0}) not found.", propName);
                        else if (from.AccessLevel < attr.WriteLevel) from.SendMessage("Setting this property ({0}) requires at least {1} access level.", propName, Mobile.GetAccessLevelName(attr.WriteLevel));
                        else if (!thisProp.CanWrite || attr.ReadOnly) from.SendMessage("Property ({0}) is read only.", propName);
                        else realProps[i] = thisProp;
                    }
                }
            }
            var ctors = type.GetConstructors();
            for (var i = 0; i < ctors.Length; ++i)
            {
                var ctor = ctors[i];
                if (!IsConstructable(ctor, from.AccessLevel))
                    continue;
                var paramList = ctor.GetParameters();
                if (args.Length == paramList.Length)
                {
                    var paramValues = ParseValues(paramList, args);
                    if (paramValues == null)
                        continue;
                    var built = Build(from, start, end, ctor, paramValues, props, realProps, packs, outline, mapAvg);
                    if (built > 0)
                        return built;
                }
            }
            return 0;
        }

        public static object[] ParseValues(ParameterInfo[] paramList, string[] args)
        {
            var values = new object[args.Length];
            for (int i = 0; i < args.Length; ++i)
            {
                var value = ParseValue(paramList[i].ParameterType, args[i]);
                if (value != null) values[i] = value;
                else return null;
            }
            return values;
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (IsEnum(type)) return Enum.Parse(type, value, true);
                else if (IsType(type)) return ScriptCompiler.FindTypeByName(value);
                else if (IsParsable(type)) return ParseParsable(type, value);
                else
                {
                    object obj = value;
                    if (value != null && value.StartsWith("0x"))
                    {
                        if (IsSignedNumeric(type)) obj = Convert.ToInt64(value.Substring(2), 16);
                        else if (IsUnsignedNumeric(type)) obj = Convert.ToUInt64(value.Substring(2), 16);
                        obj = Convert.ToInt32(value.Substring(2), 16);
                    }
                    if (obj == null && !type.IsValueType) return null;
                    else return Convert.ChangeType(obj, type);
                }
            }
            catch { return null; }
        }

        public static IEntity Build(Mobile from, ConstructorInfo ctor, object[] values, string[,] props, PropertyInfo[] realProps, ref bool sendError)
        {
            var built = ctor.Invoke(values);
            if (built != null && realProps != null)
            {
                var hadError = false;
                for (var i = 0; i < realProps.Length; ++i)
                {
                    if (realProps[i] == null)
                        continue;
                    var result = Properties.InternalSetValue(from, built, built, realProps[i], props[i, 1], props[i, 1], false);
                    if (result != "Property has been set.")
                    {
                        if (sendError)
                            from.SendMessage(result);
                        hadError = true;
                    }
                }
                if (hadError)
                    sendError = false;
            }
            return (IEntity)built;
        }

        public static int Build(Mobile from, Point3D start, Point3D end, ConstructorInfo ctor, object[] values, string[,] props, PropertyInfo[] realProps, List<Container> packs, bool outline = false, bool mapAvg = false)
        {
            try
            {
                var map = from.Map;
                var width = end.X - start.X + 1;
                var height = end.Y - start.Y + 1;
                if (outline && (width < 3 || height < 3))
                    outline = false;
                var objectCount = packs != null ? packs.Count : outline ? (width + height - 2) * 2 : width * height;
                if (objectCount >= 20)
                    from.SendMessage("Constructing {0} objects, please wait.", objectCount);
                var sendError = true;
                var b = new StringBuilder();
                b.Append("Serials: ");
                if (packs != null)
                    for (var i = 0; i < packs.Count; ++i)
                    {
                        var built = Build(from, ctor, values, props, realProps, ref sendError);
                        b.AppendFormat("0x{0:X}; ", built.Serial.Value);
                        if (built is Item)
                        {
                            var pack = packs[i];
                            pack.DropItem((Item)built);
                        }
                        else if (built is Mobile m)
                            m.MoveToWorld(new Point3D(start.X, start.Y, start.Z), map);
                    }
                else
                {
                    var z = start.Z;
                    for (var x = start.X; x <= end.X; ++x)
                        for (var y = start.Y; y <= end.Y; ++y)
                        {
                            if (outline && x != start.X && x != end.X && y != start.Y && y != end.Y)
                                continue;
                            if (mapAvg)
                                z = map.GetAverageZ(x, y);
                            var built = Build(from, ctor, values, props, realProps, ref sendError);
                            b.AppendFormat("0x{0:X}; ", built.Serial.Value);
                            if (built is Item item)
                                item.MoveToWorld(new Point3D(x, y, z), map);
                            else if (built is Mobile m)
                                m.MoveToWorld(new Point3D(x, y, z), map);
                        }
                }
                CommandLogging.WriteLine(from, b.ToString());
                return objectCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 0;
            }
        }

        public static void SendUsage(Type type, Mobile from)
        {
            var ctors = type.GetConstructors();
            var foundCtor = false;
            for (var i = 0; i < ctors.Length; ++i)
            {
                var ctor = ctors[i];
                if (!IsConstructable(ctor, from.AccessLevel))
                    continue;
                if (!foundCtor)
                {
                    foundCtor = true;
                    from.SendMessage("Usage:");
                }
                SendCtor(type, ctor, from);
            }
            if (!foundCtor)
                from.SendMessage("That type is not marked constructable.");
        }

        public static void SendCtor(Type type, ConstructorInfo ctor, Mobile from)
        {
            var paramList = ctor.GetParameters();
            var b = new StringBuilder();
            b.Append(type.Name);
            for (var i = 0; i < paramList.Length; ++i)
            {
                if (i != 0)
                    b.Append(',');
                b.Append(' ');
                b.Append(paramList[i].ParameterType.Name);
                b.Append(' ');
                b.Append(paramList[i].Name);
            }
            from.SendMessage(b.ToString());
        }

        public class AddTarget : Target
        {
            string[] _Args;

            public AddTarget(string[] args) : base(-1, true, TargetFlags.None)
            {
                _Args = args;
            }

            protected override void OnTarget(Mobile from, object o)
            {
                if (o is IPoint3D p)
                {
                    if (p is Item i)
                        p = i.GetWorldTop();
                    else if (p is Mobile m)
                        p = m.Location;
                    var point = new Point3D(p);
                    Add.Invoke(from, point, point, _Args);
                }
            }
        }

        enum TileZType
        {
            Start,
            Fixed,
            MapAverage
        }

        class TileState
        {
            public TileZType ZType;
            public int FixedZ;
            public string[] Args;
            public bool Outline;

            public TileState(TileZType zType, int fixedZ, string[] args, bool outline)
            {
                ZType = zType;
                FixedZ = fixedZ;
                Args = args;
                Outline = outline;
            }
        }

        static void TileBox_Callback(Mobile from, Map map, Point3D start, Point3D end, object state)
        {
            var ts = (TileState)state;
            var mapAvg = false;
            switch (ts.ZType)
            {
                case TileZType.Fixed:
                    {
                        start.Z = end.Z = ts.FixedZ;
                        break;
                    }
                case TileZType.MapAverage:
                    {
                        mapAvg = true;
                        break;
                    }
            }
            Invoke(from, start, end, ts.Args, null, ts.Outline, mapAvg);
        }

        static void Internal_OnCommand(CommandEventArgs e, bool outline)
        {
            if (e.Length >= 1) BoundingBoxPicker.Begin(e.Mobile, new BoundingBoxCallback(TileBox_Callback), new TileState(TileZType.Start, 0, e.Arguments, outline));
            else e.Mobile.SendMessage("Format: {0} <type> [params] [set {{<propertyName> <value> ...}}]", outline ? "Outline" : "Tile");
        }

        static void InternalRXYZ_OnCommand(CommandEventArgs e, bool outline)
        {
            if (e.Length >= 6)
            {
                var p = new Point3D(e.Mobile.X + e.GetInt32(0), e.Mobile.Y + e.GetInt32(1), e.Mobile.Z + e.GetInt32(4));
                var p2 = new Point3D(p.X + e.GetInt32(2) - 1, p.Y + e.GetInt32(3) - 1, p.Z);
                var subArgs = new string[e.Length - 5];
                for (var i = 0; i < subArgs.Length; ++i)
                    subArgs[i] = e.Arguments[i + 5];
                Invoke(e.Mobile, p, p2, subArgs, null, outline, false);
            }
            else e.Mobile.SendMessage("Format: {0}RXYZ <x> <y> <w> <h> <z> <type> [params] [set {{<propertyName> <value> ...}}]", outline ? "Outline" : "Tile");
        }

        static void InternalXYZ_OnCommand(CommandEventArgs e, bool outline)
        {
            if (e.Length >= 6)
            {
                var p = new Point3D(e.GetInt32(0), e.GetInt32(1), e.GetInt32(4));
                var p2 = new Point3D(p.X + e.GetInt32(2) - 1, p.Y + e.GetInt32(3) - 1, e.GetInt32(4));
                var subArgs = new string[e.Length - 5];
                for (var i = 0; i < subArgs.Length; ++i)
                    subArgs[i] = e.Arguments[i + 5];
                Invoke(e.Mobile, p, p2, subArgs, null, outline, false);
            }
            else e.Mobile.SendMessage("Format: {0}XYZ <x> <y> <w> <h> <z> <type> [params] [set {{<propertyName> <value> ...}}]", outline ? "Outline" : "Tile");
        }

        static void InternalZ_OnCommand(CommandEventArgs e, bool outline)
        {
            if (e.Length >= 2)
            {
                var subArgs = new string[e.Length - 1];
                for (var i = 0; i < subArgs.Length; ++i)
                    subArgs[i] = e.Arguments[i + 1];
                BoundingBoxPicker.Begin(e.Mobile, new BoundingBoxCallback(TileBox_Callback), new TileState(TileZType.Fixed, e.GetInt32(0), subArgs, outline));
            }
            else e.Mobile.SendMessage("Format: {0}Z <z> <type> [params] [set {{<propertyName> <value> ...}}]", outline ? "Outline" : "Tile");
        }

        static void InternalAvg_OnCommand(CommandEventArgs e, bool outline)
        {
            if (e.Length >= 1) BoundingBoxPicker.Begin(e.Mobile, new BoundingBoxCallback(TileBox_Callback), new TileState(TileZType.MapAverage, 0, e.Arguments, outline));
            else e.Mobile.SendMessage("Format: {0}Avg <type> [params] [set {{<propertyName> <value> ...}}]", outline ? "Outline" : "Tile");
        }

        [Usage("Tile <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name into a targeted bounding box. Optional constructor parameters. Optional set property list.")]
        public static void Tile_OnCommand(CommandEventArgs e) => Internal_OnCommand(e, false);

        [Usage("TileRXYZ <x> <y> <w> <h> <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name into a given bounding box, (x, y) parameters are relative to your characters position. Optional constructor parameters. Optional set property list.")]
        public static void TileRXYZ_OnCommand(CommandEventArgs e) => InternalRXYZ_OnCommand(e, false);

        [Usage("TileXYZ <x> <y> <w> <h> <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name into a given bounding box. Optional constructor parameters. Optional set property list.")]
        public static void TileXYZ_OnCommand(CommandEventArgs e) => InternalXYZ_OnCommand(e, false);

        [Usage("TileZ <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name into a targeted bounding box at a fixed Z location. Optional constructor parameters. Optional set property list.")]
        public static void TileZ_OnCommand(CommandEventArgs e) => InternalZ_OnCommand(e, false);

        [Usage("TileAvg <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name into a targeted bounding box on the map's average Z elevation. Optional constructor parameters. Optional set property list.")]
        public static void TileAvg_OnCommand(CommandEventArgs e) => InternalAvg_OnCommand(e, false);

        [Usage("Outline <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name around a targeted bounding box. Optional constructor parameters. Optional set property list.")]
        public static void Outline_OnCommand(CommandEventArgs e) => Internal_OnCommand(e, true);

        [Usage("OutlineRXYZ <x> <y> <w> <h> <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name around a given bounding box, (x, y) parameters are relative to your characters position. Optional constructor parameters. Optional set property list.")]
        public static void OutlineRXYZ_OnCommand(CommandEventArgs e) => InternalRXYZ_OnCommand(e, true);

        [Usage("OutlineXYZ <x> <y> <w> <h> <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name around a given bounding box. Optional constructor parameters. Optional set property list.")]
        public static void OutlineXYZ_OnCommand(CommandEventArgs e) => InternalXYZ_OnCommand(e, true);

        [Usage("OutlineZ <z> <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name around a targeted bounding box at a fixed Z location. Optional constructor parameters. Optional set property list.")]
        public static void OutlineZ_OnCommand(CommandEventArgs e) => InternalZ_OnCommand(e, true);

        [Usage("OutlineAvg <name> [params] [set {<propertyName> <value> ...}]"), Description("Tiles an item or npc by name around a targeted bounding box on the map's average Z elevation. Optional constructor parameters. Optional set property list.")]
        public static void OutlineAvg_OnCommand(CommandEventArgs e) => InternalAvg_OnCommand(e, true);

        static Type _EntityType = typeof(IEntity);

        public static bool IsEntity(Type t) => _EntityType.IsAssignableFrom(t);

        static Type _ConstructableType = typeof(ConstructableAttribute);

        public static bool IsConstructable(ConstructorInfo ctor, AccessLevel accessLevel)
        {
            var attrs = ctor.GetCustomAttributes(_ConstructableType, false);
            return attrs.Length == 0 ? false : accessLevel >= ((ConstructableAttribute)attrs[0]).AccessLevel;
        }

        static Type _EnumType = typeof(Enum);
        public static bool IsEnum(Type type) => type.IsSubclassOf(_EnumType);

        static Type _TypeType = typeof(Type);
        public static bool IsType(Type type) => type == _TypeType || type.IsSubclassOf(_TypeType);

        static Type _ParsableType = typeof(ParsableAttribute);
        public static bool IsParsable(Type type) => type.IsDefined(_ParsableType, false);

        static Type[] _ParseTypes = new Type[] { typeof(string) };
        static object[] _ParseArgs = new object[1];
        public static object ParseParsable(Type type, string value)
        {
            var method = type.GetMethod("Parse", _ParseTypes);
            _ParseArgs[0] = value;
            return method.Invoke(null, _ParseArgs);
        }

        static Type[] _SignedNumerics = new Type[]
        {
            typeof(long),
            typeof(int),
            typeof(short),
            typeof(sbyte)
        };

        public static bool IsSignedNumeric(Type type)
        {
            for (var i = 0; i < _SignedNumerics.Length; ++i)
                if (type == _SignedNumerics[i])
                    return true;
            return false;
        }

        static Type[] _UnsignedNumerics = new Type[]
        {
            typeof(ulong),
            typeof(uint),
            typeof(ushort),
            typeof(byte)
        };

        public static bool IsUnsignedNumeric(Type type)
        {
            for (var i = 0; i < _UnsignedNumerics.Length; ++i)
                if (type == _UnsignedNumerics[i])
                    return true;
            return false;
        }
    }
}