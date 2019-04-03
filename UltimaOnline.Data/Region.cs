using System;
using System.Collections.Generic;
using System.Xml;
using UltimaOnline.Network;
using UltimaOnline.Targeting;

namespace UltimaOnline
{
    public enum MusicName
    {
        Invalid = -1,
        OldUlt01 = 0,
        Create1,
        DragFlit,
        OldUlt02,
        OldUlt03,
        OldUlt04,
        OldUlt05,
        OldUlt06,
        Stones2,
        Britain1,
        Britain2,
        Bucsden,
        Jhelom,
        LBCastle,
        Linelle,
        Magincia,
        Minoc,
        Ocllo,
        Samlethe,
        Serpents,
        Skarabra,
        Trinsic,
        Vesper,
        Wind,
        Yew,
        Cave01,
        Dungeon9,
        Forest_a,
        InTown01,
        Jungle_a,
        Mountn_a,
        Plains_a,
        Sailing,
        Swamp_a,
        Tavern01,
        Tavern02,
        Tavern03,
        Tavern04,
        Combat1,
        Combat2,
        Combat3,
        Approach,
        Death,
        Victory,
        BTCastle,
        Nujelm,
        Dungeon2,
        Cove,
        Moonglow,
        Zento,
        TokunoDungeon,
        Taiko,
        DreadHornArea,
        ElfCity,
        GrizzleDungeon,
        MelisandesLair,
        ParoxysmusLair,
        GwennoConversation,
        GoodEndGame,
        GoodVsEvil,
        GreatEarthSerpents,
        Humanoids_U9,
        MinocNegative,
        Paws,
        SelimsBar,
        SerpentIsleCombat_U7,
        ValoriaShips
    }

    public class Region : IComparable
    {
        public static List<Region> Regions { get; } = new List<Region>();

        public static Region Find(Point3D p, Map map)
        {
            if (map == null)
                return Map.Internal.DefaultRegion;
            var sector = map.GetSector(p);
            var list = sector.RegionRects;
            for (var i = 0; i < list.Count; ++i)
            {
                var regRect = list[i];
                if (regRect.Contains(p))
                    return regRect.Region;
            }
            return map.DefaultRegion;
        }
        public static Type DefaultRegionType { get; set; } = typeof(Region);

        public static TimeSpan StaffLogoutDelay { get; set; } = TimeSpan.Zero;
        public static TimeSpan DefaultLogoutDelay { get; set; } = TimeSpan.FromMinutes(5.0);

        public static readonly int DefaultPriority = 50;

        public static readonly int MinZ = sbyte.MinValue;
        public static readonly int MaxZ = sbyte.MaxValue + 1;

        public static Rectangle3D ConvertTo3D(Rectangle2D rect) => new Rectangle3D(new Point3D(rect.Start, MinZ), new Point3D(rect.End, MaxZ));

        public static Rectangle3D[] ConvertTo3D(Rectangle2D[] rects)
        {
            var ret = new Rectangle3D[rects.Length];
            for (var i = 0; i < ret.Length; i++)
                ret[i] = ConvertTo3D(rects[i]);
            return ret;
        }


        string _Name;
        int _Priority;
        Point3D _GoLocation;

        public string Name { get { return _Name; } }
        public Map Map { get; }
        public Region Parent { get; }
        public List<Region> Children { get; } = new List<Region>();
        public Rectangle3D[] Area { get; }
        public Sector[] Sectors { get; private set; }
        public bool Dynamic { get; }
        public int Priority { get { return _Priority; } }
        public int ChildLevel { get; }
        public bool Registered { get; private set; }

        public Point3D GoLocation { get => _GoLocation; set => _GoLocation = value; }
        public MusicName Music { get; set; }

        public bool IsDefault => Map.DefaultRegion == this;
        public virtual MusicName DefaultMusic => Parent != null ? Parent.Music : MusicName.Invalid;

        public Region(string name, Map map, int priority, params Rectangle2D[] area) : this(name, map, priority, ConvertTo3D(area)) { }
        public Region(string name, Map map, int priority, params Rectangle3D[] area) : this(name, map, null, area) => _Priority = priority;
        public Region(string name, Map map, Region parent, params Rectangle2D[] area) : this(name, map, parent, ConvertTo3D(area)) { }
        public Region(string name, Map map, Region parent, params Rectangle3D[] area)
        {
            _Name = name;
            Map = map;
            Parent = parent;
            Area = area;
            Dynamic = true;
            Music = DefaultMusic;
            if (Parent == null)
            {
                ChildLevel = 0;
                _Priority = DefaultPriority;
            }
            else
            {
                ChildLevel = Parent.ChildLevel + 1;
                _Priority = Parent.Priority;
            }
        }

        public void Register()
        {
            if (Registered)
                return;
            OnRegister();
            Registered = true;
            if (Parent != null)
            {
                Parent.Children.Add(this);
                Parent.OnChildAdded(this);
            }
            Regions.Add(this);
            Map.RegisterRegion(this);
            var sectors = new List<Sector>();
            for (var i = 0; i < Area.Length; i++)
            {
                var rect = Area[i];
                var start = Map.Bound(new Point2D(rect.Start));
                var end = Map.Bound(new Point2D(rect.End));
                var startSector = Map.GetSector(start);
                var endSector = Map.GetSector(end);
                for (var x = startSector.X; x <= endSector.X; x++)
                    for (var y = startSector.Y; y <= endSector.Y; y++)
                    {
                        var sector = Map.GetRealSector(x, y);
                        sector.OnEnter(this, rect);
                        if (!sectors.Contains(sector))
                            sectors.Add(sector);
                    }
            }
            Sectors = sectors.ToArray();
        }

        public void Unregister()
        {
            if (!Registered)
                return;
            OnUnregister();
            Registered = false;
            if (Children.Count > 0)
                Console.WriteLine($"Warning: Unregistering region '{this}' with children");
            if (Parent != null)
            {
                Parent.Children.Remove(this);
                Parent.OnChildRemoved(this);
            }
            Regions.Remove(this);
            Map.UnregisterRegion(this);
            if (Sectors != null)
                for (var i = 0; i < Sectors.Length; i++)
                    Sectors[i].OnLeave(this);
            Sectors = null;
        }

        public bool Contains(Point3D p)
        {
            for (var i = 0; i < Area.Length; i++)
            {
                var rect = Area[i];
                if (rect.Contains(p))
                    return true;
            }
            return false;
        }

        public bool IsChildOf(Region region)
        {
            if (region == null)
                return false;
            var p = Parent;
            while (p != null)
            {
                if (p == region)
                    return true;
                p = p.Parent;
            }
            return false;
        }

        public Region GetRegion(Type regionType)
        {
            if (regionType == null)
                return null;
            var r = this;
            do
            {
                if (regionType.IsAssignableFrom(r.GetType()))
                    return r;
                r = r.Parent;
            }
            while (r != null);
            return null;
        }

        public Region GetRegion(string regionName)
        {
            if (regionName == null)
                return null;
            var r = this;
            do
            {
                if (r._Name == regionName)
                    return r;
                r = r.Parent;
            }
            while (r != null);
            return null;
        }

        public bool IsPartOf(Region region) => this == region ? true : IsChildOf(region);
        public bool IsPartOf(Type regionType) => GetRegion(regionType) != null;
        public bool IsPartOf(string regionName) => GetRegion(regionName) != null;

        public virtual bool AcceptsSpawnsFrom(Region region)
        {
            if (!AllowSpawn())
                return false;
            if (region == this)
                return true;
            if (Parent != null)
                return Parent.AcceptsSpawnsFrom(region);
            return false;
        }

        public List<Mobile> GetPlayers()
        {
            var list = new List<Mobile>();
            if (Sectors != null)
                for (var i = 0; i < Sectors.Length; i++)
                {
                    var sector = Sectors[i];
                    foreach (var player in sector.Players)
                        if (player.Region.IsPartOf(this))
                            list.Add(player);
                }
            return list;
        }

        public int GetPlayerCount()
        {
            var count = 0;
            if (Sectors != null)
                for (var i = 0; i < Sectors.Length; i++)
                {
                    var sector = Sectors[i];
                    foreach (var player in sector.Players)
                        if (player.Region.IsPartOf(this))
                            count++;
                }
            return count;
        }

        public List<Mobile> GetMobiles()
        {
            var list = new List<Mobile>();
            if (Sectors != null)
                for (var i = 0; i < Sectors.Length; i++)
                {
                    var sector = Sectors[i];
                    foreach (var mobile in sector.Mobiles)
                        if (mobile.Region.IsPartOf(this))
                            list.Add(mobile);
                }
            return list;
        }

        public int GetMobileCount()
        {
            var count = 0;
            if (Sectors != null)
                for (var i = 0; i < Sectors.Length; i++)
                {
                    var sector = Sectors[i];
                    foreach (var mobile in sector.Mobiles)
                        if (mobile.Region.IsPartOf(this))
                            count++;
                }
            return count;
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            if (!(obj is Region reg))
                throw new ArgumentException("obj is not a Region", nameof(obj));
            // Dynamic regions go first
            if (Dynamic)
            {
                if (!reg.Dynamic)
                    return -1;
            }
            else if (reg.Dynamic)
                return 1;
            var thisPriority = Priority;
            var regPriority = reg.Priority;
            return thisPriority != regPriority ? regPriority - thisPriority : reg.ChildLevel - ChildLevel;
        }

        public override string ToString() => _Name ?? GetType().Name;


        public virtual void OnRegister() { }
        public virtual void OnUnregister() { }
        public virtual void OnChildAdded(Region child) { }
        public virtual void OnChildRemoved(Region child) { }
        public virtual bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation) => m.WalkRegion == null || AcceptsSpawnsFrom(m.WalkRegion);
        public virtual void OnEnter(Mobile m) { }
        public virtual void OnExit(Mobile m) { }
        public virtual void MakeGuard(Mobile focus) => Parent?.MakeGuard(focus);
        public virtual Type GetResource(Type type) => Parent != null ? Parent.GetResource(type) : type;
        public virtual bool CanUseStuckMenu(Mobile m) => Parent != null ? Parent.CanUseStuckMenu(m) : true;
        public virtual void OnAggressed(Mobile aggressor, Mobile aggressed, bool criminal) => Parent?.OnAggressed(aggressor, aggressed, criminal);
        public virtual void OnDidHarmful(Mobile harmer, Mobile harmed) => Parent?.OnDidHarmful(harmer, harmed);
        public virtual void OnGotHarmful(Mobile harmer, Mobile harmed) => Parent?.OnGotHarmful(harmer, harmed);
        public virtual void OnLocationChanged(Mobile m, Point3D oldLocation) => Parent?.OnLocationChanged(m, oldLocation);
        public virtual bool OnTarget(Mobile m, Target t, object o) => Parent != null ? Parent.OnTarget(m, t, o) : true;
        public virtual bool OnCombatantChange(Mobile m, Mobile Old, Mobile New) => Parent != null ? Parent.OnCombatantChange(m, Old, New) : true;
        public virtual bool AllowHousing(Mobile from, Point3D p) => Parent != null ? Parent.AllowHousing(from, p) : true;
        public virtual bool SendInaccessibleMessage(Item item, Mobile from) => Parent != null ? Parent.SendInaccessibleMessage(item, from) : false;
        public virtual bool CheckAccessibility(Item item, Mobile from) => Parent != null ? Parent.CheckAccessibility(item, from) : true;
        public virtual bool OnDecay(Item item) => Parent != null ? Parent.OnDecay(item) : true;

        public virtual bool AllowHarmful(Mobile from, Mobile target) => Parent != null
                ? Parent.AllowHarmful(from, target)
                : Mobile.AllowHarmfulHandler != null ? Mobile.AllowHarmfulHandler(from, target) : true;

        public virtual void OnCriminalAction(Mobile m, bool message)
        {
            if (Parent != null)
                Parent.OnCriminalAction(m, message);
            else if (message)
                m.SendLocalizedMessage(1005040); // You've committed a criminal act!!
        }

        public virtual bool AllowBeneficial(Mobile from, Mobile target) => Parent != null
                ? Parent.AllowBeneficial(from, target)
                : Mobile.AllowBeneficialHandler != null ? Mobile.AllowBeneficialHandler(from, target) : true;

        public virtual void OnBeneficialAction(Mobile helper, Mobile target) => Parent?.OnBeneficialAction(helper, target);
        public virtual void OnGotBeneficialAction(Mobile helper, Mobile target) => Parent?.OnGotBeneficialAction(helper, target);
        public virtual void SpellDamageScalar(Mobile caster, Mobile target, ref double damage) => Parent?.SpellDamageScalar(caster, target, ref damage);
        public virtual void OnSpeech(SpeechEventArgs args) => Parent?.OnSpeech(args);
        public virtual bool OnSkillUse(Mobile m, int Skill) => Parent != null ? Parent.OnSkillUse(m, Skill) : true;
        public virtual bool OnBeginSpellCast(Mobile m, ISpell s) => Parent != null ? Parent.OnBeginSpellCast(m, s) : true;
        public virtual void OnSpellCast(Mobile m, ISpell s) => Parent?.OnSpellCast(m, s);
        public virtual bool OnResurrect(Mobile m) => Parent != null ? Parent.OnResurrect(m) : true;
        public virtual bool OnBeforeDeath(Mobile m) => Parent != null ? Parent.OnBeforeDeath(m) : true;
        public virtual void OnDeath(Mobile m) => Parent?.OnDeath(m);
        public virtual bool OnDamage(Mobile m, ref int Damage) => Parent != null ? Parent.OnDamage(m, ref Damage) : true;
        public virtual bool OnHeal(Mobile m, ref int Heal) => Parent != null ? Parent.OnHeal(m, ref Heal) : true;
        public virtual bool OnDoubleClick(Mobile m, object o) => Parent != null ? Parent.OnDoubleClick(m, o) : true;
        public virtual bool OnSingleClick(Mobile m, object o) => Parent != null ? Parent.OnSingleClick(m, o) : true;
        public virtual bool AllowSpawn() => Parent != null ? Parent.AllowSpawn() : true;
        public virtual void AlterLightLevel(Mobile m, ref int global, ref int personal) => Parent?.AlterLightLevel(m, ref global, ref personal);

        public virtual TimeSpan GetLogoutDelay(Mobile m)
        {
            if (Parent != null) return Parent.GetLogoutDelay(m);
            else if (m.AccessLevel > AccessLevel.Player) return StaffLogoutDelay;
            else return DefaultLogoutDelay;
        }

        internal static bool CanMove(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation, Map map)
        {
            var oldRegion = m.Region;
            var newRegion = Find(newLocation, map);
            while (oldRegion != newRegion)
            {
                if (!newRegion.OnMoveInto(m, d, newLocation, oldLocation)) return false;
                if (newRegion.Parent == null) return true;
                newRegion = newRegion.Parent;
            }
            return true;
        }

        internal static void OnRegionChange(Mobile m, Region oldRegion, Region newRegion)
        {
            if (newRegion != null && m.NetState != null)
            {
                m.CheckLightLevels(false);
                if (oldRegion == null || oldRegion.Music != newRegion.Music)
                    m.Send(PlayMusic.GetInstance(newRegion.Music));
            }
            var oldR = oldRegion;
            var newR = newRegion;
            while (oldR != newR)
            {
                var oldRChild = oldR != null ? oldR.ChildLevel : -1;
                var newRChild = newR != null ? newR.ChildLevel : -1;
                if (oldRChild >= newRChild)
                {
                    oldR.OnExit(m);
                    oldR = oldR.Parent;
                }
                if (newRChild >= oldRChild)
                {
                    newR.OnEnter(m);
                    newR = newR.Parent;
                }
            }
        }

        internal static void Load()
        {
            if (!System.IO.File.Exists("Data/Regions.xml"))
            {
                Console.WriteLine("Error: Data/Regions.xml does not exist");
                return;
            }
            Console.Write("Regions: Loading...");
            var doc = new XmlDocument();
            doc.Load(System.IO.Path.Combine(Core.BaseDirectory, "Data/Regions.xml"));
            var root = doc["ServerRegions"];
            if (root == null)
                Console.WriteLine("Could not find root element 'ServerRegions' in Regions.xml");
            else
                foreach (XmlElement facet in root.SelectNodes("Facet"))
                {
                    Map map = null;
                    if (ReadMap(facet, "name", ref map))
                    {
                        if (map == Map.Internal) Console.WriteLine("Invalid internal map in a facet element");
                        else LoadRegions(facet, map, null);
                    }
                }
            Console.WriteLine("done");
        }

        static void LoadRegions(XmlElement xml, Map map, Region parent)
        {
            foreach (XmlElement xmlReg in xml.SelectNodes("region"))
            {
                var type = DefaultRegionType;
                ReadType(xmlReg, "type", ref type, false);
                if (!typeof(Region).IsAssignableFrom(type))
                {
                    Console.WriteLine($"Invalid region type '{type.FullName}' in regions.xml");
                    continue;
                }
                Region region = null;
                try { region = (Region)Activator.CreateInstance(type, new object[] { xmlReg, map, parent }); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during the creation of region type '{type.FullName}': {ex}");
                    continue;
                }
                region.Register();
                LoadRegions(xmlReg, map, region);
            }
        }

        public Region(XmlElement xml, Map map, Region parent)
        {
            Map = map;
            Parent = parent;
            Dynamic = false;
            if (Parent == null)
            {
                ChildLevel = 0;
                _Priority = DefaultPriority;
            }
            else
            {
                ChildLevel = Parent.ChildLevel + 1;
                _Priority = Parent.Priority;
            }
            ReadString(xml, "name", ref _Name, false);
            if (parent == null)
                ReadInt32(xml, "priority", ref _Priority, false);
            var minZ = MinZ;
            var maxZ = MaxZ;

            var zrange = xml["zrange"];
            ReadInt32(zrange, "min", ref minZ, false);
            ReadInt32(zrange, "max", ref maxZ, false);
            var area = new List<Rectangle3D>();
            foreach (XmlElement xmlRect in xml.SelectNodes("rect"))
            {
                var rect = new Rectangle3D();
                if (ReadRectangle3D(xmlRect, minZ, maxZ, ref rect))
                    area.Add(rect);
            }
            Area = area.ToArray();
            if (Area.Length == 0)
                Console.WriteLine($"Empty area for region '{this}'");
            if (!ReadPoint3D(xml["go"], map, ref _GoLocation, false) && Area.Length > 0)
            {
                var start = Area[0].Start;
                var end = Area[0].End;
                var x = start.X + (end.X - start.X) / 2;
                var y = start.Y + (end.Y - start.Y) / 2;
                _GoLocation = new Point3D(x, y, Map.GetAverageZ(x, y));
            }
            var music = DefaultMusic;
            ReadEnum(xml["music"], "name", ref music, false);
            Music = music;
        }

        protected static string GetAttribute(XmlElement xml, string attribute, bool mandatory)
        {
            if (xml == null)
            {
                if (mandatory)
                    Console.WriteLine($"Missing element for attribute '{attribute}'");
                return null;
            }
            else if (xml.HasAttribute(attribute)) return xml.GetAttribute(attribute);
            else
            {
                if (mandatory)
                    Console.WriteLine($"Missing attribute '{attribute}' in element '{xml.Name}'");
                return null;
            }
        }

        public static bool ReadString(XmlElement xml, string attribute, ref string value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            value = s;
            return true;
        }

        public static bool ReadInt32(XmlElement xml, string attribute, ref int value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            try { value = XmlConvert.ToInt32(s); }
            catch
            {
                Console.WriteLine($"Could not parse integer attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            return true;
        }

        public static bool ReadBoolean(XmlElement xml, string attribute, ref bool value, bool mandatory)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            try { value = XmlConvert.ToBoolean(s); }
            catch
            {
                Console.WriteLine($"Could not parse boolean attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            return true;
        }

        public static bool ReadDateTime(XmlElement xml, string attribute, ref DateTime value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            try { value = XmlConvert.ToDateTime(s, XmlDateTimeSerializationMode.Utc); }
            catch
            {
                Console.WriteLine($"Could not parse DateTime attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            return true;
        }

        public static bool ReadTimeSpan(XmlElement xml, string attribute, ref TimeSpan value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            try { value = XmlConvert.ToTimeSpan(s); }
            catch
            {
                Console.WriteLine($"Could not parse TimeSpan attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            return true;
        }

        public static bool ReadEnum<T>(XmlElement xml, string attribute, ref T value, bool mandatory = true) where T : struct // We can't limit the where clause to Enums only
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            var type = typeof(T);
            if (type.IsEnum && Enum.TryParse(s, true, out T tempVal))
            {
                value = tempVal;
                return true;
            }
            else
            {
                Console.WriteLine($"Could not parse {type} enum attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
        }

        public static bool ReadMap(XmlElement xml, string attribute, ref Map value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            try { value = Map.Parse(s); }
            catch
            {
                Console.WriteLine($"Could not parse Map attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            return true;
        }

        public static bool ReadType(XmlElement xml, string attribute, ref Type value, bool mandatory = true)
        {
            var s = GetAttribute(xml, attribute, mandatory);
            if (s == null)
                return false;
            Type type;
            try { type = ScriptCompiler.FindTypeByName(s, false); }
            catch
            {
                Console.WriteLine($"Could not parse Type attribute '{attribute}' in element '{xml.Name}'");
                return false;
            }
            if (type == null)
            {
                Console.WriteLine($"Could not find Type '{s}'");
                return false;
            }
            value = type;
            return true;
        }

        public static bool ReadPoint3D(XmlElement xml, Map map, ref Point3D value, bool mandatory = true)
        {
            int x = 0, y = 0, z = 0;
            var xyOk = ReadInt32(xml, "x", ref x, mandatory) & ReadInt32(xml, "y", ref y, mandatory);
            var zOk = ReadInt32(xml, "z", ref z, mandatory && map == null);
            if (xyOk && (zOk || map != null))
            {
                if (!zOk)
                    z = map.GetAverageZ(x, y);
                value = new Point3D(x, y, z);
                return true;
            }
            return false;
        }

        public static bool ReadRectangle3D(XmlElement xml, int defaultMinZ, int defaultMaxZ, ref Rectangle3D value, bool mandatory = true)
        {
            int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            if (xml.HasAttribute("x"))
            {
                if (ReadInt32(xml, "x", ref x1, mandatory)
                    & ReadInt32(xml, "y", ref y1, mandatory)
                    & ReadInt32(xml, "width", ref x2, mandatory)
                    & ReadInt32(xml, "height", ref y2, mandatory))
                {
                    x2 += x1;
                    y2 += y1;
                }
                else return false;
            }
            else
            {
                if (!ReadInt32(xml, "x1", ref x1, mandatory)
                    | !ReadInt32(xml, "y1", ref y1, mandatory)
                    | !ReadInt32(xml, "x2", ref x2, mandatory)
                    | !ReadInt32(xml, "y2", ref y2, mandatory))
                    return false;
            }
            var z1 = defaultMinZ;
            var z2 = defaultMaxZ;
            ReadInt32(xml, "zmin", ref z1, false);
            ReadInt32(xml, "zmax", ref z2, false);
            value = new Rectangle3D(new Point3D(x1, y1, z1), new Point3D(x2, y2, z2));
            return true;
        }
    }
}