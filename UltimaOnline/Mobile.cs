using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using UltimaOnline;
using UltimaOnline.Accounting;
using UltimaOnline.Commands;
using UltimaOnline.ContextMenus;
using UltimaOnline.Guilds;
using UltimaOnline.Gumps;
using UltimaOnline.HuePickers;
using UltimaOnline.Items;
using UltimaOnline.Menus;
using UltimaOnline.Mobiles;
using UltimaOnline.Network;
using UltimaOnline.Prompts;
using UltimaOnline.Targeting;

namespace UltimaOnline
{
    public delegate void TargetCallback(Mobile from, object targeted);
    public delegate void TargetStateCallback(Mobile from, object targeted, object state);
    public delegate void TargetStateCallback<T>(Mobile from, object targeted, T state);
    public delegate void PromptCallback(Mobile from, string text);
    public delegate void PromptStateCallback(Mobile from, string text, object state);
    public delegate void PromptStateCallback<T>(Mobile from, string text, T state);

    #region [...]Mods

    public class TimedSkillMod : SkillMod
    {
        readonly DateTime _Expire;

        public TimedSkillMod(SkillName skill, bool relative, double value, TimeSpan delay)
            : this(skill, relative, value, DateTime.UtcNow + delay) { }
        public TimedSkillMod(SkillName skill, bool relative, double value, DateTime expire)
            : base(skill, relative, value) { _Expire = expire; }
        public override bool CheckCondition() => DateTime.UtcNow < _Expire;
    }

    public class EquipedSkillMod : SkillMod
    {
        Item _Item;
        Mobile _Mobile;
        public EquipedSkillMod(SkillName skill, bool relative, double value, Item item, Mobile mobile)
            : base(skill, relative, value) { _Item = item; _Mobile = mobile; }
        public override bool CheckCondition() => !_Item.Deleted && !_Mobile.Deleted && _Item.Parent == _Mobile;
    }

    public class DefaultSkillMod : SkillMod
    {
        public DefaultSkillMod(SkillName skill, bool relative, double value)
            : base(skill, relative, value) { }
        public override bool CheckCondition() => true;
    }

    public abstract class SkillMod
    {
        Mobile _Owner;
        SkillName _Skill;
        bool _Relative;
        double _Value;
        bool _ObeyCap;

        protected SkillMod(SkillName skill, bool relative, double value)
        {
            _Skill = skill;
            _Relative = relative;
            _Value = value;
        }

        public bool ObeyCap
        {
            get => _ObeyCap;
            set { _ObeyCap = value; _Owner?.Skills[_Skill]?.Update(); }
        }

        public Mobile Owner
        {
            get => _Owner;
            set { if (_Owner != value) { _Owner?.RemoveSkillMod(this); _Owner = value; _Owner?.AddSkillMod(this); } }
        }

        public void Remove() => Owner = null;

        public SkillName Skill
        {
            get => _Skill;
            set { if (_Skill != value) { var oldUpdate = _Owner?.Skills[_Skill]; _Skill = value; _Owner?.Skills[_Skill]?.Update(); oldUpdate?.Update(); } }
        }

        public bool Relative
        {
            get => _Relative;
            set { if (_Relative != value) { _Relative = value; _Owner?.Skills[_Skill]?.Update(); } }
        }

        public bool Absolute
        {
            get => !_Relative;
            set { if (_Relative == value) { _Relative = !value; _Owner?.Skills[_Skill]?.Update(); } }
        }

        public double Value
        {
            get => _Value;
            set { if (_Value != value) { _Value = value; _Owner?.Skills[_Skill]?.Update(); } }
        }

        public abstract bool CheckCondition();
    }

    public class ResistanceMod
    {
        ResistanceType _Type;
        int _Offset;

        public ResistanceMod(ResistanceType type, int offset)
        {
            _Type = type;
            _Offset = offset;
        }

        public Mobile Owner { get; set; }

        public ResistanceType Type
        {
            get => _Type;
            set { if (_Type != value) { _Type = value; Owner?.UpdateResistances(); } }
        }

        public int Offset
        {
            get => _Offset;
            set { if (_Offset != value) { _Offset = value; Owner?.UpdateResistances(); } }
        }
    }

    public class StatMod
    {
        readonly TimeSpan _Duration;
        readonly DateTime _Added;

        public StatMod(StatType type, string name, int offset, TimeSpan duration)
        {
            Type = type;
            Name = name;
            Offset = offset;
            _Duration = duration;
            _Added = DateTime.UtcNow;
        }

        public StatType Type { get; }
        public string Name { get; }
        public int Offset { get; }
        public bool HasElapsed() => _Duration == TimeSpan.Zero ? false : (DateTime.UtcNow - _Added) >= _Duration;
    }

    #endregion

    public class DamageEntry
    {
        public Mobile Damager { get; }
        public int DamageGiven { get; set; }
        public DateTime LastDamage { get; set; }
        public bool HasExpired => DateTime.UtcNow > (LastDamage + ExpireDelay);
        public List<DamageEntry> Responsible { get; set; }
        public static TimeSpan ExpireDelay { get; set; } = TimeSpan.FromMinutes(2.0);
        public DamageEntry(Mobile damager) => Damager = damager;
    }

    #region Enums

    [Flags]
    public enum StatType
    {
        Str = 1,
        Dex = 2,
        Int = 4,
        All = 7
    }

    public enum StatLockType : byte
    {
        Up,
        Down,
        Locked
    }

    [CustomEnum(new string[] { "North", "Right", "East", "Down", "South", "Left", "West", "Up" })]
    public enum Direction : byte
    {
        North = 0x0,
        Right = 0x1,
        East = 0x2,
        Down = 0x3,
        South = 0x4,
        Left = 0x5,
        West = 0x6,
        Up = 0x7,
        Mask = 0x7,
        Running = 0x80,
        ValueMask = 0x87
    }

    [Flags]
    public enum MobileDelta
    {
        None = 0x00000000,
        Name = 0x00000001,
        Flags = 0x00000002,
        Hits = 0x00000004,
        Mana = 0x00000008,
        Stam = 0x00000010,
        Stat = 0x00000020,
        Noto = 0x00000040,
        Gold = 0x00000080,
        Weight = 0x00000100,
        Direction = 0x00000200,
        Hue = 0x00000400,
        Body = 0x00000800,
        Armor = 0x00001000,
        StatCap = 0x00002000,
        GhostUpdate = 0x00004000,
        Followers = 0x00008000,
        Properties = 0x00010000,
        TithingPoints = 0x00020000,
        Resistances = 0x00040000,
        WeaponDamage = 0x00080000,
        Hair = 0x00100000,
        FacialHair = 0x00200000,
        Race = 0x00400000,
        HealthbarYellow = 0x00800000,
        HealthbarPoison = 0x01000000,

        Attributes = 0x0000001C
    }

    public enum VisibleDamageType
    {
        None,
        Related,
        Everyone,
        Selective
    }

    public enum ResistanceType
    {
        Physical,
        Fire,
        Cold,
        Poison,
        Energy
    }

    public enum ApplyPoisonResult
    {
        Poisoned,
        Immune,
        HigherPoisonActive,
        Cured
    }

    #endregion

    [Serializable]
    public class MobileNotConnectedException : Exception
    {
        public MobileNotConnectedException(Mobile source, string message)
            : base(message) { Source = source.ToString(); }
    }

    public delegate bool SkillCheckTargetHandler(Mobile from, SkillName skill, object target, double minSkill, double maxSkill);
    public delegate bool SkillCheckLocationHandler(Mobile from, SkillName skill, double minSkill, double maxSkill);
    public delegate bool SkillCheckDirectTargetHandler(Mobile from, SkillName skill, object target, double chance);
    public delegate bool SkillCheckDirectLocationHandler(Mobile from, SkillName skill, double chance);
    public delegate TimeSpan RegenRateHandler(Mobile from);
    public delegate bool AllowBeneficialHandler(Mobile from, Mobile target);
    public delegate bool AllowHarmfulHandler(Mobile from, Mobile target);
    public delegate Container CreateCorpseHandler(Mobile from, HairInfo hair, FacialHairInfo facialhair, List<Item> initialContent, List<Item> equipedItems);
    public delegate int AOSStatusHandler(Mobile from, int index);

    /// <summary>
    /// Base class representing players, npcs, and creatures.
    /// </summary>
    public class Mobile : IEntity, IHued, IComparable<Mobile>, ISerializable, ISpawnable
    {
        public int CompareTo(IEntity other) => other == null ? -1 : Serial.CompareTo(other.Serial);
        public int CompareTo(Mobile other) => CompareTo((IEntity)other);
        public int CompareTo(object other)
        {
            if (other == null || other is IEntity) return CompareTo((IEntity)other);
            throw new ArgumentException();
        }

        public static bool DragEffects { get; set; } = true;

        public static AllowBeneficialHandler AllowBeneficialHandler { get; set; }
        public static AllowHarmfulHandler AllowHarmfulHandler { get; set; }
        public static SkillCheckTargetHandler SkillCheckTargetHandler { get; set; }
        public static SkillCheckLocationHandler SkillCheckLocationHandler { get; set; }
        public static SkillCheckDirectTargetHandler SkillCheckDirectTargetHandler { get; set; }
        public static SkillCheckDirectLocationHandler SkillCheckDirectLocationHandler { get; set; }
        public static AOSStatusHandler AOSStatusHandler { get; set; }

        #region Regeneration

        public static RegenRateHandler HitsRegenRateHandler { get; set; }
        public static TimeSpan DefaultHitsRate { get; set; }
        public static RegenRateHandler StamRegenRateHandler { get; set; }
        public static TimeSpan DefaultStamRate { get; set; }
        public static RegenRateHandler ManaRegenRateHandler { get; set; }
        public static TimeSpan DefaultManaRate { get; set; }
        public static TimeSpan GetHitsRegenRate(Mobile m) => HitsRegenRateHandler == null ? DefaultHitsRate : HitsRegenRateHandler(m);
        public static TimeSpan GetStamRegenRate(Mobile m) => StamRegenRateHandler == null ? DefaultStamRate : StamRegenRateHandler(m);
        public static TimeSpan GetManaRegenRate(Mobile m) => ManaRegenRateHandler == null ? DefaultManaRate : ManaRegenRateHandler(m);

        #endregion

        class MovementRecord
        {
            public long _End;

            static Queue<MovementRecord> _InstancePool = new Queue<MovementRecord>();

            public static MovementRecord NewInstance(long end)
            {
                MovementRecord r;
                if (_InstancePool.Count > 0)
                {
                    r = _InstancePool.Dequeue();
                    r._End = end;
                }
                else r = new MovementRecord(end);
                return r;
            }

            MovementRecord(long end) => _End = end;

            public bool Expired()
            {
                var v = Core.TickCount - _End >= 0;
                if (v)
                    _InstancePool.Enqueue(this);
                return v;
            }
        }

        Map _Map;
        Point3D _Location;
        Direction _Direction;
        Body _Body;
        int _Hue;
        Poison _Poison;
        BaseGuild _Guild;
        string _GuildTitle;
        bool _Criminal;
        string _Name;
        int _Kills, _ShortTermMurders;
        string _Language;
        NetState _NetState;
        bool _Female, _Warmode, _Hidden, _Blessed, _Flying;
        int _StatCap;
        int _Str, _Dex, _Int;
        int _Hits, _Stam, _Mana;
        int _Fame, _Karma;
        AccessLevel _AccessLevel;
        Skills _Skills;
        bool _Player;
        string m_Title;
        int _LightLevel;
        int _TotalGold, _TotalItems, _TotalWeight;
        ISpell m_Spell;
        Target m_Target;
        Prompt m_Prompt;
        ContextMenu _ContextMenu;
        Mobile _Combatant;
        bool _CanHearGhosts;
        int _TithingPoints;
        bool _DisplayGuildTitle;
        Timer _ExpireCombatant;
        Timer _ExpireCriminal;
        Timer _ExpireAggrTimer;
        Timer _LogoutTimer;
        Timer _CombatTimer;
        Timer _ManaTimer, _HitsTimer, _StamTimer;
        bool _Paralyzed;
        ParalyzedTimer _ParaTimer;
        bool _Frozen;
        FrozenTimer m_FrozenTimer;
        int _Hunger;
        Region _Region;
        int _VirtualArmor;
        int _Followers, _FollowersMax;
        List<object> _actions; // prefer List<object> over ArrayList for more specific profiling information
        Queue<MovementRecord> m_MoveRecords;
        int m_WarmodeChanges = 0;
        DateTime m_NextWarmodeChange;
        WarmodeTimer m_WarmodeTimer;
        int m_VirtualArmorMod;
        VirtueInfo m_Virtues;
        Body m_BodyMod;
        Race _Race;

        static readonly TimeSpan WarmodeSpamCatch = TimeSpan.FromSeconds(Core.SE ? 1.0 : 0.5);
        static readonly TimeSpan WarmodeSpamDelay = TimeSpan.FromSeconds(Core.SE ? 4.0 : 2.0);
        const int WarmodeCatchCount = 4; // Allow four warmode changes in 0.5 seconds, any more will be delay for two seconds

        [CommandProperty(AccessLevel.GameMaster)]
        public Race Race
        {
            get
            {
                if (_Race == null)
                    _Race = Race.DefaultRace;
                return _Race;
            }
            set
            {
                var oldRace = Race;
                _Race = value;
                if (_Race == null)
                    _Race = Race.DefaultRace;
                Body = _Race.Body(this);
                UpdateResistances();
                Delta(MobileDelta.Race);
                OnRaceChange(oldRace);
            }
        }

        protected virtual void OnRaceChange(Race oldRace) { }
        public virtual double RacialSkillBonus => 0;
        public int[] Resistances { get; private set; }

        public virtual int BasePhysicalResistance => 0;
        public virtual int BaseFireResistance => 0;
        public virtual int BaseColdResistance => 0;
        public virtual int BasePoisonResistance => 0;
        public virtual int BaseEnergyResistance => 0;

        public virtual void ComputeLightLevels(out int global, out int personal)
        {
            ComputeBaseLightLevels(out global, out personal);
            _Region?.AlterLightLevel(this, ref global, ref personal);
        }

        public virtual void ComputeBaseLightLevels(out int global, out int personal)
        {
            global = 0;
            personal = _LightLevel;
        }

        public virtual void CheckLightLevels(bool forceResend) { }

        [CommandProperty(AccessLevel.Counselor)]
        public virtual int PhysicalResistance => GetResistance(ResistanceType.Physical);

        [CommandProperty(AccessLevel.Counselor)]
        public virtual int FireResistance => GetResistance(ResistanceType.Fire);

        [CommandProperty(AccessLevel.Counselor)]
        public virtual int ColdResistance => GetResistance(ResistanceType.Cold);

        [CommandProperty(AccessLevel.Counselor)]
        public virtual int PoisonResistance => GetResistance(ResistanceType.Poison);

        [CommandProperty(AccessLevel.Counselor)]
        public virtual int EnergyResistance => GetResistance(ResistanceType.Energy);

        public virtual void UpdateResistances()
        {
            if (Resistances == null)
                Resistances = new[] { int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue };
            var delta = false;
            for (var i = 0; i < Resistances.Length; ++i)
                if (Resistances[i] != int.MinValue)
                {
                    Resistances[i] = int.MinValue;
                    delta = true;
                }
            if (delta)
                Delta(MobileDelta.Resistances);
        }

        public virtual int GetResistance(ResistanceType type)
        {
            if (Resistances == null)
                Resistances = new int[5] { int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue };

            int v = (int)type;

            if (v < 0 || v >= Resistances.Length)
                return 0;

            int res = Resistances[v];

            if (res == int.MinValue)
            {
                ComputeResistances();
                res = Resistances[v];
            }

            return res;
        }

        public List<ResistanceMod> ResistanceMods { get; set; }

        public virtual void AddResistanceMod(ResistanceMod toAdd)
        {
            if (ResistanceMods == null)
            {
                ResistanceMods = new List<ResistanceMod>();
            }

            ResistanceMods.Add(toAdd);
            UpdateResistances();
        }

        public virtual void RemoveResistanceMod(ResistanceMod toRemove)
        {
            if (ResistanceMods != null)
            {
                ResistanceMods.Remove(toRemove);

                if (ResistanceMods.Count == 0)
                    ResistanceMods = null;
            }

            UpdateResistances();
        }

        private static int m_MaxPlayerResistance = 70;

        public static int MaxPlayerResistance { get { return m_MaxPlayerResistance; } set { m_MaxPlayerResistance = value; } }

        public virtual void ComputeResistances()
        {
            if (Resistances == null)
                Resistances = new int[5] { int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue };

            for (int i = 0; i < Resistances.Length; ++i)
                Resistances[i] = 0;

            Resistances[0] += this.BasePhysicalResistance;
            Resistances[1] += this.BaseFireResistance;
            Resistances[2] += this.BaseColdResistance;
            Resistances[3] += this.BasePoisonResistance;
            Resistances[4] += this.BaseEnergyResistance;

            for (int i = 0; ResistanceMods != null && i < ResistanceMods.Count; ++i)
            {
                ResistanceMod mod = ResistanceMods[i];
                int v = (int)mod.Type;

                if (v >= 0 && v < Resistances.Length)
                    Resistances[v] += mod.Offset;
            }

            for (int i = 0; i < Items.Count; ++i)
            {
                Item item = Items[i];

                if (item.CheckPropertyConfliction(this))
                    continue;

                Resistances[0] += item.PhysicalResistance;
                Resistances[1] += item.FireResistance;
                Resistances[2] += item.ColdResistance;
                Resistances[3] += item.PoisonResistance;
                Resistances[4] += item.EnergyResistance;
            }

            for (int i = 0; i < Resistances.Length; ++i)
            {
                int min = GetMinResistance((ResistanceType)i);
                int max = GetMaxResistance((ResistanceType)i);

                if (max < min)
                    max = min;

                if (Resistances[i] > max)
                    Resistances[i] = max;
                else if (Resistances[i] < min)
                    Resistances[i] = min;
            }
        }

        public virtual int GetMinResistance(ResistanceType type)
        {
            return int.MinValue;
        }

        public virtual int GetMaxResistance(ResistanceType type)
        {
            if (_Player)
                return m_MaxPlayerResistance;

            return int.MaxValue;
        }

        public int GetAOSStatus(int index)
        {
            return (AOSStatusHandler == null) ? 0 : AOSStatusHandler(this, index);
        }

        public virtual void SendPropertiesTo(Mobile from)
        {
            from.Send(PropertyList);
        }

        public virtual void OnAosSingleClick(Mobile from)
        {
            ObjectPropertyList opl = this.PropertyList;

            if (opl.Header > 0)
            {
                int hue;

                if (NameHue != -1)
                    hue = NameHue;
                else if (_AccessLevel > AccessLevel.Player)
                    hue = 11;
                else
                    hue = Notoriety.GetHue(Notoriety.Compute(from, this));

                from.Send(new MessageLocalized(Serial, Body, MessageType.Label, hue, 3, opl.Header, Name, opl.HeaderArgs));
            }
        }

        public virtual string ApplyNameSuffix(string suffix)
        {
            return suffix;
        }

        public virtual void AddNameProperties(ObjectPropertyList list)
        {
            string name = Name;

            if (name == null)
                name = String.Empty;

            string prefix = "";

            if (ShowFameTitle && (_Player || _Body.IsHuman) && _Fame >= 10000)
                prefix = _Female ? "Lady" : "Lord";

            string suffix = "";

            if (PropertyTitle && Title != null && Title.Length > 0)
                suffix = Title;

            BaseGuild guild = _Guild;

            if (guild != null && (_Player || _DisplayGuildTitle))
            {
                if (suffix.Length > 0)
                    suffix = String.Format("{0} [{1}]", suffix, Utility.FixHtml(guild.Abbreviation));
                else
                    suffix = String.Format("[{0}]", Utility.FixHtml(guild.Abbreviation));
            }

            suffix = ApplyNameSuffix(suffix);

            list.Add(1050045, "{0} \t{1}\t {2}", prefix, name, suffix); // ~1_PREFIX~~2_NAME~~3_SUFFIX~

            if (guild != null && (_DisplayGuildTitle || (_Player && guild.Type != GuildType.Regular)))
            {
                string type;

                if (guild.Type >= 0 && (int)guild.Type < m_GuildTypes.Length)
                    type = m_GuildTypes[(int)guild.Type];
                else
                    type = "";

                string title = GuildTitle;

                if (title == null)
                    title = "";
                else
                    title = title.Trim();

                if (NewGuildDisplay && title.Length > 0)
                {
                    list.Add("{0}, {1}", Utility.FixHtml(title), Utility.FixHtml(guild.Name));
                }
                else
                {
                    if (title.Length > 0)
                        list.Add("{0}, {1} Guild{2}", Utility.FixHtml(title), Utility.FixHtml(guild.Name), type);
                    else
                        list.Add(Utility.FixHtml(guild.Name));
                }
            }
        }

        public virtual bool NewGuildDisplay { get { return false; } }

        public virtual void GetProperties(ObjectPropertyList list)
        {
            AddNameProperties(list);
        }

        public virtual void GetChildProperties(ObjectPropertyList list, Item item)
        {
        }

        public virtual void GetChildNameProperties(ObjectPropertyList list, Item item)
        {
        }

        private void UpdateAggrExpire()
        {
            if (m_Deleted || (Aggressors.Count == 0 && Aggressed.Count == 0))
            {
                StopAggrExpire();
            }
            else if (_ExpireAggrTimer == null)
            {
                _ExpireAggrTimer = new ExpireAggressorsTimer(this);
                _ExpireAggrTimer.Start();
            }
        }

        private void StopAggrExpire()
        {
            if (_ExpireAggrTimer != null)
                _ExpireAggrTimer.Stop();

            _ExpireAggrTimer = null;
        }

        private void CheckAggrExpire()
        {
            for (int i = Aggressors.Count - 1; i >= 0; --i)
            {
                if (i >= Aggressors.Count)
                    continue;

                AggressorInfo info = Aggressors[i];

                if (info.Expired)
                {
                    Mobile attacker = info.Attacker;
                    attacker.RemoveAggressed(this);

                    Aggressors.RemoveAt(i);
                    info.Free();

                    if (_NetState != null && this.CanSee(attacker) && Utility.InUpdateRange(_Location, attacker._Location))
                    {
                        _NetState.Send(MobileIncoming.Create(_NetState, this, attacker));
                    }
                }
            }

            for (int i = Aggressed.Count - 1; i >= 0; --i)
            {
                if (i >= Aggressed.Count)
                    continue;

                AggressorInfo info = Aggressed[i];

                if (info.Expired)
                {
                    Mobile defender = info.Defender;
                    defender.RemoveAggressor(this);

                    Aggressed.RemoveAt(i);
                    info.Free();

                    if (_NetState != null && this.CanSee(defender) && Utility.InUpdateRange(_Location, defender._Location))
                    {
                        _NetState.Send(MobileIncoming.Create(_NetState, this, defender));
                    }
                }
            }

            UpdateAggrExpire();
        }

        public List<Mobile> Stabled { get; private set; }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public VirtueInfo Virtues { get { return m_Virtues; } set { } }

        public object Party { get; set; }
        public List<SkillMod> SkillMods { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int VirtualArmorMod
        {
            get
            {
                return m_VirtualArmorMod;
            }
            set
            {
                if (m_VirtualArmorMod != value)
                {
                    m_VirtualArmorMod = value;

                    Delta(MobileDelta.Armor);
                }
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="skill" /> changes in some way.
        /// </summary>
        public virtual void OnSkillInvalidated(Skill skill)
        {
        }

        public virtual void UpdateSkillMods()
        {
            ValidateSkillMods();

            for (int i = 0; i < SkillMods.Count; ++i)
            {
                SkillMod mod = SkillMods[i];

                Skill sk = _Skills[mod.Skill];

                if (sk != null)
                    sk.Update();
            }
        }

        public virtual void ValidateSkillMods()
        {
            for (int i = 0; i < SkillMods.Count;)
            {
                SkillMod mod = SkillMods[i];

                if (mod.CheckCondition())
                    ++i;
                else
                    InternalRemoveSkillMod(mod);
            }
        }

        public virtual void AddSkillMod(SkillMod mod)
        {
            if (mod == null)
                return;

            ValidateSkillMods();

            if (!SkillMods.Contains(mod))
            {
                SkillMods.Add(mod);
                mod.Owner = this;

                Skill sk = _Skills[mod.Skill];

                if (sk != null)
                    sk.Update();
            }
        }

        public virtual void RemoveSkillMod(SkillMod mod)
        {
            if (mod == null)
                return;

            ValidateSkillMods();

            InternalRemoveSkillMod(mod);
        }

        private void InternalRemoveSkillMod(SkillMod mod)
        {
            if (SkillMods.Contains(mod))
            {
                SkillMods.Remove(mod);
                mod.Owner = null;

                Skill sk = _Skills[mod.Skill];

                if (sk != null)
                    sk.Update();
            }
        }

        private class WarmodeTimer : Timer
        {
            private Mobile m_Mobile;
            private bool m_Value;

            public bool Value
            {
                get
                {
                    return m_Value;
                }
                set
                {
                    m_Value = value;
                }
            }

            public WarmodeTimer(Mobile m, bool value)
                : base(WarmodeSpamDelay)
            {
                m_Mobile = m;
                m_Value = value;
            }

            protected override void OnTick()
            {
                m_Mobile.Warmode = m_Value;
                m_Mobile.m_WarmodeChanges = 0;

                m_Mobile.m_WarmodeTimer = null;
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a client, <paramref name="from" />, invokes a 'help request' for the Mobile. Seemingly no longer functional in newer clients.
        /// </summary>
        public virtual void OnHelpRequest(Mobile from)
        {
        }

        public void DelayChangeWarmode(bool value)
        {
            if (m_WarmodeTimer != null)
            {
                m_WarmodeTimer.Value = value;
                return;
            }

            if (_Warmode == value)
                return;

            DateTime now = DateTime.UtcNow, next = m_NextWarmodeChange;

            if (now > next || m_WarmodeChanges == 0)
            {
                m_WarmodeChanges = 1;
                m_NextWarmodeChange = now + WarmodeSpamCatch;
            }
            else if (m_WarmodeChanges == WarmodeCatchCount)
            {
                m_WarmodeTimer = new WarmodeTimer(this, value);
                m_WarmodeTimer.Start();

                return;
            }
            else
            {
                ++m_WarmodeChanges;
            }

            Warmode = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int MeleeDamageAbsorb { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int MagicDamageAbsorb { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int SkillsTotal
        {
            get
            {
                return _Skills == null ? 0 : _Skills.Total;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int SkillsCap
        {
            get
            {
                return _Skills == null ? 0 : _Skills.Cap;
            }
            set
            {
                if (_Skills != null)
                    _Skills.Cap = value;
            }
        }

        public bool InLOS(Mobile target)
        {
            if (m_Deleted || _Map == null)
                return false;
            else if (target == this || _AccessLevel > AccessLevel.Player)
                return true;

            return _Map.LineOfSight(this, target);
        }

        public bool InLOS(object target)
        {
            if (m_Deleted || _Map == null)
                return false;
            else if (target == this || _AccessLevel > AccessLevel.Player)
                return true;
            else if (target is Item && ((Item)target).RootParent == this)
                return true;

            return _Map.LineOfSight(this, target);
        }

        public bool InLOS(Point3D target)
        {
            if (m_Deleted || _Map == null)
                return false;
            else if (_AccessLevel > AccessLevel.Player)
                return true;

            return _Map.LineOfSight(this, target);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int BaseSoundID { get; set; }

        public long NextCombatTime
        {
            get
            {
                return m_NextCombatTime;
            }
            set
            {
                m_NextCombatTime = value;
            }
        }

        public bool BeginAction(object toLock)
        {
            if (_actions == null)
            {
                _actions = new List<object>();

                _actions.Add(toLock);

                return true;
            }
            else if (!_actions.Contains(toLock))
            {
                _actions.Add(toLock);

                return true;
            }

            return false;
        }

        public bool CanBeginAction(object toLock)
        {
            return (_actions == null || !_actions.Contains(toLock));
        }

        public void EndAction(object toLock)
        {
            if (_actions != null)
            {
                _actions.Remove(toLock);

                if (_actions.Count == 0)
                {
                    _actions = null;
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int NameHue { get; set; } = -1;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Hunger
        {
            get
            {
                return _Hunger;
            }
            set
            {
                int oldValue = _Hunger;

                if (oldValue != value)
                {
                    _Hunger = value;

                    EventSink.InvokeHungerChanged(new HungerChangedEventArgs(this, oldValue));
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Thirst { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int BAC { get; set; }

        private long m_LastMoveTime;

        /// <summary>
        /// Gets or sets the number of steps this player may take when hidden before being revealed.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int AllowedStealthSteps { get; set; }

        /* Logout:
		 * 
		 * When a client logs into mobile x
		 *  - if ( x is Internalized ) move x to logout location and map
		 * 
		 * When a client attached to a mobile disconnects
		 *  - LogoutTimer is started
		 *	   - Delay is taken from Region.GetLogoutDelay to allow insta-logout regions.
		 *     - OnTick : Location and map are stored, and mobile is internalized
		 * 
		 * Some things to consider:
		 *  - An internalized person getting killed (say, by poison). Where does the body go?
		 *  - Regions now have a GetLogoutDelay( Mobile m ); virtual function (see above)
		 */
        private Point3D m_LogoutLocation;
        private Map m_LogoutMap;

        public virtual TimeSpan GetLogoutDelay()
        {
            return Region.GetLogoutDelay(this);
        }

        private StatLockType m_StrLock, m_DexLock, m_IntLock;

        private Item m_Holding;

        public Item Holding
        {
            get
            {
                return m_Holding;
            }
            set
            {
                if (m_Holding != value)
                {
                    if (m_Holding != null)
                    {
                        UpdateTotal(m_Holding, TotalType.Weight, -(m_Holding.TotalWeight + m_Holding.PileWeight));

                        if (m_Holding.HeldBy == this)
                            m_Holding.HeldBy = null;
                    }

                    if (value != null && m_Holding != null)
                        DropHolding();

                    m_Holding = value;

                    if (m_Holding != null)
                    {
                        UpdateTotal(m_Holding, TotalType.Weight, m_Holding.TotalWeight + m_Holding.PileWeight);

                        if (m_Holding.HeldBy == null)
                            m_Holding.HeldBy = this;
                    }
                }
            }
        }

        public long LastMoveTime
        {
            get
            {
                return m_LastMoveTime;
            }
            set
            {
                m_LastMoveTime = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual bool Paralyzed
        {
            get
            {
                return _Paralyzed;
            }
            set
            {
                if (_Paralyzed != value)
                {
                    _Paralyzed = value;
                    Delta(MobileDelta.Flags);

                    this.SendLocalizedMessage(_Paralyzed ? 502381 : 502382);

                    if (_ParaTimer != null)
                    {
                        _ParaTimer.Stop();
                        _ParaTimer = null;
                    }
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool DisarmReady { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool StunReady { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Frozen
        {
            get
            {
                return _Frozen;
            }
            set
            {
                if (_Frozen != value)
                {
                    _Frozen = value;
                    Delta(MobileDelta.Flags);

                    if (m_FrozenTimer != null)
                    {
                        m_FrozenTimer.Stop();
                        m_FrozenTimer = null;
                    }
                }
            }
        }

        public void Paralyze(TimeSpan duration)
        {
            if (!_Paralyzed)
            {
                Paralyzed = true;

                _ParaTimer = new ParalyzedTimer(this, duration);
                _ParaTimer.Start();
            }
        }

        public void Freeze(TimeSpan duration)
        {
            if (!_Frozen)
            {
                Frozen = true;

                m_FrozenTimer = new FrozenTimer(this, duration);
                m_FrozenTimer.Start();
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="StatLockType">lock state</see> for the <see cref="RawStr" /> property.
        /// </summary>
        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public StatLockType StrLock
        {
            get
            {
                return m_StrLock;
            }
            set
            {
                if (m_StrLock != value)
                {
                    m_StrLock = value;

                    if (_NetState != null)
                        _NetState.Send(new StatLockInfo(this));
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="StatLockType">lock state</see> for the <see cref="RawDex" /> property.
        /// </summary>
        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public StatLockType DexLock
        {
            get
            {
                return m_DexLock;
            }
            set
            {
                if (m_DexLock != value)
                {
                    m_DexLock = value;

                    if (_NetState != null)
                        _NetState.Send(new StatLockInfo(this));
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="StatLockType">lock state</see> for the <see cref="RawInt" /> property.
        /// </summary>
        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public StatLockType IntLock
        {
            get
            {
                return m_IntLock;
            }
            set
            {
                if (m_IntLock != value)
                {
                    m_IntLock = value;

                    if (_NetState != null)
                        _NetState.Send(new StatLockInfo(this));
                }
            }
        }

        public override string ToString()
        {
            return String.Format("0x{0:X} \"{1}\"", Serial.Value, Name);
        }

        public long NextActionTime { get; set; }

        public long NextActionMessage { get; set; }

        private static int m_ActionMessageDelay = 125;

        public static int ActionMessageDelay
        {
            get { return m_ActionMessageDelay; }
            set { m_ActionMessageDelay = value; }
        }

        public virtual void SendSkillMessage()
        {
            if (NextActionMessage - Core.TickCount >= 0)
                return;

            NextActionMessage = Core.TickCount + m_ActionMessageDelay;

            SendLocalizedMessage(500118); // You must wait a few moments to use another skill.
        }

        public virtual void SendActionMessage()
        {
            if (NextActionMessage - Core.TickCount >= 0)
                return;

            NextActionMessage = Core.TickCount + m_ActionMessageDelay;

            SendLocalizedMessage(500119); // You must wait to perform another action.
        }

        public virtual void ClearHands()
        {
            ClearHand(FindItemOnLayer(Layer.OneHanded));
            ClearHand(FindItemOnLayer(Layer.TwoHanded));
        }

        public virtual void ClearHand(Item item)
        {
            if (item != null && item.Movable && !item.AllowEquipedCast(this))
            {
                Container pack = this.Backpack;

                if (pack == null)
                    AddToBackpack(item);
                else
                    pack.DropItem(item);
            }
        }


        private static bool m_GlobalRegenThroughPoison = true;

        public static bool GlobalRegenThroughPoison
        {
            get { return m_GlobalRegenThroughPoison; }
            set { m_GlobalRegenThroughPoison = value; }
        }

        public virtual bool RegenThroughPoison { get { return m_GlobalRegenThroughPoison; } }

        public virtual bool CanRegenHits { get { return this.Alive && (RegenThroughPoison || !this.Poisoned); } }
        public virtual bool CanRegenStam { get { return this.Alive; } }
        public virtual bool CanRegenMana { get { return this.Alive; } }

        #region Timers

        private class ManaTimer : Timer
        {
            private Mobile m_Owner;

            public ManaTimer(Mobile m)
                : base(Mobile.GetManaRegenRate(m), Mobile.GetManaRegenRate(m))
            {
                this.Priority = TimerPriority.FiftyMS;
                m_Owner = m;
            }

            protected override void OnTick()
            {
                if (m_Owner.CanRegenMana)// m_Owner.Alive )
                    m_Owner.Mana++;

                Delay = Interval = Mobile.GetManaRegenRate(m_Owner);
            }
        }

        private class HitsTimer : Timer
        {
            private Mobile m_Owner;

            public HitsTimer(Mobile m)
                : base(Mobile.GetHitsRegenRate(m), Mobile.GetHitsRegenRate(m))
            {
                this.Priority = TimerPriority.FiftyMS;
                m_Owner = m;
            }

            protected override void OnTick()
            {
                if (m_Owner.CanRegenHits)// m_Owner.Alive && !m_Owner.Poisoned )
                    m_Owner.Hits++;

                Delay = Interval = Mobile.GetHitsRegenRate(m_Owner);
            }
        }

        private class StamTimer : Timer
        {
            private Mobile m_Owner;

            public StamTimer(Mobile m)
                : base(Mobile.GetStamRegenRate(m), Mobile.GetStamRegenRate(m))
            {
                this.Priority = TimerPriority.FiftyMS;
                m_Owner = m;
            }

            protected override void OnTick()
            {
                if (m_Owner.CanRegenStam)// m_Owner.Alive )
                    m_Owner.Stam++;

                Delay = Interval = Mobile.GetStamRegenRate(m_Owner);
            }
        }

        private class LogoutTimer : Timer
        {
            private Mobile m_Mobile;

            public LogoutTimer(Mobile m)
                : base(TimeSpan.FromDays(1.0))
            {
                Priority = TimerPriority.OneSecond;
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                if (m_Mobile._Map != Map.Internal)
                {
                    EventSink.InvokeLogout(new LogoutEventArgs(m_Mobile));

                    m_Mobile.m_LogoutLocation = m_Mobile._Location;
                    m_Mobile.m_LogoutMap = m_Mobile._Map;

                    m_Mobile.Internalize();
                }
            }
        }

        private class ParalyzedTimer : Timer
        {
            private Mobile m_Mobile;

            public ParalyzedTimer(Mobile m, TimeSpan duration)
                : base(duration)
            {
                this.Priority = TimerPriority.TwentyFiveMS;
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                m_Mobile.Paralyzed = false;
            }
        }

        private class FrozenTimer : Timer
        {
            private Mobile m_Mobile;

            public FrozenTimer(Mobile m, TimeSpan duration)
                : base(duration)
            {
                this.Priority = TimerPriority.TwentyFiveMS;
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                m_Mobile.Frozen = false;
            }
        }

        private class CombatTimer : Timer
        {
            private Mobile m_Mobile;

            public CombatTimer(Mobile m)
                : base(TimeSpan.FromSeconds(0.0), TimeSpan.FromSeconds(0.01), 0)
            {
                m_Mobile = m;

                if (!m_Mobile._Player && m_Mobile._Dex <= 100)
                    Priority = TimerPriority.FiftyMS;
            }

            protected override void OnTick()
            {
                if (Core.TickCount - m_Mobile.m_NextCombatTime >= 0)
                {
                    Mobile combatant = m_Mobile.Combatant;

                    // If no combatant, wrong map, one of us is a ghost, or cannot see, or deleted, then stop combat
                    if (combatant == null || combatant.m_Deleted || m_Mobile.m_Deleted || combatant._Map != m_Mobile._Map || !combatant.Alive || !m_Mobile.Alive || !m_Mobile.CanSee(combatant) || combatant.IsDeadBondedPet || m_Mobile.IsDeadBondedPet)
                    {
                        m_Mobile.Combatant = null;
                        return;
                    }

                    IWeapon weapon = m_Mobile.Weapon;

                    if (!m_Mobile.InRange(combatant, weapon.MaxRange))
                        return;

                    if (m_Mobile.InLOS(combatant))
                    {
                        weapon.OnBeforeSwing(m_Mobile, combatant);  //OnBeforeSwing for checking in regards to being hidden and whatnot
                        m_Mobile.RevealingAction();
                        m_Mobile.m_NextCombatTime = Core.TickCount + (int)weapon.OnSwing(m_Mobile, combatant).TotalMilliseconds;
                    }
                }
            }
        }

        private class ExpireCombatantTimer : Timer
        {
            private Mobile m_Mobile;

            public ExpireCombatantTimer(Mobile m)
                : base(TimeSpan.FromMinutes(1.0))
            {
                this.Priority = TimerPriority.FiveSeconds;
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                m_Mobile.Combatant = null;
            }
        }

        private static TimeSpan m_ExpireCriminalDelay = TimeSpan.FromMinutes(2.0);

        public static TimeSpan ExpireCriminalDelay
        {
            get { return m_ExpireCriminalDelay; }
            set { m_ExpireCriminalDelay = value; }
        }

        private class ExpireCriminalTimer : Timer
        {
            private Mobile m_Mobile;

            public ExpireCriminalTimer(Mobile m)
                : base(m_ExpireCriminalDelay)
            {
                this.Priority = TimerPriority.FiveSeconds;
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                m_Mobile.Criminal = false;
            }
        }

        private class ExpireAggressorsTimer : Timer
        {
            private Mobile m_Mobile;

            public ExpireAggressorsTimer(Mobile m)
                : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0))
            {
                m_Mobile = m;
                Priority = TimerPriority.FiveSeconds;
            }

            protected override void OnTick()
            {
                if (m_Mobile.Deleted || (m_Mobile.Aggressors.Count == 0 && m_Mobile.Aggressed.Count == 0))
                    m_Mobile.StopAggrExpire();
                else
                    m_Mobile.CheckAggrExpire();
            }
        }

        #endregion

        private long m_NextCombatTime;

        public long NextSkillTime { get; set; }

        public List<AggressorInfo> Aggressors { get; private set; }

        public List<AggressorInfo> Aggressed { get; private set; }

        private int m_ChangingCombatant;

        public bool ChangingCombatant
        {
            get { return (m_ChangingCombatant > 0); }
        }

        public virtual void Attack(Mobile m)
        {
            if (CheckAttack(m))
                Combatant = m;
        }

        public virtual bool CheckAttack(Mobile m)
        {
            return (Utility.InUpdateRange(this, m) && CanSee(m) && InLOS(m));
        }

        /// <summary>
        /// Overridable. Gets or sets which Mobile that this Mobile is currently engaged in combat with.
        /// <seealso cref="OnCombatantChange" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual Mobile Combatant
        {
            get
            {
                return _Combatant;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (_Combatant != value && value != this)
                {
                    Mobile old = _Combatant;

                    ++m_ChangingCombatant;
                    _Combatant = value;

                    if ((_Combatant != null && !CanBeHarmful(_Combatant, false)) || !Region.OnCombatantChange(this, old, _Combatant))
                    {
                        _Combatant = old;
                        --m_ChangingCombatant;
                        return;
                    }

                    if (_NetState != null)
                        _NetState.Send(new ChangeCombatant(_Combatant));

                    if (_Combatant == null)
                    {
                        if (_ExpireCombatant != null)
                            _ExpireCombatant.Stop();

                        if (_CombatTimer != null)
                            _CombatTimer.Stop();

                        _ExpireCombatant = null;
                        _CombatTimer = null;
                    }
                    else
                    {
                        if (_ExpireCombatant == null)
                            _ExpireCombatant = new ExpireCombatantTimer(this);

                        _ExpireCombatant.Start();

                        if (_CombatTimer == null)
                            _CombatTimer = new CombatTimer(this);

                        _CombatTimer.Start();
                    }

                    if (_Combatant != null && CanBeHarmful(_Combatant, false))
                    {
                        DoHarmful(_Combatant);

                        if (_Combatant != null)
                            _Combatant.PlaySound(_Combatant.GetAngerSound());
                    }

                    OnCombatantChange();
                    --m_ChangingCombatant;
                }
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked after the <see cref="Combatant" /> property has changed.
        /// <seealso cref="Combatant" />
        /// </summary>
        public virtual void OnCombatantChange()
        {
        }

        public double GetDistanceToSqrt(Point3D p)
        {
            int xDelta = _Location.X - p.X;
            int yDelta = _Location.Y - p.Y;

            return Math.Sqrt((xDelta * xDelta) + (yDelta * yDelta));
        }

        public double GetDistanceToSqrt(Mobile m)
        {
            int xDelta = _Location.X - m._Location.X;
            int yDelta = _Location.Y - m._Location.Y;

            return Math.Sqrt((xDelta * xDelta) + (yDelta * yDelta));
        }

        public double GetDistanceToSqrt(IPoint2D p)
        {
            int xDelta = _Location.X - p.X;
            int yDelta = _Location.Y - p.Y;

            return Math.Sqrt((xDelta * xDelta) + (yDelta * yDelta));
        }

        public virtual void AggressiveAction(Mobile aggressor)
        {
            AggressiveAction(aggressor, false);
        }

        public virtual void AggressiveAction(Mobile aggressor, bool criminal)
        {
            if (aggressor == this)
                return;

            AggressiveActionEventArgs args = AggressiveActionEventArgs.Create(this, aggressor, criminal);

            EventSink.InvokeAggressiveAction(args);

            args.Free();

            if (Combatant == aggressor)
            {
                if (_ExpireCombatant == null)
                    _ExpireCombatant = new ExpireCombatantTimer(this);
                else
                    _ExpireCombatant.Stop();

                _ExpireCombatant.Start();
            }

            bool addAggressor = true;

            List<AggressorInfo> list = Aggressors;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Attacker == aggressor)
                {
                    info.Refresh();
                    info.CriminalAggression = criminal;
                    info.CanReportMurder = criminal;

                    addAggressor = false;
                }
            }

            list = aggressor.Aggressors;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Attacker == this)
                {
                    info.Refresh();

                    addAggressor = false;
                }
            }

            bool addAggressed = true;

            list = Aggressed;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Defender == aggressor)
                {
                    info.Refresh();

                    addAggressed = false;
                }
            }

            list = aggressor.Aggressed;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Defender == this)
                {
                    info.Refresh();
                    info.CriminalAggression = criminal;
                    info.CanReportMurder = criminal;

                    addAggressed = false;
                }
            }

            bool setCombatant = false;

            if (addAggressor)
            {
                Aggressors.Add(AggressorInfo.Create(aggressor, this, criminal)); // new AggressorInfo( aggressor, this, criminal, true ) );

                if (this.CanSee(aggressor) && _NetState != null)
                {
                    _NetState.Send(MobileIncoming.Create(_NetState, this, aggressor));
                }

                if (Combatant == null)
                    setCombatant = true;

                UpdateAggrExpire();
            }

            if (addAggressed)
            {
                aggressor.Aggressed.Add(AggressorInfo.Create(aggressor, this, criminal)); // new AggressorInfo( aggressor, this, criminal, false ) );

                if (this.CanSee(aggressor) && _NetState != null)
                {
                    _NetState.Send(MobileIncoming.Create(_NetState, this, aggressor));
                }

                if (Combatant == null)
                    setCombatant = true;

                UpdateAggrExpire();
            }

            if (setCombatant)
                Combatant = aggressor;

            Region.OnAggressed(aggressor, this, criminal);
        }

        public void RemoveAggressed(Mobile aggressed)
        {
            if (m_Deleted)
                return;

            List<AggressorInfo> list = Aggressed;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Defender == aggressed)
                {
                    Aggressed.RemoveAt(i);
                    info.Free();

                    if (_NetState != null && this.CanSee(aggressed))
                    {
                        _NetState.Send(MobileIncoming.Create(_NetState, this, aggressed));
                    }

                    break;
                }
            }

            UpdateAggrExpire();
        }

        public void RemoveAggressor(Mobile aggressor)
        {
            if (m_Deleted)
                return;

            List<AggressorInfo> list = Aggressors;

            for (int i = 0; i < list.Count; ++i)
            {
                AggressorInfo info = list[i];

                if (info.Attacker == aggressor)
                {
                    Aggressors.RemoveAt(i);
                    info.Free();

                    if (_NetState != null && this.CanSee(aggressor))
                    {
                        _NetState.Send(MobileIncoming.Create(_NetState, this, aggressor));
                    }

                    break;
                }
            }

            UpdateAggrExpire();
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalGold
        {
            get { return GetTotal(TotalType.Gold); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalItems
        {
            get { return GetTotal(TotalType.Items); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalWeight
        {
            get { return GetTotal(TotalType.Weight); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TithingPoints
        {
            get
            {
                return _TithingPoints;
            }
            set
            {
                if (_TithingPoints != value)
                {
                    _TithingPoints = value;

                    Delta(MobileDelta.TithingPoints);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Followers
        {
            get
            {
                return _Followers;
            }
            set
            {
                if (_Followers != value)
                {
                    _Followers = value;

                    Delta(MobileDelta.Followers);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int FollowersMax
        {
            get
            {
                return _FollowersMax;
            }
            set
            {
                if (_FollowersMax != value)
                {
                    _FollowersMax = value;

                    Delta(MobileDelta.Followers);
                }
            }
        }

        public virtual int GetTotal(TotalType type)
        {
            switch (type)
            {
                case TotalType.Gold:
                    return _TotalGold;

                case TotalType.Items:
                    return _TotalItems;

                case TotalType.Weight:
                    return _TotalWeight;
            }

            return 0;
        }

        public virtual void UpdateTotal(Item sender, TotalType type, int delta)
        {
            if (delta == 0 || sender.IsVirtualItem)
                return;

            switch (type)
            {
                case TotalType.Gold:
                    _TotalGold += delta;
                    Delta(MobileDelta.Gold);
                    break;

                case TotalType.Items:
                    _TotalItems += delta;
                    break;

                case TotalType.Weight:
                    _TotalWeight += delta;
                    Delta(MobileDelta.Weight);
                    OnWeightChange(_TotalWeight - delta);
                    break;
            }
        }

        public virtual void UpdateTotals()
        {
            if (Items == null)
                return;

            int oldWeight = _TotalWeight;

            _TotalGold = 0;
            _TotalItems = 0;
            _TotalWeight = 0;

            for (int i = 0; i < Items.Count; ++i)
            {
                Item item = Items[i];

                item.UpdateTotals();

                if (item.IsVirtualItem)
                    continue;

                _TotalGold += item.TotalGold;
                _TotalItems += item.TotalItems + 1;
                _TotalWeight += item.TotalWeight + item.PileWeight;
            }

            if (m_Holding != null)
                _TotalWeight += m_Holding.TotalWeight + m_Holding.PileWeight;

            if (_TotalWeight != oldWeight)
                OnWeightChange(oldWeight);
        }

        public void ClearQuestArrow()
        {
            m_QuestArrow = null;
        }

        public void ClearTarget()
        {
            m_Target = null;
        }

        private bool m_TargetLocked;

        public bool TargetLocked
        {
            get
            {
                return m_TargetLocked;
            }
            set
            {
                m_TargetLocked = value;
            }
        }

        private class SimpleTarget : Target
        {
            private TargetCallback m_Callback;

            public SimpleTarget(int range, TargetFlags flags, bool allowGround, TargetCallback callback)
                : base(range, allowGround, flags)
            {
                m_Callback = callback;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (m_Callback != null)
                    m_Callback(from, targeted);
            }
        }

        public Target BeginTarget(int range, bool allowGround, TargetFlags flags, TargetCallback callback)
        {
            Target t = new SimpleTarget(range, flags, allowGround, callback);

            this.Target = t;

            return t;
        }

        private class SimpleStateTarget : Target
        {
            private TargetStateCallback m_Callback;
            private object m_State;

            public SimpleStateTarget(int range, TargetFlags flags, bool allowGround, TargetStateCallback callback, object state)
                : base(range, allowGround, flags)
            {
                m_Callback = callback;
                m_State = state;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (m_Callback != null)
                    m_Callback(from, targeted, m_State);
            }
        }

        public Target BeginTarget(int range, bool allowGround, TargetFlags flags, TargetStateCallback callback, object state)
        {
            Target t = new SimpleStateTarget(range, flags, allowGround, callback, state);

            this.Target = t;

            return t;
        }

        private class SimpleStateTarget<T> : Target
        {
            private TargetStateCallback<T> m_Callback;
            private T m_State;

            public SimpleStateTarget(int range, TargetFlags flags, bool allowGround, TargetStateCallback<T> callback, T state)
                : base(range, allowGround, flags)
            {
                m_Callback = callback;
                m_State = state;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (m_Callback != null)
                    m_Callback(from, targeted, m_State);
            }
        }
        public Target BeginTarget<T>(int range, bool allowGround, TargetFlags flags, TargetStateCallback<T> callback, T state)
        {
            Target t = new SimpleStateTarget<T>(range, flags, allowGround, callback, state);

            this.Target = t;

            return t;
        }

        public Target Target
        {
            get
            {
                return m_Target;
            }
            set
            {
                Target oldTarget = m_Target;
                Target newTarget = value;

                if (oldTarget == newTarget)
                    return;

                m_Target = null;

                if (oldTarget != null && newTarget != null)
                    oldTarget.Cancel(this, TargetCancelType.Overriden);

                m_Target = newTarget;

                if (newTarget != null && _NetState != null && !m_TargetLocked)
                    _NetState.Send(newTarget.GetPacketFor(_NetState));

                OnTargetChange();
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked after the <see cref="Target">Target property</see> has changed.
        /// </summary>
        protected virtual void OnTargetChange()
        {
        }

        public ContextMenu ContextMenu
        {
            get
            {
                return _ContextMenu;
            }
            set
            {
                _ContextMenu = value;

                if (_ContextMenu != null && _NetState != null)
                {
                    // Old packet is preferred until assistants catch up
                    if (_NetState.NewHaven && _ContextMenu.RequiresNewPacket)
                        Send(new DisplayContextMenu(_ContextMenu));
                    else
                        Send(new DisplayContextMenuOld(_ContextMenu));
                }
            }
        }

        public virtual bool CheckContextMenuDisplay(IEntity target)
        {
            return true;
        }

        #region Prompts
        private class SimplePrompt : Prompt
        {
            private PromptCallback m_Callback;
            private PromptCallback m_CancelCallback;
            private bool m_CallbackHandlesCancel;

            public SimplePrompt(PromptCallback callback, PromptCallback cancelCallback)
            {
                m_Callback = callback;
                m_CancelCallback = cancelCallback;
            }

            public SimplePrompt(PromptCallback callback, bool callbackHandlesCancel)
            {
                m_Callback = callback;
                m_CallbackHandlesCancel = callbackHandlesCancel;
            }

            public SimplePrompt(PromptCallback callback)
                : this(callback, false)
            {
            }

            public override void OnResponse(Mobile from, string text)
            {
                if (m_Callback != null)
                    m_Callback(from, text);
            }

            public override void OnCancel(Mobile from)
            {
                if (m_CallbackHandlesCancel && m_Callback != null)
                    m_Callback(from, "");
                else if (m_CancelCallback != null)
                    m_CancelCallback(from, "");
            }
        }
        public Prompt BeginPrompt(PromptCallback callback, PromptCallback cancelCallback)
        {
            Prompt p = new SimplePrompt(callback, cancelCallback);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt(PromptCallback callback, bool callbackHandlesCancel)
        {
            Prompt p = new SimplePrompt(callback, callbackHandlesCancel);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt(PromptCallback callback)
        {
            return BeginPrompt(callback, false);
        }

        private class SimpleStatePrompt : Prompt
        {
            private PromptStateCallback m_Callback;
            private PromptStateCallback m_CancelCallback;

            private bool m_CallbackHandlesCancel;

            private object m_State;

            public SimpleStatePrompt(PromptStateCallback callback, PromptStateCallback cancelCallback, object state)
            {
                m_Callback = callback;
                m_CancelCallback = cancelCallback;
                m_State = state;
            }
            public SimpleStatePrompt(PromptStateCallback callback, bool callbackHandlesCancel, object state)
            {
                m_Callback = callback;
                m_State = state;
                m_CallbackHandlesCancel = callbackHandlesCancel;
            }
            public SimpleStatePrompt(PromptStateCallback callback, object state)
                : this(callback, false, state)
            {
            }

            public override void OnResponse(Mobile from, string text)
            {
                if (m_Callback != null)
                    m_Callback(from, text, m_State);
            }

            public override void OnCancel(Mobile from)
            {
                if (m_CallbackHandlesCancel && m_Callback != null)
                    m_Callback(from, "", m_State);
                else if (m_CancelCallback != null)
                    m_CancelCallback(from, "", m_State);
            }
        }
        public Prompt BeginPrompt(PromptStateCallback callback, PromptStateCallback cancelCallback, object state)
        {
            Prompt p = new SimpleStatePrompt(callback, cancelCallback, state);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt(PromptStateCallback callback, bool callbackHandlesCancel, object state)
        {
            Prompt p = new SimpleStatePrompt(callback, callbackHandlesCancel, state);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt(PromptStateCallback callback, object state)
        {
            return BeginPrompt(callback, false, state);
        }

        private class SimpleStatePrompt<T> : Prompt
        {
            private PromptStateCallback<T> m_Callback;
            private PromptStateCallback<T> m_CancelCallback;

            private bool m_CallbackHandlesCancel;

            private T m_State;

            public SimpleStatePrompt(PromptStateCallback<T> callback, PromptStateCallback<T> cancelCallback, T state)
            {
                m_Callback = callback;
                m_CancelCallback = cancelCallback;
                m_State = state;
            }
            public SimpleStatePrompt(PromptStateCallback<T> callback, bool callbackHandlesCancel, T state)
            {
                m_Callback = callback;
                m_State = state;
                m_CallbackHandlesCancel = callbackHandlesCancel;
            }
            public SimpleStatePrompt(PromptStateCallback<T> callback, T state)
                : this(callback, false, state)
            {
            }

            public override void OnResponse(Mobile from, string text)
            {
                if (m_Callback != null)
                    m_Callback(from, text, m_State);
            }

            public override void OnCancel(Mobile from)
            {
                if (m_CallbackHandlesCancel && m_Callback != null)
                    m_Callback(from, "", m_State);
                else if (m_CancelCallback != null)
                    m_CancelCallback(from, "", m_State);
            }
        }
        public Prompt BeginPrompt<T>(PromptStateCallback<T> callback, PromptStateCallback<T> cancelCallback, T state)
        {
            Prompt p = new SimpleStatePrompt<T>(callback, cancelCallback, state);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt<T>(PromptStateCallback<T> callback, bool callbackHandlesCancel, T state)
        {
            Prompt p = new SimpleStatePrompt<T>(callback, callbackHandlesCancel, state);

            this.Prompt = p;
            return p;
        }
        public Prompt BeginPrompt<T>(PromptStateCallback<T> callback, T state)
        {
            return BeginPrompt(callback, false, state);
        }

        public Prompt Prompt
        {
            get
            {
                return m_Prompt;
            }
            set
            {
                Prompt oldPrompt = m_Prompt;
                Prompt newPrompt = value;

                if (oldPrompt == newPrompt)
                    return;

                m_Prompt = null;

                if (oldPrompt != null && newPrompt != null)
                    oldPrompt.OnCancel(this);

                m_Prompt = newPrompt;

                if (newPrompt != null)
                    Send(new UnicodePrompt(newPrompt));
            }
        }
        #endregion

        private bool InternalOnMove(Direction d)
        {
            if (!OnMove(d))
                return false;

            MovementEventArgs e = MovementEventArgs.Create(this, d);

            EventSink.InvokeMovement(e);

            bool ret = !e.Blocked;

            e.Free();

            return ret;
        }

        /// <summary>
        /// Overridable. Event invoked before the Mobile <see cref="Move">moves</see>.
        /// </summary>
        /// <returns>True if the move is allowed, false if not.</returns>
        protected virtual bool OnMove(Direction d)
        {
            if (_Hidden && _AccessLevel == AccessLevel.Player)
            {
                if (AllowedStealthSteps-- <= 0 || (d & Direction.Running) != 0 || this.Mounted)
                    RevealingAction();
            }

            return true;
        }

        private static Packet[][] m_MovingPacketCache = new Packet[2][]
            {
                new Packet[8],
                new Packet[8]
            };

        private bool m_Pushing;

        public bool Pushing
        {
            get
            {
                return m_Pushing;
            }
            set
            {
                m_Pushing = value;
            }
        }

        private static int m_WalkFoot = 400;
        private static int m_RunFoot = 200;
        private static int m_WalkMount = 200;
        private static int m_RunMount = 100;

        public static int WalkFoot { get { return m_WalkFoot; } set { m_WalkFoot = value; } }
        public static int RunFoot { get { return m_RunFoot; } set { m_RunFoot = value; } }
        public static int WalkMount { get { return m_WalkMount; } set { m_WalkMount = value; } }
        public static int RunMount { get { return m_RunMount; } set { m_RunMount = value; } }

        private long m_EndQueue;

        private static List<IEntity> m_MoveList = new List<IEntity>();
        private static List<Mobile> m_MoveClientList = new List<Mobile>();

        private static AccessLevel m_FwdAccessOverride = AccessLevel.Counselor;
        private static bool m_FwdEnabled = true;
        private static bool m_FwdUOTDOverride = false;
        private static int m_FwdMaxSteps = 4;

        public static AccessLevel FwdAccessOverride { get { return m_FwdAccessOverride; } set { m_FwdAccessOverride = value; } }
        public static bool FwdEnabled { get { return m_FwdEnabled; } set { m_FwdEnabled = value; } }
        public static bool FwdUOTDOverride { get { return m_FwdUOTDOverride; } set { m_FwdUOTDOverride = value; } }
        public static int FwdMaxSteps { get { return m_FwdMaxSteps; } set { m_FwdMaxSteps = value; } }

        public virtual void ClearFastwalkStack()
        {
            if (m_MoveRecords != null && m_MoveRecords.Count > 0)
                m_MoveRecords.Clear();

            m_EndQueue = Core.TickCount;
        }

        public virtual bool CheckMovement(Direction d, out int newZ)
        {
            return Movement.Movement.CheckMovement(this, d, out newZ);
        }

        public virtual bool Move(Direction d)
        {
            if (m_Deleted)
                return false;

            BankBox box = FindBankNoCreate();

            if (box != null && box.Opened)
                box.Close();

            Point3D newLocation = _Location;
            Point3D oldLocation = newLocation;

            if ((_Direction & Direction.Mask) == (d & Direction.Mask))
            {
                // We are actually moving (not just a direction change)

                if (m_Spell != null && !m_Spell.OnCasterMoving(d))
                    return false;

                if (_Paralyzed || _Frozen)
                {
                    SendLocalizedMessage(500111); // You are frozen and can not move.

                    return false;
                }

                int newZ;

                if (CheckMovement(d, out newZ))
                {
                    int x = oldLocation.X, y = oldLocation.Y;
                    int oldX = x, oldY = y;
                    int oldZ = oldLocation.Z;

                    switch (d & Direction.Mask)
                    {
                        case Direction.North:
                            --y;
                            break;
                        case Direction.Right:
                            ++x;
                            --y;
                            break;
                        case Direction.East:
                            ++x;
                            break;
                        case Direction.Down:
                            ++x;
                            ++y;
                            break;
                        case Direction.South:
                            ++y;
                            break;
                        case Direction.Left:
                            --x;
                            ++y;
                            break;
                        case Direction.West:
                            --x;
                            break;
                        case Direction.Up:
                            --x;
                            --y;
                            break;
                    }

                    newLocation.X = x;
                    newLocation.Y = y;
                    newLocation.Z = newZ;

                    m_Pushing = false;

                    Map map = _Map;

                    if (map != null)
                    {
                        Sector oldSector = map.GetSector(oldX, oldY);
                        Sector newSector = map.GetSector(x, y);

                        if (oldSector != newSector)
                        {
                            for (int i = 0; i < oldSector.Mobiles.Count; ++i)
                            {
                                Mobile m = oldSector.Mobiles[i];

                                if (m != this && m.X == oldX && m.Y == oldY && (m.Z + 15) > oldZ && (oldZ + 15) > m.Z && !m.OnMoveOff(this))
                                    return false;
                            }

                            for (int i = 0; i < oldSector.Items.Count; ++i)
                            {
                                Item item = oldSector.Items[i];

                                if (item.AtWorldPoint(oldX, oldY) && (item.Z == oldZ || ((item.Z + item.ItemData.Height) > oldZ && (oldZ + 15) > item.Z)) && !item.OnMoveOff(this))
                                    return false;
                            }

                            for (int i = 0; i < newSector.Mobiles.Count; ++i)
                            {
                                Mobile m = newSector.Mobiles[i];

                                if (m.X == x && m.Y == y && (m.Z + 15) > newZ && (newZ + 15) > m.Z && !m.OnMoveOver(this))
                                    return false;
                            }

                            for (int i = 0; i < newSector.Items.Count; ++i)
                            {
                                Item item = newSector.Items[i];

                                if (item.AtWorldPoint(x, y) && (item.Z == newZ || ((item.Z + item.ItemData.Height) > newZ && (newZ + 15) > item.Z)) && !item.OnMoveOver(this))
                                    return false;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < oldSector.Mobiles.Count; ++i)
                            {
                                Mobile m = oldSector.Mobiles[i];

                                if (m != this && m.X == oldX && m.Y == oldY && (m.Z + 15) > oldZ && (oldZ + 15) > m.Z && !m.OnMoveOff(this))
                                    return false;
                                else if (m.X == x && m.Y == y && (m.Z + 15) > newZ && (newZ + 15) > m.Z && !m.OnMoveOver(this))
                                    return false;
                            }

                            for (int i = 0; i < oldSector.Items.Count; ++i)
                            {
                                Item item = oldSector.Items[i];

                                if (item.AtWorldPoint(oldX, oldY) && (item.Z == oldZ || ((item.Z + item.ItemData.Height) > oldZ && (oldZ + 15) > item.Z)) && !item.OnMoveOff(this))
                                    return false;
                                else if (item.AtWorldPoint(x, y) && (item.Z == newZ || ((item.Z + item.ItemData.Height) > newZ && (newZ + 15) > item.Z)) && !item.OnMoveOver(this))
                                    return false;
                            }
                        }

                        if (!Region.CanMove(this, d, newLocation, oldLocation, _Map))
                            return false;
                    }
                    else
                    {
                        return false;
                    }

                    if (!InternalOnMove(d))
                        return false;

                    if (m_FwdEnabled && _NetState != null && _AccessLevel < m_FwdAccessOverride && (!m_FwdUOTDOverride || !_NetState.IsUOTDClient))
                    {
                        if (m_MoveRecords == null)
                            m_MoveRecords = new Queue<MovementRecord>(6);

                        while (m_MoveRecords.Count > 0)
                        {
                            MovementRecord r = m_MoveRecords.Peek();

                            if (r.Expired())
                                m_MoveRecords.Dequeue();
                            else
                                break;
                        }

                        if (m_MoveRecords.Count >= m_FwdMaxSteps)
                        {
                            FastWalkEventArgs fw = new FastWalkEventArgs(_NetState);
                            EventSink.InvokeFastWalk(fw);

                            if (fw.Blocked)
                                return false;
                        }

                        int delay = ComputeMovementSpeed(d);

                        long end;

                        if (m_MoveRecords.Count > 0)
                            end = m_EndQueue + delay;
                        else
                            end = Core.TickCount + delay;

                        m_MoveRecords.Enqueue(MovementRecord.NewInstance(end));

                        m_EndQueue = end;
                    }

                    m_LastMoveTime = Core.TickCount;
                }
                else
                {
                    return false;
                }

                DisruptiveAction();
            }

            if (_NetState != null)
                _NetState.Send(MovementAck.Instantiate(_NetState.Sequence, this));//new MovementAck( m_NetState.Sequence, this ) );

            SetLocation(newLocation, false);
            SetDirection(d);

            if (_Map != null)
            {
                IPooledEnumerable<IEntity> eable = _Map.GetObjectsInRange(_Location, Core.GlobalMaxUpdateRange);

                foreach (IEntity o in eable)
                {
                    if (o == this)
                        continue;

                    if (o is Mobile)
                    {
                        Mobile mob = o as Mobile;
                        if (mob.NetState != null)
                            m_MoveClientList.Add(mob);
                        m_MoveList.Add(o);
                    }
                    else if (o is Item)
                    {
                        Item item = (Item)o;

                        if (item.HandlesOnMovement)
                            m_MoveList.Add(item);
                    }
                }

                eable.Free();

                Packet[][] cache = m_MovingPacketCache;

                /*for( int i = 0; i < cache.Length; ++i )
					for( int j = 0; j < cache[i].Length; ++j )
						Packet.Release( ref cache[i][j] );*/

                foreach (Mobile m in m_MoveClientList)
                {
                    NetState ns = m.NetState;

                    if (ns != null && Utility.InUpdateRange(_Location, m._Location) && m.CanSee(this))
                    {
                        if (ns.StygianAbyss)
                        {
                            Packet p;
                            int noto = Notoriety.Compute(m, this);
                            p = cache[0][noto];

                            if (p == null)
                                cache[0][noto] = p = Packet.Acquire(new MobileMoving(this, noto));

                            ns.Send(p);
                        }
                        else
                        {
                            Packet p;
                            int noto = Notoriety.Compute(m, this);
                            p = cache[1][noto];

                            if (p == null)
                                cache[1][noto] = p = Packet.Acquire(new MobileMovingOld(this, noto));

                            ns.Send(p);
                        }
                    }
                }

                for (int i = 0; i < cache.Length; ++i)
                    for (int j = 0; j < cache[i].Length; ++j)
                        Packet.Release(ref cache[i][j]);

                for (int i = 0; i < m_MoveList.Count; ++i)
                {
                    IEntity o = m_MoveList[i];

                    if (o is Mobile)
                    {
                        ((Mobile)o).OnMovement(this, oldLocation);
                    }
                    else if (o is Item)
                    {
                        ((Item)o).OnMovement(this, oldLocation);
                    }
                }

                if (m_MoveList.Count > 0)
                    m_MoveList.Clear();

                if (m_MoveClientList.Count > 0)
                    m_MoveClientList.Clear();
            }

            OnAfterMove(oldLocation);
            return true;
        }

        public virtual void OnAfterMove(Point3D oldLocation)
        {
        }

        public int ComputeMovementSpeed()
        {
            return ComputeMovementSpeed(this.Direction, false);
        }

        public int ComputeMovementSpeed(Direction dir)
        {
            return ComputeMovementSpeed(dir, true);
        }

        public virtual int ComputeMovementSpeed(Direction dir, bool checkTurning)
        {
            int delay;

            if (Mounted)
                delay = (dir & Direction.Running) != 0 ? m_RunMount : m_WalkMount;
            else
                delay = (dir & Direction.Running) != 0 ? m_RunFoot : m_WalkFoot;

            return delay;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a Mobile <paramref name="m" /> moves off this Mobile.
        /// </summary>
        /// <returns>True if the move is allowed, false if not.</returns>
        public virtual bool OnMoveOff(Mobile m)
        {
            return true;
        }

        public virtual bool IsDeadBondedPet { get { return false; } }

        /// <summary>
        /// Overridable. Event invoked when a Mobile <paramref name="m" /> moves over this Mobile.
        /// </summary>
        /// <returns>True if the move is allowed, false if not.</returns>
        public virtual bool OnMoveOver(Mobile m)
        {
            if (_Map == null || m_Deleted)
                return true;

            return m.CheckShove(this);
        }

        public virtual bool CheckShove(Mobile shoved)
        {
            if ((_Map.Rules & MapRules.FreeMovement) == 0)
            {
                if (!shoved.Alive || !Alive || shoved.IsDeadBondedPet || IsDeadBondedPet)
                    return true;
                else if (shoved._Hidden && shoved._AccessLevel > AccessLevel.Player)
                    return true;

                if (!m_Pushing)
                {
                    m_Pushing = true;

                    int number;

                    if (this.AccessLevel > AccessLevel.Player)
                    {
                        number = shoved._Hidden ? 1019041 : 1019040;
                    }
                    else
                    {
                        if (Stam == StamMax)
                        {
                            number = shoved._Hidden ? 1019043 : 1019042;
                            Stam -= 10;

                            RevealingAction();
                        }
                        else
                        {
                            return false;
                        }
                    }

                    SendLocalizedMessage(number);
                }
            }
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile sees another Mobile, <paramref name="m" />, move.
        /// </summary>
        public virtual void OnMovement(Mobile m, Point3D oldLocation)
        {
        }

        public ISpell Spell
        {
            get
            {
                return m_Spell;
            }
            set
            {
                if (m_Spell != null && value != null)
                    Console.WriteLine("Warning: Spell has been overwritten");

                m_Spell = value;
            }
        }

        [CommandProperty(AccessLevel.Administrator)]
        public bool AutoPageNotify { get; set; }

        public virtual void CriminalAction(bool message)
        {
            if (m_Deleted)
                return;

            Criminal = true;

            this.Region.OnCriminalAction(this, message);
        }

        public virtual bool IsSnoop(Mobile from)
        {
            return (from != this);
        }

        /// <summary>
        /// Overridable. Any call to <see cref="Resurrect" /> will silently fail if this method returns false.
        /// <seealso cref="Resurrect" />
        /// </summary>
        public virtual bool CheckResurrect()
        {
            return true;
        }

        /// <summary>
        /// Overridable. Event invoked before the Mobile is <see cref="Resurrect">resurrected</see>.
        /// <seealso cref="Resurrect" />
        /// </summary>
        public virtual void OnBeforeResurrect()
        {
        }

        /// <summary>
        /// Overridable. Event invoked after the Mobile is <see cref="Resurrect">resurrected</see>.
        /// <seealso cref="Resurrect" />
        /// </summary>
        public virtual void OnAfterResurrect()
        {
        }

        public virtual void Resurrect()
        {
            if (!Alive)
            {
                if (!Region.OnResurrect(this))
                    return;

                if (!CheckResurrect())
                    return;

                OnBeforeResurrect();

                BankBox box = FindBankNoCreate();

                if (box != null && box.Opened)
                    box.Close();

                Poison = null;

                Warmode = false;

                Hits = 10;
                Stam = StamMax;
                Mana = 0;

                BodyMod = 0;
                Body = this.Race.AliveBody(this);

                ProcessDeltaQueue();

                for (int i = Items.Count - 1; i >= 0; --i)
                {
                    if (i >= Items.Count)
                        continue;

                    Item item = Items[i];

                    if (item.ItemID == 0x204E)
                        item.Delete();
                }

                this.SendIncomingPacket();
                this.SendIncomingPacket();

                OnAfterResurrect();

                //Send( new DeathStatus( false ) );
            }
        }

        private IAccount m_Account;

        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Owner)]
        public IAccount Account
        {
            get
            {
                return m_Account;
            }
            set
            {
                m_Account = value;
            }
        }

        private bool m_Deleted;

        public bool Deleted
        {
            get
            {
                return m_Deleted;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int VirtualArmor
        {
            get
            {
                return _VirtualArmor;
            }
            set
            {
                if (_VirtualArmor != value)
                {
                    _VirtualArmor = value;

                    Delta(MobileDelta.Armor);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual double ArmorRating
        {
            get
            {
                return 0.0;
            }
        }

        public void DropHolding()
        {
            Item holding = m_Holding;

            if (holding != null)
            {
                if (!holding.Deleted && holding.HeldBy == this && holding.Map == Map.Internal)
                    AddToBackpack(holding);

                Holding = null;
                holding.ClearBounce();
            }
        }

        public virtual void Delete()
        {
            if (m_Deleted)
                return;
            else if (!World.OnDelete(this))
                return;

            if (_NetState != null)
                _NetState.CancelAllTrades();

            if (_NetState != null)
                _NetState.Dispose();

            DropHolding();

            Region.OnRegionChange(this, _Region, null);

            _Region = null;
            //Is the above line REALLY needed?  The old Region system did NOT have said line
            //and worked fine, because of this a LOT of extra checks have to be done everywhere...
            //I guess this should be there for Garbage collection purposes, but, still, is it /really/ needed?

            OnDelete();

            for (int i = Items.Count - 1; i >= 0; --i)
                if (i < Items.Count)
                    Items[i].OnParentDeleted(this);

            for (int i = 0; i < Stabled.Count; i++)
                Stabled[i].Delete();

            SendRemovePacket();

            if (_Guild != null)
                _Guild.OnDelete(this);

            m_Deleted = true;

            if (_Map != null)
            {
                _Map.OnLeave(this);
                _Map = null;
            }

            m_Hair = null;
            m_FacialHair = null;
            m_MountItem = null;

            World.RemoveMobile(this);

            OnAfterDelete();

            FreeCache();
        }

        /// <summary>
        /// Overridable. Virtual event invoked before the Mobile is deleted.
        /// </summary>
        public virtual void OnDelete()
        {
            if (m_Spawner != null)
            {
                m_Spawner.Remove(this);
                m_Spawner = null;
            }
        }

        /// <summary>
        /// Overridable. Returns true if the player is alive, false if otherwise. By default, this is computed by: <c>!Deleted &amp;&amp; (!Player || !Body.IsGhost)</c>
        /// </summary>
        [CommandProperty(AccessLevel.Counselor)]
        public virtual bool Alive
        {
            get
            {
                return !m_Deleted && (!_Player || !_Body.IsGhost);
            }
        }

        public virtual bool CheckSpellCast(ISpell spell)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile casts a <paramref name="spell" />.
        /// </summary>
        /// <param name="spell"></param>
        public virtual void OnSpellCast(ISpell spell)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked after <see cref="TotalWeight" /> changes.
        /// </summary>
        public virtual void OnWeightChange(int oldValue)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the <see cref="Skill.Base" /> or <see cref="Skill.BaseFixedPoint" /> property of <paramref name="skill" /> changes.
        /// </summary>
        public virtual void OnSkillChange(SkillName skill, double oldBase)
        {
        }

        /// <summary>
        /// Overridable. Invoked after the mobile is deleted. When overriden, be sure to call the base method.
        /// </summary>
        public virtual void OnAfterDelete()
        {
            StopAggrExpire();

            CheckAggrExpire();

            if (PoisonTimer != null)
                PoisonTimer.Stop();

            if (_HitsTimer != null)
                _HitsTimer.Stop();

            if (_StamTimer != null)
                _StamTimer.Stop();

            if (_ManaTimer != null)
                _ManaTimer.Stop();

            if (_CombatTimer != null)
                _CombatTimer.Stop();

            if (_ExpireCombatant != null)
                _ExpireCombatant.Stop();

            if (_LogoutTimer != null)
                _LogoutTimer.Stop();

            if (_ExpireCriminal != null)
                _ExpireCriminal.Stop();

            if (m_WarmodeTimer != null)
                m_WarmodeTimer.Stop();

            if (_ParaTimer != null)
                _ParaTimer.Stop();

            if (m_FrozenTimer != null)
                m_FrozenTimer.Stop();

            if (m_AutoManifestTimer != null)
                m_AutoManifestTimer.Stop();
        }

        public virtual bool AllowSkillUse(SkillName name)
        {
            return true;
        }

        public virtual bool UseSkill(SkillName name)
        {
            return Skills.UseSkill(this, name);
        }

        public virtual bool UseSkill(int skillID)
        {
            return Skills.UseSkill(this, skillID);
        }

        private static CreateCorpseHandler m_CreateCorpse;

        public static CreateCorpseHandler CreateCorpseHandler
        {
            get { return m_CreateCorpse; }
            set { m_CreateCorpse = value; }
        }

        public virtual DeathMoveResult GetParentMoveResultFor(Item item)
        {
            return item.OnParentDeath(this);
        }

        public virtual DeathMoveResult GetInventoryMoveResultFor(Item item)
        {
            return item.OnInventoryDeath(this);
        }

        public virtual bool RetainPackLocsOnDeath { get { return Core.AOS; } }

        public virtual void Kill()
        {
            if (!CanBeDamaged())
                return;
            else if (!Alive || IsDeadBondedPet)
                return;
            else if (m_Deleted)
                return;
            else if (!Region.OnBeforeDeath(this))
                return;
            else if (!OnBeforeDeath())
                return;

            BankBox box = FindBankNoCreate();

            if (box != null && box.Opened)
                box.Close();

            if (_NetState != null)
                _NetState.CancelAllTrades();

            if (m_Spell != null)
                m_Spell.OnCasterKilled();
            //m_Spell.Disturb( DisturbType.Kill );

            if (m_Target != null)
                m_Target.Cancel(this, TargetCancelType.Canceled);

            DisruptiveAction();

            Warmode = false;

            DropHolding();

            Hits = 0;
            Stam = 0;
            Mana = 0;

            Poison = null;
            Combatant = null;

            if (Paralyzed)
            {
                Paralyzed = false;

                if (_ParaTimer != null)
                    _ParaTimer.Stop();
            }

            if (Frozen)
            {
                Frozen = false;

                if (m_FrozenTimer != null)
                    m_FrozenTimer.Stop();
            }

            List<Item> content = new List<Item>();
            List<Item> equip = new List<Item>();
            List<Item> moveToPack = new List<Item>();

            List<Item> itemsCopy = new List<Item>(Items);

            Container pack = this.Backpack;

            for (int i = 0; i < itemsCopy.Count; ++i)
            {
                Item item = itemsCopy[i];

                if (item == pack)
                    continue;

                DeathMoveResult res = GetParentMoveResultFor(item);

                switch (res)
                {
                    case DeathMoveResult.MoveToCorpse:
                        {
                            content.Add(item);
                            equip.Add(item);
                            break;
                        }
                    case DeathMoveResult.MoveToBackpack:
                        {
                            moveToPack.Add(item);
                            break;
                        }
                }
            }

            if (pack != null)
            {
                List<Item> packCopy = new List<Item>(pack.Items);

                for (int i = 0; i < packCopy.Count; ++i)
                {
                    Item item = packCopy[i];

                    DeathMoveResult res = GetInventoryMoveResultFor(item);

                    if (res == DeathMoveResult.MoveToCorpse)
                        content.Add(item);
                    else
                        moveToPack.Add(item);
                }

                for (int i = 0; i < moveToPack.Count; ++i)
                {
                    Item item = moveToPack[i];

                    if (RetainPackLocsOnDeath && item.Parent == pack)
                        continue;

                    pack.DropItem(item);
                }
            }

            HairInfo hair = null;
            if (m_Hair != null)
                hair = new HairInfo(m_Hair.ItemID, m_Hair.Hue);

            FacialHairInfo facialhair = null;
            if (m_FacialHair != null)
                facialhair = new FacialHairInfo(m_FacialHair.ItemID, m_FacialHair.Hue);

            Container c = (m_CreateCorpse == null ? null : m_CreateCorpse(this, hair, facialhair, content, equip));


            /*m_Corpse = c;

			for ( int i = 0; c != null && i < content.Count; ++i )
				c.DropItem( (Item)content[i] );

			if ( c != null )
				c.MoveToWorld( this.Location, this.Map );*/

            if (_Map != null)
            {
                Packet animPacket = null;

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state != _NetState)
                    {
                        if (animPacket == null)
                            animPacket = Packet.Acquire(new DeathAnimation(this, c)); ;

                        state.Send(animPacket);

                        if (!state.Mobile.CanSee(this))
                        {
                            state.Send(this.RemovePacket);
                        }
                    }
                }

                Packet.Release(animPacket);

                eable.Free();
            }

            Region.OnDeath(this);
            OnDeath(c);
        }

        private Container m_Corpse;

        [CommandProperty(AccessLevel.GameMaster)]
        public Container Corpse
        {
            get
            {
                return m_Corpse;
            }
            set
            {
                m_Corpse = value;
            }
        }

        /// <summary>
        /// Overridable. Event invoked before the Mobile is <see cref="Kill">killed</see>.
        /// <seealso cref="Kill" />
        /// <seealso cref="OnDeath" />
        /// </summary>
        /// <returns>True to continue with death, false to override it.</returns>
        public virtual bool OnBeforeDeath()
        {
            return true;
        }

        /// <summary>
        /// Overridable. Event invoked after the Mobile is <see cref="Kill">killed</see>. Primarily, this method is responsible for deleting an NPC or turning a PC into a ghost.
        /// <seealso cref="Kill" />
        /// <seealso cref="OnBeforeDeath" />
        /// </summary>
        public virtual void OnDeath(Container c)
        {
            int sound = this.GetDeathSound();

            if (sound >= 0)
                Effects.PlaySound(this, this.Map, sound);

            if (!_Player)
            {
                Delete();
            }
            else
            {
                Send(DeathStatus.Instantiate(true));

                Warmode = false;

                BodyMod = 0;
                //Body = this.Female ? 0x193 : 0x192;
                Body = this.Race.GhostBody(this);

                Item deathShroud = new Item(0x204E);

                deathShroud.Movable = false;
                deathShroud.Layer = Layer.OuterTorso;

                AddItem(deathShroud);

                Items.Remove(deathShroud);
                Items.Insert(0, deathShroud);

                Poison = null;
                Combatant = null;

                Hits = 0;
                Stam = 0;
                Mana = 0;

                EventSink.InvokePlayerDeath(new PlayerDeathEventArgs(this));

                ProcessDeltaQueue();

                Send(DeathStatus.Instantiate(false));

                CheckStatTimers();
            }
        }

        #region Get*Sound

        public virtual int GetAngerSound()
        {
            if (BaseSoundID != 0)
                return BaseSoundID;

            return -1;
        }

        public virtual int GetIdleSound()
        {
            if (BaseSoundID != 0)
                return BaseSoundID + 1;

            return -1;
        }

        public virtual int GetAttackSound()
        {
            if (BaseSoundID != 0)
                return BaseSoundID + 2;

            return -1;
        }

        public virtual int GetHurtSound()
        {
            if (BaseSoundID != 0)
                return BaseSoundID + 3;

            return -1;
        }

        public virtual int GetDeathSound()
        {
            if (BaseSoundID != 0)
            {
                return BaseSoundID + 4;
            }
            else if (_Body.IsHuman)
            {
                return Utility.Random(_Female ? 0x314 : 0x423, _Female ? 4 : 5);
            }
            else
            {
                return -1;
            }
        }

        #endregion

        private static char[] m_GhostChars = new char[2] { 'o', 'O' };

        public static char[] GhostChars { get { return m_GhostChars; } set { m_GhostChars = value; } }

        private static bool m_NoSpeechLOS;

        public static bool NoSpeechLOS { get { return m_NoSpeechLOS; } set { m_NoSpeechLOS = value; } }

        private static TimeSpan m_AutoManifestTimeout = TimeSpan.FromSeconds(5.0);

        public static TimeSpan AutoManifestTimeout { get { return m_AutoManifestTimeout; } set { m_AutoManifestTimeout = value; } }

        private Timer m_AutoManifestTimer;

        private class AutoManifestTimer : Timer
        {
            private Mobile m_Mobile;

            public AutoManifestTimer(Mobile m, TimeSpan delay)
                : base(delay)
            {
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                if (!m_Mobile.Alive)
                    m_Mobile.Warmode = false;
            }
        }

        public virtual bool CheckTarget(Mobile from, Target targ, object targeted)
        {
            return true;
        }

        private static bool m_InsuranceEnabled;

        public static bool InsuranceEnabled
        {
            get { return m_InsuranceEnabled; }
            set { m_InsuranceEnabled = value; }
        }

        public virtual void Use(Item item)
        {
            if (item == null || item.Deleted || item.QuestItem || this.Deleted)
                return;

            DisruptiveAction();

            if (m_Spell != null && !m_Spell.OnCasterUsingObject(item))
                return;

            object root = item.RootParent;
            bool okay = false;

            if (!Utility.InUpdateRange(this, item.GetWorldLocation()))
                item.OnDoubleClickOutOfRange(this);
            else if (!CanSee(item))
                item.OnDoubleClickCantSee(this);
            else if (!item.IsAccessibleTo(this))
            {
                Region reg = Region.Find(item.GetWorldLocation(), item.Map);

                if (reg == null || !reg.SendInaccessibleMessage(item, this))
                    item.OnDoubleClickNotAccessible(this);
            }
            else if (!CheckAlive(false))
                item.OnDoubleClickDead(this);
            else if (item.InSecureTrade)
                item.OnDoubleClickSecureTrade(this);
            else if (!AllowItemUse(item))
                okay = false;
            else if (!item.CheckItemUse(this, item))
                okay = false;
            else if (root != null && root is Mobile && ((Mobile)root).IsSnoop(this))
                item.OnSnoop(this);
            else if (this.Region.OnDoubleClick(this, item))
                okay = true;

            if (okay)
            {
                if (!item.Deleted)
                    item.OnItemUsed(this, item);

                if (!item.Deleted)
                    item.OnDoubleClick(this);
            }
        }

        public virtual void Use(Mobile m)
        {
            if (m == null || m.Deleted || this.Deleted)
                return;

            DisruptiveAction();

            if (m_Spell != null && !m_Spell.OnCasterUsingObject(m))
                return;

            if (!Utility.InUpdateRange(this, m))
                m.OnDoubleClickOutOfRange(this);
            else if (!CanSee(m))
                m.OnDoubleClickCantSee(this);
            else if (!CheckAlive(false))
                m.OnDoubleClickDead(this);
            else if (this.Region.OnDoubleClick(this, m) && !m.Deleted)
                m.OnDoubleClick(this);
        }

        private static int m_ActionDelay = 500;

        public static int ActionDelay
        {
            get { return m_ActionDelay; }
            set { m_ActionDelay = value; }
        }

        public virtual void Lift(Item item, int amount, out bool rejected, out LRReason reject)
        {
            rejected = true;
            reject = LRReason.Inspecific;

            if (item == null)
                return;

            Mobile from = this;
            NetState state = _NetState;

            if (from.AccessLevel >= AccessLevel.GameMaster || Core.TickCount - from.NextActionTime >= 0)
            {
                if (from.CheckAlive())
                {
                    from.DisruptiveAction();

                    if (from.Holding != null)
                    {
                        reject = LRReason.AreHolding;
                    }
                    else if (from.AccessLevel < AccessLevel.GameMaster && !from.InRange(item.GetWorldLocation(), 2))
                    {
                        reject = LRReason.OutOfRange;
                    }
                    else if (!from.CanSee(item) || !from.InLOS(item))
                    {
                        reject = LRReason.OutOfSight;
                    }
                    else if (!item.VerifyMove(from))
                    {
                        reject = LRReason.CannotLift;
                    }
                    else if (!item.IsAccessibleTo(from))
                    {
                        reject = LRReason.CannotLift;
                    }
                    else if (item.Nontransferable && amount != item.Amount)
                    {
                        if (item.QuestItem)
                            from.SendLocalizedMessage(1074868); // Stacks of quest items cannot be unstacked.

                        reject = LRReason.CannotLift;
                    }
                    else if (!item.CheckLift(from, item, ref reject))
                    {
                    }
                    else
                    {
                        object root = item.RootParent;

                        if (root != null && root is Mobile && !((Mobile)root).CheckNonlocalLift(from, item))
                        {
                            reject = LRReason.TryToSteal;
                        }
                        else if (!from.OnDragLift(item) || !item.OnDragLift(from))
                        {
                            reject = LRReason.Inspecific;
                        }
                        else if (!from.CheckAlive())
                        {
                            reject = LRReason.Inspecific;
                        }
                        else
                        {
                            item.SetLastMoved();

                            if (item.Spawner != null)
                            {
                                item.Spawner.Remove(item);
                                item.Spawner = null;
                            }

                            if (amount == 0)
                                amount = 1;

                            if (amount > item.Amount)
                                amount = item.Amount;

                            int oldAmount = item.Amount;
                            //item.Amount = amount; //Set in LiftItemDupe

                            if (amount < oldAmount)
                                LiftItemDupe(item, amount);
                            //item.Dupe( oldAmount - amount );

                            Map map = from.Map;

                            if (DragEffects && map != null && (root == null || root is Item))
                            {
                                IPooledEnumerable<NetState> eable = map.GetClientsInRange(from.Location);
                                Packet p = null;

                                foreach (NetState ns in eable)
                                {
                                    if (ns.Mobile != from && ns.Mobile.CanSee(from) && ns.Mobile.InLOS(from) && ns.Mobile.CanSee(root))
                                    {
                                        if (p == null)
                                        {
                                            IEntity src;

                                            if (root == null)
                                                src = new Entity(Serial.Zero, item.Location, map);
                                            else
                                                src = new Entity(((Item)root).Serial, ((Item)root).Location, map);

                                            p = Packet.Acquire(new DragEffect(src, from, item.ItemID, item.Hue, amount));
                                        }

                                        ns.Send(p);
                                    }
                                }

                                Packet.Release(p);

                                eable.Free();
                            }

                            Point3D fixLoc = item.Location;
                            Map fixMap = item.Map;
                            bool shouldFix = (item.Parent == null);

                            item.RecordBounce();
                            item.OnItemLifted(from, item);
                            item.Internalize();

                            from.Holding = item;

                            int liftSound = item.GetLiftSound(from);

                            if (liftSound != -1)
                                from.Send(new PlaySound(liftSound, from));

                            from.NextActionTime = Core.TickCount + m_ActionDelay;

                            if (fixMap != null && shouldFix)
                                fixMap.FixColumn(fixLoc.X, fixLoc.Y);

                            reject = LRReason.Inspecific;
                            rejected = false;
                        }
                    }
                }
                else
                {
                    reject = LRReason.Inspecific;
                }
            }
            else
            {
                SendActionMessage();
                reject = LRReason.Inspecific;
            }

            if (rejected && state != null)
            {
                state.Send(new LiftRej(reject));

                if (item.Deleted)
                    return;

                if (item.Parent is Item)
                {
                    if (state.ContainerGridLines)
                        state.Send(new ContainerContentUpdate6017(item));
                    else
                        state.Send(new ContainerContentUpdate(item));
                }
                else if (item.Parent is Mobile)
                    state.Send(new EquipUpdate(item));
                else
                    item.SendInfoTo(state);

                if (ObjectPropertyList.Enabled && item.Parent != null)
                    state.Send(item.OPLPacket);
            }
        }

        public static Item LiftItemDupe(Item oldItem, int amount)
        {
            Item item;
            try
            {
                item = (Item)Activator.CreateInstance(oldItem.GetType());
            }
            catch
            {
                Console.WriteLine("Warning: 0x{0:X}: Item must have a zero paramater constructor to be separated from a stack. '{1}'.", oldItem.Serial.Value, oldItem.GetType().Name);
                return null;
            }
            item.Visible = oldItem.Visible;
            item.Movable = oldItem.Movable;
            item.LootType = oldItem.LootType;
            item.Direction = oldItem.Direction;
            item.Hue = oldItem.Hue;
            item.ItemID = oldItem.ItemID;
            item.Location = oldItem.Location;
            item.Layer = oldItem.Layer;
            item.Name = oldItem.Name;
            item.Weight = oldItem.Weight;

            item.Amount = oldItem.Amount - amount;
            item.Map = oldItem.Map;

            oldItem.Amount = amount;
            oldItem.OnAfterDuped(item);

            if (oldItem.Parent is Mobile)
            {
                ((Mobile)oldItem.Parent).AddItem(item);
            }
            else if (oldItem.Parent is Item)
            {
                ((Item)oldItem.Parent).AddItem(item);
            }

            item.Delta(ItemDelta.Update);

            return item;
        }

        public virtual void SendDropEffect(Item item)
        {
            if (DragEffects && !item.Deleted)
            {
                Map map = _Map;
                object root = item.RootParent;

                if (map != null && (root == null || root is Item))
                {
                    IPooledEnumerable<NetState> eable = map.GetClientsInRange(_Location);
                    Packet p = null;

                    foreach (NetState ns in eable)
                    {
                        if (ns.StygianAbyss)
                            continue;

                        if (ns.Mobile != this && ns.Mobile.CanSee(this) && ns.Mobile.InLOS(this) && ns.Mobile.CanSee(root))
                        {
                            if (p == null)
                            {
                                IEntity trg;

                                if (root == null)
                                    trg = new Entity(Serial.Zero, item.Location, map);
                                else
                                    trg = new Entity(((Item)root).Serial, ((Item)root).Location, map);

                                p = Packet.Acquire(new DragEffect(this, trg, item.ItemID, item.Hue, item.Amount));
                            }

                            ns.Send(p);
                        }
                    }

                    Packet.Release(p);

                    eable.Free();
                }
            }
        }

        public virtual bool Drop(Item to, Point3D loc)
        {
            Mobile from = this;
            Item item = from.Holding;

            bool valid = (item != null && item.HeldBy == from && item.Map == Map.Internal);

            from.Holding = null;

            if (!valid)
            {
                return false;
            }

            bool bounced = true;

            item.SetLastMoved();

            if (to == null || !item.DropToItem(from, to, loc))
                item.Bounce(from);
            else
                bounced = false;

            item.ClearBounce();

            if (!bounced)
                SendDropEffect(item);

            return !bounced;
        }

        public virtual bool Drop(Point3D loc)
        {
            Mobile from = this;
            Item item = from.Holding;

            bool valid = (item != null && item.HeldBy == from && item.Map == Map.Internal);

            from.Holding = null;

            if (!valid)
            {
                return false;
            }

            bool bounced = true;

            item.SetLastMoved();

            if (!item.DropToWorld(from, loc))
                item.Bounce(from);
            else
                bounced = false;

            item.ClearBounce();

            if (!bounced)
                SendDropEffect(item);

            return !bounced;
        }

        public virtual bool Drop(Mobile to, Point3D loc)
        {
            Mobile from = this;
            Item item = from.Holding;

            bool valid = (item != null && item.HeldBy == from && item.Map == Map.Internal);

            from.Holding = null;

            if (!valid)
            {
                return false;
            }

            bool bounced = true;

            item.SetLastMoved();

            if (to == null || !item.DropToMobile(from, to, loc))
                item.Bounce(from);
            else
                bounced = false;

            item.ClearBounce();

            if (!bounced)
                SendDropEffect(item);

            return !bounced;
        }

        private static object m_GhostMutateContext = new object();

        public virtual bool MutateSpeech(List<Mobile> hears, ref string text, ref object context)
        {
            if (Alive)
                return false;

            StringBuilder sb = new StringBuilder(text.Length, text.Length);

            for (int i = 0; i < text.Length; ++i)
            {
                if (text[i] != ' ')
                    sb.Append(m_GhostChars[Utility.Random(m_GhostChars.Length)]);
                else
                    sb.Append(' ');
            }

            text = sb.ToString();
            context = m_GhostMutateContext;
            return true;
        }

        public virtual void Manifest(TimeSpan delay)
        {
            Warmode = true;

            if (m_AutoManifestTimer == null)
                m_AutoManifestTimer = new AutoManifestTimer(this, delay);
            else
                m_AutoManifestTimer.Stop();

            m_AutoManifestTimer.Start();
        }

        public virtual bool CheckSpeechManifest()
        {
            if (Alive)
                return false;

            TimeSpan delay = m_AutoManifestTimeout;

            if (delay > TimeSpan.Zero && (!Warmode || m_AutoManifestTimer != null))
            {
                Manifest(delay);
                return true;
            }

            return false;
        }

        public virtual bool CheckHearsMutatedSpeech(Mobile m, object context)
        {
            if (context == m_GhostMutateContext)
                return (m.Alive && !m.CanHearGhosts);

            return true;
        }

        private void AddSpeechItemsFrom(List<IEntity> list, Container cont)
        {
            for (int i = 0; i < cont.Items.Count; ++i)
            {
                Item item = cont.Items[i];

                if (item.HandlesOnSpeech)
                    list.Add(item);

                if (item is Container)
                    AddSpeechItemsFrom(list, (Container)item);
            }
        }

        private class LocationComparer : IComparer<IEntity>
        {
            private static LocationComparer m_Instance;

            public static LocationComparer GetInstance(IEntity relativeTo)
            {
                if (m_Instance == null)
                    m_Instance = new LocationComparer(relativeTo);
                else
                    m_Instance.m_RelativeTo = relativeTo;

                return m_Instance;
            }

            private IEntity m_RelativeTo;

            public IEntity RelativeTo
            {
                get { return m_RelativeTo; }
                set { m_RelativeTo = value; }
            }

            public LocationComparer(IEntity relativeTo)
            {
                m_RelativeTo = relativeTo;
            }

            private int GetDistance(IEntity p)
            {
                int x = m_RelativeTo.X - p.X;
                int y = m_RelativeTo.Y - p.Y;
                int z = m_RelativeTo.Z - p.Z;

                x *= 11;
                y *= 11;

                return (x * x) + (y * y) + (z * z);
            }

            public int Compare(IEntity x, IEntity y)
            {
                return GetDistance(x) - GetDistance(y);
            }
        }

        #region Get*InRange

        public IPooledEnumerable<Item> GetItemsInRange(int range)
        {
            Map map = _Map;

            if (map == null)
                return UltimaOnline.Map.NullEnumerable<Item>.Instance;

            return map.GetItemsInRange(_Location, range);
        }

        public IPooledEnumerable<IEntity> GetObjectsInRange(int range)
        {
            Map map = _Map;

            if (map == null)
                return UltimaOnline.Map.NullEnumerable<IEntity>.Instance;

            return map.GetObjectsInRange(_Location, range);
        }

        public IPooledEnumerable<Mobile> GetMobilesInRange(int range)
        {
            Map map = _Map;

            if (map == null)
                return UltimaOnline.Map.NullEnumerable<Mobile>.Instance;

            return map.GetMobilesInRange(_Location, range);
        }

        public IPooledEnumerable<NetState> GetClientsInRange(int range)
        {
            Map map = _Map;

            if (map == null)
                return UltimaOnline.Map.NullEnumerable<NetState>.Instance;

            return map.GetClientsInRange(_Location, range);
        }

        #endregion

        private static List<Mobile> m_Hears = new List<Mobile>();
        private static List<IEntity> m_OnSpeech = new List<IEntity>();

        public virtual void DoSpeech(string text, int[] keywords, MessageType type, int hue)
        {
            if (m_Deleted || CommandSystem.Handle(this, text, type))
                return;

            int range = 15;

            switch (type)
            {
                case MessageType.Regular:
                    SpeechHue = hue;
                    break;
                case MessageType.Emote:
                    EmoteHue = hue;
                    break;
                case MessageType.Whisper:
                    WhisperHue = hue;
                    range = 1;
                    break;
                case MessageType.Yell:
                    YellHue1 = hue;
                    range = 18;
                    break;
                default:
                    type = MessageType.Regular;
                    break;
            }

            SpeechEventArgs regArgs = new SpeechEventArgs(this, text, type, hue, keywords);

            EventSink.InvokeSpeech(regArgs);
            this.Region.OnSpeech(regArgs);
            OnSaid(regArgs);

            if (regArgs.Blocked)
                return;

            text = regArgs.Speech;

            if (string.IsNullOrEmpty(text))
                return;

            List<Mobile> hears = m_Hears;
            List<IEntity> onSpeech = m_OnSpeech;

            if (_Map != null)
            {
                IPooledEnumerable<IEntity> eable = _Map.GetObjectsInRange(_Location, range);

                foreach (IEntity o in eable)
                {
                    if (o is Mobile)
                    {
                        Mobile heard = (Mobile)o;

                        if (heard.CanSee(this) && (m_NoSpeechLOS || !heard.Player || heard.InLOS(this)))
                        {
                            if (heard._NetState != null)
                                hears.Add(heard);

                            if (heard.HandlesOnSpeech(this))
                                onSpeech.Add(heard);

                            for (int i = 0; i < heard.Items.Count; ++i)
                            {
                                Item item = heard.Items[i];

                                if (item.HandlesOnSpeech)
                                    onSpeech.Add(item);

                                if (item is Container)
                                    AddSpeechItemsFrom(onSpeech, (Container)item);
                            }
                        }
                    }
                    else if (o is Item)
                    {
                        if (((Item)o).HandlesOnSpeech)
                            onSpeech.Add(o);

                        if (o is Container)
                            AddSpeechItemsFrom(onSpeech, (Container)o);
                    }
                }

                eable.Free();

                object mutateContext = null;
                string mutatedText = text;
                SpeechEventArgs mutatedArgs = null;

                if (MutateSpeech(hears, ref mutatedText, ref mutateContext))
                    mutatedArgs = new SpeechEventArgs(this, mutatedText, type, hue, new int[0]);

                CheckSpeechManifest();

                ProcessDelta();

                Packet regp = null;
                Packet mutp = null;

                // TODO: Should this be sorted like onSpeech is below?

                for (int i = 0; i < hears.Count; ++i)
                {
                    Mobile heard = hears[i];

                    if (mutatedArgs == null || !CheckHearsMutatedSpeech(heard, mutateContext))
                    {
                        heard.OnSpeech(regArgs);

                        NetState ns = heard.NetState;

                        if (ns != null)
                        {
                            if (regp == null)
                                regp = Packet.Acquire(new UnicodeMessage(Serial, Body, type, hue, 3, _Language, Name, text));

                            ns.Send(regp);
                        }
                    }
                    else
                    {
                        heard.OnSpeech(mutatedArgs);

                        NetState ns = heard.NetState;

                        if (ns != null)
                        {
                            if (mutp == null)
                                mutp = Packet.Acquire(new UnicodeMessage(Serial, Body, type, hue, 3, _Language, Name, mutatedText));

                            ns.Send(mutp);
                        }
                    }
                }

                Packet.Release(regp);
                Packet.Release(mutp);

                if (onSpeech.Count > 1)
                    onSpeech.Sort(LocationComparer.GetInstance(this));

                for (int i = 0; i < onSpeech.Count; ++i)
                {
                    IEntity obj = onSpeech[i];

                    if (obj is Mobile)
                    {
                        Mobile heard = (Mobile)obj;

                        if (mutatedArgs == null || !CheckHearsMutatedSpeech(heard, mutateContext))
                            heard.OnSpeech(regArgs);
                        else
                            heard.OnSpeech(mutatedArgs);
                    }
                    else
                    {
                        Item item = (Item)obj;

                        item.OnSpeech(regArgs);
                    }
                }

                if (m_Hears.Count > 0)
                    m_Hears.Clear();

                if (m_OnSpeech.Count > 0)
                    m_OnSpeech.Clear();
            }
        }

        private static VisibleDamageType m_VisibleDamageType;

        public static VisibleDamageType VisibleDamageType
        {
            get { return m_VisibleDamageType; }
            set { m_VisibleDamageType = value; }
        }

        private List<DamageEntry> m_DamageEntries;

        public List<DamageEntry> DamageEntries
        {
            get { return m_DamageEntries; }
        }

        public static Mobile GetDamagerFrom(DamageEntry de)
        {
            return (de == null ? null : de.Damager);
        }

        public Mobile FindMostRecentDamager(bool allowSelf)
        {
            return GetDamagerFrom(FindMostRecentDamageEntry(allowSelf));
        }

        public DamageEntry FindMostRecentDamageEntry(bool allowSelf)
        {
            for (int i = m_DamageEntries.Count - 1; i >= 0; --i)
            {
                if (i >= m_DamageEntries.Count)
                    continue;

                DamageEntry de = m_DamageEntries[i];

                if (de.HasExpired)
                    m_DamageEntries.RemoveAt(i);
                else if (allowSelf || de.Damager != this)
                    return de;
            }

            return null;
        }

        public Mobile FindLeastRecentDamager(bool allowSelf)
        {
            return GetDamagerFrom(FindLeastRecentDamageEntry(allowSelf));
        }

        public DamageEntry FindLeastRecentDamageEntry(bool allowSelf)
        {
            for (int i = 0; i < m_DamageEntries.Count; ++i)
            {
                if (i < 0)
                    continue;

                DamageEntry de = m_DamageEntries[i];

                if (de.HasExpired)
                {
                    m_DamageEntries.RemoveAt(i);
                    --i;
                }
                else if (allowSelf || de.Damager != this)
                {
                    return de;
                }
            }

            return null;
        }

        public Mobile FindMostTotalDamger(bool allowSelf)
        {
            return GetDamagerFrom(FindMostTotalDamageEntry(allowSelf));
        }

        public DamageEntry FindMostTotalDamageEntry(bool allowSelf)
        {
            DamageEntry mostTotal = null;

            for (int i = m_DamageEntries.Count - 1; i >= 0; --i)
            {
                if (i >= m_DamageEntries.Count)
                    continue;

                DamageEntry de = m_DamageEntries[i];

                if (de.HasExpired)
                    m_DamageEntries.RemoveAt(i);
                else if ((allowSelf || de.Damager != this) && (mostTotal == null || de.DamageGiven > mostTotal.DamageGiven))
                    mostTotal = de;
            }

            return mostTotal;
        }

        public Mobile FindLeastTotalDamger(bool allowSelf)
        {
            return GetDamagerFrom(FindLeastTotalDamageEntry(allowSelf));
        }

        public DamageEntry FindLeastTotalDamageEntry(bool allowSelf)
        {
            DamageEntry mostTotal = null;

            for (int i = m_DamageEntries.Count - 1; i >= 0; --i)
            {
                if (i >= m_DamageEntries.Count)
                    continue;

                DamageEntry de = m_DamageEntries[i];

                if (de.HasExpired)
                    m_DamageEntries.RemoveAt(i);
                else if ((allowSelf || de.Damager != this) && (mostTotal == null || de.DamageGiven < mostTotal.DamageGiven))
                    mostTotal = de;
            }

            return mostTotal;
        }

        public DamageEntry FindDamageEntryFor(Mobile m)
        {
            for (int i = m_DamageEntries.Count - 1; i >= 0; --i)
            {
                if (i >= m_DamageEntries.Count)
                    continue;

                DamageEntry de = m_DamageEntries[i];

                if (de.HasExpired)
                    m_DamageEntries.RemoveAt(i);
                else if (de.Damager == m)
                    return de;
            }

            return null;
        }

        public virtual Mobile GetDamageMaster(Mobile damagee)
        {
            return null;
        }

        public virtual DamageEntry RegisterDamage(int amount, Mobile from)
        {
            DamageEntry de = FindDamageEntryFor(from);

            if (de == null)
                de = new DamageEntry(from);

            de.DamageGiven += amount;
            de.LastDamage = DateTime.UtcNow;

            m_DamageEntries.Remove(de);
            m_DamageEntries.Add(de);

            Mobile master = from.GetDamageMaster(this);

            if (master != null)
            {
                List<DamageEntry> list = de.Responsible;

                if (list == null)
                    de.Responsible = list = new List<DamageEntry>();

                DamageEntry resp = null;

                for (int i = 0; i < list.Count; ++i)
                {
                    DamageEntry check = list[i];

                    if (check.Damager == master)
                    {
                        resp = check;
                        break;
                    }
                }

                if (resp == null)
                    list.Add(resp = new DamageEntry(master));

                resp.DamageGiven += amount;
                resp.LastDamage = DateTime.UtcNow;
            }

            return de;
        }

        private Mobile m_LastKiller;

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile LastKiller
        {
            get { return m_LastKiller; }
            set { m_LastKiller = value; }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile is <see cref="Damage">damaged</see>. It is called before <see cref="Hits">hit points</see> are lowered or the Mobile is <see cref="Kill">killed</see>.
        /// <seealso cref="Damage" />
        /// <seealso cref="Hits" />
        /// <seealso cref="Kill" />
        /// </summary>
        public virtual void OnDamage(int amount, Mobile from, bool willKill)
        {
        }

        public virtual void Damage(int amount)
        {
            Damage(amount, null);
        }

        public virtual bool CanBeDamaged()
        {
            return !_Blessed;
        }

        public virtual void Damage(int amount, Mobile from)
        {
            Damage(amount, from, true);
        }

        public virtual void Damage(int amount, Mobile from, bool informMount)
        {
            if (!CanBeDamaged() || m_Deleted)
                return;

            if (!this.Region.OnDamage(this, ref amount))
                return;

            if (amount > 0)
            {
                int oldHits = Hits;
                int newHits = oldHits - amount;

                if (m_Spell != null)
                    m_Spell.OnCasterHurt();

                //if ( m_Spell != null && m_Spell.State == SpellState.Casting )
                //	m_Spell.Disturb( DisturbType.Hurt, false, true );

                if (from != null)
                    RegisterDamage(amount, from);

                DisruptiveAction();

                Paralyzed = false;

                switch (m_VisibleDamageType)
                {
                    case VisibleDamageType.Related:
                        {
                            SendVisibleDamageRelated(from, amount);
                            break;
                        }
                    case VisibleDamageType.Everyone:
                        {
                            SendVisibleDamageEveryone(amount);
                            break;
                        }
                    case VisibleDamageType.Selective:
                        {
                            SendVisibleDamageSelective(from, amount);
                            break;
                        }
                }

                OnDamage(amount, from, newHits < 0);

                IMount m = this.Mount;
                if (m != null && informMount)
                    m.OnRiderDamaged(amount, from, newHits < 0);

                if (newHits < 0)
                {
                    m_LastKiller = from;

                    Hits = 0;

                    if (oldHits >= 0)
                        Kill();
                }
                else
                {
                    Hits = newHits;
                }
            }
        }

        public void SendVisibleDamageRelated(Mobile from, int amount)
        {
            NetState ourState = _NetState, theirState = (from == null ? null : from._NetState);

            if (ourState == null)
            {
                Mobile master = GetDamageMaster(from);

                if (master != null)
                    ourState = master._NetState;
            }

            if (theirState == null && from != null)
            {
                Mobile master = from.GetDamageMaster(this);

                if (master != null)
                    theirState = master._NetState;
            }

            if (amount > 0 && (ourState != null || theirState != null))
            {
                Packet p = null;// = new DamagePacket( this, amount );

                if (ourState != null)
                {
                    if (ourState.DamagePacket)
                        p = Packet.Acquire(new DamagePacket(this, amount));
                    else
                        p = Packet.Acquire(new DamagePacketOld(this, amount));

                    ourState.Send(p);
                }

                if (theirState != null && theirState != ourState)
                {
                    bool newPacket = theirState.DamagePacket;

                    if (newPacket && (p == null || !(p is DamagePacket)))
                    {
                        Packet.Release(p);
                        p = Packet.Acquire(new DamagePacket(this, amount));
                    }
                    else if (!newPacket && (p == null || !(p is DamagePacketOld)))
                    {
                        Packet.Release(p);
                        p = Packet.Acquire(new DamagePacketOld(this, amount));
                    }

                    theirState.Send(p);
                }

                Packet.Release(p);
            }
        }

        public void SendVisibleDamageEveryone(int amount)
        {
            if (amount < 0)
                return;

            Map map = _Map;

            if (map == null)
                return;

            IPooledEnumerable<NetState> eable = map.GetClientsInRange(_Location);

            Packet pNew = null;
            Packet pOld = null;

            foreach (NetState ns in eable)
            {
                if (ns.Mobile.CanSee(this))
                {
                    if (ns.DamagePacket)
                    {
                        if (pNew == null)
                            pNew = Packet.Acquire(new DamagePacket(this, amount));

                        ns.Send(pNew);
                    }
                    else
                    {
                        if (pOld == null)
                            pOld = Packet.Acquire(new DamagePacketOld(this, amount));

                        ns.Send(pOld);
                    }
                }
            }

            Packet.Release(pNew);
            Packet.Release(pOld);

            eable.Free();
        }

        public static bool m_DefaultShowVisibleDamage, m_DefaultCanSeeVisibleDamage;

        public static bool DefaultShowVisibleDamage { get { return m_DefaultShowVisibleDamage; } set { m_DefaultShowVisibleDamage = value; } }
        public static bool DefaultCanSeeVisibleDamage { get { return m_DefaultCanSeeVisibleDamage; } set { m_DefaultCanSeeVisibleDamage = value; } }

        public virtual bool ShowVisibleDamage { get { return m_DefaultShowVisibleDamage; } }
        public virtual bool CanSeeVisibleDamage { get { return m_DefaultCanSeeVisibleDamage; } }

        public void SendVisibleDamageSelective(Mobile from, int amount)
        {
            NetState ourState = _NetState, theirState = (from == null ? null : from._NetState);

            Mobile damager = from;
            Mobile damaged = this;

            if (ourState == null)
            {
                Mobile master = GetDamageMaster(from);

                if (master != null)
                {
                    damaged = master;
                    ourState = master._NetState;
                }
            }

            if (!damaged.ShowVisibleDamage)
                return;

            if (theirState == null && from != null)
            {
                Mobile master = from.GetDamageMaster(this);

                if (master != null)
                {
                    damager = master;
                    theirState = master._NetState;
                }
            }

            if (amount > 0 && (ourState != null || theirState != null))
            {
                if (damaged.CanSeeVisibleDamage && ourState != null)
                {
                    if (ourState.DamagePacket)
                        ourState.Send(new DamagePacket(this, amount));
                    else
                        ourState.Send(new DamagePacketOld(this, amount));
                }

                if (theirState != null && theirState != ourState && damager.CanSeeVisibleDamage)
                {
                    if (theirState.DamagePacket)
                        theirState.Send(new DamagePacket(this, amount));
                    else
                        theirState.Send(new DamagePacketOld(this, amount));
                }
            }
        }

        public void Heal(int amount)
        {
            Heal(amount, this, true);
        }

        public void Heal(int amount, Mobile from)
        {
            Heal(amount, from, true);
        }

        public void Heal(int amount, Mobile from, bool message)
        {
            if (!Alive || IsDeadBondedPet)
                return;

            if (!Region.OnHeal(this, ref amount))
                return;

            OnHeal(ref amount, from);

            if ((Hits + amount) > HitsMax)
            {
                amount = HitsMax - Hits;
            }

            Hits += amount;

            if (message && amount > 0 && _NetState != null)
                _NetState.Send(new MessageLocalizedAffix(Serial.MinusOne, -1, MessageType.Label, 0x3B2, 3, 1008158, "", AffixType.Append | AffixType.System, amount.ToString(), ""));
        }

        public virtual void OnHeal(ref int amount, Mobile from)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Squelched { get; set; }

        public virtual void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            switch (version)
            {
                case 32:
                    {
                        // Removed StuckMenu
                        goto case 31;
                    }
                case 31:
                    {
                        LastStrGain = reader.ReadDeltaTime();
                        LastIntGain = reader.ReadDeltaTime();
                        LastDexGain = reader.ReadDeltaTime();

                        goto case 30;
                    }
                case 30:
                    {
                        byte hairflag = reader.ReadByte();

                        if ((hairflag & 0x01) != 0)
                            m_Hair = new HairInfo(reader);
                        if ((hairflag & 0x02) != 0)
                            m_FacialHair = new FacialHairInfo(reader);

                        goto case 29;
                    }
                case 29:
                    {
                        _Race = reader.ReadRace();
                        goto case 28;
                    }
                case 28:
                    {
                        if (version <= 30)
                            LastStatGain = reader.ReadDeltaTime();

                        goto case 27;
                    }
                case 27:
                    {
                        _TithingPoints = reader.ReadInt();

                        goto case 26;
                    }
                case 26:
                case 25:
                case 24:
                    {
                        m_Corpse = reader.ReadItem() as Container;

                        goto case 23;
                    }
                case 23:
                    {
                        m_CreationTime = reader.ReadDateTime();

                        goto case 22;
                    }
                case 22: // Just removed followers
                case 21:
                    {
                        Stabled = reader.ReadStrongMobileList();

                        goto case 20;
                    }
                case 20:
                    {
                        CantWalk = reader.ReadBool();

                        goto case 19;
                    }
                case 19: // Just removed variables
                case 18:
                    {
                        m_Virtues = new VirtueInfo(reader);

                        goto case 17;
                    }
                case 17:
                    {
                        Thirst = reader.ReadInt();
                        BAC = reader.ReadInt();

                        goto case 16;
                    }
                case 16:
                    {
                        _ShortTermMurders = reader.ReadInt();

                        if (version <= 24)
                        {
                            reader.ReadDateTime();
                            reader.ReadDateTime();
                        }

                        goto case 15;
                    }
                case 15:
                    {
                        if (version < 22)
                            reader.ReadInt(); // followers

                        _FollowersMax = reader.ReadInt();

                        goto case 14;
                    }
                case 14:
                    {
                        MagicDamageAbsorb = reader.ReadInt();

                        goto case 13;
                    }
                case 13:
                    {
                        GuildFealty = reader.ReadMobile();

                        goto case 12;
                    }
                case 12:
                    {
                        _Guild = reader.ReadGuild();

                        goto case 11;
                    }
                case 11:
                    {
                        _DisplayGuildTitle = reader.ReadBool();

                        goto case 10;
                    }
                case 10:
                    {
                        CanSwim = reader.ReadBool();

                        goto case 9;
                    }
                case 9:
                    {
                        Squelched = reader.ReadBool();

                        goto case 8;
                    }
                case 8:
                    {
                        m_Holding = reader.ReadItem();

                        goto case 7;
                    }
                case 7:
                    {
                        _VirtualArmor = reader.ReadInt();

                        goto case 6;
                    }
                case 6:
                    {
                        BaseSoundID = reader.ReadInt();

                        goto case 5;
                    }
                case 5:
                    {
                        DisarmReady = reader.ReadBool();
                        StunReady = reader.ReadBool();

                        goto case 4;
                    }
                case 4:
                    {
                        if (version <= 25)
                        {
                            Poison.Deserialize(reader);
                        }

                        goto case 3;
                    }
                case 3:
                    {
                        _StatCap = reader.ReadInt();

                        goto case 2;
                    }
                case 2:
                    {
                        NameHue = reader.ReadInt();

                        goto case 1;
                    }
                case 1:
                    {
                        _Hunger = reader.ReadInt();

                        goto case 0;
                    }
                case 0:
                    {
                        if (version < 21)
                            Stabled = new List<Mobile>();

                        if (version < 18)
                            m_Virtues = new VirtueInfo();

                        if (version < 11)
                            _DisplayGuildTitle = true;

                        if (version < 3)
                            _StatCap = 225;

                        if (version < 15)
                        {
                            _Followers = 0;
                            _FollowersMax = 5;
                        }

                        _Location = reader.ReadPoint3D();
                        _Body = new Body(reader.ReadInt());
                        _Name = reader.ReadString();
                        _GuildTitle = reader.ReadString();
                        _Criminal = reader.ReadBool();
                        _Kills = reader.ReadInt();
                        SpeechHue = reader.ReadInt();
                        EmoteHue = reader.ReadInt();
                        WhisperHue = reader.ReadInt();
                        YellHue1 = reader.ReadInt();
                        _Language = reader.ReadString();
                        _Female = reader.ReadBool();
                        _Warmode = reader.ReadBool();
                        _Hidden = reader.ReadBool();
                        _Direction = (Direction)reader.ReadByte();
                        _Hue = reader.ReadInt();
                        _Str = reader.ReadInt();
                        _Dex = reader.ReadInt();
                        _Int = reader.ReadInt();
                        _Hits = reader.ReadInt();
                        _Stam = reader.ReadInt();
                        _Mana = reader.ReadInt();
                        _Map = reader.ReadMap();
                        _Blessed = reader.ReadBool();
                        _Fame = reader.ReadInt();
                        _Karma = reader.ReadInt();
                        _AccessLevel = (AccessLevel)reader.ReadByte();

                        _Skills = new Skills(this, reader);

                        Items = reader.ReadStrongItemList();

                        _Player = reader.ReadBool();
                        m_Title = reader.ReadString();
                        Profile = reader.ReadString();
                        ProfileLocked = reader.ReadBool();

                        if (version <= 18)
                        {
                            reader.ReadInt();
                            reader.ReadInt();
                            reader.ReadInt();
                        }

                        AutoPageNotify = reader.ReadBool();

                        m_LogoutLocation = reader.ReadPoint3D();
                        m_LogoutMap = reader.ReadMap();

                        m_StrLock = (StatLockType)reader.ReadByte();
                        m_DexLock = (StatLockType)reader.ReadByte();
                        m_IntLock = (StatLockType)reader.ReadByte();

                        StatMods = new List<StatMod>();
                        SkillMods = new List<SkillMod>();

                        if (version < 32)
                        {
                            if (reader.ReadBool())
                            {
                                int count = reader.ReadInt();
                                for (int i = 0; i < count; ++i)
                                {
                                    reader.ReadDateTime();
                                }
                            }
                        }

                        if (_Player && _Map != Map.Internal)
                        {
                            m_LogoutLocation = _Location;
                            m_LogoutMap = _Map;

                            _Map = Map.Internal;
                        }

                        if (_Map != null)
                            _Map.OnEnter(this);

                        if (_Criminal)
                        {
                            if (_ExpireCriminal == null)
                                _ExpireCriminal = new ExpireCriminalTimer(this);

                            _ExpireCriminal.Start();
                        }

                        if (ShouldCheckStatTimers)
                            CheckStatTimers();

                        if (!_Player && _Dex <= 100 && _CombatTimer != null)
                            _CombatTimer.Priority = TimerPriority.FiftyMS;
                        else if (_CombatTimer != null)
                            _CombatTimer.Priority = TimerPriority.EveryTick;

                        UpdateRegion();

                        UpdateResistances();

                        break;
                    }
            }

            if (!_Player)
                Utility.Intern(ref _Name);

            Utility.Intern(ref m_Title);
            Utility.Intern(ref _Language);

            /*	//Moved into cleanup in scripts.
			if( version < 30 )
				Timer.DelayCall( TimeSpan.Zero, new TimerCallback( ConvertHair ) );
			 * */

        }

        public void ConvertHair()
        {
            Item hair;

            if ((hair = FindItemOnLayer(Layer.Hair)) != null)
            {
                HairItemID = hair.ItemID;
                HairHue = hair.Hue;
                hair.Delete();
            }

            if ((hair = FindItemOnLayer(Layer.FacialHair)) != null)
            {
                FacialHairItemID = hair.ItemID;
                FacialHairHue = hair.Hue;
                hair.Delete();
            }
        }

        public virtual bool ShouldCheckStatTimers { get { return true; } }

        public virtual void CheckStatTimers()
        {
            if (m_Deleted)
                return;

            if (Hits < HitsMax)
            {
                if (CanRegenHits)
                {
                    if (_HitsTimer == null)
                        _HitsTimer = new HitsTimer(this);

                    _HitsTimer.Start();
                }
                else if (_HitsTimer != null)
                {
                    _HitsTimer.Stop();
                }
            }
            else
            {
                Hits = HitsMax;
            }

            if (Stam < StamMax)
            {
                if (CanRegenStam)
                {
                    if (_StamTimer == null)
                        _StamTimer = new StamTimer(this);

                    _StamTimer.Start();
                }
                else if (_StamTimer != null)
                {
                    _StamTimer.Stop();
                }
            }
            else
            {
                Stam = StamMax;
            }

            if (Mana < ManaMax)
            {
                if (CanRegenMana)
                {
                    if (_ManaTimer == null)
                        _ManaTimer = new ManaTimer(this);

                    _ManaTimer.Start();
                }
                else if (_ManaTimer != null)
                {
                    _ManaTimer.Stop();
                }
            }
            else
            {
                Mana = ManaMax;
            }
        }

        private DateTime m_CreationTime;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime CreationTime
        {
            get
            {
                return m_CreationTime;
            }
        }

        int ISerializable.TypeReference
        {
            get { return m_TypeRef; }
        }

        int ISerializable.SerialIdentity
        {
            get { return Serial; }
        }

        public virtual void Serialize(GenericWriter writer)
        {
            writer.Write((int)32); // version

            writer.WriteDeltaTime(LastStrGain);
            writer.WriteDeltaTime(LastIntGain);
            writer.WriteDeltaTime(LastDexGain);

            byte hairflag = 0x00;

            if (m_Hair != null)
                hairflag |= 0x01;
            if (m_FacialHair != null)
                hairflag |= 0x02;

            writer.Write((byte)hairflag);

            if ((hairflag & 0x01) != 0)
                m_Hair.Serialize(writer);
            if ((hairflag & 0x02) != 0)
                m_FacialHair.Serialize(writer);

            writer.Write(this.Race);

            writer.Write((int)_TithingPoints);

            writer.Write(m_Corpse);

            writer.Write(m_CreationTime);

            writer.Write(Stabled, true);

            writer.Write(CantWalk);

            VirtueInfo.Serialize(writer, m_Virtues);

            writer.Write(Thirst);
            writer.Write(BAC);

            writer.Write(_ShortTermMurders);
            //writer.Write( m_ShortTermElapse );
            //writer.Write( m_LongTermElapse );

            //writer.Write( m_Followers );
            writer.Write(_FollowersMax);

            writer.Write(MagicDamageAbsorb);

            writer.Write(GuildFealty);

            writer.Write(_Guild);

            writer.Write(_DisplayGuildTitle);

            writer.Write(CanSwim);

            writer.Write(Squelched);

            writer.Write(m_Holding);

            writer.Write(_VirtualArmor);

            writer.Write(BaseSoundID);

            writer.Write(DisarmReady);
            writer.Write(StunReady);

            //Poison.Serialize( m_Poison, writer );

            writer.Write(_StatCap);

            writer.Write(NameHue);

            writer.Write(_Hunger);

            writer.Write(_Location);
            writer.Write((int)_Body);
            writer.Write(_Name);
            writer.Write(_GuildTitle);
            writer.Write(_Criminal);
            writer.Write(_Kills);
            writer.Write(SpeechHue);
            writer.Write(EmoteHue);
            writer.Write(WhisperHue);
            writer.Write(YellHue1);
            writer.Write(_Language);
            writer.Write(_Female);
            writer.Write(_Warmode);
            writer.Write(_Hidden);
            writer.Write((byte)_Direction);
            writer.Write(_Hue);
            writer.Write(_Str);
            writer.Write(_Dex);
            writer.Write(_Int);
            writer.Write(_Hits);
            writer.Write(_Stam);
            writer.Write(_Mana);

            writer.Write(_Map);

            writer.Write(_Blessed);
            writer.Write(_Fame);
            writer.Write(_Karma);
            writer.Write((byte)_AccessLevel);
            _Skills.Serialize(writer);

            writer.Write(Items);

            writer.Write(_Player);
            writer.Write(m_Title);
            writer.Write(Profile);
            writer.Write(ProfileLocked);
            writer.Write(AutoPageNotify);

            writer.Write(m_LogoutLocation);
            writer.Write(m_LogoutMap);

            writer.Write((byte)m_StrLock);
            writer.Write((byte)m_DexLock);
            writer.Write((byte)m_IntLock);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int LightLevel
        {
            get
            {
                return _LightLevel;
            }
            set
            {
                if (_LightLevel != value)
                {
                    _LightLevel = value;

                    CheckLightLevels(false);

                    /*if ( m_NetState != null )
						m_NetState.Send( new PersonalLightLevel( this ) );*/
                }
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public string Profile { get; set; }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public bool ProfileLocked { get; set; }

        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Administrator)]
        public bool Player
        {
            get
            {
                return _Player;
            }
            set
            {
                _Player = value;
                InvalidateProperties();

                if (!_Player && _Dex <= 100 && _CombatTimer != null)
                    _CombatTimer.Priority = TimerPriority.FiftyMS;
                else if (_CombatTimer != null)
                    _CombatTimer.Priority = TimerPriority.EveryTick;

                CheckStatTimers();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string Title
        {
            get
            {
                return m_Title;
            }
            set
            {
                m_Title = value;
                InvalidateProperties();
            }
        }

        private static string[] m_AccessLevelNames = new string[]
            {
                "a player",
                "a counselor",
                "a game master",
                "a seer",
                "an administrator",
                "a developer",
                "an owner"
            };

        public static string GetAccessLevelName(AccessLevel level)
        {
            return m_AccessLevelNames[(int)level];
        }

        public virtual bool CanPaperdollBeOpenedBy(Mobile from)
        {
            return (Body.IsHuman || Body.IsGhost || IsBodyMod);
        }

        public virtual void GetChildContextMenuEntries(Mobile from, List<ContextMenuEntry> list, Item item)
        {
        }

        public virtual void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            if (m_Deleted)
                return;

            if (CanPaperdollBeOpenedBy(from))
                list.Add(new PaperdollEntry(this));

            if (from == this && Backpack != null && CanSee(Backpack) && CheckAlive(false))
                list.Add(new OpenBackpackEntry(this));
        }

        public void Internalize()
        {
            Map = Map.Internal;
        }

        public List<Item> Items { get; private set; }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="item" /> is <see cref="AddItem">added</see> from the Mobile, such as when it is equiped.
        /// <seealso cref="Items" />
        /// <seealso cref="OnItemRemoved" />
        /// </summary>
        public virtual void OnItemAdded(Item item)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="item" /> is <see cref="RemoveItem">removed</see> from the Mobile.
        /// <seealso cref="Items" />
        /// <seealso cref="OnItemAdded" />
        /// </summary>
        public virtual void OnItemRemoved(Item item)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="item" /> is becomes a child of the Mobile; it's worn or contained at some level of the Mobile's <see cref="Mobile.Backpack">backpack</see> or <see cref="Mobile.BankBox">bank box</see>
        /// <seealso cref="OnSubItemRemoved" />
        /// <seealso cref="OnItemAdded" />
        /// </summary>
        public virtual void OnSubItemAdded(Item item)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="item" /> is removed from the Mobile, its <see cref="Mobile.Backpack">backpack</see>, or its <see cref="Mobile.BankBox">bank box</see>.
        /// <seealso cref="OnSubItemAdded" />
        /// <seealso cref="OnItemRemoved" />
        /// </summary>
        public virtual void OnSubItemRemoved(Item item)
        {
        }

        public virtual void OnItemBounceCleared(Item item)
        {
        }

        public virtual void OnSubItemBounceCleared(Item item)
        {
        }

        public virtual int MaxWeight { get { return int.MaxValue; } }

        public void AddItem(Item item)
        {
            if (item == null || item.Deleted)
                return;

            if (item.Parent == this)
                return;
            else if (item.Parent is Mobile)
                ((Mobile)item.Parent).RemoveItem(item);
            else if (item.Parent is Item)
                ((Item)item.Parent).RemoveItem(item);
            else
                item.SendRemovePacket();

            item.Parent = this;
            item.Map = _Map;

            Items.Add(item);

            if (!item.IsVirtualItem)
            {
                UpdateTotal(item, TotalType.Gold, item.TotalGold);
                UpdateTotal(item, TotalType.Items, item.TotalItems + 1);
                UpdateTotal(item, TotalType.Weight, item.TotalWeight + item.PileWeight);
            }

            item.Delta(ItemDelta.Update);

            item.OnAdded(this);
            OnItemAdded(item);

            if (item.PhysicalResistance != 0 || item.FireResistance != 0 || item.ColdResistance != 0 ||
                item.PoisonResistance != 0 || item.EnergyResistance != 0)
                UpdateResistances();
        }

        private static IWeapon m_DefaultWeapon;

        public static IWeapon DefaultWeapon
        {
            get
            {
                return m_DefaultWeapon;
            }
            set
            {
                m_DefaultWeapon = value;
            }
        }

        public void RemoveItem(Item item)
        {
            if (item == null || Items == null)
                return;

            if (Items.Contains(item))
            {
                item.SendRemovePacket();

                //int oldCount = m_Items.Count;

                Items.Remove(item);

                if (!item.IsVirtualItem)
                {
                    UpdateTotal(item, TotalType.Gold, -item.TotalGold);
                    UpdateTotal(item, TotalType.Items, -(item.TotalItems + 1));
                    UpdateTotal(item, TotalType.Weight, -(item.TotalWeight + item.PileWeight));
                }

                item.Parent = null;

                item.OnRemoved(this);
                OnItemRemoved(item);

                if (item.PhysicalResistance != 0 || item.FireResistance != 0 || item.ColdResistance != 0 ||
                    item.PoisonResistance != 0 || item.EnergyResistance != 0)
                    UpdateResistances();
            }
        }

        public virtual void Animate(int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay)
        {
            Map map = _Map;

            if (map != null)
            {
                ProcessDelta();

                Packet p = null;
                //Packet pNew = null;

                IPooledEnumerable<NetState> eable = map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this))
                    {
                        state.Mobile.ProcessDelta();

                        //if ( state.StygianAbyss ) {
                        //if( pNew == null )
                        //pNew = Packet.Acquire( new NewMobileAnimation( this, action, frameCount, delay ) );

                        //state.Send( pNew );
                        //} else {
                        if (p == null)
                        {
                            #region SA
                            if (Body.IsGargoyle)
                            {
                                frameCount = 10;

                                if (Flying)
                                {
                                    if (action >= 9 && action <= 11)
                                    {
                                        action = 71;
                                    }
                                    else if (action >= 12 && action <= 14)
                                    {
                                        action = 72;
                                    }
                                    else if (action == 20)
                                    {
                                        action = 77;
                                    }
                                    else if (action == 31)
                                    {
                                        action = 71;
                                    }
                                    else if (action == 34)
                                    {
                                        action = 78;
                                    }
                                    else if (action >= 200 && action <= 259)
                                    {
                                        action = 75;
                                    }
                                    else if (action >= 260 && action <= 270)
                                    {
                                        action = 75;
                                    }
                                }
                                else
                                {
                                    if (action >= 200 && action <= 259)
                                    {
                                        action = 17;
                                    }
                                    else if (action >= 260 && action <= 270)
                                    {
                                        action = 16;
                                    }
                                }
                            }
                            #endregion

                            p = Packet.Acquire(new MobileAnimation(this, action, frameCount, repeatCount, forward, repeat, delay));
                        }

                        state.Send(p);
                        //}
                    }
                }

                Packet.Release(p);
                //Packet.Release( pNew );

                eable.Free();
            }
        }

        public void SendSound(int soundID)
        {
            if (soundID != -1 && _NetState != null)
                Send(new PlaySound(soundID, this));
        }

        public void SendSound(int soundID, IPoint3D p)
        {
            if (soundID != -1 && _NetState != null)
                Send(new PlaySound(soundID, p));
        }

        public void PlaySound(int soundID)
        {
            if (soundID == -1)
                return;

            if (_Map != null)
            {
                Packet p = Packet.Acquire(new PlaySound(soundID, this));

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        [CommandProperty(AccessLevel.Counselor)]
        public Skills Skills
        {
            get
            {
                return _Skills;
            }
            set
            {
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.Administrator)]
        public AccessLevel AccessLevel
        {
            get
            {
                return _AccessLevel;
            }
            set
            {
                AccessLevel oldValue = _AccessLevel;

                if (oldValue != value)
                {
                    _AccessLevel = value;
                    Delta(MobileDelta.Noto);
                    InvalidateProperties();

                    SendMessage("Your access level has been changed. You are now {0}.", GetAccessLevelName(value));

                    ClearScreen();
                    SendEverything();

                    OnAccessLevelChanged(oldValue);
                }
            }
        }

        public virtual void OnAccessLevelChanged(AccessLevel oldLevel)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Fame
        {
            get
            {
                return _Fame;
            }
            set
            {
                int oldValue = _Fame;

                if (oldValue != value)
                {
                    _Fame = value;

                    if (ShowFameTitle && (_Player || _Body.IsHuman) && (oldValue >= 10000) != (value >= 10000))
                        InvalidateProperties();

                    OnFameChange(oldValue);
                }
            }
        }

        public virtual void OnFameChange(int oldValue)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Karma
        {
            get
            {
                return _Karma;
            }
            set
            {
                int old = _Karma;

                if (old != value)
                {
                    _Karma = value;
                    OnKarmaChange(old);
                }
            }
        }

        public virtual void OnKarmaChange(int oldValue)
        {
        }

        // Mobile did something which should unhide him
        public virtual void RevealingAction()
        {
            if (_Hidden && _AccessLevel == AccessLevel.Player)
                Hidden = false;

            DisruptiveAction(); // Anything that unhides you will also distrupt meditation
        }

        #region Say/SayTo/Emote/Whisper/Yell
        public void SayTo(Mobile to, bool ascii, string text)
        {
            PrivateOverheadMessage(MessageType.Regular, SpeechHue, ascii, text, to.NetState);
        }

        public void SayTo(Mobile to, string text)
        {
            SayTo(to, false, text);
        }

        public void SayTo(Mobile to, string format, params object[] args)
        {
            SayTo(to, false, String.Format(format, args));
        }

        public void SayTo(Mobile to, bool ascii, string format, params object[] args)
        {
            SayTo(to, ascii, String.Format(format, args));
        }

        public void SayTo(Mobile to, int number)
        {
            to.Send(new MessageLocalized(Serial, Body, MessageType.Regular, SpeechHue, 3, number, Name, ""));
        }

        public void SayTo(Mobile to, int number, string args)
        {
            to.Send(new MessageLocalized(Serial, Body, MessageType.Regular, SpeechHue, 3, number, Name, args));
        }

        public void Say(bool ascii, string text)
        {
            PublicOverheadMessage(MessageType.Regular, SpeechHue, ascii, text);
        }

        public void Say(string text)
        {
            PublicOverheadMessage(MessageType.Regular, SpeechHue, false, text);
        }

        public void Say(string format, params object[] args)
        {
            Say(String.Format(format, args));
        }

        public void Say(int number, AffixType type, string affix, string args)
        {
            PublicOverheadMessage(MessageType.Regular, SpeechHue, number, type, affix, args);
        }

        public void Say(int number)
        {
            Say(number, "");
        }

        public void Say(int number, string args)
        {
            PublicOverheadMessage(MessageType.Regular, SpeechHue, number, args);
        }

        public void Emote(string text)
        {
            PublicOverheadMessage(MessageType.Emote, EmoteHue, false, text);
        }

        public void Emote(string format, params object[] args)
        {
            Emote(String.Format(format, args));
        }

        public void Emote(int number)
        {
            Emote(number, "");
        }

        public void Emote(int number, string args)
        {
            PublicOverheadMessage(MessageType.Emote, EmoteHue, number, args);
        }

        public void Whisper(string text)
        {
            PublicOverheadMessage(MessageType.Whisper, WhisperHue, false, text);
        }

        public void Whisper(string format, params object[] args)
        {
            Whisper(String.Format(format, args));
        }

        public void Whisper(int number)
        {
            Whisper(number, "");
        }

        public void Whisper(int number, string args)
        {
            PublicOverheadMessage(MessageType.Whisper, WhisperHue, number, args);
        }

        public void Yell(string text)
        {
            PublicOverheadMessage(MessageType.Yell, YellHue1, false, text);
        }

        public void Yell(string format, params object[] args)
        {
            Yell(String.Format(format, args));
        }

        public void Yell(int number)
        {
            Yell(number, "");
        }

        public void Yell(int number, string args)
        {
            PublicOverheadMessage(MessageType.Yell, YellHue1, number, args);
        }
        #endregion

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Blessed
        {
            get
            {
                return _Blessed;
            }
            set
            {
                if (_Blessed != value)
                {
                    _Blessed = value;
                    Delta(MobileDelta.HealthbarYellow);
                }
            }
        }

        public void SendRemovePacket()
        {
            SendRemovePacket(true);
        }

        public void SendRemovePacket(bool everyone)
        {
            if (_Map != null)
            {
                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state != _NetState && (everyone || !state.Mobile.CanSee(this)))
                        state.Send(this.RemovePacket);
                }

                eable.Free();
            }
        }

        public void ClearScreen()
        {
            NetState ns = _NetState;

            if (_Map != null && ns != null)
            {
                IPooledEnumerable<IEntity> eable = _Map.GetObjectsInRange(_Location, Core.GlobalMaxUpdateRange);

                foreach (IEntity o in eable)
                {
                    if (o is Mobile)
                    {
                        Mobile m = (Mobile)o;

                        if (m != this && Utility.InUpdateRange(_Location, m._Location))
                            ns.Send(m.RemovePacket);
                    }
                    else if (o is Item)
                    {
                        Item item = (Item)o;

                        if (InRange(item.Location, item.GetUpdateRange(this)))
                            ns.Send(item.RemovePacket);
                    }
                }

                eable.Free();
            }
        }

        public bool Send(Packet p)
        {
            return Send(p, false);
        }

        public bool Send(Packet p, bool throwOnOffline)
        {
            if (_NetState != null)
            {
                _NetState.Send(p);
                return true;
            }
            else if (throwOnOffline)
            {
                throw new MobileNotConnectedException(this, "Packet could not be sent.");
            }
            else
            {
                return false;
            }
        }

        #region Gumps/Menus

        public bool SendHuePicker(HuePicker p)
        {
            return SendHuePicker(p, false);
        }

        public bool SendHuePicker(HuePicker p, bool throwOnOffline)
        {
            if (_NetState != null)
            {
                p.SendTo(_NetState);
                return true;
            }
            else if (throwOnOffline)
            {
                throw new MobileNotConnectedException(this, "Hue picker could not be sent.");
            }
            else
            {
                return false;
            }
        }

        public Gump FindGump(Type type)
        {
            NetState ns = _NetState;

            if (ns != null)
            {
                foreach (Gump gump in ns.Gumps)
                {
                    if (type.IsAssignableFrom(gump.GetType()))
                    {
                        return gump;
                    }
                }
            }

            return null;
        }

        public bool CloseGump(Type type)
        {
            if (_NetState != null)
            {
                Gump gump = FindGump(type);

                if (gump != null)
                {
                    _NetState.Send(new CloseGump(gump.TypeID, 0));

                    _NetState.RemoveGump(gump);

                    gump.OnServerClose(_NetState);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        [Obsolete("Use CloseGump( Type ) instead.")]
        public bool CloseGump(Type type, int buttonID)
        {
            return CloseGump(type);
        }

        [Obsolete("Use CloseGump( Type ) instead.")]
        public bool CloseGump(Type type, int buttonID, bool throwOnOffline)
        {
            return CloseGump(type);
        }

        public bool CloseAllGumps()
        {
            NetState ns = _NetState;

            if (ns != null)
            {
                List<Gump> gumps = new List<Gump>(ns.Gumps);

                ns.ClearGumps();

                foreach (Gump gump in gumps)
                {
                    ns.Send(new CloseGump(gump.TypeID, 0));

                    gump.OnServerClose(ns);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        [Obsolete("Use CloseAllGumps() instead.", false)]
        public bool CloseAllGumps(bool throwOnOffline)
        {
            return CloseAllGumps();
        }

        public bool HasGump(Type type)
        {
            return (FindGump(type) != null);
        }

        [Obsolete("Use HasGump( Type ) instead.", false)]
        public bool HasGump(Type type, bool throwOnOffline)
        {
            return HasGump(type);
        }

        public bool SendGump(Gump g)
        {
            return SendGump(g, false);
        }

        public bool SendGump(Gump g, bool throwOnOffline)
        {
            if (_NetState != null)
            {
                g.SendTo(_NetState);
                return true;
            }
            else if (throwOnOffline)
            {
                throw new MobileNotConnectedException(this, "Gump could not be sent.");
            }
            else
            {
                return false;
            }
        }

        public bool SendMenu(IMenu m)
        {
            return SendMenu(m, false);
        }

        public bool SendMenu(IMenu m, bool throwOnOffline)
        {
            if (_NetState != null)
            {
                m.SendTo(_NetState);
                return true;
            }
            else if (throwOnOffline)
            {
                throw new MobileNotConnectedException(this, "Menu could not be sent.");
            }
            else
            {
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Overridable. Event invoked before the Mobile says something.
        /// <seealso cref="DoSpeech" />
        /// </summary>
        public virtual void OnSaid(SpeechEventArgs e)
        {
            if (Squelched)
            {
                if (Core.ML)
                    this.SendLocalizedMessage(500168); // You can not say anything, you have been muted.
                else
                    this.SendMessage("You can not say anything, you have been squelched."); //Cliloc ITSELF changed during ML.

                e.Blocked = true;
            }

            if (!e.Blocked)
                RevealingAction();
        }

        public virtual bool HandlesOnSpeech(Mobile from)
        {
            return false;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile hears speech. This event will only be invoked if <see cref="HandlesOnSpeech" /> returns true.
        /// <seealso cref="DoSpeech" />
        /// </summary>
        public virtual void OnSpeech(SpeechEventArgs e)
        {
        }

        public void SendEverything()
        {
            NetState ns = _NetState;

            if (_Map != null && ns != null)
            {
                IPooledEnumerable<IEntity> eable = _Map.GetObjectsInRange(_Location, Core.GlobalMaxUpdateRange);

                foreach (IEntity o in eable)
                {
                    if (o is Item)
                    {
                        Item item = (Item)o;

                        if (CanSee(item) && InRange(item.Location, item.GetUpdateRange(this)))
                            item.SendInfoTo(ns);
                    }
                    else if (o is Mobile)
                    {
                        Mobile m = (Mobile)o;

                        if (CanSee(m) && Utility.InUpdateRange(_Location, m._Location))
                        {
                            ns.Send(MobileIncoming.Create(ns, this, m));

                            if (ns.StygianAbyss)
                            {
                                if (m.Poisoned)
                                    ns.Send(new HealthbarPoison(m));

                                if (m.Blessed || m.YellowHealthbar)
                                    ns.Send(new HealthbarYellow(m));
                            }

                            if (m.IsDeadBondedPet)
                                ns.Send(new BondedStatus(0, m.Serial, 1));

                            if (ObjectPropertyList.Enabled)
                            {
                                ns.Send(m.OPLPacket);

                                //foreach ( Item item in m.m_Items )
                                //	ns.Send( item.OPLPacket );
                            }
                        }
                    }
                }

                eable.Free();
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public Map Map
        {
            get
            {
                return _Map;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (_Map != value)
                {
                    if (_NetState != null)
                        _NetState.ValidateAllTrades();

                    Map oldMap = _Map;

                    if (_Map != null)
                    {
                        _Map.OnLeave(this);

                        ClearScreen();
                        SendRemovePacket();
                    }

                    for (int i = 0; i < Items.Count; ++i)
                        Items[i].Map = value;

                    _Map = value;

                    UpdateRegion();

                    if (_Map != null)
                        _Map.OnEnter(this);

                    NetState ns = _NetState;

                    if (ns != null && _Map != null)
                    {
                        ns.Sequence = 0;
                        ns.Send(new MapChange(this));
                        ns.Send(new MapPatches());
                        ns.Send(SeasonChange.Instantiate(GetSeason(), true));

                        if (ns.StygianAbyss)
                            ns.Send(new MobileUpdate(this));
                        else
                            ns.Send(new MobileUpdateOld(this));

                        ClearFastwalkStack();
                    }

                    if (ns != null)
                    {
                        if (_Map != null)
                            ns.Send(new ServerChange(this, _Map));

                        ns.Sequence = 0;
                        ClearFastwalkStack();

                        ns.Send(MobileIncoming.Create(ns, this, this));

                        if (ns.StygianAbyss)
                        {
                            ns.Send(new MobileUpdate(this));
                            CheckLightLevels(true);
                            ns.Send(new MobileUpdate(this));
                        }
                        else
                        {
                            ns.Send(new MobileUpdateOld(this));
                            CheckLightLevels(true);
                            ns.Send(new MobileUpdateOld(this));
                        }
                    }

                    SendEverything();
                    SendIncomingPacket();

                    if (ns != null)
                    {
                        ns.Sequence = 0;
                        ClearFastwalkStack();

                        ns.Send(MobileIncoming.Create(ns, this, this));

                        if (ns.StygianAbyss)
                        {
                            ns.Send(SupportedFeatures.Instantiate(ns));
                            ns.Send(new MobileUpdate(this));
                            ns.Send(new MobileAttributes(this));
                        }
                        else
                        {
                            ns.Send(SupportedFeatures.Instantiate(ns));
                            ns.Send(new MobileUpdateOld(this));
                            ns.Send(new MobileAttributes(this));
                        }
                    }

                    OnMapChange(oldMap);
                }
            }
        }

        public void UpdateRegion()
        {
            if (m_Deleted)
                return;

            Region newRegion = Region.Find(_Location, _Map);

            if (newRegion != _Region)
            {
                Region.OnRegionChange(this, _Region, newRegion);

                _Region = newRegion;
                OnRegionChange(_Region, newRegion);
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <see cref="Map" /> changes.
        /// </summary>
        protected virtual void OnMapChange(Map oldMap)
        {
        }

        #region Beneficial Checks/Actions

        public virtual bool CanBeBeneficial(Mobile target)
        {
            return CanBeBeneficial(target, true, false);
        }

        public virtual bool CanBeBeneficial(Mobile target, bool message)
        {
            return CanBeBeneficial(target, message, false);
        }

        public virtual bool CanBeBeneficial(Mobile target, bool message, bool allowDead)
        {
            if (target == null)
                return false;

            if (m_Deleted || target.m_Deleted || !Alive || IsDeadBondedPet || (!allowDead && (!target.Alive || target.IsDeadBondedPet)))
            {
                if (message)
                    SendLocalizedMessage(1001017); // You can not perform beneficial acts on your target.

                return false;
            }

            if (target == this)
                return true;

            if ( /*m_Player &&*/ !Region.AllowBeneficial(this, target))
            {
                // TODO: Pets
                //if ( !(target.m_Player || target.Body.IsHuman || target.Body.IsAnimal) )
                //{
                if (message)
                    SendLocalizedMessage(1001017); // You can not perform beneficial acts on your target.

                return false;
                //}
            }

            return true;
        }

        public virtual bool IsBeneficialCriminal(Mobile target)
        {
            if (this == target)
                return false;

            int n = Notoriety.Compute(this, target);

            return (n == Notoriety.Criminal || n == Notoriety.Murderer);
        }

        /// <summary>
        /// Overridable. Event invoked when the Mobile <see cref="DoBeneficial">does a beneficial action</see>.
        /// </summary>
        public virtual void OnBeneficialAction(Mobile target, bool isCriminal)
        {
            if (isCriminal)
                CriminalAction(false);
        }

        public virtual void DoBeneficial(Mobile target)
        {
            if (target == null)
                return;

            OnBeneficialAction(target, IsBeneficialCriminal(target));

            Region.OnBeneficialAction(this, target);
            target.Region.OnGotBeneficialAction(this, target);
        }

        public virtual bool BeneficialCheck(Mobile target)
        {
            if (CanBeBeneficial(target, true))
            {
                DoBeneficial(target);
                return true;
            }

            return false;
        }

        #endregion

        #region Harmful Checks/Actions

        public virtual bool CanBeHarmful(Mobile target)
        {
            return CanBeHarmful(target, true);
        }

        public virtual bool CanBeHarmful(Mobile target, bool message)
        {
            return CanBeHarmful(target, message, false);
        }

        public virtual bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness)
        {
            if (target == null)
                return false;

            if (m_Deleted || (!ignoreOurBlessedness && _Blessed) || target.m_Deleted || target._Blessed || !Alive || IsDeadBondedPet || !target.Alive || target.IsDeadBondedPet)
            {
                if (message)
                    SendLocalizedMessage(1001018); // You can not perform negative acts on your target.

                return false;
            }

            if (target == this)
                return true;

            // TODO: Pets
            if ( /*m_Player &&*/ !Region.AllowHarmful(this, target))//(target.m_Player || target.Body.IsHuman) && !Region.AllowHarmful( this, target )  )
            {
                if (message)
                    SendLocalizedMessage(1001018); // You can not perform negative acts on your target.

                return false;
            }

            return true;
        }

        public virtual bool IsHarmfulCriminal(Mobile target)
        {
            if (this == target)
                return false;

            return (Notoriety.Compute(this, target) == Notoriety.Innocent);
        }

        /// <summary>
        /// Overridable. Event invoked when the Mobile <see cref="DoHarmful">does a harmful action</see>.
        /// </summary>
        public virtual void OnHarmfulAction(Mobile target, bool isCriminal)
        {
            if (isCriminal)
                CriminalAction(false);
        }

        public virtual void DoHarmful(Mobile target)
        {
            DoHarmful(target, false);
        }

        public virtual void DoHarmful(Mobile target, bool indirect)
        {
            if (target == null || m_Deleted)
                return;

            bool isCriminal = IsHarmfulCriminal(target);

            OnHarmfulAction(target, isCriminal);
            target.AggressiveAction(this, isCriminal);

            this.Region.OnDidHarmful(this, target);
            target.Region.OnGotHarmful(this, target);

            if (!indirect)
                Combatant = target;

            if (_ExpireCombatant == null)
                _ExpireCombatant = new ExpireCombatantTimer(this);
            else
                _ExpireCombatant.Stop();

            _ExpireCombatant.Start();
        }

        public virtual bool HarmfulCheck(Mobile target)
        {
            if (CanBeHarmful(target))
            {
                DoHarmful(target);
                return true;
            }

            return false;
        }

        #endregion

        #region Stats

        /// <summary>
        /// Gets a list of all <see cref="StatMod">StatMod's</see> currently active for the Mobile.
        /// </summary>
        public List<StatMod> StatMods { get; private set; }

        public bool RemoveStatMod(string name)
        {
            for (int i = 0; i < StatMods.Count; ++i)
            {
                StatMod check = StatMods[i];

                if (check.Name == name)
                {
                    StatMods.RemoveAt(i);
                    CheckStatTimers();
                    Delta(MobileDelta.Stat | GetStatDelta(check.Type));
                    return true;
                }
            }

            return false;
        }

        public StatMod GetStatMod(string name)
        {
            for (int i = 0; i < StatMods.Count; ++i)
            {
                StatMod check = StatMods[i];

                if (check.Name == name)
                    return check;
            }

            return null;
        }

        public void AddStatMod(StatMod mod)
        {
            for (int i = 0; i < StatMods.Count; ++i)
            {
                StatMod check = StatMods[i];

                if (check.Name == mod.Name)
                {
                    Delta(MobileDelta.Stat | GetStatDelta(check.Type));
                    StatMods.RemoveAt(i);
                    break;
                }
            }

            StatMods.Add(mod);
            Delta(MobileDelta.Stat | GetStatDelta(mod.Type));
            CheckStatTimers();
        }

        private MobileDelta GetStatDelta(StatType type)
        {
            MobileDelta delta = 0;

            if ((type & StatType.Str) != 0)
                delta |= MobileDelta.Hits;

            if ((type & StatType.Dex) != 0)
                delta |= MobileDelta.Stam;

            if ((type & StatType.Int) != 0)
                delta |= MobileDelta.Mana;

            return delta;
        }

        /// <summary>
        /// Computes the total modified offset for the specified stat type. Expired <see cref="StatMod" /> instances are removed.
        /// </summary>
        public int GetStatOffset(StatType type)
        {
            int offset = 0;

            for (int i = 0; i < StatMods.Count; ++i)
            {
                StatMod mod = StatMods[i];

                if (mod.HasElapsed())
                {
                    StatMods.RemoveAt(i);
                    Delta(MobileDelta.Stat | GetStatDelta(mod.Type));
                    CheckStatTimers();

                    --i;
                }
                else if ((mod.Type & type) != 0)
                {
                    offset += mod.Offset;
                }
            }

            return offset;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the <see cref="RawStr" /> changes.
        /// <seealso cref="RawStr" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        public virtual void OnRawStrChange(int oldValue)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <see cref="RawDex" /> changes.
        /// <seealso cref="RawDex" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        public virtual void OnRawDexChange(int oldValue)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the <see cref="RawInt" /> changes.
        /// <seealso cref="RawInt" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        public virtual void OnRawIntChange(int oldValue)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the <see cref="RawStr" />, <see cref="RawDex" />, or <see cref="RawInt" /> changes.
        /// <seealso cref="OnRawStrChange" />
        /// <seealso cref="OnRawDexChange" />
        /// <seealso cref="OnRawIntChange" />
        /// </summary>
        public virtual void OnRawStatChange(StatType stat, int oldValue)
        {
        }

        /// <summary>
        /// Gets or sets the base, unmodified, strength of the Mobile. Ranges from 1 to 65000, inclusive.
        /// <seealso cref="Str" />
        /// <seealso cref="StatMod" />
        /// <seealso cref="OnRawStrChange" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int RawStr
        {
            get
            {
                return _Str;
            }
            set
            {
                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                if (_Str != value)
                {
                    int oldValue = _Str;

                    _Str = value;
                    Delta(MobileDelta.Stat | MobileDelta.Hits);

                    if (Hits < HitsMax)
                    {
                        if (_HitsTimer == null)
                            _HitsTimer = new HitsTimer(this);

                        _HitsTimer.Start();
                    }
                    else if (Hits > HitsMax)
                    {
                        Hits = HitsMax;
                    }

                    OnRawStrChange(oldValue);
                    OnRawStatChange(StatType.Str, oldValue);
                }
            }
        }

        /// <summary>
        /// Gets or sets the effective strength of the Mobile. This is the sum of the <see cref="RawStr" /> plus any additional modifiers. Any attempts to set this value when under the influence of a <see cref="StatMod" /> will result in no change. It ranges from 1 to 65000, inclusive.
        /// <seealso cref="RawStr" />
        /// <seealso cref="StatMod" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int Str
        {
            get
            {
                int value = _Str + GetStatOffset(StatType.Str);

                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                return value;
            }
            set
            {
                if (StatMods.Count == 0)
                    RawStr = value;
            }
        }

        /// <summary>
        /// Gets or sets the base, unmodified, dexterity of the Mobile. Ranges from 1 to 65000, inclusive.
        /// <seealso cref="Dex" />
        /// <seealso cref="StatMod" />
        /// <seealso cref="OnRawDexChange" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int RawDex
        {
            get
            {
                return _Dex;
            }
            set
            {
                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                if (_Dex != value)
                {
                    int oldValue = _Dex;

                    _Dex = value;
                    Delta(MobileDelta.Stat | MobileDelta.Stam);

                    if (Stam < StamMax)
                    {
                        if (_StamTimer == null)
                            _StamTimer = new StamTimer(this);

                        _StamTimer.Start();
                    }
                    else if (Stam > StamMax)
                    {
                        Stam = StamMax;
                    }

                    OnRawDexChange(oldValue);
                    OnRawStatChange(StatType.Dex, oldValue);
                }
            }
        }

        /// <summary>
        /// Gets or sets the effective dexterity of the Mobile. This is the sum of the <see cref="RawDex" /> plus any additional modifiers. Any attempts to set this value when under the influence of a <see cref="StatMod" /> will result in no change. It ranges from 1 to 65000, inclusive.
        /// <seealso cref="RawDex" />
        /// <seealso cref="StatMod" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int Dex
        {
            get
            {
                int value = _Dex + GetStatOffset(StatType.Dex);

                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                return value;
            }
            set
            {
                if (StatMods.Count == 0)
                    RawDex = value;
            }
        }

        /// <summary>
        /// Gets or sets the base, unmodified, intelligence of the Mobile. Ranges from 1 to 65000, inclusive.
        /// <seealso cref="Int" />
        /// <seealso cref="StatMod" />
        /// <seealso cref="OnRawIntChange" />
        /// <seealso cref="OnRawStatChange" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int RawInt
        {
            get
            {
                return _Int;
            }
            set
            {
                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                if (_Int != value)
                {
                    int oldValue = _Int;

                    _Int = value;
                    Delta(MobileDelta.Stat | MobileDelta.Mana);

                    if (Mana < ManaMax)
                    {
                        if (_ManaTimer == null)
                            _ManaTimer = new ManaTimer(this);

                        _ManaTimer.Start();
                    }
                    else if (Mana > ManaMax)
                    {
                        Mana = ManaMax;
                    }

                    OnRawIntChange(oldValue);
                    OnRawStatChange(StatType.Int, oldValue);
                }
            }
        }

        /// <summary>
        /// Gets or sets the effective intelligence of the Mobile. This is the sum of the <see cref="RawInt" /> plus any additional modifiers. Any attempts to set this value when under the influence of a <see cref="StatMod" /> will result in no change. It ranges from 1 to 65000, inclusive.
        /// <seealso cref="RawInt" />
        /// <seealso cref="StatMod" />
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int Int
        {
            get
            {
                int value = _Int + GetStatOffset(StatType.Int);

                if (value < 1)
                    value = 1;
                else if (value > 65000)
                    value = 65000;

                return value;
            }
            set
            {
                if (StatMods.Count == 0)
                    RawInt = value;
            }
        }

        public virtual void OnHitsChange(int oldValue)
        {
        }

        public virtual void OnStamChange(int oldValue)
        {
        }

        public virtual void OnManaChange(int oldValue)
        {
        }

        /// <summary>
        /// Gets or sets the current hit point of the Mobile. This value ranges from 0 to <see cref="HitsMax" />, inclusive. When set to the value of <see cref="HitsMax" />, the <see cref="AggressorInfo.CanReportMurder">CanReportMurder</see> flag of all aggressors is reset to false, and the list of damage entries is cleared.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int Hits
        {
            get
            {
                return _Hits;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (value < 0)
                {
                    value = 0;
                }
                else if (value >= HitsMax)
                {
                    value = HitsMax;

                    if (_HitsTimer != null)
                        _HitsTimer.Stop();

                    for (int i = 0; i < Aggressors.Count; i++) //reset reports on full HP
                        Aggressors[i].CanReportMurder = false;

                    if (m_DamageEntries.Count > 0)
                        m_DamageEntries.Clear(); // reset damage entries on full HP
                }

                if (value < HitsMax)
                {
                    if (CanRegenHits)
                    {
                        if (_HitsTimer == null)
                            _HitsTimer = new HitsTimer(this);

                        _HitsTimer.Start();
                    }
                    else if (_HitsTimer != null)
                    {
                        _HitsTimer.Stop();
                    }
                }

                if (_Hits != value)
                {
                    int oldValue = _Hits;
                    _Hits = value;
                    Delta(MobileDelta.Hits);
                    OnHitsChange(oldValue);
                }
            }
        }

        /// <summary>
        /// Overridable. Gets the maximum hit point of the Mobile. By default, this returns: <c>50 + (<see cref="Str" /> / 2)</c>
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int HitsMax
        {
            get
            {
                return 50 + (Str / 2);
            }
        }

        /// <summary>
        /// Gets or sets the current stamina of the Mobile. This value ranges from 0 to <see cref="StamMax" />, inclusive.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int Stam
        {
            get
            {
                return _Stam;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (value < 0)
                {
                    value = 0;
                }
                else if (value >= StamMax)
                {
                    value = StamMax;

                    if (_StamTimer != null)
                        _StamTimer.Stop();
                }

                if (value < StamMax)
                {
                    if (CanRegenStam)
                    {
                        if (_StamTimer == null)
                            _StamTimer = new StamTimer(this);

                        _StamTimer.Start();
                    }
                    else if (_StamTimer != null)
                    {
                        _StamTimer.Stop();
                    }
                }

                if (_Stam != value)
                {
                    int oldValue = _Stam;
                    _Stam = value;
                    Delta(MobileDelta.Stam);
                    OnStamChange(oldValue);
                }
            }
        }

        /// <summary>
        /// Overridable. Gets the maximum stamina of the Mobile. By default, this returns: <c><see cref="Dex" /></c>
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int StamMax
        {
            get
            {
                return Dex;
            }
        }

        /// <summary>
        /// Gets or sets the current stamina of the Mobile. This value ranges from 0 to <see cref="ManaMax" />, inclusive.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int Mana
        {
            get
            {
                return _Mana;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (value < 0)
                {
                    value = 0;
                }
                else if (value >= ManaMax)
                {
                    value = ManaMax;

                    if (_ManaTimer != null)
                        _ManaTimer.Stop();

                    if (Meditating)
                    {
                        Meditating = false;
                        SendLocalizedMessage(501846); // You are at peace.
                    }
                }

                if (value < ManaMax)
                {
                    if (CanRegenMana)
                    {
                        if (_ManaTimer == null)
                            _ManaTimer = new ManaTimer(this);

                        _ManaTimer.Start();
                    }
                    else if (_ManaTimer != null)
                    {
                        _ManaTimer.Stop();
                    }
                }

                if (_Mana != value)
                {
                    int oldValue = _Mana;
                    _Mana = value;
                    Delta(MobileDelta.Mana);
                    OnManaChange(oldValue);
                }
            }
        }

        /// <summary>
        /// Overridable. Gets the maximum mana of the Mobile. By default, this returns: <c><see cref="Int" /></c>
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int ManaMax
        {
            get
            {
                return Int;
            }
        }

        #endregion

        public virtual int Luck
        {
            get { return 0; }
        }

        public virtual int HuedItemID
        {
            get
            {
                return (_Female ? 0x2107 : 0x2106);
            }
        }

        private int m_HueMod = -1;

        [Hue, CommandProperty(AccessLevel.GameMaster)]
        public int HueMod
        {
            get
            {
                return m_HueMod;
            }
            set
            {
                if (m_HueMod != value)
                {
                    m_HueMod = value;

                    Delta(MobileDelta.Hue);
                }
            }
        }

        [Hue, CommandProperty(AccessLevel.GameMaster)]
        public virtual int Hue
        {
            get
            {
                if (m_HueMod != -1)
                    return m_HueMod;

                return _Hue;
            }
            set
            {
                int oldHue = _Hue;

                if (oldHue != value)
                {
                    _Hue = value;

                    Delta(MobileDelta.Hue);
                }
            }
        }


        public void SetDirection(Direction dir)
        {
            _Direction = dir;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Direction Direction
        {
            get
            {
                return _Direction;
            }
            set
            {
                if (_Direction != value)
                {
                    _Direction = value;

                    Delta(MobileDelta.Direction);
                    //ProcessDelta();
                }
            }
        }

        public virtual int GetSeason()
        {
            if (_Map != null)
                return _Map.Season;

            return 1;
        }

        public virtual int GetPacketFlags()
        {
            int flags = 0x0;

            if (_Paralyzed || _Frozen)
                flags |= 0x01;

            if (_Female)
                flags |= 0x02;

            if (_Flying)
                flags |= 0x04;

            if (_Blessed || m_YellowHealthbar)
                flags |= 0x08;

            if (_Warmode)
                flags |= 0x40;

            if (_Hidden)
                flags |= 0x80;

            return flags;
        }

        // Pre-7.0.0.0 Packet Flags
        public virtual int GetOldPacketFlags()
        {
            int flags = 0x0;

            if (_Paralyzed || _Frozen)
                flags |= 0x01;

            if (_Female)
                flags |= 0x02;

            if (_Poison != null)
                flags |= 0x04;

            if (_Blessed || m_YellowHealthbar)
                flags |= 0x08;

            if (_Warmode)
                flags |= 0x40;

            if (_Hidden)
                flags |= 0x80;

            return flags;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Female
        {
            get
            {
                return _Female;
            }
            set
            {
                if (_Female != value)
                {
                    _Female = value;
                    Delta(MobileDelta.Flags);
                    OnGenderChanged(!_Female);
                }
            }
        }

        public virtual void OnGenderChanged(bool oldFemale)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Flying
        {
            get
            {
                return _Flying;
            }
            set
            {
                if (_Flying != value)
                {
                    _Flying = value;
                    Delta(MobileDelta.Flags);
                }
            }
        }

        public virtual void ToggleFlying()
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Warmode
        {
            get
            {
                return _Warmode;
            }
            set
            {
                if (m_Deleted)
                    return;

                if (_Warmode != value)
                {
                    if (m_AutoManifestTimer != null)
                    {
                        m_AutoManifestTimer.Stop();
                        m_AutoManifestTimer = null;
                    }

                    _Warmode = value;
                    Delta(MobileDelta.Flags);

                    if (_NetState != null)
                        Send(SetWarMode.Instantiate(value));

                    if (!_Warmode)
                        Combatant = null;

                    if (!Alive)
                    {
                        if (value)
                            Delta(MobileDelta.GhostUpdate);
                        else
                            SendRemovePacket(false);
                    }

                    OnWarmodeChanged();
                }
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked after the Warmode property has changed.
        /// </summary>
        public virtual void OnWarmodeChanged()
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Hidden
        {
            get
            {
                return _Hidden;
            }
            set
            {
                if (_Hidden != value)
                {
                    _Hidden = value;
                    //Delta( MobileDelta.Flags );

                    OnHiddenChanged();
                }
            }
        }

        public virtual void OnHiddenChanged()
        {
            AllowedStealthSteps = 0;

            if (_Map != null)
            {
                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (!state.Mobile.CanSee(this))
                    {
                        state.Send(this.RemovePacket);
                    }
                    else
                    {
                        state.Send(MobileIncoming.Create(state, state.Mobile, this));

                        if (IsDeadBondedPet)
                            state.Send(new BondedStatus(0, Serial, 1));

                        if (ObjectPropertyList.Enabled)
                        {
                            state.Send(OPLPacket);

                            //foreach ( Item item in m_Items )
                            //	state.Send( item.OPLPacket );
                        }
                    }
                }

                eable.Free();
            }
        }

        public virtual void OnConnected()
        {
        }

        public virtual void OnDisconnected()
        {
        }

        public virtual void OnNetStateChanged()
        {
        }

        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Owner)]
        public NetState NetState
        {
            get
            {
                if (_NetState != null && _NetState.Socket == null && !_NetState.IsDisposing)
                {
                    NetState = null;
                }

                return _NetState;
            }
            set
            {
                if (_NetState != value)
                {
                    if (_Map != null)
                        _Map.OnClientChange(_NetState, value, this);

                    if (m_Target != null)
                        m_Target.Cancel(this, TargetCancelType.Disconnected);

                    if (m_QuestArrow != null)
                        QuestArrow = null;

                    if (m_Spell != null)
                        m_Spell.OnConnectionChanged();

                    //if ( m_Spell != null )
                    //	m_Spell.FinishSequence();

                    if (_NetState != null)
                        _NetState.CancelAllTrades();

                    BankBox box = FindBankNoCreate();

                    if (box != null && box.Opened)
                        box.Close();

                    // REMOVED:
                    //m_Actions.Clear();

                    _NetState = value;

                    if (_NetState == null)
                    {
                        OnDisconnected();
                        EventSink.InvokeDisconnected(new DisconnectedEventArgs(this));

                        // Disconnected, start the logout timer

                        if (_LogoutTimer == null)
                            _LogoutTimer = new LogoutTimer(this);
                        else
                            _LogoutTimer.Stop();

                        _LogoutTimer.Delay = GetLogoutDelay();
                        _LogoutTimer.Start();
                    }
                    else
                    {
                        OnConnected();
                        EventSink.InvokeConnected(new ConnectedEventArgs(this));

                        // Connected, stop the logout timer and if needed, move to the world

                        if (_LogoutTimer != null)
                            _LogoutTimer.Stop();

                        _LogoutTimer = null;

                        if (_Map == Map.Internal && m_LogoutMap != null)
                        {
                            Map = m_LogoutMap;
                            Location = m_LogoutLocation;
                        }
                    }

                    for (int i = Items.Count - 1; i >= 0; --i)
                    {
                        if (i >= Items.Count)
                            continue;

                        Item item = Items[i];

                        if (item is SecureTradeContainer)
                        {
                            for (int j = item.Items.Count - 1; j >= 0; --j)
                            {
                                if (j < item.Items.Count)
                                {
                                    item.Items[j].OnSecureTrade(this, this, this, false);
                                    AddToBackpack(item.Items[j]);
                                }
                            }

                            Timer.DelayCall(TimeSpan.Zero, delegate { item.Delete(); });
                        }
                    }

                    DropHolding();
                    OnNetStateChanged();
                }
            }
        }

        public virtual bool CanSee(object o)
        {
            if (o is Item)
            {
                return CanSee((Item)o);
            }
            else if (o is Mobile)
            {
                return CanSee((Mobile)o);
            }
            else
            {
                return true;
            }
        }

        public virtual bool CanSee(Item item)
        {
            if (_Map == Map.Internal)
                return false;
            else if (item.Map == Map.Internal)
                return false;

            if (item.Parent != null)
            {
                if (item.Parent is Item)
                {
                    Item parent = item.Parent as Item;

                    if (!(CanSee(parent) && parent.IsChildVisibleTo(this, item)))
                        return false;
                }
                else if (item.Parent is Mobile)
                {
                    if (!CanSee((Mobile)item.Parent))
                        return false;
                }
            }

            if (item is BankBox)
            {
                BankBox box = item as BankBox;

                if (box != null && _AccessLevel <= AccessLevel.Counselor && (box.Owner != this || !box.Opened))
                    return false;
            }
            else if (item is SecureTradeContainer)
            {
                SecureTrade trade = ((SecureTradeContainer)item).Trade;

                if (trade != null && trade.From.Mobile != this && trade.To.Mobile != this)
                    return false;
            }

            return !item.Deleted && item.Map == _Map && (item.Visible || _AccessLevel > AccessLevel.Counselor);
        }

        public virtual bool CanSee(Mobile m)
        {
            if (m_Deleted || m.m_Deleted || _Map == Map.Internal || m._Map == Map.Internal)
                return false;

            return this == m || (
                m._Map == _Map &&
                (!m.Hidden || (_AccessLevel != AccessLevel.Player && (_AccessLevel >= m.AccessLevel || _AccessLevel >= AccessLevel.Administrator))) &&
                ((m.Alive || (Core.SE && Skills.SpiritSpeak.Value >= 100.0)) || !Alive || _AccessLevel > AccessLevel.Player || m.Warmode));

        }

        public virtual bool CanBeRenamedBy(Mobile from)
        {
            return (from.AccessLevel >= AccessLevel.GameMaster && from._AccessLevel > _AccessLevel);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string Language
        {
            get
            {
                return _Language;
            }
            set
            {
                if (_Language != value)
                    _Language = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int SpeechHue { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EmoteHue { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int WhisperHue { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int YellHue
        {
            get
            {
                return YellHue1;
            }
            set
            {
                YellHue1 = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string GuildTitle
        {
            get
            {
                return _GuildTitle;
            }
            set
            {
                string old = _GuildTitle;

                if (old != value)
                {
                    _GuildTitle = value;

                    if (_Guild != null && !_Guild.Disbanded && _GuildTitle != null)
                        this.SendLocalizedMessage(1018026, true, _GuildTitle); // Your guild title has changed :

                    InvalidateProperties();

                    OnGuildTitleChange(old);
                }
            }
        }

        public virtual void OnGuildTitleChange(string oldTitle)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool DisplayGuildTitle
        {
            get
            {
                return _DisplayGuildTitle;
            }
            set
            {
                _DisplayGuildTitle = value;
                InvalidateProperties();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile GuildFealty { get; set; }

        private string m_NameMod;

        [CommandProperty(AccessLevel.GameMaster)]
        public string NameMod
        {
            get
            {
                return m_NameMod;
            }
            set
            {
                if (m_NameMod != value)
                {
                    m_NameMod = value;
                    Delta(MobileDelta.Name);
                    InvalidateProperties();
                }
            }
        }

        private bool m_YellowHealthbar;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool YellowHealthbar
        {
            get
            {
                return m_YellowHealthbar;
            }
            set
            {
                m_YellowHealthbar = value;
                Delta(MobileDelta.HealthbarYellow);
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string RawName
        {
            get { return _Name; }
            set { Name = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string Name
        {
            get
            {
                if (m_NameMod != null)
                    return m_NameMod;

                return _Name;
            }
            set
            {
                if (_Name != value) // I'm leaving out the && m_NameMod == null
                {
                    string oldName = _Name;
                    _Name = value;
                    OnAfterNameChange(oldName, _Name);
                    Delta(MobileDelta.Name);
                    InvalidateProperties();
                }
            }
        }

        public virtual void OnAfterNameChange(string oldName, string newName)
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastStrGain { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastIntGain { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastDexGain { get; set; }

        public DateTime LastStatGain
        {
            get
            {
                DateTime d = LastStrGain;

                if (LastIntGain > d)
                    d = LastIntGain;

                if (LastDexGain > d)
                    d = LastDexGain;

                return d;
            }
            set
            {
                LastStrGain = value;
                LastIntGain = value;
                LastDexGain = value;
            }
        }

        public BaseGuild Guild
        {
            get
            {
                return _Guild;
            }
            set
            {
                BaseGuild old = _Guild;

                if (old != value)
                {
                    if (value == null)
                        GuildTitle = null;

                    _Guild = value;

                    Delta(MobileDelta.Noto);
                    InvalidateProperties();

                    OnGuildChange(old);
                }
            }
        }

        public virtual void OnGuildChange(BaseGuild oldGuild)
        {
        }

        #region Poison/Curing

        public Timer PoisonTimer { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public Poison Poison
        {
            get
            {
                return _Poison;
            }
            set
            {
                /*if ( m_Poison != value && (m_Poison == null || value == null || m_Poison.Level < value.Level) )
				{*/
                _Poison = value;
                Delta(MobileDelta.HealthbarPoison);

                if (PoisonTimer != null)
                {
                    PoisonTimer.Stop();
                    PoisonTimer = null;
                }

                if (_Poison != null)
                {
                    PoisonTimer = _Poison.ConstructTimer(this);

                    if (PoisonTimer != null)
                        PoisonTimer.Start();
                }

                CheckStatTimers();
                /*}*/
            }
        }

        /// <summary>
        /// Overridable. Event invoked when a call to <see cref="ApplyPoison" /> failed because <see cref="CheckPoisonImmunity" /> returned false: the Mobile was resistant to the poison. By default, this broadcasts an overhead message: * The poison seems to have no effect. *
        /// <seealso cref="CheckPoisonImmunity" />
        /// <seealso cref="ApplyPoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual void OnPoisonImmunity(Mobile from, Poison poison)
        {
            this.PublicOverheadMessage(MessageType.Emote, 0x3B2, 1005534); // * The poison seems to have no effect. *
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a call to <see cref="ApplyPoison" /> failed because <see cref="CheckHigherPoison" /> returned false: the Mobile was already poisoned by an equal or greater strength poison.
        /// <seealso cref="CheckHigherPoison" />
        /// <seealso cref="ApplyPoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual void OnHigherPoison(Mobile from, Poison poison)
        {
        }

        /// <summary>
        /// Overridable. Event invoked when a call to <see cref="ApplyPoison" /> succeeded. By default, this broadcasts an overhead message varying by the level of the poison. Example: * Zippy begins to spasm uncontrollably. *
        /// <seealso cref="ApplyPoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual void OnPoisoned(Mobile from, Poison poison, Poison oldPoison)
        {
            if (poison != null)
            {
                this.LocalOverheadMessage(MessageType.Regular, 0x21, 1042857 + (poison.Level * 2));
                this.NonlocalOverheadMessage(MessageType.Regular, 0x21, 1042858 + (poison.Level * 2), Name);
            }
        }

        /// <summary>
        /// Overridable. Called from <see cref="ApplyPoison" />, this method checks if the Mobile is immune to some <see cref="Poison" />. If true, <see cref="OnPoisonImmunity" /> will be invoked and <see cref="ApplyPoisonResult.Immune" /> is returned.
        /// <seealso cref="OnPoisonImmunity" />
        /// <seealso cref="ApplyPoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual bool CheckPoisonImmunity(Mobile from, Poison poison)
        {
            return false;
        }

        /// <summary>
        /// Overridable. Called from <see cref="ApplyPoison" />, this method checks if the Mobile is already poisoned by some <see cref="Poison" /> of equal or greater strength. If true, <see cref="OnHigherPoison" /> will be invoked and <see cref="ApplyPoisonResult.HigherPoisonActive" /> is returned.
        /// <seealso cref="OnHigherPoison" />
        /// <seealso cref="ApplyPoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual bool CheckHigherPoison(Mobile from, Poison poison)
        {
            return (_Poison != null && _Poison.Level >= poison.Level);
        }

        /// <summary>
        /// Overridable. Attempts to apply poison to the Mobile. Checks are made such that no <see cref="CheckHigherPoison">higher poison is active</see> and that the Mobile is not <see cref="CheckPoisonImmunity">immune to the poison</see>. Provided those assertions are true, the <paramref name="poison" /> is applied and <see cref="OnPoisoned" /> is invoked.
        /// <seealso cref="Poison" />
        /// <seealso cref="CurePoison" />
        /// </summary>
        /// <returns>One of four possible values:
        /// <list type="table">
        /// <item>
        /// <term><see cref="ApplyPoisonResult.Cured">Cured</see></term>
        /// <description>The <paramref name="poison" /> parameter was null and so <see cref="CurePoison" /> was invoked.</description>
        /// </item>
        /// <item>
        /// <term><see cref="ApplyPoisonResult.HigherPoisonActive">HigherPoisonActive</see></term>
        /// <description>The call to <see cref="CheckHigherPoison" /> returned false.</description>
        /// </item>
        /// <item>
        /// <term><see cref="ApplyPoisonResult.Immune">Immune</see></term>
        /// <description>The call to <see cref="CheckPoisonImmunity" /> returned false.</description>
        /// </item>
        /// <item>
        /// <term><see cref="ApplyPoisonResult.Poisoned">Poisoned</see></term>
        /// <description>The <paramref name="poison" /> was successfully applied.</description>
        /// </item>
        /// </list>
        /// </returns>
        public virtual ApplyPoisonResult ApplyPoison(Mobile from, Poison poison)
        {
            if (poison == null)
            {
                CurePoison(from);
                return ApplyPoisonResult.Cured;
            }

            if (CheckHigherPoison(from, poison))
            {
                OnHigherPoison(from, poison);
                return ApplyPoisonResult.HigherPoisonActive;
            }

            if (CheckPoisonImmunity(from, poison))
            {
                OnPoisonImmunity(from, poison);
                return ApplyPoisonResult.Immune;
            }

            Poison oldPoison = _Poison;
            this.Poison = poison;

            OnPoisoned(from, poison, oldPoison);

            return ApplyPoisonResult.Poisoned;
        }

        /// <summary>
        /// Overridable. Called from <see cref="CurePoison" />, this method checks to see that the Mobile can be cured of <see cref="Poison" />
        /// <seealso cref="CurePoison" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual bool CheckCure(Mobile from)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a call to <see cref="CurePoison" /> succeeded.
        /// <seealso cref="CurePoison" />
        /// <seealso cref="CheckCure" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual void OnCured(Mobile from, Poison oldPoison)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when a call to <see cref="CurePoison" /> failed.
        /// <seealso cref="CurePoison" />
        /// <seealso cref="CheckCure" />
        /// <seealso cref="Poison" />
        /// </summary>
        public virtual void OnFailedCure(Mobile from)
        {
        }

        /// <summary>
        /// Overridable. Attempts to cure any poison that is currently active.
        /// </summary>
        /// <returns>True if poison was cured, false if otherwise.</returns>
        public virtual bool CurePoison(Mobile from)
        {
            if (CheckCure(from))
            {
                Poison oldPoison = _Poison;
                this.Poison = null;

                OnCured(from, oldPoison);

                return true;
            }

            OnFailedCure(from);

            return false;
        }

        #endregion

        private ISpawner m_Spawner;

        public ISpawner Spawner { get { return m_Spawner; } set { m_Spawner = value; } }

        private Region m_WalkRegion;

        public Region WalkRegion { get { return m_WalkRegion; } set { m_WalkRegion = value; } }

        public virtual void OnBeforeSpawn(Point3D location, Map m)
        {
        }

        public virtual void OnAfterSpawn()
        {
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Poisoned
        {
            get
            {
                return (_Poison != null);
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsBodyMod
        {
            get
            {
                return (m_BodyMod.BodyID != 0);
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Body BodyMod
        {
            get
            {
                return m_BodyMod;
            }
            set
            {
                if (m_BodyMod != value)
                {
                    m_BodyMod = value;

                    Delta(MobileDelta.Body);
                    InvalidateProperties();

                    CheckStatTimers();
                }
            }
        }

        private static int[] m_InvalidBodies = new int[]
            {
                32,
                95,
                156,
                197,
                198,
            };

        [Body, CommandProperty(AccessLevel.GameMaster)]
        public Body Body
        {
            get
            {
                if (IsBodyMod)
                    return m_BodyMod;

                return _Body;
            }
            set
            {
                if (_Body != value && !IsBodyMod)
                {
                    _Body = SafeBody(value);

                    Delta(MobileDelta.Body);
                    InvalidateProperties();

                    CheckStatTimers();
                }
            }
        }

        public virtual int SafeBody(int body)
        {
            int delta = -1;

            for (int i = 0; delta < 0 && i < m_InvalidBodies.Length; ++i)
                delta = (m_InvalidBodies[i] - body);

            if (delta != 0)
                return body;

            return 0;
        }

        [Body, CommandProperty(AccessLevel.GameMaster)]
        public int BodyValue
        {
            get
            {
                return Body.BodyID;
            }
            set
            {
                Body = value;
            }
        }

        [CommandProperty(AccessLevel.Counselor)]
        public Serial Serial { get; }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public Point3D Location
        {
            get
            {
                return _Location;
            }
            set
            {
                SetLocation(value, true);
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public Point3D LogoutLocation
        {
            get
            {
                return m_LogoutLocation;
            }
            set
            {
                m_LogoutLocation = value;
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public Map LogoutMap
        {
            get
            {
                return m_LogoutMap;
            }
            set
            {
                m_LogoutMap = value;
            }
        }

        public Region Region
        {
            get
            {
                if (_Region == null)
                    if (this.Map == null)
                        return Map.Internal.DefaultRegion;
                    else
                        return this.Map.DefaultRegion;
                else
                    return _Region;
            }
        }

        public void FreeCache()
        {
            Packet.Release(ref m_RemovePacket);
            Packet.Release(ref m_PropertyList);
            Packet.Release(ref m_OPLPacket);
        }

        private Packet m_RemovePacket;
        private object rpLock = new object();

        public Packet RemovePacket
        {
            get
            {
                if (m_RemovePacket == null)
                {
                    lock (rpLock)
                    {
                        if (m_RemovePacket == null)
                        {
                            m_RemovePacket = new RemoveMobile(this);
                            m_RemovePacket.SetStatic();
                        }
                    }
                }

                return m_RemovePacket;
            }
        }

        private Packet m_OPLPacket;
        private object oplLock = new object();

        public Packet OPLPacket
        {
            get
            {
                if (m_OPLPacket == null)
                {
                    lock (oplLock)
                    {
                        if (m_OPLPacket == null)
                        {
                            m_OPLPacket = new OPLInfo(PropertyList);
                            m_OPLPacket.SetStatic();
                        }
                    }
                }

                return m_OPLPacket;
            }
        }

        private ObjectPropertyList m_PropertyList;

        public ObjectPropertyList PropertyList
        {
            get
            {
                if (m_PropertyList == null)
                {
                    m_PropertyList = new ObjectPropertyList(this);

                    GetProperties(m_PropertyList);

                    m_PropertyList.Terminate();
                    m_PropertyList.SetStatic();
                }

                return m_PropertyList;
            }
        }

        public void ClearProperties()
        {
            Packet.Release(ref m_PropertyList);
            Packet.Release(ref m_OPLPacket);
        }

        public void InvalidateProperties()
        {
            if (!ObjectPropertyList.Enabled)
                return;

            if (_Map != null && _Map != Map.Internal && !World.Loading)
            {
                ObjectPropertyList oldList = m_PropertyList;
                Packet.Release(ref m_PropertyList);
                ObjectPropertyList newList = PropertyList;

                if (oldList == null || oldList.Hash != newList.Hash)
                {
                    Packet.Release(ref m_OPLPacket);
                    Delta(MobileDelta.Properties);
                }
            }
            else
            {
                ClearProperties();
            }
        }

        private int m_SolidHueOverride = -1;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SolidHueOverride
        {
            get { return m_SolidHueOverride; }
            set { if (m_SolidHueOverride == value) return; m_SolidHueOverride = value; Delta(MobileDelta.Hue | MobileDelta.Body); }
        }

        public virtual void MoveToWorld(Point3D newLocation, Map map)
        {
            if (m_Deleted)
                return;

            if (_Map == map)
            {
                SetLocation(newLocation, true);
                return;
            }

            BankBox box = FindBankNoCreate();

            if (box != null && box.Opened)
                box.Close();

            Point3D oldLocation = _Location;
            Map oldMap = _Map;

            Region oldRegion = _Region;

            if (oldMap != null)
            {
                oldMap.OnLeave(this);

                ClearScreen();
                SendRemovePacket();
            }

            for (int i = 0; i < Items.Count; ++i)
                Items[i].Map = map;

            _Map = map;

            _Location = newLocation;

            NetState ns = _NetState;

            if (_Map != null)
            {
                _Map.OnEnter(this);

                UpdateRegion();

                if (ns != null && _Map != null)
                {
                    ns.Sequence = 0;
                    ns.Send(new MapChange(this));
                    ns.Send(new MapPatches());
                    ns.Send(SeasonChange.Instantiate(GetSeason(), true));

                    if (ns.StygianAbyss)
                        ns.Send(new MobileUpdate(this));
                    else
                        ns.Send(new MobileUpdateOld(this));

                    ClearFastwalkStack();
                }
            }
            else
            {
                UpdateRegion();
            }

            if (ns != null)
            {
                if (_Map != null)
                    Send(new ServerChange(this, _Map));

                ns.Sequence = 0;
                ClearFastwalkStack();

                ns.Send(MobileIncoming.Create(ns, this, this));

                if (ns.StygianAbyss)
                {
                    ns.Send(new MobileUpdate(this));
                    CheckLightLevels(true);
                    ns.Send(new MobileUpdate(this));
                }
                else
                {
                    ns.Send(new MobileUpdateOld(this));
                    CheckLightLevels(true);
                    ns.Send(new MobileUpdateOld(this));
                }
            }

            SendEverything();
            SendIncomingPacket();

            if (ns != null)
            {
                ns.Sequence = 0;
                ClearFastwalkStack();

                ns.Send(MobileIncoming.Create(ns, this, this));

                if (ns.StygianAbyss)
                {
                    ns.Send(SupportedFeatures.Instantiate(ns));
                    ns.Send(new MobileUpdate(this));
                    ns.Send(new MobileAttributes(this));
                }
                else
                {
                    ns.Send(SupportedFeatures.Instantiate(ns));
                    ns.Send(new MobileUpdateOld(this));
                    ns.Send(new MobileAttributes(this));
                }
            }

            OnMapChange(oldMap);
            OnLocationChange(oldLocation);

            if (_Region != null)
                _Region.OnLocationChanged(this, oldLocation);
        }

        public virtual void SetLocation(Point3D newLocation, bool isTeleport)
        {
            if (m_Deleted)
                return;

            Point3D oldLocation = _Location;

            if (oldLocation != newLocation)
            {
                _Location = newLocation;
                UpdateRegion();

                BankBox box = FindBankNoCreate();

                if (box != null && box.Opened)
                    box.Close();

                if (_NetState != null)
                    _NetState.ValidateAllTrades();

                if (_Map != null)
                    _Map.OnMove(oldLocation, this);

                if (isTeleport && _NetState != null && (!_NetState.HighSeas || !m_NoMoveHS))
                {
                    _NetState.Sequence = 0;

                    if (_NetState.StygianAbyss)
                        _NetState.Send(new MobileUpdate(this));
                    else
                        _NetState.Send(new MobileUpdateOld(this));

                    ClearFastwalkStack();
                }

                Map map = _Map;

                if (map != null)
                {
                    // First, send a remove message to everyone who can no longer see us. (inOldRange && !inNewRange)

                    IPooledEnumerable<NetState> eable = map.GetClientsInRange(oldLocation);

                    foreach (NetState ns in eable)
                    {
                        if (ns != _NetState && !Utility.InUpdateRange(newLocation, ns.Mobile.Location))
                        {
                            ns.Send(this.RemovePacket);
                        }
                    }

                    eable.Free();

                    NetState ourState = _NetState;

                    // Check to see if we are attached to a client
                    if (ourState != null)
                    {
                        IPooledEnumerable<IEntity> eeable = map.GetObjectsInRange(newLocation, Core.GlobalMaxUpdateRange);

                        // We are attached to a client, so it's a bit more complex. We need to send new items and people to ourself, and ourself to other clients

                        foreach (IEntity o in eeable)
                        {
                            if (o is Item)
                            {
                                Item item = (Item)o;

                                int range = item.GetUpdateRange(this);
                                Point3D loc = item.Location;

                                if (!Utility.InRange(oldLocation, loc, range) && Utility.InRange(newLocation, loc, range) && CanSee(item))
                                    item.SendInfoTo(ourState);
                            }
                            else if (o != this && o is Mobile)
                            {
                                Mobile m = (Mobile)o;

                                if (!Utility.InUpdateRange(newLocation, m._Location))
                                    continue;

                                bool inOldRange = Utility.InUpdateRange(oldLocation, m._Location);

                                if (m._NetState != null && ((isTeleport && (!m._NetState.HighSeas || !m_NoMoveHS)) || !inOldRange) && m.CanSee(this))
                                {
                                    m._NetState.Send(MobileIncoming.Create(m._NetState, m, this));

                                    if (m._NetState.StygianAbyss)
                                    {
                                        //if ( m_Poison != null )
                                        m._NetState.Send(new HealthbarPoison(this));

                                        //if ( m_Blessed || m_YellowHealthbar )
                                        m._NetState.Send(new HealthbarYellow(this));
                                    }

                                    if (IsDeadBondedPet)
                                        m._NetState.Send(new BondedStatus(0, Serial, 1));

                                    if (ObjectPropertyList.Enabled)
                                    {
                                        m._NetState.Send(OPLPacket);

                                        //foreach ( Item item in m_Items )
                                        //	m.m_NetState.Send( item.OPLPacket );
                                    }
                                }

                                if (!inOldRange && CanSee(m))
                                {
                                    ourState.Send(MobileIncoming.Create(ourState, this, m));

                                    if (ourState.StygianAbyss)
                                    {
                                        //if ( m.Poisoned )
                                        ourState.Send(new HealthbarPoison(m));

                                        //if ( m.Blessed || m.YellowHealthbar )
                                        ourState.Send(new HealthbarYellow(m));
                                    }

                                    if (m.IsDeadBondedPet)
                                        ourState.Send(new BondedStatus(0, m.Serial, 1));

                                    if (ObjectPropertyList.Enabled)
                                    {
                                        ourState.Send(m.OPLPacket);

                                        //foreach ( Item item in m.m_Items )
                                        //	ourState.Send( item.OPLPacket );
                                    }
                                }
                            }
                        }

                        eeable.Free();
                    }
                    else
                    {
                        eable = map.GetClientsInRange(newLocation);

                        // We're not attached to a client, so simply send an Incoming
                        foreach (NetState ns in eable)
                        {
                            if (((isTeleport && (!ns.HighSeas || !m_NoMoveHS)) || !Utility.InUpdateRange(oldLocation, ns.Mobile.Location)) && ns.Mobile.CanSee(this))
                            {
                                ns.Send(MobileIncoming.Create(ns, ns.Mobile, this));

                                if (ns.StygianAbyss)
                                {
                                    //if ( m_Poison != null )
                                    ns.Send(new HealthbarPoison(this));

                                    //if ( m_Blessed || m_YellowHealthbar )
                                    ns.Send(new HealthbarYellow(this));
                                }

                                if (IsDeadBondedPet)
                                    ns.Send(new BondedStatus(0, Serial, 1));

                                if (ObjectPropertyList.Enabled)
                                {
                                    ns.Send(OPLPacket);

                                    //foreach ( Item item in m_Items )
                                    //	ns.Send( item.OPLPacket );
                                }
                            }
                        }

                        eable.Free();
                    }
                }

                OnLocationChange(oldLocation);

                this.Region.OnLocationChanged(this, oldLocation);
            }
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <see cref="Location" /> changes.
        /// </summary>
        protected virtual void OnLocationChange(Point3D oldLocation)
        {
        }

        #region Hair

        private HairInfo m_Hair;
        private FacialHairInfo m_FacialHair;

        [CommandProperty(AccessLevel.GameMaster)]
        public int HairItemID
        {
            get
            {
                if (m_Hair == null)
                    return 0;

                return m_Hair.ItemID;
            }
            set
            {
                if (m_Hair == null && value > 0)
                    m_Hair = new HairInfo(value);
                else if (value <= 0)
                    m_Hair = null;
                else
                    m_Hair.ItemID = value;

                Delta(MobileDelta.Hair);
            }
        }

        //		[CommandProperty( AccessLevel.GameMaster )]
        //		public int HairSerial { get { return HairInfo.FakeSerial( this ); } }

        [CommandProperty(AccessLevel.GameMaster)]
        public int FacialHairItemID
        {
            get
            {
                if (m_FacialHair == null)
                    return 0;

                return m_FacialHair.ItemID;
            }
            set
            {
                if (m_FacialHair == null && value > 0)
                    m_FacialHair = new FacialHairInfo(value);
                else if (value <= 0)
                    m_FacialHair = null;
                else
                    m_FacialHair.ItemID = value;

                Delta(MobileDelta.FacialHair);
            }
        }

        //		[CommandProperty( AccessLevel.GameMaster )]
        //		public int FacialHairSerial { get { return FacialHairInfo.FakeSerial( this ); } }

        [CommandProperty(AccessLevel.GameMaster)]
        public int HairHue
        {
            get
            {
                if (m_Hair == null)
                    return 0;
                return m_Hair.Hue;
            }
            set
            {
                if (m_Hair != null)
                {
                    m_Hair.Hue = value;
                    Delta(MobileDelta.Hair);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int FacialHairHue
        {
            get
            {
                if (m_FacialHair == null)
                    return 0;

                return m_FacialHair.Hue;
            }
            set
            {
                if (m_FacialHair != null)
                {
                    m_FacialHair.Hue = value;
                    Delta(MobileDelta.FacialHair);
                }
            }
        }

        #endregion

        public bool HasFreeHand()
        {
            return FindItemOnLayer(Layer.TwoHanded) == null;
        }

        private IWeapon m_Weapon;

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual IWeapon Weapon
        {
            get
            {
                Item item = m_Weapon as Item;

                if (item != null && !item.Deleted && item.Parent == this && CanSee(item))
                    return m_Weapon;

                m_Weapon = null;

                item = FindItemOnLayer(Layer.OneHanded);

                if (item == null)
                    item = FindItemOnLayer(Layer.TwoHanded);

                if (item is IWeapon)
                    return (m_Weapon = (IWeapon)item);
                else
                    return GetDefaultWeapon();
            }
        }

        public virtual IWeapon GetDefaultWeapon()
        {
            return m_DefaultWeapon;
        }

        private BankBox m_BankBox;

        [CommandProperty(AccessLevel.GameMaster)]
        public BankBox BankBox
        {
            get
            {
                if (m_BankBox != null && !m_BankBox.Deleted && m_BankBox.Parent == this)
                    return m_BankBox;

                m_BankBox = FindItemOnLayer(Layer.Bank) as BankBox;

                if (m_BankBox == null)
                    AddItem(m_BankBox = new BankBox(this));

                return m_BankBox;
            }
        }

        public BankBox FindBankNoCreate()
        {
            if (m_BankBox != null && !m_BankBox.Deleted && m_BankBox.Parent == this)
                return m_BankBox;

            m_BankBox = FindItemOnLayer(Layer.Bank) as BankBox;

            return m_BankBox;
        }

        private Container m_Backpack;

        [CommandProperty(AccessLevel.GameMaster)]
        public Container Backpack
        {
            get
            {
                if (m_Backpack != null && !m_Backpack.Deleted && m_Backpack.Parent == this)
                    return m_Backpack;

                return (m_Backpack = (FindItemOnLayer(Layer.Backpack) as Container));
            }
        }

        public virtual bool KeepsItemsOnDeath { get { return _AccessLevel > AccessLevel.Player; } }

        public Item FindItemOnLayer(Layer layer)
        {
            List<Item> eq = Items;
            int count = eq.Count;

            for (int i = 0; i < count; ++i)
            {
                Item item = eq[i];

                if (!item.Deleted && item.Layer == layer)
                {
                    return item;
                }
            }

            return null;
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int X
        {
            get { return _Location.X; }
            set { Location = new Point3D(value, _Location.Y, _Location.Z); }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int Y
        {
            get { return _Location.Y; }
            set { Location = new Point3D(_Location.X, value, _Location.Z); }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int Z
        {
            get { return _Location.Z; }
            set { Location = new Point3D(_Location.X, _Location.Y, value); }
        }

        #region Effects & Particles

        public void MovingEffect(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes, int hue, int renderMode)
        {
            Effects.SendMovingEffect(this, to, itemID, speed, duration, fixedDirection, explodes, hue, renderMode);
        }

        public void MovingEffect(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes)
        {
            Effects.SendMovingEffect(this, to, itemID, speed, duration, fixedDirection, explodes, 0, 0);
        }

        public void MovingParticles(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes, int hue, int renderMode, int effect, int explodeEffect, int explodeSound, EffectLayer layer, int unknown)
        {
            Effects.SendMovingParticles(this, to, itemID, speed, duration, fixedDirection, explodes, effect, explodeEffect, explodeSound, layer, unknown, hue, renderMode);
        }

        public void MovingParticles(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes, int hue, int renderMode, int effect, int explodeEffect, int explodeSound, int unknown)
        {
            Effects.SendMovingParticles(this, to, itemID, speed, duration, fixedDirection, explodes, effect, explodeEffect, explodeSound, (EffectLayer)255, unknown, hue, renderMode);
        }

        public void MovingParticles(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes, int effect, int explodeEffect, int explodeSound, int unknown)
        {
            Effects.SendMovingParticles(this, to, itemID, speed, duration, fixedDirection, explodes, effect, explodeEffect, explodeSound, unknown);
        }

        public void MovingParticles(IEntity to, int itemID, int speed, int duration, bool fixedDirection, bool explodes, int effect, int explodeEffect, int explodeSound)
        {
            Effects.SendMovingParticles(this, to, itemID, speed, duration, fixedDirection, explodes, effect, explodeEffect, explodeSound, 0, 0, 0);
        }

        public void FixedEffect(int itemID, int speed, int duration, int hue, int renderMode)
        {
            Effects.SendTargetEffect(this, itemID, speed, duration, hue, renderMode);
        }

        public void FixedEffect(int itemID, int speed, int duration)
        {
            Effects.SendTargetEffect(this, itemID, speed, duration, 0, 0);
        }

        public void FixedParticles(int itemID, int speed, int duration, int effect, int hue, int renderMode, EffectLayer layer, int unknown)
        {
            Effects.SendTargetParticles(this, itemID, speed, duration, effect, layer, unknown, hue, renderMode);
        }

        public void FixedParticles(int itemID, int speed, int duration, int effect, int hue, int renderMode, EffectLayer layer)
        {
            Effects.SendTargetParticles(this, itemID, speed, duration, effect, layer, 0, hue, renderMode);
        }

        public void FixedParticles(int itemID, int speed, int duration, int effect, EffectLayer layer, int unknown)
        {
            Effects.SendTargetParticles(this, itemID, speed, duration, effect, layer, unknown, 0, 0);
        }

        public void FixedParticles(int itemID, int speed, int duration, int effect, EffectLayer layer)
        {
            Effects.SendTargetParticles(this, itemID, speed, duration, effect, layer, 0, 0, 0);
        }

        public void BoltEffect(int hue)
        {
            Effects.SendBoltEffect(this, true, hue);
        }

        #endregion

        public void SendIncomingPacket()
        {
            if (_Map != null)
            {
                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this))
                    {
                        state.Send(MobileIncoming.Create(state, state.Mobile, this));

                        if (state.StygianAbyss)
                        {
                            if (_Poison != null)
                                state.Send(new HealthbarPoison(this));

                            if (_Blessed || m_YellowHealthbar)
                                state.Send(new HealthbarYellow(this));
                        }

                        if (IsDeadBondedPet)
                            state.Send(new BondedStatus(0, Serial, 1));

                        if (ObjectPropertyList.Enabled)
                        {
                            state.Send(OPLPacket);

                            //foreach ( Item item in m_Items )
                            //	state.Send( item.OPLPacket );
                        }
                    }
                }

                eable.Free();
            }
        }

        public bool PlaceInBackpack(Item item)
        {
            if (item.Deleted)
                return false;

            Container pack = this.Backpack;

            return pack != null && pack.TryDropItem(this, item, false);
        }

        public bool AddToBackpack(Item item)
        {
            if (item.Deleted)
                return false;

            if (!PlaceInBackpack(item))
            {
                Point3D loc = _Location;
                Map map = _Map;

                if ((map == null || map == Map.Internal) && m_LogoutMap != null)
                {
                    loc = m_LogoutLocation;
                    map = m_LogoutMap;
                }

                item.MoveToWorld(loc, map);
                return false;
            }

            return true;
        }

        public virtual bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            return true;
        }

        public virtual bool CheckNonlocalLift(Mobile from, Item item)
        {
            if (from == this || (from.AccessLevel > this.AccessLevel && from.AccessLevel >= AccessLevel.GameMaster))
                return true;

            return false;
        }

        public bool HasTrade
        {
            get
            {
                if (_NetState != null)
                    return _NetState.Trades.Count > 0;

                return false;
            }
        }

        public virtual bool CheckTrade(Mobile to, Item item, SecureTradeContainer cont, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            return true;
        }
        public bool OpenTrade(Mobile from)
        {
            return OpenTrade(from, null);
        }

        public virtual bool OpenTrade(Mobile from, Item offer)
        {
            if (!from.Player || !Player || !from.Alive || !Alive)
            {
                return false;
            }

            NetState ourState = _NetState;
            NetState theirState = from._NetState;

            if (ourState == null || theirState == null)
            {
                return false;
            }

            SecureTradeContainer cont = theirState.FindTradeContainer(this);

            if (!from.CheckTrade(this, offer, cont, true, true, 0, 0))
            {
                return false;
            }

            if (cont == null)
            {
                cont = theirState.AddTrade(ourState);
            }

            if (offer != null)
            {
                cont.DropItem(offer);
            }

            return true;
        }

        /// <summary>
        /// Overridable. Event invoked when a Mobile (<paramref name="from" />) drops an <see cref="Item"><paramref name="dropped" /></see> onto the Mobile.
        /// </summary>
        public virtual bool OnDragDrop(Mobile from, Item dropped)
        {
            if (from == this)
            {
                Container pack = this.Backpack;

                if (pack != null)
                    return dropped.DropToItem(from, pack, new Point3D(-1, -1, 0));

                return false;
            }
            else if (from.InRange(Location, 2))
            {
                return OpenTrade(from, dropped);
            }
            else
            {
                return false;
            }
        }

        public virtual bool CheckEquip(Item item)
        {
            for (int i = 0; i < Items.Count; ++i)
                if (Items[i].CheckConflictingLayer(this, item, item.Layer) || item.CheckConflictingLayer(this, Items[i], Items[i].Layer))
                    return false;

            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to wear <paramref name="item" />.
        /// </summary>
        /// <returns>True if the request is accepted, false if otherwise.</returns>
        public virtual bool OnEquip(Item item)
        {
            // For some reason OSI allows equipping quest items, but they are unmarked in the process
            if (item.QuestItem)
            {
                item.QuestItem = false;
                SendLocalizedMessage(1074769); // An item must be in your backpack (and not in a container within) to be toggled as a quest item.
            }

            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to lift <paramref name="item" />.
        /// </summary>
        /// <returns>True if the lift is allowed, false if otherwise.</returns>
        /// <example>
        /// The following example demonstrates usage. It will disallow any attempts to pick up a pick axe if the Mobile does not have enough strength.
        /// <code>
        /// public override bool OnDragLift( Item item )
        /// {
        ///		if ( item is Pickaxe &amp;&amp; this.Str &lt; 60 )
        ///		{
        ///			SendMessage( "That is too heavy for you to lift." );
        ///			return false;
        ///		}
        ///		
        ///		return base.OnDragLift( item );
        /// }</code>
        /// </example>
        public virtual bool OnDragLift(Item item)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to drop <paramref name="item" /> into a <see cref="Container"><paramref name="container" /></see>.
        /// </summary>
        /// <returns>True if the drop is allowed, false if otherwise.</returns>
        public virtual bool OnDroppedItemInto(Item item, Container container, Point3D loc)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to drop <paramref name="item" /> directly onto another <see cref="Item" />, <paramref name="target" />. This is the case of stacking items.
        /// </summary>
        /// <returns>True if the drop is allowed, false if otherwise.</returns>
        public virtual bool OnDroppedItemOnto(Item item, Item target)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to drop <paramref name="item" /> into another <see cref="Item" />, <paramref name="target" />. The target item is most likely a <see cref="Container" />.
        /// </summary>
        /// <returns>True if the drop is allowed, false if otherwise.</returns>
        public virtual bool OnDroppedItemToItem(Item item, Item target, Point3D loc)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to give <paramref name="item" /> to a Mobile (<paramref name="target" />).
        /// </summary>
        /// <returns>True if the drop is allowed, false if otherwise.</returns>
        public virtual bool OnDroppedItemToMobile(Item item, Mobile target)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile attempts to drop <paramref name="item" /> to the world at a <see cref="Point3D"><paramref name="location" /></see>.
        /// </summary>
        /// <returns>True if the drop is allowed, false if otherwise.</returns>
        public virtual bool OnDroppedItemToWorld(Item item, Point3D location)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event when <paramref name="from" /> successfully uses <paramref name="item" /> while it's on this Mobile.
        /// <seealso cref="Item.OnItemUsed" />
        /// </summary>
        public virtual void OnItemUsed(Mobile from, Item item)
        {
        }

        public virtual bool CheckNonlocalDrop(Mobile from, Item item, Item target)
        {
            if (from == this || (from.AccessLevel > this.AccessLevel && from.AccessLevel >= AccessLevel.GameMaster))
                return true;

            return false;
        }

        public virtual bool CheckItemUse(Mobile from, Item item)
        {
            return true;
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <paramref name="from" /> successfully lifts <paramref name="item" /> from this Mobile.
        /// <seealso cref="Item.OnItemLifted" />
        /// </summary>
        public virtual void OnItemLifted(Mobile from, Item item)
        {
        }

        public virtual bool AllowItemUse(Item item)
        {
            return true;
        }

        public virtual bool AllowEquipFrom(Mobile mob)
        {
            return (mob == this || (mob.AccessLevel >= AccessLevel.GameMaster && mob.AccessLevel > this.AccessLevel));
        }

        public virtual bool EquipItem(Item item)
        {
            if (item == null || item.Deleted || !item.CanEquip(this))
                return false;

            if (CheckEquip(item) && OnEquip(item) && item.OnEquip(this))
            {
                if (m_Spell != null && !m_Spell.OnCasterEquiping(item))
                    return false;

                //if ( m_Spell != null && m_Spell.State == SpellState.Casting )
                //	m_Spell.Disturb( DisturbType.EquipRequest );

                AddItem(item);
                return true;
            }

            return false;
        }

        internal int m_TypeRef;

        public Mobile(Serial serial)
        {
            _Region = Map.Internal.DefaultRegion;
            Serial = serial;
            Aggressors = new List<AggressorInfo>();
            Aggressed = new List<AggressorInfo>();
            NextSkillTime = Core.TickCount;
            m_DamageEntries = new List<DamageEntry>();

            Type ourType = this.GetType();
            m_TypeRef = World.m_MobileTypes.IndexOf(ourType);

            if (m_TypeRef == -1)
            {
                World.m_MobileTypes.Add(ourType);
                m_TypeRef = World.m_MobileTypes.Count - 1;
            }
        }

        public Mobile()
        {
            _Region = Map.Internal.DefaultRegion;
            Serial = UltimaOnline.Serial.NewMobile;

            DefaultMobileInit();

            World.AddMobile(this);

            Type ourType = this.GetType();
            m_TypeRef = World.m_MobileTypes.IndexOf(ourType);

            if (m_TypeRef == -1)
            {
                World.m_MobileTypes.Add(ourType);
                m_TypeRef = World.m_MobileTypes.Count - 1;
            }
        }

        public void DefaultMobileInit()
        {
            _StatCap = 225;
            _FollowersMax = 5;
            _Skills = new Skills(this);
            Items = new List<Item>();
            StatMods = new List<StatMod>();
            SkillMods = new List<SkillMod>();
            Map = Map.Internal;
            AutoPageNotify = true;
            Aggressors = new List<AggressorInfo>();
            Aggressed = new List<AggressorInfo>();
            m_Virtues = new VirtueInfo();
            Stabled = new List<Mobile>();
            m_DamageEntries = new List<DamageEntry>();

            NextSkillTime = Core.TickCount;
            m_CreationTime = DateTime.UtcNow;
        }

        private static Queue<Mobile> _DeltaQueue = new Queue<Mobile>();
        private static Queue<Mobile> _DeltaQueueR = new Queue<Mobile>();

        private bool m_InDeltaQueue;
        private MobileDelta m_DeltaFlags;

        public virtual void Delta(MobileDelta flag)
        {
            if (_Map == null || _Map == Map.Internal || m_Deleted)
                return;

            m_DeltaFlags |= flag;

            if (!m_InDeltaQueue)
            {
                m_InDeltaQueue = true;

                if (_processing)
                {
                    lock (_DeltaQueueR)
                    {
                        _DeltaQueueR.Enqueue(this);

                        try
                        {
                            using (StreamWriter op = new StreamWriter("delta-recursion.log", true))
                            {
                                op.WriteLine("# {0}", DateTime.UtcNow);
                                op.WriteLine(new System.Diagnostics.StackTrace());
                                op.WriteLine();
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    _DeltaQueue.Enqueue(this);
                }
            }

            Core.Set();
        }

        private bool m_NoMoveHS;

        public bool NoMoveHS
        {
            get { return m_NoMoveHS; }
            set { m_NoMoveHS = value; }
        }

        #region GetDirectionTo[..]

        public Direction GetDirectionTo(int x, int y)
        {
            int dx = _Location.X - x;
            int dy = _Location.Y - y;

            int rx = (dx - dy) * 44;
            int ry = (dx + dy) * 44;

            int ax = Math.Abs(rx);
            int ay = Math.Abs(ry);

            Direction ret;

            if (((ay >> 1) - ax) >= 0)
                ret = (ry > 0) ? Direction.Up : Direction.Down;
            else if (((ax >> 1) - ay) >= 0)
                ret = (rx > 0) ? Direction.Left : Direction.Right;
            else if (rx >= 0 && ry >= 0)
                ret = Direction.West;
            else if (rx >= 0 && ry < 0)
                ret = Direction.South;
            else if (rx < 0 && ry < 0)
                ret = Direction.East;
            else
                ret = Direction.North;

            return ret;
        }

        public Direction GetDirectionTo(Point2D p)
        {
            return GetDirectionTo(p.X, p.Y);
        }

        public Direction GetDirectionTo(Point3D p)
        {
            return GetDirectionTo(p.X, p.Y);
        }

        public Direction GetDirectionTo(IPoint2D p)
        {
            if (p == null)
                return Direction.North;

            return GetDirectionTo(p.X, p.Y);
        }

        #endregion

        public virtual void ProcessDelta()
        {
            Mobile m = this;
            MobileDelta delta;

            delta = m.m_DeltaFlags;

            if (delta == MobileDelta.None)
                return;

            MobileDelta attrs = delta & MobileDelta.Attributes;

            m.m_DeltaFlags = MobileDelta.None;
            m.m_InDeltaQueue = false;

            bool sendHits = false, sendStam = false, sendMana = false, sendAll = false, sendAny = false;
            bool sendIncoming = false, sendNonlocalIncoming = false;
            bool sendUpdate = false, sendRemove = false;
            bool sendPublicStats = false, sendPrivateStats = false;
            bool sendMoving = false, sendNonlocalMoving = false;
            bool sendOPLUpdate = ObjectPropertyList.Enabled && (delta & MobileDelta.Properties) != 0;

            bool sendHair = false, sendFacialHair = false, removeHair = false, removeFacialHair = false;

            bool sendHealthbarPoison = false, sendHealthbarYellow = false;

            if (attrs != MobileDelta.None)
            {
                sendAny = true;

                if (attrs == MobileDelta.Attributes)
                {
                    sendAll = true;
                }
                else
                {
                    sendHits = ((attrs & MobileDelta.Hits) != 0);
                    sendStam = ((attrs & MobileDelta.Stam) != 0);
                    sendMana = ((attrs & MobileDelta.Mana) != 0);
                }
            }

            if ((delta & MobileDelta.GhostUpdate) != 0)
            {
                sendNonlocalIncoming = true;
            }

            if ((delta & MobileDelta.Hue) != 0)
            {
                sendNonlocalIncoming = true;
                sendUpdate = true;
                sendRemove = true;
            }

            if ((delta & MobileDelta.Direction) != 0)
            {
                sendNonlocalMoving = true;
                sendUpdate = true;
            }

            if ((delta & MobileDelta.Body) != 0)
            {
                sendUpdate = true;
                sendIncoming = true;
            }

            /*if ( (delta & MobileDelta.Hue) != 0 )
				{
					sendNonlocalIncoming = true;
					sendUpdate = true;
				}
				else if ( (delta & (MobileDelta.Direction | MobileDelta.Body)) != 0 )
				{
					sendNonlocalMoving = true;
					sendUpdate = true;
				}
				else*/
            if ((delta & (MobileDelta.Flags | MobileDelta.Noto)) != 0)
            {
                sendMoving = true;
            }

            if ((delta & MobileDelta.HealthbarPoison) != 0)
            {
                sendHealthbarPoison = true;
            }

            if ((delta & MobileDelta.HealthbarYellow) != 0)
            {
                sendHealthbarYellow = true;
            }

            if ((delta & MobileDelta.Name) != 0)
            {
                sendAll = false;
                sendHits = false;
                sendAny = sendStam || sendMana;
                sendPublicStats = true;
            }

            if ((delta & (MobileDelta.WeaponDamage | MobileDelta.Resistances | MobileDelta.Stat |
                MobileDelta.Weight | MobileDelta.Gold | MobileDelta.Armor | MobileDelta.StatCap |
                MobileDelta.Followers | MobileDelta.TithingPoints | MobileDelta.Race)) != 0)
            {
                sendPrivateStats = true;
            }

            if ((delta & MobileDelta.Hair) != 0)
            {
                if (m.HairItemID <= 0)
                    removeHair = true;

                sendHair = true;
            }

            if ((delta & MobileDelta.FacialHair) != 0)
            {
                if (m.FacialHairItemID <= 0)
                    removeFacialHair = true;

                sendFacialHair = true;
            }

            Packet[][] cache = new Packet[2][] { new Packet[8], new Packet[8] };

            NetState ourState = m._NetState;

            if (ourState != null)
            {
                if (sendUpdate)
                {
                    ourState.Sequence = 0;

                    if (ourState.StygianAbyss)
                        ourState.Send(new MobileUpdate(m));
                    else
                        ourState.Send(new MobileUpdateOld(m));

                    ClearFastwalkStack();
                }

                if (sendIncoming)
                    ourState.Send(MobileIncoming.Create(ourState, m, m));

                if (ourState.StygianAbyss)
                {
                    if (sendMoving)
                    {
                        int noto = Notoriety.Compute(m, m);
                        ourState.Send(cache[0][noto] = Packet.Acquire(new MobileMoving(m, noto)));
                    }

                    if (sendHealthbarPoison)
                        ourState.Send(new HealthbarPoison(m));

                    if (sendHealthbarYellow)
                        ourState.Send(new HealthbarYellow(m));
                }
                else
                {
                    if (sendMoving || sendHealthbarPoison || sendHealthbarYellow)
                    {
                        int noto = Notoriety.Compute(m, m);
                        ourState.Send(cache[1][noto] = Packet.Acquire(new MobileMovingOld(m, noto)));
                    }
                }

                if (sendPublicStats || sendPrivateStats)
                {
                    ourState.Send(new MobileStatusExtended(m, _NetState));
                }
                else if (sendAll)
                {
                    ourState.Send(new MobileAttributes(m));
                }
                else if (sendAny)
                {
                    if (sendHits)
                        ourState.Send(new MobileHits(m));

                    if (sendStam)
                        ourState.Send(new MobileStam(m));

                    if (sendMana)
                        ourState.Send(new MobileMana(m));
                }

                if (sendStam || sendMana)
                {
                    IParty ip = Party as IParty;

                    if (ip != null && sendStam)
                        ip.OnStamChanged(this);

                    if (ip != null && sendMana)
                        ip.OnManaChanged(this);
                }

                if (sendHair)
                {
                    if (removeHair)
                        ourState.Send(new RemoveHair(m));
                    else
                        ourState.Send(new HairEquipUpdate(m));
                }

                if (sendFacialHair)
                {
                    if (removeFacialHair)
                        ourState.Send(new RemoveFacialHair(m));
                    else
                        ourState.Send(new FacialHairEquipUpdate(m));
                }

                if (sendOPLUpdate)
                    ourState.Send(OPLPacket);
            }

            sendMoving = sendMoving || sendNonlocalMoving;
            sendIncoming = sendIncoming || sendNonlocalIncoming;
            sendHits = sendHits || sendAll;

            if (m._Map != null && (sendRemove || sendIncoming || sendPublicStats || sendHits || sendMoving || sendOPLUpdate || sendHair || sendFacialHair || sendHealthbarPoison || sendHealthbarYellow))
            {
                Mobile beholder;

                Packet hitsPacket = null;
                Packet statPacketTrue = null;
                Packet statPacketFalse = null;
                Packet deadPacket = null;
                Packet hairPacket = null;
                Packet facialhairPacket = null;
                Packet hbpPacket = null;
                Packet hbyPacket = null;

                IPooledEnumerable<NetState> eable = m.Map.GetClientsInRange(m._Location);

                foreach (NetState state in eable)
                {
                    beholder = state.Mobile;

                    if (beholder != m && beholder.CanSee(m))
                    {
                        if (sendRemove)
                            state.Send(this.RemovePacket);

                        if (sendIncoming)
                        {
                            state.Send(MobileIncoming.Create(state, beholder, m));

                            if (m.IsDeadBondedPet)
                            {
                                if (deadPacket == null)
                                    deadPacket = Packet.Acquire(new BondedStatus(0, m.Serial, 1));

                                state.Send(deadPacket);
                            }
                        }

                        if (state.StygianAbyss)
                        {
                            if (sendMoving)
                            {
                                int noto = Notoriety.Compute(beholder, m);

                                Packet p = cache[0][noto];

                                if (p == null)
                                    cache[0][noto] = p = Packet.Acquire(new MobileMoving(m, noto));

                                state.Send(p);
                            }

                            if (sendHealthbarPoison)
                            {
                                if (hbpPacket == null)
                                    hbpPacket = Packet.Acquire(new HealthbarPoison(m));

                                state.Send(hbpPacket);
                            }

                            if (sendHealthbarYellow)
                            {
                                if (hbyPacket == null)
                                    hbyPacket = Packet.Acquire(new HealthbarYellow(m));

                                state.Send(hbyPacket);
                            }
                        }
                        else
                        {
                            if (sendMoving || sendHealthbarPoison || sendHealthbarYellow)
                            {
                                int noto = Notoriety.Compute(beholder, m);

                                Packet p = cache[1][noto];

                                if (p == null)
                                    cache[1][noto] = p = Packet.Acquire(new MobileMovingOld(m, noto));

                                state.Send(p);
                            }
                        }

                        if (sendPublicStats)
                        {
                            if (m.CanBeRenamedBy(beholder))
                            {
                                if (statPacketTrue == null)
                                    statPacketTrue = Packet.Acquire(new MobileStatusCompact(true, m));

                                state.Send(statPacketTrue);
                            }
                            else
                            {
                                if (statPacketFalse == null)
                                    statPacketFalse = Packet.Acquire(new MobileStatusCompact(false, m));

                                state.Send(statPacketFalse);
                            }
                        }
                        else if (sendHits)
                        {
                            if (hitsPacket == null)
                                hitsPacket = Packet.Acquire(new MobileHitsN(m));

                            state.Send(hitsPacket);
                        }

                        if (sendHair)
                        {
                            if (hairPacket == null)
                            {
                                if (removeHair)
                                    hairPacket = Packet.Acquire(new RemoveHair(m));
                                else
                                    hairPacket = Packet.Acquire(new HairEquipUpdate(m));
                            }

                            state.Send(hairPacket);
                        }

                        if (sendFacialHair)
                        {
                            if (facialhairPacket == null)
                            {
                                if (removeFacialHair)
                                    facialhairPacket = Packet.Acquire(new RemoveFacialHair(m));
                                else
                                    facialhairPacket = Packet.Acquire(new FacialHairEquipUpdate(m));
                            }

                            state.Send(facialhairPacket);
                        }

                        if (sendOPLUpdate)
                            state.Send(this.OPLPacket);
                    }
                }

                Packet.Release(hitsPacket);
                Packet.Release(statPacketTrue);
                Packet.Release(statPacketFalse);
                Packet.Release(deadPacket);
                Packet.Release(hairPacket);
                Packet.Release(facialhairPacket);
                Packet.Release(hbpPacket);
                Packet.Release(hbyPacket);

                eable.Free();
            }

            if (sendMoving || sendNonlocalMoving || sendHealthbarPoison || sendHealthbarYellow)
            {
                for (int i = 0; i < cache.Length; ++i)
                    for (int j = 0; j < cache[i].Length; ++j)
                        Packet.Release(ref cache[i][j]);
            }
        }

        static bool _processing = false;

        public static void ProcessDeltaQueue()
        {
            _processing = true;
            if (_DeltaQueue.Count >= 512)
            {
                Parallel.ForEach(_DeltaQueue, m => m.ProcessDelta());
                _DeltaQueue.Clear();
            }
            else while (_DeltaQueue.Count > 0) _DeltaQueue.Dequeue().ProcessDelta();
            _processing = false;
            while (_DeltaQueueR.Count > 0) _DeltaQueueR.Dequeue().ProcessDelta();
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public int Kills
        {
            get => _Kills;
            set
            {
                var oldValue = _Kills;
                if (_Kills != value)
                {
                    _Kills = value;
                    if (_Kills < 0)
                        _Kills = 0;
                    if ((oldValue >= 5) != (_Kills >= 5))
                    {
                        Delta(MobileDelta.Noto);
                        InvalidateProperties();
                    }
                    OnKillsChange(oldValue);
                }
            }
        }

        public virtual void OnKillsChange(int oldValue) { }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ShortTermMurders
        {
            get => _ShortTermMurders;
            set
            {
                if (_ShortTermMurders != value)
                {
                    _ShortTermMurders = value;
                    if (_ShortTermMurders < 0)
                        _ShortTermMurders = 0;
                }
            }
        }

        [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
        public bool Criminal
        {
            get => _Criminal;
            set
            {
                if (_Criminal != value)
                {
                    _Criminal = value;
                    Delta(MobileDelta.Noto);
                    InvalidateProperties();
                }
                if (_Criminal)
                {
                    if (_ExpireCriminal == null) _ExpireCriminal = new ExpireCriminalTimer(this);
                    else _ExpireCriminal.Stop();
                    _ExpireCriminal.Start();
                }
                else if (_ExpireCriminal != null)
                {
                    _ExpireCriminal.Stop();
                    _ExpireCriminal = null;
                }
            }
        }

        public bool CheckAlive(bool message = true)
        {
            if (!Alive)
            {
                if (message)
                    LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019048); // I am dead and cannot do that.
                return false;
            }
            return true;
        }

        #region Overhead messages

        public void PublicOverheadMessage(MessageType type, int hue, bool ascii, string text) => PublicOverheadMessage(type, hue, ascii, text, true);

        public void PublicOverheadMessage(MessageType type, int hue, bool ascii, string text, bool noLineOfSight)
        {
            if (_Map != null)
            {
                Packet p = null;

                if (ascii)
                    p = new AsciiMessage(Serial, Body, type, hue, 3, Name, text);
                else
                    p = new UnicodeMessage(Serial, Body, type, hue, 3, _Language, Name, text);

                p.Acquire();

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this) && (noLineOfSight || state.Mobile.InLOS(this)))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number)
        {
            PublicOverheadMessage(type, hue, number, "", true);
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number, string args)
        {
            PublicOverheadMessage(type, hue, number, args, true);
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number, string args, bool noLineOfSight)
        {
            if (_Map != null)
            {
                Packet p = Packet.Acquire(new MessageLocalized(Serial, Body, type, hue, 3, number, Name, args));

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this) && (noLineOfSight || state.Mobile.InLOS(this)))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number, AffixType affixType, string affix, string args)
        {
            PublicOverheadMessage(type, hue, number, affixType, affix, args, true);
        }

        public void PublicOverheadMessage(MessageType type, int hue, int number, AffixType affixType, string affix, string args, bool noLineOfSight)
        {
            if (_Map != null)
            {
                Packet p = Packet.Acquire(new MessageLocalizedAffix(Serial, Body, type, hue, 3, number, Name, affixType, affix, args));

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state.Mobile.CanSee(this) && (noLineOfSight || state.Mobile.InLOS(this)))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        public void PrivateOverheadMessage(MessageType type, int hue, bool ascii, string text, NetState state)
        {
            if (state == null)
                return;

            if (ascii)
                state.Send(new AsciiMessage(Serial, Body, type, hue, 3, Name, text));
            else
                state.Send(new UnicodeMessage(Serial, Body, type, hue, 3, _Language, Name, text));
        }

        public void PrivateOverheadMessage(MessageType type, int hue, int number, NetState state)
        {
            PrivateOverheadMessage(type, hue, number, "", state);
        }

        public void PrivateOverheadMessage(MessageType type, int hue, int number, string args, NetState state)
        {
            if (state == null)
                return;

            state.Send(new MessageLocalized(Serial, Body, type, hue, 3, number, Name, args));
        }

        public void LocalOverheadMessage(MessageType type, int hue, bool ascii, string text)
        {
            NetState ns = _NetState;

            if (ns != null)
            {
                if (ascii)
                    ns.Send(new AsciiMessage(Serial, Body, type, hue, 3, Name, text));
                else
                    ns.Send(new UnicodeMessage(Serial, Body, type, hue, 3, _Language, Name, text));
            }
        }

        public void LocalOverheadMessage(MessageType type, int hue, int number)
        {
            LocalOverheadMessage(type, hue, number, "");
        }

        public void LocalOverheadMessage(MessageType type, int hue, int number, string args)
        {
            NetState ns = _NetState;

            if (ns != null)
                ns.Send(new MessageLocalized(Serial, Body, type, hue, 3, number, Name, args));
        }

        public void NonlocalOverheadMessage(MessageType type, int hue, int number)
        {
            NonlocalOverheadMessage(type, hue, number, "");
        }

        public void NonlocalOverheadMessage(MessageType type, int hue, int number, string args)
        {
            if (_Map != null)
            {
                Packet p = Packet.Acquire(new MessageLocalized(Serial, Body, type, hue, 3, number, Name, args));

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state != _NetState && state.Mobile.CanSee(this))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        public void NonlocalOverheadMessage(MessageType type, int hue, bool ascii, string text)
        {
            if (_Map != null)
            {
                Packet p = null;

                if (ascii)
                    p = new AsciiMessage(Serial, Body, type, hue, 3, Name, text);
                else
                    p = new UnicodeMessage(Serial, Body, type, hue, 3, Language, Name, text);

                p.Acquire();

                IPooledEnumerable<NetState> eable = _Map.GetClientsInRange(_Location);

                foreach (NetState state in eable)
                {
                    if (state != _NetState && state.Mobile.CanSee(this))
                    {
                        state.Send(p);
                    }
                }

                Packet.Release(p);

                eable.Free();
            }
        }

        #endregion

        #region SendLocalizedMessage

        public void SendLocalizedMessage(int number)
        {
            NetState ns = _NetState;

            if (ns != null)
                ns.Send(MessageLocalized.InstantiateGeneric(number));
        }

        public void SendLocalizedMessage(int number, string args)
        {
            SendLocalizedMessage(number, args, 0x3B2);
        }

        public void SendLocalizedMessage(int number, string args, int hue)
        {
            if (hue == 0x3B2 && (args == null || args.Length == 0))
            {
                NetState ns = _NetState;

                if (ns != null)
                    ns.Send(MessageLocalized.InstantiateGeneric(number));
            }
            else
            {
                NetState ns = _NetState;

                if (ns != null)
                    ns.Send(new MessageLocalized(Serial.MinusOne, -1, MessageType.Regular, hue, 3, number, "System", args));
            }
        }

        public void SendLocalizedMessage(int number, bool append, string affix)
        {
            SendLocalizedMessage(number, append, affix, "", 0x3B2);
        }

        public void SendLocalizedMessage(int number, bool append, string affix, string args)
        {
            SendLocalizedMessage(number, append, affix, args, 0x3B2);
        }

        public void SendLocalizedMessage(int number, bool append, string affix, string args, int hue)
        {
            NetState ns = _NetState;

            if (ns != null)
                ns.Send(new MessageLocalizedAffix(Serial.MinusOne, -1, MessageType.Regular, hue, 3, number, "System", (append ? AffixType.Append : AffixType.Prepend) | AffixType.System, affix, args));
        }

        #endregion

        public void LaunchBrowser(string url)
        {
            if (_NetState != null)
                _NetState.LaunchBrowser(url);
        }

        #region Send[ASCII]Message

        public void SendMessage(string text)
        {
            SendMessage(0x3B2, text);
        }

        public void SendMessage(string format, params object[] args)
        {
            SendMessage(0x3B2, String.Format(format, args));
        }

        public void SendMessage(int hue, string text)
        {
            NetState ns = _NetState;

            if (ns != null)
                ns.Send(new UnicodeMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "ENU", "System", text));
        }

        public void SendMessage(int hue, string format, params object[] args)
        {
            SendMessage(hue, String.Format(format, args));
        }

        public void SendAsciiMessage(string text)
        {
            SendAsciiMessage(0x3B2, text);
        }

        public void SendAsciiMessage(string format, params object[] args)
        {
            SendAsciiMessage(0x3B2, String.Format(format, args));
        }

        public void SendAsciiMessage(int hue, string text)
        {
            NetState ns = _NetState;

            if (ns != null)
                ns.Send(new AsciiMessage(Serial.MinusOne, -1, MessageType.Regular, hue, 3, "System", text));
        }

        public void SendAsciiMessage(int hue, string format, params object[] args)
        {
            SendAsciiMessage(hue, String.Format(format, args));
        }

        #endregion

        #region InRange
        public bool InRange(Point2D p, int range)
        {
            return p.X >= (_Location.X - range)
                && p.X <= (_Location.X + range)
                && p.Y >= (_Location.Y - range)
                && p.Y <= (_Location.Y + range);
        }

        public bool InRange(Point3D p, int range)
        {
            return p.X >= (_Location.X - range)
                && p.X <= (_Location.X + range)
                && p.Y >= (_Location.Y - range)
                && p.Y <= (_Location.Y + range);
        }

        public bool InRange(IPoint2D p, int range)
        {
            return p.X >= (_Location.X - range)
                && p.X <= (_Location.X + range)
                && p.Y >= (_Location.Y - range)
                && p.Y <= (_Location.Y + range);
        }
        #endregion

        public void InitStats(int str, int dex, int intel)
        {
            _Str = str;
            _Dex = dex;
            _Int = intel;

            Hits = HitsMax;
            Stam = StamMax;
            Mana = ManaMax;

            Delta(MobileDelta.Stat | MobileDelta.Hits | MobileDelta.Stam | MobileDelta.Mana);
        }

        public virtual void DisplayPaperdollTo(Mobile to)
        {
            EventSink.InvokePaperdollRequest(new PaperdollRequestEventArgs(to, this));
        }

        private static bool m_DisableDismountInWarmode;

        public static bool DisableDismountInWarmode { get { return m_DisableDismountInWarmode; } set { m_DisableDismountInWarmode = value; } }

        #region OnDoubleClick[..]

        /// <summary>
        /// Overridable. Event invoked when the Mobile is double clicked. By default, this method can either dismount or open the paperdoll.
        /// <seealso cref="CanPaperdollBeOpenedBy" />
        /// <seealso cref="DisplayPaperdollTo" />
        /// </summary>
        public virtual void OnDoubleClick(Mobile from)
        {
            if (this == from && (!m_DisableDismountInWarmode || !_Warmode))
            {
                IMount mount = Mount;

                if (mount != null)
                {
                    mount.Rider = null;
                    return;
                }
            }

            if (CanPaperdollBeOpenedBy(from))
                DisplayPaperdollTo(from);
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile is double clicked by someone who is over 18 tiles away.
        /// <seealso cref="OnDoubleClick" />
        /// </summary>
        public virtual void OnDoubleClickOutOfRange(Mobile from)
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the Mobile is double clicked by someone who can no longer see the Mobile. This may happen, for example, using 'Last Object' after the Mobile has hidden.
        /// <seealso cref="OnDoubleClick" />
        /// </summary>
        public virtual void OnDoubleClickCantSee(Mobile from)
        {
        }

        /// <summary>
        /// Overridable. Event invoked when the Mobile is double clicked by someone who is not alive. Similar to <see cref="OnDoubleClick" />, this method will show the paperdoll. It does not, however, provide any dismount functionality.
        /// <seealso cref="OnDoubleClick" />
        /// </summary>
        public virtual void OnDoubleClickDead(Mobile from)
        {
            if (CanPaperdollBeOpenedBy(from))
                DisplayPaperdollTo(from);
        }

        #endregion

        /// <summary>
        /// Overridable. Event invoked when the Mobile requests to open his own paperdoll via the 'Open Paperdoll' macro.
        /// </summary>
        public virtual void OnPaperdollRequest()
        {
            if (CanPaperdollBeOpenedBy(this))
                DisplayPaperdollTo(this);
        }

        private static int m_BodyWeight = 14;

        public static int BodyWeight { get { return m_BodyWeight; } set { m_BodyWeight = value; } }

        /// <summary>
        /// Overridable. Event invoked when <paramref name="from" /> wants to see this Mobile's stats.
        /// </summary>
        /// <param name="from"></param>
        public virtual void OnStatsQuery(Mobile from)
        {
            if (from.Map == this.Map && Utility.InUpdateRange(this, from) && from.CanSee(this))
                from.Send(new MobileStatus(from, this, _NetState));

            if (from == this)
                Send(new StatLockInfo(this));

            IParty ip = Party as IParty;

            if (ip != null)
                ip.OnStatsQuery(from, this);
        }

        /// <summary>
        /// Overridable. Event invoked when <paramref name="from" /> wants to see this Mobile's skills.
        /// </summary>
        public virtual void OnSkillsQuery(Mobile from)
        {
            if (from == this)
                Send(new SkillUpdate(_Skills));
        }

        /// <summary>
        /// Overridable. Virtual event invoked when <see cref="Region" /> changes.
        /// </summary>
        public virtual void OnRegionChange(Region Old, Region New)
        {
        }

        private Item m_MountItem;

        [CommandProperty(AccessLevel.GameMaster)]
        public IMount Mount
        {
            get
            {
                IMountItem mountItem = null;

                if (m_MountItem != null && !m_MountItem.Deleted && m_MountItem.Parent == this)
                    mountItem = (IMountItem)m_MountItem;

                if (mountItem == null)
                    m_MountItem = (mountItem = (FindItemOnLayer(Layer.Mount) as IMountItem)) as Item;

                return mountItem == null ? null : mountItem.Mount;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Mounted
        {
            get
            {
                return (Mount != null);
            }
        }

        private QuestArrow m_QuestArrow;

        public QuestArrow QuestArrow
        {
            get
            {
                return m_QuestArrow;
            }
            set
            {
                if (m_QuestArrow != value)
                {
                    if (m_QuestArrow != null)
                        m_QuestArrow.Stop();

                    m_QuestArrow = value;
                }
            }
        }

        private static string[] m_GuildTypes = new string[]
            {
                "",
                " (Chaos)",
                " (Order)"
            };

        public virtual bool CanTarget { get { return true; } }
        public virtual bool ClickTitle { get { return true; } }

        public virtual bool PropertyTitle { get { return m_OldPropertyTitles ? ClickTitle : true; } }

        private static bool m_DisableHiddenSelfClick = true;
        private static bool m_AsciiClickMessage = true;
        private static bool m_GuildClickMessage = true;
        private static bool m_OldPropertyTitles;

        public static bool DisableHiddenSelfClick { get { return m_DisableHiddenSelfClick; } set { m_DisableHiddenSelfClick = value; } }
        public static bool AsciiClickMessage { get { return m_AsciiClickMessage; } set { m_AsciiClickMessage = value; } }
        public static bool GuildClickMessage { get { return m_GuildClickMessage; } set { m_GuildClickMessage = value; } }
        public static bool OldPropertyTitles { get { return m_OldPropertyTitles; } set { m_OldPropertyTitles = value; } }

        public virtual bool ShowFameTitle { get { return true; } }//(m_Player || m_Body.IsHuman) && m_Fame >= 10000; } 

        /// <summary>
        /// Overridable. Event invoked when the Mobile is single clicked.
        /// </summary>
        public virtual void OnSingleClick(Mobile from)
        {
            if (m_Deleted)
                return;
            else if (AccessLevel == AccessLevel.Player && DisableHiddenSelfClick && Hidden && from == this)
                return;

            if (m_GuildClickMessage)
            {
                BaseGuild guild = _Guild;

                if (guild != null && (_DisplayGuildTitle || (_Player && guild.Type != GuildType.Regular)))
                {
                    string title = GuildTitle;
                    string type;

                    if (title == null)
                        title = "";
                    else
                        title = title.Trim();

                    if (guild.Type >= 0 && (int)guild.Type < m_GuildTypes.Length)
                        type = m_GuildTypes[(int)guild.Type];
                    else
                        type = "";

                    string text = String.Format(title.Length <= 0 ? "[{1}]{2}" : "[{0}, {1}]{2}", title, guild.Abbreviation, type);

                    PrivateOverheadMessage(MessageType.Regular, SpeechHue, true, text, from.NetState);
                }
            }

            int hue;

            if (NameHue != -1)
                hue = NameHue;
            else if (AccessLevel > AccessLevel.Player)
                hue = 11;
            else
                hue = Notoriety.GetHue(Notoriety.Compute(from, this));

            string name = Name;

            if (name == null)
                name = String.Empty;

            string prefix = "";

            if (ShowFameTitle && (_Player || _Body.IsHuman) && _Fame >= 10000)
                prefix = (_Female ? "Lady" : "Lord");

            string suffix = "";

            if (ClickTitle && Title != null && Title.Length > 0)
                suffix = Title;

            suffix = ApplyNameSuffix(suffix);

            string val;

            if (prefix.Length > 0 && suffix.Length > 0)
                val = String.Concat(prefix, " ", name, " ", suffix);
            else if (prefix.Length > 0)
                val = String.Concat(prefix, " ", name);
            else if (suffix.Length > 0)
                val = String.Concat(name, " ", suffix);
            else
                val = name;

            PrivateOverheadMessage(MessageType.Label, hue, m_AsciiClickMessage, val, from.NetState);
        }

        public bool CheckSkill(SkillName skill, double minSkill, double maxSkill)
        {
            if (SkillCheckLocationHandler == null)
                return false;
            else
                return SkillCheckLocationHandler(this, skill, minSkill, maxSkill);
        }

        public bool CheckSkill(SkillName skill, double chance)
        {
            if (SkillCheckDirectLocationHandler == null)
                return false;
            else
                return SkillCheckDirectLocationHandler(this, skill, chance);
        }

        public bool CheckTargetSkill(SkillName skill, object target, double minSkill, double maxSkill)
        {
            if (SkillCheckTargetHandler == null)
                return false;
            else
                return SkillCheckTargetHandler(this, skill, target, minSkill, maxSkill);
        }

        public bool CheckTargetSkill(SkillName skill, object target, double chance)
        {
            if (SkillCheckDirectTargetHandler == null)
                return false;
            else
                return SkillCheckDirectTargetHandler(this, skill, target, chance);
        }

        public virtual void DisruptiveAction()
        {
            if (Meditating)
            {
                Meditating = false;
                SendLocalizedMessage(500134); // You stop meditating.
            }
        }

        #region Armor
        public Item ShieldArmor
        {
            get
            {
                return FindItemOnLayer(Layer.TwoHanded) as Item;
            }
        }

        public Item NeckArmor
        {
            get
            {
                return FindItemOnLayer(Layer.Neck) as Item;
            }
        }

        public Item HandArmor
        {
            get
            {
                return FindItemOnLayer(Layer.Gloves) as Item;
            }
        }

        public Item HeadArmor
        {
            get
            {
                return FindItemOnLayer(Layer.Helm) as Item;
            }
        }

        public Item ArmsArmor
        {
            get
            {
                return FindItemOnLayer(Layer.Arms) as Item;
            }
        }

        public Item LegsArmor
        {
            get
            {
                Item ar = FindItemOnLayer(Layer.InnerLegs) as Item;

                if (ar == null)
                    ar = FindItemOnLayer(Layer.Pants) as Item;

                return ar;
            }
        }

        public Item ChestArmor
        {
            get
            {
                Item ar = FindItemOnLayer(Layer.InnerTorso) as Item;

                if (ar == null)
                    ar = FindItemOnLayer(Layer.Shirt) as Item;

                return ar;
            }
        }

        public Item Talisman
        {
            get
            {
                return FindItemOnLayer(Layer.Talisman) as Item;
            }
        }
        #endregion

        /// <summary>
        /// Gets or sets the maximum attainable value for <see cref="RawStr" />, <see cref="RawDex" />, and <see cref="RawInt" />.
        /// </summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int StatCap
        {
            get
            {
                return _StatCap;
            }
            set
            {
                if (_StatCap != value)
                {
                    _StatCap = value;

                    Delta(MobileDelta.StatCap);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Meditating { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool CanSwim { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool CantWalk { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool CanHearGhosts
        {
            get
            {
                return _CanHearGhosts || AccessLevel >= AccessLevel.Counselor;
            }
            set
            {
                _CanHearGhosts = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int RawStatTotal
        {
            get
            {
                return RawStr + RawDex + RawInt;
            }
        }

        public long NextSpellTime { get; set; }

        public static TimeSpan DefaultStamRate1 { get => DefaultStamRate; set => DefaultStamRate = value; }
        public int YellHue1 { get; set; }

        /// <summary>
        /// Overridable. Virtual event invoked when the sector this Mobile is in gets <see cref="Sector.Activate">activated</see>.
        /// </summary>
        public virtual void OnSectorActivate()
        {
        }

        /// <summary>
        /// Overridable. Virtual event invoked when the sector this Mobile is in gets <see cref="Sector.Deactivate">deactivated</see>.
        /// </summary>
        public virtual void OnSectorDeactivate()
        {
        }
    }
}
