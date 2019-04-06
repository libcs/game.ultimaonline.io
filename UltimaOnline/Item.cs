using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UltimaOnline.ContextMenus;
using UltimaOnline.Items;
using UltimaOnline.Network;

namespace UltimaOnline
{
    /// <summary>
    /// Enumeration of item layer values.
    /// </summary>
    public enum Layer : byte
    {
        /// <summary>
        /// Invalid layer.
        /// </summary>
        Invalid = 0x00,
        /// <summary>
        /// First valid layer. Equivalent to <c>Layer.OneHanded</c>.
        /// </summary>
        FirstValid = 0x01,
        /// <summary>
        /// One handed weapon.
        /// </summary>
        OneHanded = 0x01,
        /// <summary>
        /// Two handed weapon or shield.
        /// </summary>
        TwoHanded = 0x02,
        /// <summary>
        /// Shoes.
        /// </summary>
        Shoes = 0x03,
        /// <summary>
        /// Pants.
        /// </summary>
        Pants = 0x04,
        /// <summary>
        /// Shirts.
        /// </summary>
        Shirt = 0x05,
        /// <summary>
        /// Helmets, hats, and masks.
        /// </summary>
        Helm = 0x06,
        /// <summary>
        /// Gloves.
        /// </summary>
        Gloves = 0x07,
        /// <summary>
        /// Rings.
        /// </summary>
        Ring = 0x08,
        /// <summary>
        /// Talismans.
        /// </summary>
        Talisman = 0x09,
        /// <summary>
        /// Gorgets and necklaces.
        /// </summary>
        Neck = 0x0A,
        /// <summary>
        /// Hair.
        /// </summary>
        Hair = 0x0B,
        /// <summary>
        /// Half aprons.
        /// </summary>
        Waist = 0x0C,
        /// <summary>
        /// Torso, inner layer.
        /// </summary>
        InnerTorso = 0x0D,
        /// <summary>
        /// Bracelets.
        /// </summary>
        Bracelet = 0x0E,
        /// <summary>
        /// Unused.
        /// </summary>
        Unused_xF = 0x0F,
        /// <summary>
        /// Beards and mustaches.
        /// </summary>
        FacialHair = 0x10,
        /// <summary>
        /// Torso, outer layer.
        /// </summary>
        MiddleTorso = 0x11,
        /// <summary>
        /// Earings.
        /// </summary>
        Earrings = 0x12,
        /// <summary>
        /// Arms and sleeves.
        /// </summary>
        Arms = 0x13,
        /// <summary>
        /// Cloaks.
        /// </summary>
        Cloak = 0x14,
        /// <summary>
        /// Backpacks.
        /// </summary>
        Backpack = 0x15,
        /// <summary>
        /// Torso, outer layer.
        /// </summary>
        OuterTorso = 0x16,
        /// <summary>
        /// Leggings, outer layer.
        /// </summary>
        OuterLegs = 0x17,
        /// <summary>
        /// Leggings, inner layer.
        /// </summary>
        InnerLegs = 0x18,
        /// <summary>
        /// Last valid non-internal layer. Equivalent to <c>Layer.InnerLegs</c>.
        /// </summary>
        LastUserValid = 0x18,
        /// <summary>
        /// Mount item layer.
        /// </summary>
        Mount = 0x19,
        /// <summary>
        /// Vendor 'buy pack' layer.
        /// </summary>
        ShopBuy = 0x1A,
        /// <summary>
        /// Vendor 'resale pack' layer.
        /// </summary>
        ShopResale = 0x1B,
        /// <summary>
        /// Vendor 'sell pack' layer.
        /// </summary>
        ShopSell = 0x1C,
        /// <summary>
        /// Bank box layer.
        /// </summary>
        Bank = 0x1D,
        /// <summary>
        /// Last valid layer. Equivalent to <c>Layer.Bank</c>.
        /// </summary>
        LastValid = 0x1D
    }

    /// <summary>
    /// Internal flags used to signal how the item should be updated and resent to nearby clients.
    /// </summary>
    [Flags]
    public enum ItemDelta
    {
        /// <summary>
        /// Nothing.
        /// </summary>
        None = 0x00000000,
        /// <summary>
        /// Resend the item.
        /// </summary>
        Update = 0x00000001,
        /// <summary>
        /// Resend the item only if it is equiped.
        /// </summary>
        EquipOnly = 0x00000002,
        /// <summary>
        /// Resend the item's properties.
        /// </summary>
        Properties = 0x00000004
    }

    /// <summary>
    /// Enumeration containing possible ways to handle item ownership on death.
    /// </summary>
    public enum DeathMoveResult
    {
        /// <summary>
        /// The item should be placed onto the corpse.
        /// </summary>
        MoveToCorpse,
        /// <summary>
        /// The item should remain equiped.
        /// </summary>
        RemainEquiped,
        /// <summary>
        /// The item should be placed into the owners backpack.
        /// </summary>
        MoveToBackpack
    }

    /// <summary>
    /// Enumeration containing all possible light types. These are only applicable to light source items, like lanterns, candles, braziers, etc.
    /// </summary>
    public enum LightType
    {
        /// <summary>
        /// Window shape, arched, ray shining east.
        /// </summary>
        ArchedWindowEast,
        /// <summary>
        /// Medium circular shape.
        /// </summary>
        Circle225,
        /// <summary>
        /// Small circular shape.
        /// </summary>
        Circle150,
        /// <summary>
        /// Door shape, shining south.
        /// </summary>
        DoorSouth,
        /// <summary>
        /// Door shape, shining east.
        /// </summary>
        DoorEast,
        /// <summary>
        /// Large semicircular shape (180 degrees), north wall.
        /// </summary>
        NorthBig,
        /// <summary>
        /// Large pie shape (90 degrees), north-east corner.
        /// </summary>
        NorthEastBig,
        /// <summary>
        /// Large semicircular shape (180 degrees), east wall.
        /// </summary>
        EastBig,
        /// <summary>
        /// Large semicircular shape (180 degrees), west wall.
        /// </summary>
        WestBig,
        /// <summary>
        /// Large pie shape (90 degrees), south-west corner.
        /// </summary>
        SouthWestBig,
        /// <summary>
        /// Large semicircular shape (180 degrees), south wall.
        /// </summary>
        SouthBig,
        /// <summary>
        /// Medium semicircular shape (180 degrees), north wall.
        /// </summary>
        NorthSmall,
        /// <summary>
        /// Medium pie shape (90 degrees), north-east corner.
        /// </summary>
        NorthEastSmall,
        /// <summary>
        /// Medium semicircular shape (180 degrees), east wall.
        /// </summary>
        EastSmall,
        /// <summary>
        /// Medium semicircular shape (180 degrees), west wall.
        /// </summary>
        WestSmall,
        /// <summary>
        /// Medium semicircular shape (180 degrees), south wall.
        /// </summary>
        SouthSmall,
        /// <summary>
        /// Shaped like a wall decoration, north wall.
        /// </summary>
        DecorationNorth,
        /// <summary>
        /// Shaped like a wall decoration, north-east corner.
        /// </summary>
        DecorationNorthEast,
        /// <summary>
        /// Small semicircular shape (180 degrees), east wall.
        /// </summary>
        EastTiny,
        /// <summary>
        /// Shaped like a wall decoration, west wall.
        /// </summary>
        DecorationWest,
        /// <summary>
        /// Shaped like a wall decoration, south-west corner.
        /// </summary>
        DecorationSouthWest,
        /// <summary>
        /// Small semicircular shape (180 degrees), south wall.
        /// </summary>
        SouthTiny,
        /// <summary>
        /// Window shape, rectangular, no ray, shining south.
        /// </summary>
        RectWindowSouthNoRay,
        /// <summary>
        /// Window shape, rectangular, no ray, shining east.
        /// </summary>
        RectWindowEastNoRay,
        /// <summary>
        /// Window shape, rectangular, ray shining south.
        /// </summary>
        RectWindowSouth,
        /// <summary>
        /// Window shape, rectangular, ray shining east.
        /// </summary>
        RectWindowEast,
        /// <summary>
        /// Window shape, arched, no ray, shining south.
        /// </summary>
        ArchedWindowSouthNoRay,
        /// <summary>
        /// Window shape, arched, no ray, shining east.
        /// </summary>
        ArchedWindowEastNoRay,
        /// <summary>
        /// Window shape, arched, ray shining south.
        /// </summary>
        ArchedWindowSouth,
        /// <summary>
        /// Large circular shape.
        /// </summary>
        Circle300,
        /// <summary>
        /// Large pie shape (90 degrees), north-west corner.
        /// </summary>
        NorthWestBig,
        /// <summary>
        /// Negative light. Medium pie shape (90 degrees), south-east corner.
        /// </summary>
        DarkSouthEast,
        /// <summary>
        /// Negative light. Medium semicircular shape (180 degrees), south wall.
        /// </summary>
        DarkSouth,
        /// <summary>
        /// Negative light. Medium pie shape (90 degrees), north-west corner.
        /// </summary>
        DarkNorthWest,
        /// <summary>
        /// Negative light. Medium pie shape (90 degrees), south-east corner. Equivalent to <c>LightType.SouthEast</c>.
        /// </summary>
        DarkSouthEast2,
        /// <summary>
        /// Negative light. Medium circular shape (180 degrees), east wall.
        /// </summary>
        DarkEast,
        /// <summary>
        /// Negative light. Large circular shape.
        /// </summary>
        DarkCircle300,
        /// <summary>
        /// Opened door shape, shining south.
        /// </summary>
        DoorOpenSouth,
        /// <summary>
        /// Opened door shape, shining east.
        /// </summary>
        DoorOpenEast,
        /// <summary>
        /// Window shape, square, ray shining east.
        /// </summary>
        SquareWindowEast,
        /// <summary>
        /// Window shape, square, no ray, shining east.
        /// </summary>
        SquareWindowEastNoRay,
        /// <summary>
        /// Window shape, square, ray shining south.
        /// </summary>
        SquareWindowSouth,
        /// <summary>
        /// Window shape, square, no ray, shining south.
        /// </summary>
        SquareWindowSouthNoRay,
        /// <summary>
        /// Empty.
        /// </summary>
        Empty,
        /// <summary>
        /// Window shape, skinny, no ray, shining south.
        /// </summary>
        SkinnyWindowSouthNoRay,
        /// <summary>
        /// Window shape, skinny, ray shining east.
        /// </summary>
        SkinnyWindowEast,
        /// <summary>
        /// Window shape, skinny, no ray, shining east.
        /// </summary>
        SkinnyWindowEastNoRay,
        /// <summary>
        /// Shaped like a hole, shining south.
        /// </summary>
        HoleSouth,
        /// <summary>
        /// Shaped like a hole, shining south.
        /// </summary>
        HoleEast,
        /// <summary>
        /// Large circular shape with a moongate graphic embeded.
        /// </summary>
        Moongate,
        /// <summary>
        /// Unknown usage. Many rows of slightly angled lines.
        /// </summary>
        Strips,
        /// <summary>
        /// Shaped like a small hole, shining south.
        /// </summary>
        SmallHoleSouth,
        /// <summary>
        /// Shaped like a small hole, shining east.
        /// </summary>
        SmallHoleEast,
        /// <summary>
        /// Large semicircular shape (180 degrees), north wall. Identical graphic as <c>LightType.NorthBig</c>, but slightly different positioning.
        /// </summary>
        NorthBig2,
        /// <summary>
        /// Large semicircular shape (180 degrees), west wall. Identical graphic as <c>LightType.WestBig</c>, but slightly different positioning.
        /// </summary>
        WestBig2,
        /// <summary>
        /// Large pie shape (90 degrees), north-west corner. Equivalent to <c>LightType.NorthWestBig</c>.
        /// </summary>
        NorthWestBig2
    }

    /// <summary>
    /// Enumeration of an item's loot and steal state.
    /// </summary>
    public enum LootType : byte
    {
        /// <summary>
        /// Stealable. Lootable.
        /// </summary>
        Regular = 0,
        /// <summary>
        /// Unstealable. Unlootable, unless owned by a murderer.
        /// </summary>
        Newbied = 1,
        /// <summary>
        /// Unstealable. Unlootable, always.
        /// </summary>
        Blessed = 2,
        /// <summary>
        /// Stealable. Lootable, always.
        /// </summary>
        Cursed = 3
    }

    public class BounceInfo
    {
        public BounceInfo(Item item)
        {
            Map = item.Map;
            Location = item.Location;
            WorldLoc = item.GetWorldLocation();
            Parent = item.Parent;
        }
        BounceInfo(Map map, Point3D loc, Point3D worldLoc, IEntity parent)
        {
            Map = map;
            Location = loc;
            WorldLoc = worldLoc;
            Parent = parent;
        }

        public Map Map;
        public Point3D Location, WorldLoc;
        public IEntity Parent;

        public static BounceInfo Deserialize(GenericReader r)
        {
            if (r.ReadBool())
            {
                var map = r.ReadMap();
                var loc = r.ReadPoint3D();
                var worldLoc = r.ReadPoint3D();
                IEntity parent;
                Serial serial = r.ReadInt();
                if (serial.IsItem) parent = World.FindItem(serial);
                else if (serial.IsMobile) parent = World.FindMobile(serial);
                else parent = null;
                return new BounceInfo(map, loc, worldLoc, parent);
            }
            else return null;
        }

        public static void Serialize(BounceInfo info, GenericWriter w)
        {
            if (info == null)
                w.Write(false);
            else
            {
                w.Write(true);
                w.Write(info.Map);
                w.Write(info.Location);
                w.Write(info.WorldLoc);
                if (info.Parent is Mobile) w.Write((Mobile)info.Parent);
                else if (info.Parent is Item) w.Write((Item)info.Parent);
                else w.Write((Serial)0);
            }
        }
    }

    public enum TotalType
    {
        Gold,
        Items,
        Weight,
    }

    [Flags]
    public enum ExpandFlag
    {
        None = 0x000,

        Name = 0x001,
        Items = 0x002,
        Bounce = 0x004,
        Holder = 0x008,
        Blessed = 0x010,
        TempFlag = 0x020,
        SaveFlag = 0x040,
        Weight = 0x080,
        Spawner = 0x100
    }

    public class Item : IEntity, IHued, IComparable<Item>, ISerializable, ISpawnable
    {
        public static readonly List<Item> EmptyItems = new List<Item>();

        public int CompareTo(IEntity other) => other == null ? -1 : Serial.CompareTo(other.Serial);

        public int CompareTo(Item other) => CompareTo((IEntity)other);

        public int CompareTo(object other)
        {
            if (other == null || other is IEntity)
                return CompareTo((IEntity)other);
            throw new ArgumentException();
        }

        Point3D _Location;
        int _ItemID;
        int _Hue;
        int _Amount;
        Layer _Layer;
        IEntity _Parent; // Mobile, Item, or null=World
        Map _Map;
        LootType _LootType;
        Direction _Direction;

        ItemDelta _DeltaFlags;
        ImplFlag _Flags;

        #region Packet caches
        Packet _WorldPacket;
        Packet _WorldPacketSA;
        Packet _WorldPacketHS;
        Packet _RemovePacket;
        Packet _OPLPacket;
        ObjectPropertyList _PropertyList;
        #endregion

        public int TempFlags
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null ? info.TempFlags : 0;
            }
            set
            {
                var info = AcquireCompactInfo();
                info.TempFlags = value;
                if (info.TempFlags == 0)
                    VerifyCompactInfo();
            }
        }

        public int SavedFlags
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null ? info.SavedFlags : 0;
            }
            set
            {
                var info = AcquireCompactInfo();
                info.SavedFlags = value;
                if (info.SavedFlags == 0)
                    VerifyCompactInfo();
            }
        }

        /// <summary>
        /// The <see cref="Mobile" /> who is currently <see cref="Mobile.Holding">holding</see> this item.
        /// </summary>
        public Mobile HeldBy
        {
            get
            {
                var info = LookupCompactInfo();
                return info?.HeldBy;
            }
            set
            {
                var info = AcquireCompactInfo();
                info.HeldBy = value;
                if (info.HeldBy == null)
                    VerifyCompactInfo();
            }
        }

        [Flags]
        enum ImplFlag : byte
        {
            None = 0x00,
            Visible = 0x01,
            Movable = 0x02,
            Deleted = 0x04,
            Stackable = 0x08,
            InQueue = 0x10,
            Insured = 0x20,
            PayedInsurance = 0x40,
            QuestItem = 0x80
        }

        class CompactInfo
        {
            public string Name;
            public List<Item> Items;
            public BounceInfo Bounce;
            public Mobile HeldBy;
            public Mobile BlessedFor;
            public ISpawner Spawner;
            public int TempFlags;
            public int SavedFlags;
            public double Weight = -1;
        }

        CompactInfo _compactInfo;

        public ExpandFlag GetExpandFlags()
        {
            var info = LookupCompactInfo();
            ExpandFlag flags = 0;
            if (info != null)
            {
                if (info.BlessedFor != null) flags |= ExpandFlag.Blessed;
                if (info.Bounce != null) flags |= ExpandFlag.Bounce;
                if (info.HeldBy != null) flags |= ExpandFlag.Holder;
                if (info.Items != null) flags |= ExpandFlag.Items;
                if (info.Name != null) flags |= ExpandFlag.Name;
                if (info.Spawner != null) flags |= ExpandFlag.Spawner;
                if (info.SavedFlags != 0) flags |= ExpandFlag.SaveFlag;
                if (info.TempFlags != 0) flags |= ExpandFlag.TempFlag;
                if (info.Weight != -1) flags |= ExpandFlag.Weight;
            }
            return flags;
        }

        CompactInfo LookupCompactInfo() => _compactInfo;

        CompactInfo AcquireCompactInfo()
        {
            if (_compactInfo == null)
                _compactInfo = new CompactInfo();
            return _compactInfo;
        }

        void ReleaseCompactInfo() => _compactInfo = null;

        void VerifyCompactInfo()
        {
            var info = _compactInfo;
            if (info == null)
                return;
            var isValid = info.Name != null
                || info.Items != null
                || info.Bounce != null
                || info.HeldBy != null
                || info.BlessedFor != null
                || info.Spawner != null
                || info.TempFlags != 0
                || info.SavedFlags != 0
                || info.Weight != -1;
            if (!isValid)
                ReleaseCompactInfo();
        }

        public List<Item> LookupItems()
        {
            if (this is Container cont)
                return cont._Items;
            var info = LookupCompactInfo();
            return info?.Items;
        }

        public List<Item> AcquireItems()
        {
            if (this is Container c)
            {
                if (c._Items == null)
                    c._Items = new List<Item>();
                return c._Items;
            }
            var info = AcquireCompactInfo();
            if (info.Items == null)
                info.Items = new List<Item>();
            return info.Items;
        }

        void SetFlag(ImplFlag flag, bool value)
        {
            if (value) _Flags |= flag;
            else _Flags &= ~flag;
        }

        bool GetFlag(ImplFlag flag) => (_Flags & flag) != 0;

        public BounceInfo GetBounce()
        {
            var info = LookupCompactInfo();
            return info?.Bounce;
        }

        public void RecordBounce()
        {
            var info = AcquireCompactInfo();
            info.Bounce = new BounceInfo(this);
        }

        public void ClearBounce()
        {
            var info = LookupCompactInfo();
            if (info != null)
            {
                var bounce = info.Bounce;
                if (bounce != null)
                {
                    info.Bounce = null;
                    if (bounce.Parent is Item i)
                    {
                        if (!i.Deleted)
                            i.OnItemBounceCleared(this);
                    }
                    else if (bounce.Parent is Mobile m)
                    {
                        if (!m.Deleted)
                            m.OnItemBounceCleared(this);
                    }
                    VerifyCompactInfo();
                }
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a client, <paramref name="from" />, invokes a 'help request' for the Item. Seemingly no longer functional in newer clients.
        /// </summary>
        public virtual void OnHelpRequest(Mobile from) { }

        /// <summary>
        /// Overridable. Method checked to see if the item can be traded.
        /// </summary>
        /// <returns>True if the trade is allowed, false if not.</returns>
        public virtual bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => true;

        /// <summary>
        /// Overridable. Virtual event invoked when a trade has completed, either successfully or not.
        /// </summary>
        public virtual void OnSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) { }

        /// <summary>
        /// Overridable. Method checked to see if the elemental resistances of this Item conflict with another Item on the <see cref="Mobile" />.
        /// </summary>
        /// <returns>
        /// <list type="table">
        /// <item>
        /// <term>True</term>
        /// <description>There is a confliction. The elemental resistance bonuses of this Item should not be applied to the <see cref="Mobile" /></description>
        /// </item>
        /// <item>
        /// <term>False</term>
        /// <description>There is no confliction. The bonuses should be applied.</description>
        /// </item>
        /// </list>
        /// </returns>
        public virtual bool CheckPropertyConfliction(Mobile m) => false;

        /// <summary>
        /// Overridable. Sends the <see cref="PropertyList">object property list</see> to <paramref name="from" />.
        /// </summary>
        public virtual void SendPropertiesTo(Mobile from) => from.Send(PropertyList);

        /// <summary>
        /// Overridable. Adds the name of this item to the given <see cref="ObjectPropertyList" />. This method should be overriden if the item requires a complex naming format.
        /// </summary>
        public virtual void AddNameProperty(ObjectPropertyList list)
        {
            var name = Name;
            if (name == null)
            {
                if (_Amount <= 1) list.Add(LabelNumber);
                else list.Add(1050039, "{0}\t#{1}", _Amount, LabelNumber); // ~1_NUMBER~ ~2_ITEMNAME~
            }
            else
            {
                if (_Amount <= 1) list.Add(name);
                else list.Add(1050039, "{0}\t{1}", _Amount, Name); // ~1_NUMBER~ ~2_ITEMNAME~
            }
        }

        /// <summary>
        /// Overridable. Adds the loot type of this item to the given <see cref="ObjectPropertyList" />. By default, this will be either 'blessed', 'cursed', or 'insured'.
        /// </summary>
        public virtual void AddLootTypeProperty(ObjectPropertyList list)
        {
            if (_LootType == LootType.Blessed) list.Add(1038021); // blessed
            else if (_LootType == LootType.Cursed) list.Add(1049643); // cursed
            else if (Insured) list.Add(1061682); // <b>insured</b>
        }

        /// <summary>
        /// Overridable. Adds any elemental resistances of this item to the given <see cref="ObjectPropertyList" />.
        /// </summary>
        public virtual void AddResistanceProperties(ObjectPropertyList list)
        {
            int v = PhysicalResistance;
            if (v != 0) list.Add(1060448, v.ToString()); // physical resist ~1_val~%
            v = FireResistance;
            if (v != 0) list.Add(1060447, v.ToString()); // fire resist ~1_val~%
            v = ColdResistance;
            if (v != 0) list.Add(1060445, v.ToString()); // cold resist ~1_val~%
            v = PoisonResistance;
            if (v != 0) list.Add(1060449, v.ToString()); // poison resist ~1_val~%
            v = EnergyResistance;
            if (v != 0) list.Add(1060446, v.ToString()); // energy resist ~1_val~%
        }

        /// <summary>
        /// Overridable. Determines whether the item will show <see cref="AddWeightProperty" />. 
        /// </summary>
        public virtual bool DisplayWeight
        {
            get
            {
                if (!Core.ML) return false;
                if (!Movable && !(IsLockedDown || IsSecure) && ItemData.Weight == 255) return false;
                return true;
            }
        }

        /// <summary>
        /// Overridable. Displays cliloc 1072788-1072789. 
        /// </summary>
        public virtual void AddWeightProperty(ObjectPropertyList list)
        {
            var weight = PileWeight + TotalWeight;
            if (weight == 1) list.Add(1072788, weight.ToString()); //Weight: ~1_WEIGHT~ stone
            else list.Add(1072789, weight.ToString()); //Weight: ~1_WEIGHT~ stones
        }

        /// <summary>
        /// Overridable. Adds header properties. By default, this invokes <see cref="AddNameProperty" />, <see cref="AddBlessedForProperty" /> (if applicable), and <see cref="AddLootTypeProperty" /> (if <see cref="DisplayLootType" />).
        /// </summary>
        public virtual void AddNameProperties(ObjectPropertyList list)
        {
            AddNameProperty(list);
            if (IsSecure) AddSecureProperty(list);
            else if (IsLockedDown) AddLockedDownProperty(list);
            var blessedFor = BlessedFor;
            if (blessedFor != null && !blessedFor.Deleted) AddBlessedForProperty(list, blessedFor);
            if (DisplayLootType) AddLootTypeProperty(list);
            if (DisplayWeight) AddWeightProperty(list);
            if (QuestItem) AddQuestItemProperty(list);
            AppendChildNameProperties(list);
        }

        /// <summary>
        /// Overridable. Adds the "Quest Item" property to the given <see cref="ObjectPropertyList" />.
        /// </summary>
        public virtual void AddQuestItemProperty(ObjectPropertyList list) => list.Add(1072351); // Quest Item

        /// <summary>
        /// Overridable. Adds the "Locked Down & Secure" property to the given <see cref="ObjectPropertyList" />.
        /// </summary>
        public virtual void AddSecureProperty(ObjectPropertyList list) => list.Add(501644); // locked down & secure

        /// <summary>
        /// Overridable. Adds the "Locked Down" property to the given <see cref="ObjectPropertyList" />.
        /// </summary>
        public virtual void AddLockedDownProperty(ObjectPropertyList list) => list.Add(501643); // locked down

        /// <summary>
        /// Overridable. Adds the "Blessed for ~1_NAME~" property to the given <see cref="ObjectPropertyList" />.
        /// </summary>
        public virtual void AddBlessedForProperty(ObjectPropertyList list, Mobile m) => list.Add(1062203, "{0}", m.Name); // Blessed for ~1_NAME~

        /// <summary>
        /// Overridable. Fills an <see cref="ObjectPropertyList" /> with everything applicable. By default, this invokes <see cref="AddNameProperties" />, then <see cref="Item.GetChildProperties">Item.GetChildProperties</see> or <see cref="Mobile.GetChildProperties">Mobile.GetChildProperties</see>. This method should be overriden to add any custom properties.
        /// </summary>
        public virtual void GetProperties(ObjectPropertyList list) => AddNameProperties(list);

        /// <summary>
        /// Overridable. Event invoked when a child (<paramref name="item" />) is building it's <see cref="ObjectPropertyList" />. Recursively calls <see cref="Item.GetChildProperties">Item.GetChildProperties</see> or <see cref="Mobile.GetChildProperties">Mobile.GetChildProperties</see>.
        /// </summary>
        public virtual void GetChildProperties(ObjectPropertyList list, Item item)
        {
            if (_Parent is Item i) i.GetChildProperties(list, item);
            else if (_Parent is Mobile m) m.GetChildProperties(list, item);
        }

        /// <summary>
        /// Overridable. Event invoked when a child (<paramref name="item" />) is building it's Name <see cref="ObjectPropertyList" />. Recursively calls <see cref="Item.GetChildProperties">Item.GetChildNameProperties</see> or <see cref="Mobile.GetChildProperties">Mobile.GetChildNameProperties</see>.
        /// </summary>
        public virtual void GetChildNameProperties(ObjectPropertyList list, Item item)
        {
            if (_Parent is Item i) i.GetChildNameProperties(list, item);
            else if (_Parent is Mobile m) m.GetChildNameProperties(list, item);
        }

        public virtual bool IsChildVisibleTo(Mobile m, Item child) => true;

        public void Bounce(Mobile from)
        {
            if (_Parent is Item i) i.RemoveItem(this);
            else if (_Parent is Mobile m) m.RemoveItem(this);
            _Parent = null;
            var bounce = GetBounce();
            if (bounce != null)
            {
                var parent = bounce.Parent;
                if (parent == null || parent.Deleted)
                    MoveToWorld(bounce.WorldLoc, bounce.Map);
                else if (parent is Item i2)
                {
                    var root = i2.RootParent;
                    if (i2.IsAccessibleTo(from) && (!(root is Mobile) || ((Mobile)root).CheckNonlocalDrop(from, this, i2)))
                    {
                        Location = bounce.Location;
                        i2.AddItem(this);
                    }
                    else MoveToWorld(from.Location, from.Map);
                }
                else if (parent is Mobile m2)
                {
                    if (!m2.EquipItem(this))
                        MoveToWorld(bounce.WorldLoc, bounce.Map);
                }
                else MoveToWorld(bounce.WorldLoc, bounce.Map);
                ClearBounce();
            }
            else MoveToWorld(from.Location, from.Map);
        }

        /// <summary>
        /// Overridable. Method checked to see if this item may be equiped while casting a spell. By default, this returns false. It is overriden on spellbook and spell channeling weapons or shields.
        /// </summary>
        /// <returns>True if it may, false if not.</returns>
        /// <example>
        /// <code>
        ///	public override bool AllowEquipedCast(Mobile from) => from.Int &gt;= 100 ? true : base.AllowEquipedCast(from);
        /// </code>
        /// When placed in an Item script, the item may be cast when equiped if the <paramref name="from" /> has 100 or more intelligence. Otherwise, it will drop to their backpack.
        /// </example>
        public virtual bool AllowEquipedCast(Mobile from) => false;

        public virtual bool CheckConflictingLayer(Mobile m, Item item, Layer layer) => _Layer == layer;

        public virtual bool CanEquip(Mobile m) => _Layer != Layer.Invalid && m.FindItemOnLayer(_Layer) == null;

        public virtual void GetChildContextMenuEntries(Mobile from, List<ContextMenuEntry> list, Item item)
        {
            if (_Parent is Item i) i.GetChildContextMenuEntries(from, list, item);
            else if (_Parent is Mobile m) m.GetChildContextMenuEntries(from, list, item);
        }

        public virtual void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            if (_Parent is Item i) i.GetChildContextMenuEntries(from, list, this);
            else if (_Parent is Mobile m) m.GetChildContextMenuEntries(from, list, this);
        }

        public virtual bool VerifyMove(Mobile from) => Movable;

        public virtual DeathMoveResult OnParentDeath(Mobile parent)
        {
            if (!Movable) return DeathMoveResult.RemainEquiped;
            else if (parent.KeepsItemsOnDeath) return DeathMoveResult.MoveToBackpack;
            else if (CheckBlessed(parent)) return DeathMoveResult.MoveToBackpack;
            else if (CheckNewbied() && parent.Kills < 5) return DeathMoveResult.MoveToBackpack;
            else if (parent.Player && Nontransferable) return DeathMoveResult.MoveToBackpack;
            else return DeathMoveResult.MoveToCorpse;
        }

        public virtual DeathMoveResult OnInventoryDeath(Mobile parent)
        {
            if (!Movable) return DeathMoveResult.MoveToBackpack;
            else if (parent.KeepsItemsOnDeath) return DeathMoveResult.MoveToBackpack;
            else if (CheckBlessed(parent)) return DeathMoveResult.MoveToBackpack;
            else if (CheckNewbied() && parent.Kills < 5) return DeathMoveResult.MoveToBackpack;
            else if (parent.Player && Nontransferable) return DeathMoveResult.MoveToBackpack;
            else return DeathMoveResult.MoveToCorpse;
        }

        /// <summary>
        /// Moves the Item to <paramref name="location" />. The Item does not change maps.
        /// </summary>
        public virtual void MoveToWorld(Point3D location) => MoveToWorld(location, _Map);

        public void LabelTo(Mobile to, int number) => to.Send(new MessageLocalized(Serial, _ItemID, MessageType.Label, 0x3B2, 3, number, string.Empty, string.Empty));
        public void LabelTo(Mobile to, int number, string args) => to.Send(new MessageLocalized(Serial, _ItemID, MessageType.Label, 0x3B2, 3, number, string.Empty, args));
        public void LabelTo(Mobile to, string text) => to.Send(new UnicodeMessage(Serial, _ItemID, MessageType.Label, 0x3B2, 3, "ENU", string.Empty, text));
        public void LabelTo(Mobile to, string format, params object[] args) => LabelTo(to, string.Format(format, args));

        public void LabelToAffix(Mobile to, int number, AffixType type, string affix, string args = null) => to.Send(new MessageLocalizedAffix(Serial, _ItemID, MessageType.Label, 0x3B2, 3, number, string.Empty, type, affix, args ?? string.Empty));

        public virtual void LabelLootTypeTo(Mobile to)
        {
            if (_LootType == LootType.Blessed) LabelTo(to, 1041362); // (blessed)
            else if (_LootType == LootType.Cursed) LabelTo(to, "(cursed)");
        }

        public bool AtWorldPoint(int x, int y) => _Parent == null && _Location.X == x && _Location.Y == y;

        public bool AtPoint(int x, int y) => _Location.X == x && _Location.Y == y;

        /// <summary>
        /// Moves the Item to a given <paramref name="location" /> and <paramref name="map" />.
        /// </summary>
        public void MoveToWorld(Point3D location, Map map)
        {
            if (Deleted)
                return;
            var oldLocation = GetWorldLocation();
            var oldRealLocation = _Location;
            SetLastMoved();
            if (Parent is Mobile m) m.RemoveItem(this);
            else if (Parent is Item i) i.RemoveItem(this);
            if (_Map != map)
            {
                var old = _Map;
                if (_Map != null)
                {
                    _Map.OnLeave(this);
                    if (oldLocation.X != 0)
                    {
                        var eable = _Map.GetClientsInRange(oldLocation, GetMaxUpdateRange());
                        foreach (var state in eable)
                        {
                            var m2 = state.Mobile;
                            if (m2.InRange(oldLocation, GetUpdateRange(m2)))
                                state.Send(RemovePacket);
                        }
                        eable.Free();
                    }
                }
                _Location = location;
                OnLocationChange(oldRealLocation);
                ReleaseWorldPackets();
                var items = LookupItems();
                if (items != null)
                    for (var i = 0; i < items.Count; ++i)
                        items[i].Map = map;
                _Map = map;
                if (_Map != null)
                    _Map.OnEnter(this);
                OnMapChange();
                if (_Map != null)
                {
                    var eable = _Map.GetClientsInRange(_Location, GetMaxUpdateRange());
                    foreach (var state in eable)
                    {
                        var m2 = state.Mobile;
                        if (m2.CanSee(this) && m2.InRange(_Location, GetUpdateRange(m2)))
                            SendInfoTo(state);
                    }
                    eable.Free();
                }
                RemDelta(ItemDelta.Update);
                if (old == null || old == Map.Internal)
                    InvalidateProperties();
            }
            else if (_Map != null)
            {
                IPooledEnumerable<NetState> eable;
                if (oldLocation.X != 0)
                {
                    eable = _Map.GetClientsInRange(oldLocation, GetMaxUpdateRange());
                    foreach (var state in eable)
                    {
                        var m2 = state.Mobile;
                        if (!m2.InRange(location, GetUpdateRange(m2)))
                            state.Send(RemovePacket);
                    }
                    eable.Free();
                }
                var oldInternalLocation = _Location;
                _Location = location;
                OnLocationChange(oldRealLocation);
                ReleaseWorldPackets();
                eable = _Map.GetClientsInRange(_Location, GetMaxUpdateRange());
                foreach (NetState state in eable)
                {
                    var m2 = state.Mobile;
                    if (m2.CanSee(this) && m2.InRange(_Location, GetUpdateRange(m2)))
                        SendInfoTo(state);
                }
                eable.Free();
                _Map.OnMove(oldInternalLocation, this);
                RemDelta(ItemDelta.Update);
            }
            else
            {
                Map = map;
                Location = location;
            }
        }

        /// <summary>
        /// Has the item been deleted?
        /// </summary>
        public bool Deleted => GetFlag(ImplFlag.Deleted);

        [CommandProperty(AccessLevel.GameMaster)]
        public LootType LootType
        {
            get => _LootType;
            set
            {
                if (_LootType != value)
                {
                    _LootType = value;
                    if (DisplayLootType)
                        InvalidateProperties();
                }
            }
        }

        public static TimeSpan DefaultDecayTime { get; set; } = TimeSpan.FromHours(1.0);

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual TimeSpan DecayTime => DefaultDecayTime;

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual bool Decays => Movable && Visible/* && Spawner == null*/; // TODO: Make item decay an option on the spawner

        public virtual bool OnDecay() => Decays && Parent == null && Map != Map.Internal && Region.Find(Location, Map).OnDecay(this);

        public void SetLastMoved() => LastMoved = DateTime.UtcNow;

        public DateTime LastMoved { get; set; }

        public virtual bool StackWith(Mobile from, Item dropped, bool playSound = true)
        {
            if (dropped.Stackable && Stackable && dropped.GetType() == GetType() && dropped.ItemID == ItemID && dropped.Hue == Hue && dropped.Name == Name && (dropped.Amount + Amount) <= 60000 && dropped != this && !dropped.Nontransferable && !Nontransferable)
            {
                if (_LootType != dropped._LootType)
                    _LootType = LootType.Regular;
                Amount += dropped.Amount;
                dropped.Delete();
                if (playSound && from != null)
                {
                    var soundID = GetDropSound();
                    if (soundID == -1)
                        soundID = 0x42;
                    from.SendSound(soundID, GetWorldLocation());
                }
                return true;
            }
            return false;
        }

        public virtual bool OnDragDrop(Mobile from, Item dropped) => Parent is Container c ? c.OnStackAttempt(from, this, dropped) : StackWith(from, dropped);

        public Rectangle2D GetGraphicBounds()
        {
            var itemID = _ItemID;
            var doubled = _Amount > 1;
            if (itemID >= 0xEEA && itemID <= 0xEF2) // Are we coins?
            {
                var coinBase = (itemID - 0xEEA) / 3;
                coinBase *= 3;
                coinBase += 0xEEA;
                doubled = false;
                if (_Amount <= 1) itemID = coinBase; // A single coin
                else if (_Amount <= 5) itemID = coinBase + 1; // A stack of coins
                else itemID = coinBase + 2; // A pile of coins
            }
            var bounds = ItemBounds.Table[itemID & 0x3FFF];
            if (doubled)
                bounds.Set(bounds.X, bounds.Y, bounds.Width + 5, bounds.Height + 5);
            return bounds;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Stackable
        {
            get => GetFlag(ImplFlag.Stackable);
            set => SetFlag(ImplFlag.Stackable, value);
        }

        private object _rpl = new object();

        public Packet RemovePacket
        {
            get
            {
                if (_RemovePacket == null)
                    lock (_rpl)
                        if (_RemovePacket == null)
                        {
                            _RemovePacket = new RemoveItem(this);
                            _RemovePacket.SetStatic();
                        }
                return _RemovePacket;
            }
        }

        object _opll = new object();
        public Packet OPLPacket
        {
            get
            {
                if (_OPLPacket == null)
                    lock (_opll)
                        if (_OPLPacket == null)
                        {
                            _OPLPacket = new OPLInfo(PropertyList);
                            _OPLPacket.SetStatic();
                        }
                return _OPLPacket;
            }
        }

        public ObjectPropertyList PropertyList
        {
            get
            {
                if (_PropertyList == null)
                {
                    _PropertyList = new ObjectPropertyList(this);
                    GetProperties(_PropertyList);
                    AppendChildProperties(_PropertyList);
                    _PropertyList.Terminate();
                    _PropertyList.SetStatic();
                }
                return _PropertyList;
            }
        }

        public virtual void AppendChildProperties(ObjectPropertyList list)
        {
            if (_Parent is Item i) i.GetChildProperties(list, this);
            else if (_Parent is Mobile m) m.GetChildProperties(list, this);
        }

        public virtual void AppendChildNameProperties(ObjectPropertyList list)
        {
            if (_Parent is Item i) i.GetChildNameProperties(list, this);
            else if (_Parent is Mobile m) m.GetChildNameProperties(list, this);
        }

        public void ClearProperties()
        {
            Packet.Release(ref _PropertyList);
            Packet.Release(ref _OPLPacket);
        }

        public void InvalidateProperties()
        {
            if (!ObjectPropertyList.Enabled)
                return;
            if (_Map != null && _Map != Map.Internal && !World.Loading)
            {
                var oldList = _PropertyList;
                _PropertyList = null;
                var newList = PropertyList;
                if (oldList == null || oldList.Hash != newList.Hash)
                {
                    Packet.Release(ref _OPLPacket);
                    Delta(ItemDelta.Properties);
                }
            }
            else ClearProperties();
        }

        object _wpl = new object();
        object _wplsa = new object();
        object _wplhs = new object();

        public Packet WorldPacket
        {
            get
            {
                // This needs to be invalidated when any of the following changes:
                //  - ItemID
                //  - Amount
                //  - Location
                //  - Hue
                //  - Packet Flags
                //  - Direction
                if (_WorldPacket == null)
                    lock (_wpl)
                        if (_WorldPacket == null)
                        {
                            _WorldPacket = new WorldItem(this);
                            _WorldPacket.SetStatic();
                        }
                return _WorldPacket;
            }
        }

        public Packet WorldPacketSA
        {
            get
            {
                // This needs to be invalidated when any of the following changes:
                //  - ItemID
                //  - Amount
                //  - Location
                //  - Hue
                //  - Packet Flags
                //  - Direction
                if (_WorldPacketSA == null)
                    lock (_wplsa)
                        if (_WorldPacketSA == null)
                        {
                            _WorldPacketSA = new WorldItemSA(this);
                            _WorldPacketSA.SetStatic();
                        }
                return _WorldPacketSA;
            }
        }

        public Packet WorldPacketHS
        {
            get
            {
                // This needs to be invalidated when any of the following changes:
                //  - ItemID
                //  - Amount
                //  - Location
                //  - Hue
                //  - Packet Flags
                //  - Direction
                if (_WorldPacketHS == null)
                    lock (_wplhs)
                        if (_WorldPacketHS == null)
                        {
                            _WorldPacketHS = new WorldItemHS(this);
                            _WorldPacketHS.SetStatic();
                        }
                return _WorldPacketHS;
            }
        }

        public void ReleaseWorldPackets()
        {
            Packet.Release(ref _WorldPacket);
            Packet.Release(ref _WorldPacketSA);
            Packet.Release(ref _WorldPacketHS);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Visible
        {
            get => GetFlag(ImplFlag.Visible);
            set
            {
                if (GetFlag(ImplFlag.Visible) != value)
                {
                    SetFlag(ImplFlag.Visible, value);
                    ReleaseWorldPackets();
                    if (_Map != null)
                    {
                        var worldLoc = GetWorldLocation();
                        var eable = _Map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                        foreach (var state in eable)
                        {
                            var m = state.Mobile;
                            if (!m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                                state.Send(RemovePacket);
                        }
                        eable.Free();
                    }
                    Delta(ItemDelta.Update);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Movable
        {
            get => GetFlag(ImplFlag.Movable);
            set
            {
                if (GetFlag(ImplFlag.Movable) != value)
                {
                    SetFlag(ImplFlag.Movable, value);
                    ReleaseWorldPackets();
                    Delta(ItemDelta.Update);
                }
            }
        }

        public virtual bool ForceShowProperties => false;

        public virtual int GetPacketFlags()
        {
            var flags = 0;
            if (!Visible)
                flags |= 0x80;
            if (Movable || ForceShowProperties)
                flags |= 0x20;
            return flags;
        }

        public virtual bool OnMoveOff(Mobile m) => true;

        public virtual bool OnMoveOver(Mobile m) => true;

        public virtual bool HandlesOnMovement => false;

        public virtual void OnMovement(Mobile m, Point3D oldLocation) { }

        public void Internalize() => MoveToWorld(Point3D.Zero, Map.Internal);

        public virtual void OnMapChange() { }

        public virtual void OnRemoved(IEntity parent) { }

        public virtual void OnAdded(IEntity parent) { }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public Map Map
        {
            get => _Map;
            set
            {
                if (_Map != value)
                {
                    var old = _Map;
                    if (_Map != null && _Parent == null)
                    {
                        _Map.OnLeave(this);
                        SendRemovePacket();
                    }
                    var items = LookupItems();
                    if (items != null)
                        for (var i = 0; i < items.Count; ++i)
                            items[i].Map = value;
                    _Map = value;
                    if (_Map != null && _Parent == null)
                        _Map.OnEnter(this);
                    Delta(ItemDelta.Update);
                    OnMapChange();
                    if (old == null || old == Map.Internal)
                        InvalidateProperties();
                }
            }
        }

        [Flags]
        enum SaveFlag
        {
            None = 0x00000000,
            Direction = 0x00000001,
            Bounce = 0x00000002,
            LootType = 0x00000004,
            LocationFull = 0x00000008,
            ItemID = 0x00000010,
            Hue = 0x00000020,
            Amount = 0x00000040,
            Layer = 0x00000080,
            Name = 0x00000100,
            Parent = 0x00000200,
            Items = 0x00000400,
            WeightNot1or0 = 0x00000800,
            Map = 0x00001000,
            Visible = 0x00002000,
            Movable = 0x00004000,
            Stackable = 0x00008000,
            WeightIs0 = 0x00010000,
            LocationSByteZ = 0x00020000,
            LocationShortXY = 0x00040000,
            LocationByteXY = 0x00080000,
            ImplFlags = 0x00100000,
            InsuredFor = 0x00200000,
            BlessedFor = 0x00400000,
            HeldBy = 0x00800000,
            IntWeight = 0x01000000,
            SavedFlags = 0x02000000,
            NullWeight = 0x04000000
        }

        static void SetSaveFlag(ref SaveFlag flags, SaveFlag toSet, bool setIf)
        {
            if (setIf)
                flags |= toSet;
        }

        static bool GetSaveFlag(SaveFlag flags, SaveFlag toGet) => (flags & toGet) != 0;

        int ISerializable.TypeReference => _TypeRef;

        int ISerializable.SerialIdentity => Serial;

        public virtual void Serialize(GenericWriter w)
        {
            w.Write(9); // version
            var flags = SaveFlag.None;
            int x = _Location.X, y = _Location.Y, z = _Location.Z;
            if (x != 0 || y != 0 || z != 0)
            {
                if (x >= short.MinValue && x <= short.MaxValue && y >= short.MinValue && y <= short.MaxValue && z >= sbyte.MinValue && z <= sbyte.MaxValue)
                {
                    if (x != 0 || y != 0)
                    {
                        if (x >= byte.MinValue && x <= byte.MaxValue && y >= byte.MinValue && y <= byte.MaxValue) flags |= SaveFlag.LocationByteXY;
                        else flags |= SaveFlag.LocationShortXY;
                    }
                    if (z != 0) flags |= SaveFlag.LocationSByteZ;
                }
                else flags |= SaveFlag.LocationFull;
            }
            var info = LookupCompactInfo();
            var items = LookupItems();

            if (_Direction != Direction.North) flags |= SaveFlag.Direction;
            if (info != null && info.Bounce != null) flags |= SaveFlag.Bounce;
            if (_LootType != LootType.Regular) flags |= SaveFlag.LootType;
            if (_ItemID != 0) flags |= SaveFlag.ItemID;
            if (_Hue != 0) flags |= SaveFlag.Hue;
            if (_Amount != 1) flags |= SaveFlag.Amount;
            if (_Layer != Layer.Invalid) flags |= SaveFlag.Layer;
            if (info != null && info.Name != null) flags |= SaveFlag.Name;
            if (_Parent != null) flags |= SaveFlag.Parent;
            if (items != null && items.Count > 0) flags |= SaveFlag.Items;
            if (_Map != Map.Internal) flags |= SaveFlag.Map;
            //if (_InsuredFor != null && !_InsuredFor.Deleted) flags |= SaveFlag.InsuredFor;
            if (info != null && info.BlessedFor != null && !info.BlessedFor.Deleted) flags |= SaveFlag.BlessedFor;
            if (info != null && info.HeldBy != null && !info.HeldBy.Deleted) flags |= SaveFlag.HeldBy;
            if (info != null && info.SavedFlags != 0) flags |= SaveFlag.SavedFlags;
            if (info == null || info.Weight == -1) flags |= SaveFlag.NullWeight;
            else
            {
                if (info.Weight == 0.0) flags |= SaveFlag.WeightIs0;
                else if (info.Weight != 1.0)
                {
                    if (info.Weight == (int)info.Weight) flags |= SaveFlag.IntWeight;
                    else flags |= SaveFlag.WeightNot1or0;
                }
            }
            var implFlags = _Flags & (ImplFlag.Visible | ImplFlag.Movable | ImplFlag.Stackable | ImplFlag.Insured | ImplFlag.PayedInsurance | ImplFlag.QuestItem);
            if (implFlags != (ImplFlag.Visible | ImplFlag.Movable)) flags |= SaveFlag.ImplFlags;
            w.Write((int)flags);

            /* begin last moved time optimization */
            var ticks = LastMoved.Ticks;
            var now = DateTime.UtcNow.Ticks;
            TimeSpan d;
            try { d = new TimeSpan(ticks - now); }
            catch { if (ticks < now) d = TimeSpan.MaxValue; else d = TimeSpan.MaxValue; }
            var minutes = -d.TotalMinutes;
            if (minutes < int.MinValue) minutes = int.MinValue;
            else if (minutes > int.MaxValue) minutes = int.MaxValue;
            w.WriteEncodedInt((int)minutes);
            /* end */

            if (GetSaveFlag(flags, SaveFlag.Direction)) w.Write((byte)_Direction);
            if (GetSaveFlag(flags, SaveFlag.Bounce)) BounceInfo.Serialize(info.Bounce, w);
            if (GetSaveFlag(flags, SaveFlag.LootType)) w.Write((byte)_LootType);
            if (GetSaveFlag(flags, SaveFlag.LocationFull)) { w.WriteEncodedInt(x); w.WriteEncodedInt(y); w.WriteEncodedInt(z); }
            else
            {
                if (GetSaveFlag(flags, SaveFlag.LocationByteXY)) { w.Write((byte)x); w.Write((byte)y); }
                else if (GetSaveFlag(flags, SaveFlag.LocationShortXY)) { w.Write((short)x); w.Write((short)y); }
                if (GetSaveFlag(flags, SaveFlag.LocationSByteZ)) w.Write((sbyte)z);
            }
            if (GetSaveFlag(flags, SaveFlag.ItemID)) w.WriteEncodedInt(_ItemID);
            if (GetSaveFlag(flags, SaveFlag.Hue)) w.WriteEncodedInt(_Hue);
            if (GetSaveFlag(flags, SaveFlag.Amount)) w.WriteEncodedInt(_Amount);
            if (GetSaveFlag(flags, SaveFlag.Layer)) w.Write((byte)_Layer);
            if (GetSaveFlag(flags, SaveFlag.Name)) w.Write(info.Name);
            if (GetSaveFlag(flags, SaveFlag.Parent)) w.Write(_Parent != null && !_Parent.Deleted ? _Parent.Serial : Serial.MinusOne);
            if (GetSaveFlag(flags, SaveFlag.Items)) w.Write(items, false);
            if (GetSaveFlag(flags, SaveFlag.IntWeight)) w.WriteEncodedInt((int)info.Weight);
            else if (GetSaveFlag(flags, SaveFlag.WeightNot1or0)) w.Write(info.Weight);
            if (GetSaveFlag(flags, SaveFlag.Map)) w.Write(_Map);
            if (GetSaveFlag(flags, SaveFlag.ImplFlags)) w.WriteEncodedInt((int)implFlags);
            if (GetSaveFlag(flags, SaveFlag.InsuredFor)) w.Write((Mobile)null);
            if (GetSaveFlag(flags, SaveFlag.BlessedFor)) w.Write(info.BlessedFor);
            if (GetSaveFlag(flags, SaveFlag.HeldBy)) w.Write(info.HeldBy);
            if (GetSaveFlag(flags, SaveFlag.SavedFlags)) w.WriteEncodedInt(info.SavedFlags);
        }

        public IPooledEnumerable<IEntity> GetObjectsInRange(int range)
        {
            var map = _Map;
            return map == null
                ? Map.NullEnumerable<IEntity>.Instance
                : _Parent == null ? map.GetObjectsInRange(_Location, range) : map.GetObjectsInRange(GetWorldLocation(), range);
        }

        public IPooledEnumerable<Item> GetItemsInRange(int range)
        {
            var map = _Map;
            return map == null
                ? Map.NullEnumerable<Item>.Instance
                : _Parent == null ? map.GetItemsInRange(_Location, range) : map.GetItemsInRange(GetWorldLocation(), range);
        }

        public IPooledEnumerable<Mobile> GetMobilesInRange(int range)
        {
            var map = _Map;
            return map == null
                ? Map.NullEnumerable<Mobile>.Instance
                : _Parent == null ? map.GetMobilesInRange(_Location, range) : map.GetMobilesInRange(GetWorldLocation(), range);
        }

        public IPooledEnumerable<NetState> GetClientsInRange(int range)
        {
            var map = _Map;
            return map == null
                ? UltimaOnline.Map.NullEnumerable<NetState>.Instance
                : _Parent == null ? map.GetClientsInRange(_Location, range) : map.GetClientsInRange(GetWorldLocation(), range);
        }

        public static int LockedDownFlag { get; set; }

        public static int SecureFlag { get; set; }

        public bool IsLockedDown
        {
            get => GetTempFlag(LockedDownFlag);
            set { SetTempFlag(LockedDownFlag, value); InvalidateProperties(); }
        }

        public bool IsSecure
        {
            get => GetTempFlag(SecureFlag);
            set { SetTempFlag(SecureFlag, value); InvalidateProperties(); }
        }

        public bool GetTempFlag(int flag)
        {
            var info = LookupCompactInfo();
            return info == null ? false : (info.TempFlags & flag) != 0;
        }

        public void SetTempFlag(int flag, bool value)
        {
            var info = AcquireCompactInfo();
            if (value) info.TempFlags |= flag;
            else info.TempFlags &= ~flag;
            if (info.TempFlags == 0)
                VerifyCompactInfo();
        }

        public bool GetSavedFlag(int flag)
        {
            var info = LookupCompactInfo();
            return info == null ? false : (info.SavedFlags & flag) != 0;
        }

        public void SetSavedFlag(int flag, bool value)
        {
            var info = AcquireCompactInfo();

            if (value) info.SavedFlags |= flag;
            else info.SavedFlags &= ~flag;
            if (info.SavedFlags == 0)
                VerifyCompactInfo();
        }

        public virtual void Deserialize(GenericReader r)
        {
            var version = r.ReadInt();
            SetLastMoved();
            switch (version)
            {
                case 9:
                case 8:
                case 7:
                case 6:
                    {
                        var flags = (SaveFlag)r.ReadInt();
                        if (version < 7)
                            LastMoved = r.ReadDeltaTime();
                        else
                        {
                            var minutes = r.ReadEncodedInt();
                            try { LastMoved = DateTime.UtcNow - TimeSpan.FromMinutes(minutes); }
                            catch { LastMoved = DateTime.UtcNow; }
                        }
                        if (GetSaveFlag(flags, SaveFlag.Direction)) _Direction = (Direction)r.ReadByte();
                        if (GetSaveFlag(flags, SaveFlag.Bounce)) AcquireCompactInfo().Bounce = BounceInfo.Deserialize(r);
                        if (GetSaveFlag(flags, SaveFlag.LootType)) _LootType = (LootType)r.ReadByte();
                        int x = 0, y = 0, z = 0;
                        if (GetSaveFlag(flags, SaveFlag.LocationFull)) { x = r.ReadEncodedInt(); y = r.ReadEncodedInt(); z = r.ReadEncodedInt(); }
                        else
                        {
                            if (GetSaveFlag(flags, SaveFlag.LocationByteXY)) { x = r.ReadByte(); y = r.ReadByte(); }
                            else if (GetSaveFlag(flags, SaveFlag.LocationShortXY)) { x = r.ReadShort(); y = r.ReadShort(); }
                            if (GetSaveFlag(flags, SaveFlag.LocationSByteZ)) z = r.ReadSByte();
                        }
                        _Location = new Point3D(x, y, z);
                        if (GetSaveFlag(flags, SaveFlag.ItemID)) _ItemID = r.ReadEncodedInt();
                        if (GetSaveFlag(flags, SaveFlag.Hue)) _Hue = r.ReadEncodedInt();
                        _Amount = GetSaveFlag(flags, SaveFlag.Amount) ? r.ReadEncodedInt() : 1;
                        if (GetSaveFlag(flags, SaveFlag.Layer)) _Layer = (Layer)r.ReadByte();
                        if (GetSaveFlag(flags, SaveFlag.Name))
                        {
                            var name = r.ReadString();
                            if (name != DefaultName)
                                AcquireCompactInfo().Name = name;
                        }
                        if (GetSaveFlag(flags, SaveFlag.Parent))
                        {
                            Serial parent = r.ReadInt();
                            if (parent.IsMobile) _Parent = World.FindMobile(parent);
                            else if (parent.IsItem) _Parent = World.FindItem(parent);
                            else _Parent = null;
                            if (_Parent == null && (parent.IsMobile || parent.IsItem))
                                Delete();
                        }
                        if (GetSaveFlag(flags, SaveFlag.Items))
                        {
                            var items = r.ReadStrongItemList();
                            if (this is Container c) c._Items = items;
                            else AcquireCompactInfo().Items = items;
                        }

                        if (version < 8 || !GetSaveFlag(flags, SaveFlag.NullWeight))
                        {
                            double weight;
                            if (GetSaveFlag(flags, SaveFlag.IntWeight)) weight = r.ReadEncodedInt();
                            else if (GetSaveFlag(flags, SaveFlag.WeightNot1or0)) weight = r.ReadDouble();
                            else if (GetSaveFlag(flags, SaveFlag.WeightIs0)) weight = 0.0;
                            else weight = 1.0;
                            if (weight != DefaultWeight)
                                AcquireCompactInfo().Weight = weight;
                        }
                        _Map = GetSaveFlag(flags, SaveFlag.Map) ? r.ReadMap() : Map.Internal;
                        SetFlag(ImplFlag.Visible, GetSaveFlag(flags, SaveFlag.Visible) ? r.ReadBool() : true);
                        SetFlag(ImplFlag.Movable, GetSaveFlag(flags, SaveFlag.Movable) ? r.ReadBool() : true);
                        if (GetSaveFlag(flags, SaveFlag.Stackable)) SetFlag(ImplFlag.Stackable, r.ReadBool());
                        if (GetSaveFlag(flags, SaveFlag.ImplFlags)) _Flags = (ImplFlag)r.ReadEncodedInt();
                        if (GetSaveFlag(flags, SaveFlag.InsuredFor)) r.ReadMobile(); /*_InsuredFor = */
                        if (GetSaveFlag(flags, SaveFlag.BlessedFor)) AcquireCompactInfo().BlessedFor = r.ReadMobile();
                        if (GetSaveFlag(flags, SaveFlag.HeldBy)) AcquireCompactInfo().HeldBy = r.ReadMobile();
                        if (GetSaveFlag(flags, SaveFlag.SavedFlags)) AcquireCompactInfo().SavedFlags = r.ReadEncodedInt();
                        if (_Map != null && _Parent == null)
                            _Map.OnEnter(this);
                        break;
                    }
                case 5:
                    {
                        var flags = (SaveFlag)r.ReadInt();
                        LastMoved = r.ReadDeltaTime();
                        if (GetSaveFlag(flags, SaveFlag.Direction)) _Direction = (Direction)r.ReadByte();
                        if (GetSaveFlag(flags, SaveFlag.Bounce)) AcquireCompactInfo().Bounce = BounceInfo.Deserialize(r);
                        if (GetSaveFlag(flags, SaveFlag.LootType)) _LootType = (LootType)r.ReadByte();
                        if (GetSaveFlag(flags, SaveFlag.LocationFull)) _Location = r.ReadPoint3D();
                        if (GetSaveFlag(flags, SaveFlag.ItemID)) _ItemID = r.ReadInt();
                        if (GetSaveFlag(flags, SaveFlag.Hue)) _Hue = r.ReadInt();
                        _Amount = GetSaveFlag(flags, SaveFlag.Amount) ? r.ReadInt() : 1;
                        if (GetSaveFlag(flags, SaveFlag.Layer)) _Layer = (Layer)r.ReadByte();
                        if (GetSaveFlag(flags, SaveFlag.Name))
                        {
                            var name = r.ReadString();
                            if (name != DefaultName)
                                AcquireCompactInfo().Name = name;
                        }
                        if (GetSaveFlag(flags, SaveFlag.Parent))
                        {
                            Serial parent = r.ReadInt();
                            if (parent.IsMobile) _Parent = World.FindMobile(parent);
                            else if (parent.IsItem) _Parent = World.FindItem(parent);
                            else _Parent = null;
                            if (_Parent == null && (parent.IsMobile || parent.IsItem))
                                Delete();
                        }
                        if (GetSaveFlag(flags, SaveFlag.Items))
                        {
                            var items = r.ReadStrongItemList();
                            if (this is Container c) c._Items = items;
                            else AcquireCompactInfo().Items = items;
                        }
                        double weight;
                        if (GetSaveFlag(flags, SaveFlag.IntWeight)) weight = r.ReadEncodedInt();
                        else if (GetSaveFlag(flags, SaveFlag.WeightNot1or0)) weight = r.ReadDouble();
                        else if (GetSaveFlag(flags, SaveFlag.WeightIs0)) weight = 0.0;
                        else weight = 1.0;
                        if (weight != DefaultWeight)
                            AcquireCompactInfo().Weight = weight;
                        _Map = GetSaveFlag(flags, SaveFlag.Map) ? r.ReadMap() : _Map = Map.Internal;
                        SetFlag(ImplFlag.Visible, GetSaveFlag(flags, SaveFlag.Visible) ? r.ReadBool() : true);
                        SetFlag(ImplFlag.Movable, GetSaveFlag(flags, SaveFlag.Movable) ? r.ReadBool() : true);
                        if (GetSaveFlag(flags, SaveFlag.Stackable)) SetFlag(ImplFlag.Stackable, r.ReadBool());
                        if (_Map != null && _Parent == null)
                            _Map.OnEnter(this);
                        break;
                    }
                case 4: // Just removed variables
                case 3:
                    {
                        _Direction = (Direction)r.ReadInt();
                        goto case 2;
                    }
                case 2:
                    {
                        AcquireCompactInfo().Bounce = BounceInfo.Deserialize(r);
                        LastMoved = r.ReadDeltaTime();
                        goto case 1;
                    }
                case 1:
                    {
                        _LootType = (LootType)r.ReadByte();//m_Newbied = reader.ReadBool();
                        goto case 0;
                    }
                case 0:
                    {
                        _Location = r.ReadPoint3D();
                        _ItemID = r.ReadInt();
                        _Hue = r.ReadInt();
                        _Amount = r.ReadInt();
                        _Layer = (Layer)r.ReadByte();
                        var name = r.ReadString();
                        if (name != DefaultName)
                            AcquireCompactInfo().Name = name;
                        Serial parent = r.ReadInt();
                        if (parent.IsMobile) _Parent = World.FindMobile(parent);
                        else if (parent.IsItem) _Parent = World.FindItem(parent);
                        else _Parent = null;
                        if (_Parent == null && (parent.IsMobile || parent.IsItem))
                            Delete();
                        var count = r.ReadInt();
                        if (count > 0)
                        {
                            var items = new List<Item>(count);
                            for (var i = 0; i < count; ++i)
                            {
                                var item = r.ReadItem();
                                if (item != null)
                                    items.Add(item);
                            }
                            if (this is Container c) c._Items = items;
                            else AcquireCompactInfo().Items = items;
                        }
                        var weight = r.ReadDouble();
                        if (weight != DefaultWeight)
                            AcquireCompactInfo().Weight = weight;
                        if (version <= 3) { r.ReadInt(); r.ReadInt(); r.ReadInt(); }
                        _Map = r.ReadMap();
                        SetFlag(ImplFlag.Visible, r.ReadBool());
                        SetFlag(ImplFlag.Movable, r.ReadBool());
                        if (version <= 3) r.ReadBool(); /*_Deleted =*/
                        Stackable = r.ReadBool();
                        if (_Map != null && _Parent == null)
                            _Map.OnEnter(this);
                        break;
                    }
            }
            if (HeldBy != null)
                Timer.DelayCall(TimeSpan.Zero, new TimerCallback(FixHolding_Sandbox));
            //if (version < 9)
            VerifyCompactInfo();
        }

        void FixHolding_Sandbox()
        {
            var heldBy = HeldBy;
            if (heldBy != null)
            {
                if (GetBounce() != null) Bounce(heldBy);
                else
                {
                    heldBy.Holding = null;
                    heldBy.AddToBackpack(this);
                    ClearBounce();
                }
            }
        }

        public virtual int GetMaxUpdateRange() => 18;

        public virtual int GetUpdateRange(Mobile m) => 18;

        public void SendInfoTo(NetState state) => SendInfoTo(state, ObjectPropertyList.Enabled);
        public virtual void SendInfoTo(NetState state, bool sendOplPacket)
        {
            state.Send(GetWorldPacketFor(state));
            if (sendOplPacket)
                state.Send(OPLPacket);
        }

        protected virtual Packet GetWorldPacketFor(NetState state) => state.HighSeas ? WorldPacketHS : state.StygianAbyss ? WorldPacketSA : WorldPacket;

        public virtual bool IsVirtualItem => false;

        public virtual int GetTotal(TotalType type) => 0;

        public virtual void UpdateTotal(Item sender, TotalType type, int delta)
        {
            if (!IsVirtualItem)
            {
                if (_Parent is Item i) i.UpdateTotal(sender, type, delta);
                else if (_Parent is Mobile m) m.UpdateTotal(sender, type, delta);
                else if (HeldBy != null) (HeldBy as Mobile).UpdateTotal(sender, type, delta);
            }
        }

        public virtual void UpdateTotals() { }

        public virtual int LabelNumber => _ItemID < 0x4000 ? 1020000 + _ItemID : 1078872 + _ItemID;

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalGold => GetTotal(TotalType.Gold);

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalItems => GetTotal(TotalType.Items);

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalWeight => GetTotal(TotalType.Weight);

        public virtual double DefaultWeight
        {
            get
            {
                if (_ItemID < 0 || _ItemID > TileData.MaxItemValue || this is BaseMulti)
                    return 0;
                var weight = TileData.ItemTable[_ItemID].Weight;
                if (weight == 255 || weight == 0)
                    weight = 1;
                return weight;
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public double Weight
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null && info.Weight != -1 ? info.Weight : DefaultWeight;
            }
            set
            {
                if (Weight != value)
                {
                    var info = AcquireCompactInfo();
                    var oldPileWeight = PileWeight;
                    info.Weight = value;
                    if (info.Weight == -1)
                        VerifyCompactInfo();
                    var newPileWeight = PileWeight;
                    UpdateTotal(this, TotalType.Weight, newPileWeight - oldPileWeight);
                    InvalidateProperties();
                }
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int PileWeight => (int)Math.Ceiling(Weight * Amount);

        public virtual int HuedItemID => _ItemID;

        [Hue, CommandProperty(AccessLevel.GameMaster)]
        public virtual int Hue
        {
            get => _Hue;
            set
            {
                if (_Hue != value)
                {
                    _Hue = value;
                    ReleaseWorldPackets();
                    Delta(ItemDelta.Update);
                }
            }
        }

        public const int QuestItemHue = 0x4EA; // Hmmmm... "for EA"?

        public virtual bool Nontransferable => QuestItem;

        public virtual void HandleInvalidTransfer(Mobile from)
        {
            // OSI sends 1074769, bug!
            if (QuestItem)
                from.SendLocalizedMessage(1049343); // You can only drop quest items into the top-most level of your backpack while you still need them for your quest.
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual Layer Layer
        {
            get => _Layer;
            set
            {
                if (_Layer != value)
                {
                    _Layer = value;
                    Delta(ItemDelta.EquipOnly);
                }
            }
        }

        public List<Item> Items
        {
            get
            {
                var items = LookupItems();
                if (items == null)
                    items = EmptyItems;
                return items;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public IEntity RootParent
        {
            get
            {
                var p = _Parent;
                while (p is Item item)
                {
                    if (item._Parent == null) break;
                    else p = item._Parent;
                }
                return p;
            }
        }

        public bool ParentsContain<T>() where T : Item
        {
            var p = _Parent;
            while (p is Item item)
            {
                if (p is T)
                    return true;
                if (item._Parent == null) break;
                else p = item._Parent;
            }
            return false;
        }

        public virtual void AddItem(Item item)
        {
            if (item == null || item.Deleted || item._Parent == this)
                return;
            else if (item == this)
            {
                Console.WriteLine("Warning: Adding item to itself: [0x{Serial.Value:X} {GetType().Name}].AddItem( [0x{item.Serial.Value:X} {item.GetType().Name}] )");
                Console.WriteLine(new StackTrace());
                return;
            }
            else if (IsChildOf(item))
            {
                Console.WriteLine("Warning: Adding parent item to child: [0x{0:X} {1}].AddItem( [0x{2:X} {3}] )", this.Serial.Value, this.GetType().Name, item.Serial.Value, item.GetType().Name);
                Console.WriteLine(new StackTrace());
                return;
            }
            else if (item._Parent is Mobile m) m.RemoveItem(item);
            else if (item._Parent is Item i) i.RemoveItem(item);
            else item.SendRemovePacket();
            item.Parent = this;
            item.Map = _Map;
            var items = AcquireItems();
            items.Add(item);
            if (!item.IsVirtualItem)
            {
                UpdateTotal(item, TotalType.Gold, item.TotalGold);
                UpdateTotal(item, TotalType.Items, item.TotalItems + 1);
                UpdateTotal(item, TotalType.Weight, item.TotalWeight + item.PileWeight);
            }
            item.Delta(ItemDelta.Update);
            item.OnAdded(this);
            OnItemAdded(item);
        }

        static List<Item> _DeltaQueue = new List<Item>();

        public void Delta(ItemDelta flags)
        {
            if (_Map == null || _Map == Map.Internal)
                return;
            _DeltaFlags |= flags;
            if (!GetFlag(ImplFlag.InQueue))
            {
                SetFlag(ImplFlag.InQueue, true);
                if (_processing)
                {
                    try
                    {
                        using (var op = new StreamWriter("delta-recursion.log", true))
                        {
                            op.WriteLine("# {0}", DateTime.UtcNow);
                            op.WriteLine(new StackTrace());
                            op.WriteLine();
                        }
                    }
                    catch { }
                }
                else _DeltaQueue.Add(this);
            }
            Core.Set();
        }

        public void RemDelta(ItemDelta flags)
        {
            _DeltaFlags &= ~flags;
            if (GetFlag(ImplFlag.InQueue) && _DeltaFlags == ItemDelta.None)
            {
                SetFlag(ImplFlag.InQueue, false);
                if (_processing)
                {
                    try
                    {
                        using (var op = new StreamWriter("delta-recursion.log", true))
                        {
                            op.WriteLine("# {0}", DateTime.UtcNow);
                            op.WriteLine(new StackTrace());
                            op.WriteLine();
                        }
                    }
                    catch { }
                }
                else _DeltaQueue.Remove(this);
            }
        }

        public bool NoMoveHS { get; set; }

        public void ProcessDelta()
        {
            var flags = _DeltaFlags;
            SetFlag(ImplFlag.InQueue, false);
            _DeltaFlags = ItemDelta.None;
            var map = _Map;
            if (map != null && !Deleted)
            {
                var sendOPLUpdate = ObjectPropertyList.Enabled && (flags & ItemDelta.Properties) != 0;
                if (_Parent is Container contParent && !contParent.IsPublicContainer & (flags & ItemDelta.Update) != 0)
                {
                    var worldLoc = GetWorldLocation();
                    var rootParent = contParent.RootParent as Mobile;
                    Mobile tradeRecip = null;
                    if (rootParent != null)
                    {
                        var ns = rootParent.NetState;
                        if (ns != null && rootParent.CanSee(this) && rootParent.InRange(worldLoc, GetUpdateRange(rootParent)))
                        {
                            ns.Send(ns.ContainerGridLines ? (Packet)new ContainerContentUpdate6017(this) : new ContainerContentUpdate(this));
                            if (ObjectPropertyList.Enabled)
                                ns.Send(OPLPacket);
                        }
                    }
                    var stc = GetSecureTradeCont();
                    if (stc != null)
                    {
                        var st = stc.Trade;
                        if (st != null)
                        {
                            var test = st.From.Mobile;
                            if (test != null && test != rootParent)
                                tradeRecip = test;
                            test = st.To.Mobile;
                            if (test != null && test != rootParent)
                                tradeRecip = test;
                            if (tradeRecip != null)
                            {
                                var ns = tradeRecip.NetState;
                                if (ns != null && tradeRecip.CanSee(this) && tradeRecip.InRange(worldLoc, GetUpdateRange(tradeRecip)))
                                {
                                    ns.Send(ns.ContainerGridLines ? (Packet)new ContainerContentUpdate6017(this) : new ContainerContentUpdate(this));
                                    if (ObjectPropertyList.Enabled)
                                        ns.Send(OPLPacket);
                                }
                            }
                        }
                    }
                    var openers = contParent.Openers;
                    if (openers != null)
                        lock (openers)
                        {
                            for (var i = 0; i < openers.Count; ++i)
                            {
                                var mob = openers[i];
                                var range = GetUpdateRange(mob);
                                if (mob.Map != map || !mob.InRange(worldLoc, range))
                                    openers.RemoveAt(i--);
                                else
                                {
                                    if (mob == rootParent || mob == tradeRecip)
                                        continue;
                                    var ns = mob.NetState;
                                    if (ns != null && mob.CanSee(this))
                                    {
                                        ns.Send(ns.ContainerGridLines ? (Packet)new ContainerContentUpdate6017(this) : new ContainerContentUpdate(this));
                                        if (ObjectPropertyList.Enabled)
                                            ns.Send(OPLPacket);
                                    }
                                }
                            }
                            if (openers.Count == 0)
                                contParent.Openers = null;
                        }
                    return;
                }

                if ((flags & ItemDelta.Update) != 0)
                {
                    Packet p = null;
                    var worldLoc = GetWorldLocation();
                    var eable = map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                    foreach (var state in eable)
                    {
                        var m = state.Mobile;
                        if (m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                        {
                            if (_Parent == null)
                                SendInfoTo(state, ObjectPropertyList.Enabled);
                            else
                            {
                                if (p == null)
                                {
                                    if (_Parent is Item)
                                        state.Send(state.ContainerGridLines ? (Packet)new ContainerContentUpdate6017(this) : new ContainerContentUpdate(this));
                                    else if (_Parent is Mobile)
                                    {
                                        p = new EquipUpdate(this);
                                        p.Acquire();
                                        state.Send(p);
                                    }
                                }
                                else state.Send(p);
                                if (ObjectPropertyList.Enabled)
                                    state.Send(OPLPacket);
                            }
                        }
                    }
                    if (p != null)
                        Packet.Release(p);
                    eable.Free();
                    sendOPLUpdate = false;
                }
                else if ((flags & ItemDelta.EquipOnly) != 0)
                {
                    if (_Parent is Mobile)
                    {
                        Packet p = null;
                        var worldLoc = GetWorldLocation();
                        var eable = map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                        foreach (var state in eable)
                        {
                            var m = state.Mobile;
                            if (m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                            {
                                //if (sendOPLUpdate) state.Send(RemovePacket);
                                if (p == null)
                                    p = Packet.Acquire(new EquipUpdate(this));
                                state.Send(p);
                                if (ObjectPropertyList.Enabled)
                                    state.Send(OPLPacket);
                            }
                        }
                        Packet.Release(p);
                        eable.Free();
                        sendOPLUpdate = false;
                    }
                }
                if (sendOPLUpdate)
                {
                    var worldLoc = GetWorldLocation();
                    var eable = map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                    foreach (var state in eable)
                    {
                        var m = state.Mobile;
                        if (m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                            state.Send(OPLPacket);
                    }
                    eable.Free();
                }
            }
        }

        static bool _processing = false;

        public static void ProcessDeltaQueue()
        {
            _processing = true;
            if (_DeltaQueue.Count >= 512) Parallel.ForEach(_DeltaQueue, i => i.ProcessDelta());
            else for (var i = 0; i < _DeltaQueue.Count; i++) _DeltaQueue[i].ProcessDelta();
            _DeltaQueue.Clear();
            _processing = false;
        }

        public virtual void OnDelete()
        {
            if (Spawner != null)
            {
                Spawner.Remove(this);
                Spawner = null;
            }
        }

        public virtual void OnParentDeleted(IEntity parent) => Delete();

        public virtual void FreeCache()
        {
            ReleaseWorldPackets();
            Packet.Release(ref _RemovePacket);
            Packet.Release(ref _OPLPacket);
            Packet.Release(ref _PropertyList);
        }

        public virtual void Delete()
        {
            if (Deleted) return;
            else if (!World.OnDelete(this)) return;
            OnDelete();
            var items = LookupItems();
            if (items != null)
                for (var i = items.Count - 1; i >= 0; --i)
                    if (i < items.Count)
                        items[i].OnParentDeleted(this);
            SendRemovePacket();
            SetFlag(ImplFlag.Deleted, true);
            if (Parent is Mobile m) m.RemoveItem(this);
            else if (Parent is Item i) i.RemoveItem(this);
            ClearBounce();
            if (_Map != null)
            {
                if (_Parent == null)
                    _Map.OnLeave(this);
                _Map = null;
            }
            World.RemoveItem(this);
            OnAfterDelete();
            FreeCache();
        }

        public void PublicOverheadMessage(MessageType type, int hue, bool ascii, string text)
        {
            if (_Map != null)
            {
                Packet p = null;
                var worldLoc = GetWorldLocation();
                var eable = _Map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                foreach (var state in eable)
                {
                    var m = state.Mobile;
                    if (m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                    {
                        if (p == null)
                        {
                            if (ascii) p = new AsciiMessage(Serial, _ItemID, type, hue, 3, this.Name, text);
                            else p = new UnicodeMessage(Serial, _ItemID, type, hue, 3, "ENU", this.Name, text);
                            p.Acquire();
                        }
                        state.Send(p);
                    }
                }
                Packet.Release(p);
                eable.Free();
            }
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number, string args = null)
        {
            if (_Map != null)
            {
                Packet p = null;
                var worldLoc = GetWorldLocation();
                var eable = _Map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                foreach (var state in eable)
                {
                    var m = state.Mobile;
                    if (m.CanSee(this) && m.InRange(worldLoc, GetUpdateRange(m)))
                    {
                        if (p == null)
                            p = Packet.Acquire(new MessageLocalized(Serial, _ItemID, type, hue, 3, number, Name, args ?? string.Empty));
                        state.Send(p);
                    }
                }
                Packet.Release(p);
                eable.Free();
            }
        }

        public virtual void OnAfterDelete() { }

        public virtual void RemoveItem(Item item)
        {
            var items = LookupItems();
            if (items != null && items.Contains(item))
            {
                item.SendRemovePacket();
                items.Remove(item);
                if (!item.IsVirtualItem)
                {
                    UpdateTotal(item, TotalType.Gold, -item.TotalGold);
                    UpdateTotal(item, TotalType.Items, -(item.TotalItems + 1));
                    UpdateTotal(item, TotalType.Weight, -(item.TotalWeight + item.PileWeight));
                }
                item.Parent = null;
                item.OnRemoved(this);
                OnItemRemoved(item);
            }
        }

        public virtual void OnAfterDuped(Item newItem) { }

        public virtual bool OnDragLift(Mobile from) => true;

        public virtual bool OnEquip(Mobile from) => true;

        public ISpawner Spawner
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null ? info.Spawner : null;
            }
            set
            {
                var info = AcquireCompactInfo();
                info.Spawner = value;
                if (info.Spawner == null)
                    VerifyCompactInfo();
            }
        }

        public virtual void OnBeforeSpawn(Point3D location, Map m) { }

        public virtual void OnAfterSpawn() { }

        public virtual int PhysicalResistance => 0;
        public virtual int FireResistance => 0;
        public virtual int ColdResistance => 0;
        public virtual int PoisonResistance => 0;
        public virtual int EnergyResistance => 0;

        [CommandProperty(AccessLevel.Counselor)]
        public Serial Serial { get; }

        #region Location Location Location!

        public virtual void OnLocationChange(Point3D oldLocation) { }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public virtual Point3D Location
        {
            get => _Location;
            set
            {
                var oldLocation = _Location;
                if (oldLocation != value)
                {
                    if (_Map != null)
                    {
                        if (_Parent == null)
                        {
                            IPooledEnumerable<NetState> eable;
                            if (_Location.X != 0)
                            {
                                eable = _Map.GetClientsInRange(oldLocation, GetMaxUpdateRange());
                                foreach (var state in eable)
                                {
                                    var m = state.Mobile;
                                    if (!m.InRange(value, GetUpdateRange(m)))
                                        state.Send(RemovePacket);
                                }
                                eable.Free();
                            }
                            var oldLoc = _Location;
                            _Location = value;
                            ReleaseWorldPackets();
                            SetLastMoved();
                            eable = _Map.GetClientsInRange(_Location, GetMaxUpdateRange());
                            foreach (var state in eable)
                            {
                                var m = state.Mobile;
                                if (m.CanSee(this) && m.InRange(_Location, GetUpdateRange(m)) && (!state.HighSeas || !NoMoveHS || (_DeltaFlags & ItemDelta.Update) != 0 || !m.InRange(oldLoc, GetUpdateRange(m))))
                                    SendInfoTo(state);
                            }
                            eable.Free();
                            RemDelta(ItemDelta.Update);
                        }
                        else if (_Parent is Item)
                        {
                            _Location = value;
                            ReleaseWorldPackets();
                            Delta(ItemDelta.Update);
                        }
                        else
                        {
                            _Location = value;
                            ReleaseWorldPackets();
                        }
                        if (_Parent == null)
                            _Map.OnMove(oldLocation, this);
                    }
                    else
                    {
                        _Location = value;
                        ReleaseWorldPackets();
                    }
                    OnLocationChange(oldLocation);
                }
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int X
        {
            get => _Location.X;
            set => Location = new Point3D(value, _Location.Y, _Location.Z);
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int Y
        {
            get => _Location.Y;
            set => Location = new Point3D(_Location.X, value, _Location.Z);
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int Z
        {
            get => _Location.Z;
            set => Location = new Point3D(_Location.X, _Location.Y, value);
        }
        #endregion

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int ItemID
        {
            get => _ItemID;
            set
            {
                if (_ItemID != value)
                {
                    var oldPileWeight = PileWeight;
                    _ItemID = value;
                    ReleaseWorldPackets();
                    var newPileWeight = PileWeight;
                    UpdateTotal(this, TotalType.Weight, newPileWeight - oldPileWeight);
                    InvalidateProperties();
                    Delta(ItemDelta.Update);
                }
            }
        }

        public virtual string DefaultName => null;

        [CommandProperty(AccessLevel.GameMaster)]
        public string Name
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null && info.Name != null ? info.Name : DefaultName;
            }
            set
            {
                if (value == null || value != DefaultName)
                {
                    var info = AcquireCompactInfo();
                    info.Name = value;
                    if (info.Name == null)
                        VerifyCompactInfo();
                    InvalidateProperties();
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Developer)]
        public IEntity Parent
        {
            get => _Parent;
            set
            {
                if (_Parent == value)
                    return;
                var oldParent = _Parent;
                _Parent = value;
                if (_Map != null)
                {
                    if (oldParent != null && _Parent == null) _Map.OnEnter(this);
                    else if (_Parent != null) _Map.OnLeave(this);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public LightType Light
        {
            get => (LightType)_Direction;
            set
            {
                if ((LightType)_Direction != value)
                {
                    _Direction = (Direction)value;
                    ReleaseWorldPackets();
                    Delta(ItemDelta.Update);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Direction Direction
        {
            get => _Direction;
            set
            {
                if (_Direction != value)
                {
                    _Direction = value;
                    ReleaseWorldPackets();
                    Delta(ItemDelta.Update);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Amount
        {
            get => _Amount;
            set
            {
                var oldValue = _Amount;
                if (oldValue != value)
                {
                    var oldPileWeight = PileWeight;
                    _Amount = value;
                    ReleaseWorldPackets();
                    var newPileWeight = PileWeight;
                    UpdateTotal(this, TotalType.Weight, newPileWeight - oldPileWeight);
                    OnAmountChange(oldValue);
                    Delta(ItemDelta.Update);
                    if (oldValue > 1 || value > 1)
                        InvalidateProperties();
                    if (!Stackable && _Amount > 1)
                        Console.WriteLine("Warning: 0x{Serial.Value:X}: Amount changed for non-stackable item '{_Amount}'. ({GetType().Name})");
                }
            }
        }

        protected virtual void OnAmountChange(int oldValue) { }

        public virtual bool HandlesOnSpeech => false;

        public virtual void OnSpeech(SpeechEventArgs e) { }

        public virtual bool OnDroppedToMobile(Mobile from, Mobile target)
        {
            if (Nontransferable && from.Player)
            {
                HandleInvalidTransfer(from);
                return false;
            }
            return true;
        }

        public virtual bool DropToMobile(Mobile from, Mobile target, Point3D p)
        {
            if (Deleted || from.Deleted || target.Deleted || from.Map != target.Map || from.Map == null || target.Map == null) return false;
            else if (from.AccessLevel < AccessLevel.GameMaster && !from.InRange(target.Location, 2)) return false;
            else if (!from.CanSee(target) || !from.InLOS(target)) return false;
            else if (!from.OnDroppedItemToMobile(this, target)) return false;
            else if (!OnDroppedToMobile(from, target)) return false;
            else if (!target.OnDragDrop(from, this)) return false;
            else return true;
        }

        public virtual bool OnDroppedInto(Mobile from, Container target, Point3D p)
        {
            if (!from.OnDroppedItemInto(this, target, p))
                return false;
            else if (Nontransferable && from.Player && target != from.Backpack)
            {
                HandleInvalidTransfer(from);
                return false;
            }
            return target.OnDragDropInto(from, this, p);
        }

        public virtual bool OnDroppedOnto(Mobile from, Item target)
        {
            if (Deleted || from.Deleted || target.Deleted || from.Map != target.Map || from.Map == null || target.Map == null) return false;
            else if (from.AccessLevel < AccessLevel.GameMaster && !from.InRange(target.GetWorldLocation(), 2)) return false;
            else if (!from.CanSee(target) || !from.InLOS(target)) return false;
            else if (!target.IsAccessibleTo(from)) return false;
            else if (!from.OnDroppedItemOnto(this, target)) return false;
            else if (Nontransferable && from.Player && target != from.Backpack)
            {
                HandleInvalidTransfer(from);
                return false;
            }
            else return target.OnDragDrop(from, this);
        }

        public virtual bool DropToItem(Mobile from, Item target, Point3D p)
        {
            if (Deleted || from.Deleted || target.Deleted || from.Map != target.Map || from.Map == null || target.Map == null)
                return false;
            var root = target.RootParent;
            if (from.AccessLevel < AccessLevel.GameMaster && !from.InRange(target.GetWorldLocation(), 2)) return false;
            else if (!from.CanSee(target) || !from.InLOS(target)) return false;
            else if (!target.IsAccessibleTo(from)) return false;
            else if (root is Mobile m && !m.CheckNonlocalDrop(from, this, target)) return false;
            else if (!from.OnDroppedItemToItem(this, target, p)) return false;
            else if (target is Container && p.X != -1 && p.Y != -1) return OnDroppedInto(from, (Container)target, p);
            else return OnDroppedOnto(from, target);
        }

        public virtual bool OnDroppedToWorld(Mobile from, Point3D p)
        {
            if (Nontransferable && from.Player)
            {
                HandleInvalidTransfer(from);
                return false;
            }
            return true;
        }

        public virtual int GetLiftSound(Mobile from) => 0x57;

        static int _OpenSlots;

        public virtual bool DropToWorld(Mobile from, Point3D p)
        {
            if (Deleted || from.Deleted || from.Map == null) return false;
            else if (!from.InRange(p, 2)) return false;
            var map = from.Map;
            if (map == null)
                return false;
            int x = p.X, y = p.Y;
            var z = int.MinValue;
            var maxZ = from.Z + 16;
            var landTile = map.Tiles.GetLandTile(x, y);
            var landFlags = TileData.LandTable[landTile.ID & TileData.MaxLandValue].Flags;
            int landZ = 0, landAvg = 0, landTop = 0;
            map.GetAverageZ(x, y, ref landZ, ref landAvg, ref landTop);
            if (!landTile.Ignored && (landFlags & TileFlag.Impassable) == 0)
                if (landAvg <= maxZ)
                    z = landAvg;
            var tiles = map.Tiles.GetStaticTiles(x, y, true);
            for (var i = 0; i < tiles.Length; ++i)
            {
                var tile = tiles[i];
                var id = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
                if (!id.Surface)
                    continue;
                var top = tile.Z + id.CalcHeight;
                if (top > maxZ || top < z)
                    continue;
                z = top;
            }
            var items = new List<Item>();
            var eable = map.GetItemsInRange(p, 0);
            foreach (var item in eable)
            {
                if (item is BaseMulti || item.ItemID > TileData.MaxItemValue)
                    continue;
                items.Add(item);
                var id = item.ItemData;
                if (!id.Surface)
                    continue;
                var top = item.Z + id.CalcHeight;
                if (top > maxZ || top < z)
                    continue;
                z = top;
            }
            eable.Free();
            if (z == int.MinValue)
                return false;
            if (z > maxZ)
                return false;
            _OpenSlots = (1 << 20) - 1;
            var surfaceZ = z;
            for (var i = 0; i < tiles.Length; ++i)
            {
                var tile = tiles[i];
                var id = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
                var checkZ = tile.Z;
                var checkTop = checkZ + id.CalcHeight;
                if (checkTop == checkZ && !id.Surface)
                    ++checkTop;
                var zStart = checkZ - z;
                var zEnd = checkTop - z;
                if (zStart >= 20 || zEnd < 0)
                    continue;
                if (zStart < 0)
                    zStart = 0;
                if (zEnd > 19)
                    zEnd = 19;
                var bitCount = zEnd - zStart;
                _OpenSlots &= ~(((1 << bitCount) - 1) << zStart);
            }
            for (var i = 0; i < items.Count; ++i)
            {
                var item = items[i];
                var id = item.ItemData;
                var checkZ = item.Z;
                var checkTop = checkZ + id.CalcHeight;
                if (checkTop == checkZ && !id.Surface)
                    ++checkTop;
                var zStart = checkZ - z;
                var zEnd = checkTop - z;
                if (zStart >= 20 || zEnd < 0)
                    continue;
                if (zStart < 0)
                    zStart = 0;
                if (zEnd > 19)
                    zEnd = 19;
                var bitCount = zEnd - zStart;
                _OpenSlots &= ~(((1 << bitCount) - 1) << zStart);
            }
            var height = ItemData.Height;
            if (height == 0)
                ++height;
            if (height > 30)
                height = 30;
            var match = (1 << height) - 1;
            var okay = false;
            for (var i = 0; i < 20; ++i)
            {
                if ((i + height) > 20)
                    match >>= 1;
                okay = ((_OpenSlots >> i) & match) == match;
                if (okay)
                {
                    z += i;
                    break;
                }
            }
            if (!okay)
                return false;
            height = ItemData.Height;
            if (height == 0)
                ++height;
            if (landAvg > z && (z + height) > landZ) return false;
            else if ((landFlags & TileFlag.Impassable) != 0 && landAvg > surfaceZ && (z + height) > landZ) return false;
            for (var i = 0; i < tiles.Length; ++i)
            {
                var tile = tiles[i];
                var id = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
                var checkZ = tile.Z;
                var checkTop = checkZ + id.CalcHeight;
                if (checkTop > z && (z + height) > checkZ) return false;
                else if ((id.Surface || id.Impassable) && checkTop > surfaceZ && (z + height) > checkZ) return false;
            }
            for (var i = 0; i < items.Count; ++i)
            {
                var item = items[i];
                var id = item.ItemData;
                //var checkZ = item.Z;
                //var checkTop = checkZ + id.CalcHeight;
                if ((item.Z + id.CalcHeight) > z && (z + height) > item.Z)
                    return false;
            }
            p = new Point3D(x, y, z);
            if (!from.InLOS(new Point3D(x, y, z + 1))) return false;
            else if (!from.OnDroppedItemToWorld(this, p)) return false;
            else if (!OnDroppedToWorld(from, p)) return false;
            var soundID = GetDropSound();
            MoveToWorld(p, from.Map);
            from.SendSound(soundID == -1 ? 0x42 : soundID, GetWorldLocation());
            return true;
        }

        public void SendRemovePacket()
        {
            if (!Deleted && _Map != null)
            {
                var worldLoc = GetWorldLocation();
                var eable = _Map.GetClientsInRange(worldLoc, GetMaxUpdateRange());
                foreach (var state in eable)
                {
                    var m = state.Mobile;
                    if (m.InRange(worldLoc, GetUpdateRange(m)))
                        state.Send(RemovePacket);
                }
                eable.Free();
            }
        }

        public virtual int GetDropSound() => -1;

        public Point3D GetWorldLocation()
        {
            var root = RootParent;
            return root == null ? _Location : root.Location;
            //return root == null ? _Location : new Point3D((IPoint3D)root);
        }

        public virtual bool BlocksFit => false;

        public Point3D GetSurfaceTop()
        {
            var root = RootParent;
            return root == null
                ? new Point3D(_Location.X, _Location.Y, _Location.Z + (ItemData.Surface ? ItemData.CalcHeight : 0))
                : root.Location;
        }

        public Point3D GetWorldTop()
        {
            var root = RootParent;
            return root == null ? new Point3D(_Location.X, _Location.Y, _Location.Z + ItemData.CalcHeight) : root.Location;
        }

        public void SendLocalizedMessageTo(Mobile to, int number)
        {
            if (Deleted || !to.CanSee(this))
                return;
            to.Send(new MessageLocalized(Serial, ItemID, MessageType.Regular, 0x3B2, 3, number, string.Empty, string.Empty));
        }

        public void SendLocalizedMessageTo(Mobile to, int number, string args)
        {
            if (Deleted || !to.CanSee(this))
                return;
            to.Send(new MessageLocalized(Serial, ItemID, MessageType.Regular, 0x3B2, 3, number, string.Empty, args));
        }

        public void SendLocalizedMessageTo(Mobile to, int number, AffixType affixType, string affix, string args)
        {
            if (Deleted || !to.CanSee(this))
                return;
            to.Send(new MessageLocalizedAffix(Serial, ItemID, MessageType.Regular, 0x3B2, 3, number, string.Empty, affixType, affix, args));
        }

        #region OnDoubleClick[...]

        public virtual void OnDoubleClick(Mobile from) { }

        public virtual void OnDoubleClickOutOfRange(Mobile from) { }

        public virtual void OnDoubleClickCantSee(Mobile from) { }

        public virtual void OnDoubleClickDead(Mobile from) => from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019048); // I am dead and cannot do that.

        public virtual void OnDoubleClickNotAccessible(Mobile from) => from.SendLocalizedMessage(500447); // That is not accessible.

        public virtual void OnDoubleClickSecureTrade(Mobile from) => from.SendLocalizedMessage(500447); // That is not accessible.

        #endregion

        public virtual void OnSnoop(Mobile from) { }

        public bool InSecureTrade => GetSecureTradeCont() != null;

        public SecureTradeContainer GetSecureTradeCont()
        {
            object p = this;
            while (p is Item i)
            {
                if (p is SecureTradeContainer s)
                    return s;
                p = i._Parent;
            }
            return null;
        }

        public virtual void OnItemAdded(Item item)
        {
            if (_Parent is Item i) i.OnSubItemAdded(item);
            else if (_Parent is Mobile m) m.OnSubItemAdded(item);
        }

        public virtual void OnItemRemoved(Item item)
        {
            if (_Parent is Item i) i.OnSubItemRemoved(item);
            else if (_Parent is Mobile m) m.OnSubItemRemoved(item);
        }

        public virtual void OnSubItemAdded(Item item)
        {
            if (_Parent is Item i) i.OnSubItemAdded(item);
            else if (_Parent is Mobile m) m.OnSubItemAdded(item);
        }

        public virtual void OnSubItemRemoved(Item item)
        {
            if (_Parent is Item i) i.OnSubItemRemoved(item);
            else if (_Parent is Mobile m) m.OnSubItemRemoved(item);
        }

        public virtual void OnItemBounceCleared(Item item)
        {
            if (_Parent is Item i) i.OnSubItemBounceCleared(item);
            else if (_Parent is Mobile m) m.OnSubItemBounceCleared(item);
        }

        public virtual void OnSubItemBounceCleared(Item item)
        {
            if (_Parent is Item i)
                i.OnSubItemBounceCleared(item);
            else if (_Parent is Mobile m) m.OnSubItemBounceCleared(item);
        }

        public virtual bool CheckTarget(Mobile from, UltimaOnline.Targeting.Target targ, object targeted)
        {
            if (_Parent is Item i) return i.CheckTarget(from, targ, targeted);
            else if (_Parent is Mobile m) return m.CheckTarget(from, targ, targeted);
            return true;
        }

        public virtual bool IsAccessibleTo(Mobile check)
        {
            if (_Parent is Item i)
                return i.IsAccessibleTo(check);
            var reg = Region.Find(GetWorldLocation(), _Map);
            return reg.CheckAccessibility(this, check);
            //var cont = GetSecureTradeCont();
            //return cont != null && !cont.IsChildOf(check) ? false : true;
        }

        public bool IsChildOf(IEntity o, bool allowNull = false)
        {
            var p = _Parent;
            if ((p == null || o == null) && !allowNull)
                return false;
            if (p == o)
                return true;
            while (p is Item item)
            {
                if (item._Parent == null)
                    break;
                else
                {
                    p = item._Parent;
                    if (p == o)
                        return true;
                }
            }
            return false;
        }

        public ItemData ItemData => TileData.ItemTable[_ItemID & TileData.MaxItemValue];

        public virtual void OnItemUsed(Mobile from, Item item)
        {
            if (_Parent is Item i) i.OnItemUsed(from, item);
            else if (_Parent is Mobile m) m.OnItemUsed(from, item);
        }

        public bool CheckItemUse(Mobile from) => CheckItemUse(from, this);
        public virtual bool CheckItemUse(Mobile from, Item item)
        {
            if (_Parent is Item i) return i.CheckItemUse(from, item);
            else if (_Parent is Mobile m) return m.CheckItemUse(from, item);
            else return true;
        }

        public virtual void OnItemLifted(Mobile from, Item item)
        {
            if (_Parent is Item i) i.OnItemLifted(from, item);
            else if (_Parent is Mobile m) m.OnItemLifted(from, item);
        }

        public bool CheckLift(Mobile from)
        {
            var reject = LRReason.Inspecific;
            return CheckLift(from, this, ref reject);
        }

        public virtual bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            if (_Parent is Item i) return i.CheckLift(from, item, ref reject);
            else if (_Parent is Mobile m) return m.CheckLift(from, item, ref reject);
            else return true;
        }

        public virtual bool CanTarget => true;
        public virtual bool DisplayLootType => true;

        public virtual void OnSingleClickContained(Mobile from, Item item)
        {
            if (_Parent is Item i) i.OnSingleClickContained(from, item);
        }

        public virtual void OnAosSingleClick(Mobile from)
        {
            var opl = PropertyList;
            if (opl.Header > 0)
                from.Send(new MessageLocalized(Serial, _ItemID, MessageType.Label, 0x3B2, 3, opl.Header, Name, opl.HeaderArgs));
        }

        public virtual void OnSingleClick(Mobile from)
        {
            if (Deleted || !from.CanSee(this))
                return;
            if (DisplayLootType)
                LabelLootTypeTo(from);
            var ns = from.NetState;
            if (ns != null)
            {
                if (Name == null)
                {
                    ns.Send(_Amount <= 1
                        ? (Packet)new MessageLocalized(Serial, _ItemID, MessageType.Label, 0x3B2, 3, LabelNumber, string.Empty, string.Empty)
                        : new MessageLocalizedAffix(Serial, _ItemID, MessageType.Label, 0x3B2, 3, LabelNumber, string.Empty, AffixType.Append, $" : {_Amount}", string.Empty));
                }
                else ns.Send(new UnicodeMessage(Serial, _ItemID, MessageType.Label, 0x3B2, 3, "ENU", string.Empty, Name + (_Amount > 1 ? $" : {_Amount}" : string.Empty)));
            }
        }

        public static bool ScissorCopyLootType { get; set; }

        public virtual void ScissorHelper(Mobile from, Item newItem, int amountPerOldItem, bool carryHue = true)
        {
            var amount = Amount;
            if (amount > (60000 / amountPerOldItem)) // let's not go over 60000
                amount = (60000 / amountPerOldItem);
            Amount -= amount;
            var ourHue = Hue;
            var thisMap = Map;
            var thisParent = _Parent;
            var worldLoc = GetWorldLocation();
            var type = LootType;
            if (Amount == 0)
                Delete();
            newItem.Amount = amount * amountPerOldItem;
            if (carryHue)
                newItem.Hue = ourHue;
            if (ScissorCopyLootType)
                newItem.LootType = type;
            if (!(thisParent is Container) || !((Container)thisParent).TryDropItem(from, newItem, false))
                newItem.MoveToWorld(worldLoc, thisMap);
        }

        public virtual void Consume(int amount = 1)
        {
            Amount -= amount;
            if (Amount <= 0)
                Delete();
        }

        public virtual void ReplaceWith(Item newItem)
        {
            if (_Parent is Container c)
            {
                c.AddItem(newItem);
                newItem.Location = _Location;
            }
            else
                newItem.MoveToWorld(GetWorldLocation(), _Map);
            Delete();
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool QuestItem
        {
            get => GetFlag(ImplFlag.QuestItem);
            set
            {
                SetFlag(ImplFlag.QuestItem, value);
                InvalidateProperties();
                ReleaseWorldPackets();
                Delta(ItemDelta.Update);
            }
        }

        public bool Insured
        {
            get => GetFlag(ImplFlag.Insured);
            set { SetFlag(ImplFlag.Insured, value); InvalidateProperties(); }
        }

        public bool PayedInsurance
        {
            get => GetFlag(ImplFlag.PayedInsurance);
            set { SetFlag(ImplFlag.PayedInsurance, value); }
        }

        public Mobile BlessedFor
        {
            get
            {
                var info = LookupCompactInfo();
                return info != null ? info.BlessedFor : null;
            }
            set
            {
                var info = AcquireCompactInfo();
                info.BlessedFor = value;
                if (info.BlessedFor == null)
                    VerifyCompactInfo();
                InvalidateProperties();
            }
        }

        public static TimeSpan DDT { get => DefaultDecayTime; set => DefaultDecayTime = value; }

        public virtual bool CheckBlessed(object obj) => CheckBlessed(obj as Mobile);
        public virtual bool CheckBlessed(Mobile m) => _LootType == LootType.Blessed || (Mobile.InsuranceEnabled && Insured) ? true : m != null && m == this.BlessedFor;

        public virtual bool CheckNewbied() => _LootType == LootType.Newbied;

        public virtual bool IsStandardLoot() => Mobile.InsuranceEnabled && Insured ? false : BlessedFor != null ? false : _LootType == LootType.Regular;

        public override string ToString() => $"0x{Serial.Value:X} \"{GetType().Name}\"";

        internal int _TypeRef;

        public Item()
        {
            Serial = Serial.NewItem;
            //_Items = new ArrayList(1);
            Visible = true;
            Movable = true;
            Amount = 1;
            _Map = Map.Internal;
            SetLastMoved();
            World.AddItem(this);
            var ourType = GetType();
            _TypeRef = World._ItemTypes.IndexOf(ourType);
            if (_TypeRef == -1)
            {
                World._ItemTypes.Add(ourType);
                _TypeRef = World._ItemTypes.Count - 1;
            }
        }

        [Constructable]
        public Item(int itemID) : this() => _ItemID = itemID;

        public Item(Serial serial)
        {
            Serial = serial;
            var ourType = GetType();
            _TypeRef = World._ItemTypes.IndexOf(ourType);
            if (_TypeRef == -1)
            {
                World._ItemTypes.Add(ourType);
                _TypeRef = World._ItemTypes.Count - 1;
            }
        }

        public virtual void OnSectorActivate() { }

        public virtual void OnSectorDeactivate() { }
    }
}