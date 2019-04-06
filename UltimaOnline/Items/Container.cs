using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UltimaOnline.Network;

namespace UltimaOnline.Items
{
    public delegate void OnItemConsumed(Item item, int amount);
    public delegate int CheckItemGroup(Item a, Item b);

    public delegate void ContainerSnoopHandler(Container cont, Mobile from);

    public class Container : Item
    {
        public static ContainerSnoopHandler SnoopHandler { get; set; }

        ContainerData _containerData;
        int _DropSound;
        int _GumpID;
        int _MaxItems;

        int _TotalItems;
        int _TotalWeight;
        int _TotalGold;
        internal List<Item> _Items;

        public ContainerData ContainerData
        {
            get
            {
                if (_containerData == null)
                    UpdateContainerData();
                return _containerData;
            }
            set => _containerData = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int ItemID
        {
            get => base.ItemID;
            set
            {
                var oldID = ItemID;
                base.ItemID = value;
                if (ItemID != oldID)
                    UpdateContainerData();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int GumpID
        {
            get => _GumpID == -1 ? DefaultGumpID : _GumpID;
            set => _GumpID = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int DropSound
        {
            get => _DropSound == -1 ? DefaultDropSound : _DropSound;
            set => _DropSound = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int MaxItems
        {
            get => _MaxItems == -1 ? DefaultMaxItems : _MaxItems;
            set { _MaxItems = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual int MaxWeight => Parent is Container && ((Container)Parent).MaxWeight == 0 ? 0 : DefaultMaxWeight;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool LiftOverride { get; set; }

        public virtual void UpdateContainerData() => ContainerData = ContainerData.GetData(ItemID);
        public virtual Rectangle2D Bounds => ContainerData.Bounds;
        public virtual int DefaultGumpID => ContainerData.GumpID;
        public virtual int DefaultDropSound => ContainerData.DropSound;
        public virtual int DefaultMaxItems => GlobalMaxItems;
        public virtual int DefaultMaxWeight => GlobalMaxWeight;

        public virtual bool IsDecoContainer => !Movable && !IsLockedDown && !IsSecure && Parent == null && !LiftOverride;

        public virtual int GetDroppedSound(Item item)
        {
            var dropSound = item.GetDropSound();
            return dropSound != -1 ? dropSound : DropSound;
        }

        public override void OnSnoop(Mobile from) => SnoopHandler?.Invoke(this, from);

        public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            if (from.AccessLevel < AccessLevel.GameMaster && IsDecoContainer)
            {
                reject = LRReason.CannotLift;
                return false;
            }
            return base.CheckLift(from, item, ref reject);
        }

        public override bool CheckItemUse(Mobile from, Item item)
        {
            if (item != this && from.AccessLevel < AccessLevel.GameMaster && IsDecoContainer)
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
                return false;
            }
            return base.CheckItemUse(from, item);
        }

        public virtual bool CheckHold(Mobile m, Item item, bool message, bool checkItems = true, int plusItems = 0, int plusWeight = 0)
        {
            if (m.AccessLevel < AccessLevel.GameMaster)
            {
                if (IsDecoContainer)
                {
                    if (message)
                        SendCantStoreMessage(m, item);
                    return false;
                }
                var maxItems = MaxItems;
                if (checkItems && maxItems != 0 && (TotalItems + plusItems + item.TotalItems + (item.IsVirtualItem ? 0 : 1)) > maxItems)
                {
                    if (message)
                        SendFullItemsMessage(m, item);
                    return false;
                }
                else
                {
                    var maxWeight = MaxWeight;
                    if (maxWeight != 0 && (TotalWeight + plusWeight + item.TotalWeight + item.PileWeight) > maxWeight)
                    {
                        if (message)
                            SendFullWeightMessage(m, item);
                        return false;
                    }
                }
            }
            var parent = Parent;
            while (parent != null)
            {
                if (parent is Container c) return c.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
                else if (parent is Item i) parent = i.Parent;
                else break;
            }
            return true;
        }

        public virtual void SendFullItemsMessage(Mobile to, Item item) => to.SendMessage("That container cannot hold more items.");

        public virtual void SendFullWeightMessage(Mobile to, Item item) => to.SendMessage("That container cannot hold more weight.");

        public virtual void SendCantStoreMessage(Mobile to, Item item) => to.SendLocalizedMessage(500176); // That is not your container, you can't store things here.

        public virtual bool OnDragDropInto(Mobile from, Item item, Point3D p)
        {
            if (!CheckHold(from, item, true, true))
                return false;
            item.Location = new Point3D(p.X, p.Y, 0);
            AddItem(item);
            from.SendSound(GetDroppedSound(item), GetWorldLocation());
            return true;
        }

        class GroupComparer : IComparer
        {
            CheckItemGroup _Grouper;
            public GroupComparer(CheckItemGroup grouper) => _Grouper = grouper;

            public int Compare(object x, object y) => _Grouper((Item)x, (Item)y);
        }

        #region Consume[...]

        public bool ConsumeTotalGrouped(Type type, int amount, bool recurse, OnItemConsumed callback, CheckItemGroup grouper)
        {
            if (grouper == null)
                throw new ArgumentNullException();
            var typedItems = FindItemsByType(type, recurse);
            var groups = new List<List<Item>>();
            var idx = 0;
            while (idx < typedItems.Length)
            {
                var a = typedItems[idx++];
                var group = new List<Item> { a };
                while (idx < typedItems.Length)
                {
                    var b = typedItems[idx];
                    int v = grouper(a, b);
                    if (v == 0) group.Add(b);
                    else break;
                    ++idx;
                }
                groups.Add(group);
            }
            var items = new Item[groups.Count][];
            var totals = new int[groups.Count];
            var hasEnough = false;
            for (var i = 0; i < groups.Count; ++i)
            {
                items[i] = groups[i].ToArray();
                for (var j = 0; j < items[i].Length; ++j)
                    totals[i] += items[i][j].Amount;
                if (totals[i] >= amount)
                    hasEnough = true;
            }
            if (!hasEnough)
                return false;
            for (var i = 0; i < items.Length; ++i)
                if (totals[i] >= amount)
                {
                    var need = amount;
                    for (var j = 0; j < items[i].Length; ++j)
                    {
                        var item = items[i][j];
                        var theirAmount = item.Amount;
                        if (theirAmount < need)
                        {
                            callback?.Invoke(item, theirAmount);
                            item.Delete();
                            need -= theirAmount;
                        }
                        else
                        {
                            callback?.Invoke(item, need);
                            item.Consume(need);
                            break;
                        }
                    }
                    break;
                }
            return true;
        }

        public int ConsumeTotalGrouped(Type[] types, int[] amounts, bool recurse, OnItemConsumed callback, CheckItemGroup grouper)
        {
            if (types.Length != amounts.Length)
                throw new ArgumentException();
            else if (grouper == null)
                throw new ArgumentNullException();
            var items = new Item[types.Length][][];
            var totals = new int[types.Length][];
            for (var i = 0; i < types.Length; ++i)
            {
                var typedItems = FindItemsByType(types[i], recurse);
                var groups = new List<List<Item>>();
                var idx = 0;
                while (idx < typedItems.Length)
                {
                    var a = typedItems[idx++];
                    var group = new List<Item> { a };
                    while (idx < typedItems.Length)
                    {
                        var b = typedItems[idx];
                        var v = grouper(a, b);
                        if (v == 0) group.Add(b);
                        else break;
                        ++idx;
                    }
                    groups.Add(group);
                }
                items[i] = new Item[groups.Count][];
                totals[i] = new int[groups.Count];
                var hasEnough = false;
                for (var j = 0; j < groups.Count; ++j)
                {
                    items[i][j] = groups[j].ToArray();
                    for (var k = 0; k < items[i][j].Length; ++k)
                        totals[i][j] += items[i][j][k].Amount;
                    if (totals[i][j] >= amounts[i])
                        hasEnough = true;
                }
                if (!hasEnough)
                    return i;
            }
            for (var i = 0; i < items.Length; ++i)
                for (var j = 0; j < items[i].Length; ++j)
                    if (totals[i][j] >= amounts[i])
                    {
                        var need = amounts[i];
                        for (var k = 0; k < items[i][j].Length; ++k)
                        {
                            var item = items[i][j][k];
                            var theirAmount = item.Amount;
                            if (theirAmount < need)
                            {
                                callback?.Invoke(item, theirAmount);
                                item.Delete();
                                need -= theirAmount;
                            }
                            else
                            {
                                callback?.Invoke(item, need);
                                item.Consume(need);
                                break;
                            }
                        }
                        break;
                    }
            return -1;
        }

        public int ConsumeTotalGrouped(Type[][] types, int[] amounts, bool recurse, OnItemConsumed callback, CheckItemGroup grouper)
        {
            if (types.Length != amounts.Length)
                throw new ArgumentException();
            else if (grouper == null)
                throw new ArgumentNullException();
            var items = new Item[types.Length][][];
            var totals = new int[types.Length][];
            for (var i = 0; i < types.Length; ++i)
            {
                var typedItems = FindItemsByType(types[i], recurse);
                var groups = new List<List<Item>>();
                var idx = 0;
                while (idx < typedItems.Length)
                {
                    var a = typedItems[idx++];
                    var group = new List<Item> { a };
                    while (idx < typedItems.Length)
                    {
                        var b = typedItems[idx];
                        var v = grouper(a, b);
                        if (v == 0) group.Add(b);
                        else break;
                        ++idx;
                    }
                    groups.Add(group);
                }
                items[i] = new Item[groups.Count][];
                totals[i] = new int[groups.Count];
                var hasEnough = false;
                for (var j = 0; j < groups.Count; ++j)
                {
                    items[i][j] = groups[j].ToArray();
                    for (var k = 0; k < items[i][j].Length; ++k)
                        totals[i][j] += items[i][j][k].Amount;
                    if (totals[i][j] >= amounts[i])
                        hasEnough = true;
                }
                if (!hasEnough)
                    return i;
            }
            for (var i = 0; i < items.Length; ++i)
                for (var j = 0; j < items[i].Length; ++j)
                    if (totals[i][j] >= amounts[i])
                    {
                        var need = amounts[i];
                        for (var k = 0; k < items[i][j].Length; ++k)
                        {
                            var item = items[i][j][k];
                            var theirAmount = item.Amount;
                            if (theirAmount < need)
                            {
                                callback?.Invoke(item, theirAmount);
                                item.Delete();
                                need -= theirAmount;
                            }
                            else
                            {
                                callback?.Invoke(item, need);
                                item.Consume(need);
                                break;
                            }
                        }
                        break;
                    }
            return -1;
        }

        public int ConsumeTotal(Type[][] types, int[] amounts, bool recurse = true, OnItemConsumed callback = null)
        {
            if (types.Length != amounts.Length)
                throw new ArgumentException();
            var items = new Item[types.Length][];
            var totals = new int[types.Length];
            for (var i = 0; i < types.Length; ++i)
            {
                items[i] = FindItemsByType(types[i], recurse);
                for (var j = 0; j < items[i].Length; ++j)
                    totals[i] += items[i][j].Amount;
                if (totals[i] < amounts[i])
                    return i;
            }
            for (var i = 0; i < types.Length; ++i)
            {
                var need = amounts[i];
                for (var j = 0; j < items[i].Length; ++j)
                {
                    var item = items[i][j];
                    var theirAmount = item.Amount;
                    if (theirAmount < need)
                    {
                        callback?.Invoke(item, theirAmount);
                        item.Delete();
                        need -= theirAmount;
                    }
                    else
                    {
                        callback?.Invoke(item, need);
                        item.Consume(need);
                        break;
                    }
                }
            }
            return -1;
        }

        public int ConsumeTotal(Type[] types, int[] amounts, bool recurse = true, OnItemConsumed callback = null)
        {
            if (types.Length != amounts.Length)
                throw new ArgumentException();
            var items = new Item[types.Length][];
            var totals = new int[types.Length];
            for (var i = 0; i < types.Length; ++i)
            {
                items[i] = FindItemsByType(types[i], recurse);
                for (var j = 0; j < items[i].Length; ++j)
                    totals[i] += items[i][j].Amount;
                if (totals[i] < amounts[i])
                    return i;
            }
            for (var i = 0; i < types.Length; ++i)
            {
                var need = amounts[i];
                for (var j = 0; j < items[i].Length; ++j)
                {
                    var item = items[i][j];
                    var theirAmount = item.Amount;
                    if (theirAmount < need)
                    {
                        callback?.Invoke(item, theirAmount);
                        item.Delete();
                        need -= theirAmount;
                    }
                    else
                    {
                        callback?.Invoke(item, need);
                        item.Consume(need);
                        break;
                    }
                }
            }
            return -1;
        }

        public bool ConsumeTotal(Type type, int amount, bool recurse = true, OnItemConsumed callback = null)
        {
            var items = FindItemsByType(type, recurse);
            // First pass, compute total
            var total = 0;
            for (var i = 0; i < items.Length; ++i)
                total += items[i].Amount;
            if (total >= amount)
            {
                // We've enough, so consume it
                var need = amount;
                for (var i = 0; i < items.Length; ++i)
                {
                    var item = items[i];
                    var theirAmount = item.Amount;
                    if (theirAmount < need)
                    {
                        callback?.Invoke(item, theirAmount);
                        item.Delete();
                        need -= theirAmount;
                    }
                    else
                    {
                        callback?.Invoke(item, need);
                        item.Consume(need);
                        return true;
                    }
                }
            }
            return false;
        }

        public int ConsumeUpTo(Type type, int amount, bool recurse = true)
        {
            var consumed = 0;
            var toDelete = new Queue<Item>();
            RecurseConsumeUpTo(this, type, amount, recurse, ref consumed, toDelete);
            while (toDelete.Count > 0)
                toDelete.Dequeue().Delete();
            return consumed;
        }

        static void RecurseConsumeUpTo(Item current, Type type, int amount, bool recurse, ref int consumed, Queue<Item> toDelete)
        {
            if (current != null && current.Items.Count > 0)
            {
                var list = current.Items;
                for (var i = 0; i < list.Count; ++i)
                {
                    var item = list[i];
                    if (type.IsAssignableFrom(item.GetType()))
                    {
                        var need = amount - consumed;
                        var theirAmount = item.Amount;
                        if (theirAmount <= need)
                        {
                            toDelete.Enqueue(item);
                            consumed += theirAmount;
                        }
                        else
                        {
                            item.Amount -= need;
                            consumed += need;
                            return;
                        }
                    }
                    else if (recurse && item is Container)
                        RecurseConsumeUpTo(item, type, amount, recurse, ref consumed, toDelete);
                }
            }
        }

        #endregion

        #region Get[BestGroup]Amount

        public int GetBestGroupAmount(Type type, bool recurse, CheckItemGroup grouper)
        {
            if (grouper == null)
                throw new ArgumentNullException();
            var best = 0;
            var typedItems = FindItemsByType(type, recurse);
            var groups = new List<List<Item>>();
            var idx = 0;
            while (idx < typedItems.Length)
            {
                var a = typedItems[idx++];
                var group = new List<Item> { a };
                while (idx < typedItems.Length)
                {
                    var b = typedItems[idx];
                    var v = grouper(a, b);
                    if (v == 0) group.Add(b);
                    else break;
                    ++idx;
                }
                groups.Add(group);
            }
            for (var i = 0; i < groups.Count; ++i)
            {
                var items = groups[i].ToArray();
                var total = 0;
                for (var j = 0; j < items.Length; ++j)
                    total += items[j].Amount;
                if (total >= best)
                    best = total;
            }
            return best;
        }

        public int GetBestGroupAmount(Type[] types, bool recurse, CheckItemGroup grouper)
        {
            if (grouper == null)
                throw new ArgumentNullException();
            var best = 0;
            var typedItems = FindItemsByType(types, recurse);
            var groups = new List<List<Item>>();
            var idx = 0;
            while (idx < typedItems.Length)
            {
                var a = typedItems[idx++];
                var group = new List<Item> { a };
                while (idx < typedItems.Length)
                {
                    var b = typedItems[idx];
                    var v = grouper(a, b);
                    if (v == 0) group.Add(b);
                    else break;
                    ++idx;
                }
                groups.Add(group);
            }
            for (var j = 0; j < groups.Count; ++j)
            {
                var items = groups[j].ToArray();
                var total = 0;
                for (var k = 0; k < items.Length; ++k)
                    total += items[k].Amount;
                if (total >= best)
                    best = total;
            }
            return best;
        }

        public int GetBestGroupAmount(Type[][] types, bool recurse, CheckItemGroup grouper)
        {
            if (grouper == null)
                throw new ArgumentNullException();
            var best = 0;
            for (var i = 0; i < types.Length; ++i)
            {
                var typedItems = FindItemsByType(types[i], recurse);
                var groups = new List<List<Item>>();
                var idx = 0;
                while (idx < typedItems.Length)
                {
                    var a = typedItems[idx++];
                    var group = new List<Item> { a };
                    while (idx < typedItems.Length)
                    {
                        var b = typedItems[idx];
                        var v = grouper(a, b);
                        if (v == 0) group.Add(b);
                        else break;
                        ++idx;
                    }
                    groups.Add(group);
                }
                for (var j = 0; j < groups.Count; ++j)
                {
                    var items = groups[j].ToArray();
                    var total = 0;
                    for (var k = 0; k < items.Length; ++k)
                        total += items[k].Amount;
                    if (total >= best)
                        best = total;
                }
            }
            return best;
        }

        public int GetAmount(Type type, bool recurse = true)
        {
            var items = FindItemsByType(type, recurse);
            var amount = 0;
            for (var i = 0; i < items.Length; ++i)
                amount += items[i].Amount;
            return amount;
        }

        public int GetAmount(Type[] types, bool recurse = true)
        {
            var items = FindItemsByType(types, recurse);
            var amount = 0;
            for (var i = 0; i < items.Length; ++i)
                amount += items[i].Amount;
            return amount;
        }

        #endregion

        static List<Item> _findItemsList = new List<Item>();

        #region Non-Generic FindItem[s] by Type

        public Item[] FindItemsByType(Type type, bool recurse = true)
        {
            if (_findItemsList.Count > 0)
                _findItemsList.Clear();
            RecurseFindItemsByType(this, type, recurse, _findItemsList);
            return _findItemsList.ToArray();
        }
        static void RecurseFindItemsByType(Item current, Type type, bool recurse, List<Item> list)
        {
            if (current != null && current.Items.Count > 0)
            {
                var items = current.Items;
                for (var i = 0; i < items.Count; ++i)
                {
                    var item = items[i];
                    if (type.IsAssignableFrom(item.GetType())) // item.GetType().IsAssignableFrom(type))
                        list.Add(item);
                    if (recurse && item is Container)
                        RecurseFindItemsByType(item, type, recurse, list);
                }
            }
        }

        public Item[] FindItemsByType(Type[] types, bool recurse = true)
        {
            if (_findItemsList.Count > 0)
                _findItemsList.Clear();
            RecurseFindItemsByType(this, types, recurse, _findItemsList);
            return _findItemsList.ToArray();
        }
        static void RecurseFindItemsByType(Item current, Type[] types, bool recurse, List<Item> list)
        {
            if (current != null && current.Items.Count > 0)
            {
                var items = current.Items;
                for (var i = 0; i < items.Count; ++i)
                {
                    var item = items[i];
                    if (InTypeList(item, types))
                        list.Add(item);
                    if (recurse && item is Container)
                        RecurseFindItemsByType(item, types, recurse, list);
                }
            }
        }

        public Item FindItemByType(Type type, bool recurse = true) => RecurseFindItemByType(this, type, recurse);
        static Item RecurseFindItemByType(Item current, Type type, bool recurse)
        {
            if (current != null && current.Items.Count > 0)
            {
                var list = current.Items;
                for (var i = 0; i < list.Count; ++i)
                {
                    var item = list[i];
                    if (type.IsAssignableFrom(item.GetType())) return item;
                    else if (recurse && item is Container)
                    {
                        var check = RecurseFindItemByType(item, type, recurse);
                        if (check != null)
                            return check;
                    }
                }
            }
            return null;
        }

        public Item FindItemByType(Type[] types, bool recurse = true) => RecurseFindItemByType(this, types, recurse);
        static Item RecurseFindItemByType(Item current, Type[] types, bool recurse)
        {
            if (current != null && current.Items.Count > 0)
            {
                var list = current.Items;
                for (var i = 0; i < list.Count; ++i)
                {
                    var item = list[i];

                    if (InTypeList(item, types)) return item;
                    else if (recurse && item is Container)
                    {
                        var check = RecurseFindItemByType(item, types, recurse);
                        if (check != null)
                            return check;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Generic FindItem[s] by Type

        public List<T> FindItemsByType<T>() where T : Item => FindItemsByType<T>(true, null);
        public List<T> FindItemsByType<T>(bool recurse) where T : Item => FindItemsByType<T>(recurse, null);
        public List<T> FindItemsByType<T>(Predicate<T> predicate) where T : Item => FindItemsByType<T>(true, predicate);
        public List<T> FindItemsByType<T>(bool recurse, Predicate<T> predicate) where T : Item
        {
            if (_findItemsList.Count > 0)
                _findItemsList.Clear();
            var list = new List<T>();
            RecurseFindItemsByType<T>(this, recurse, list, predicate);
            return list;
        }

        static void RecurseFindItemsByType<T>(Item current, bool recurse, List<T> list, Predicate<T> predicate) where T : Item
        {
            if (current != null && current.Items.Count > 0)
            {
                var items = current.Items;
                for (var i = 0; i < items.Count; ++i)
                {
                    var item = items[i];
                    if (typeof(T).IsAssignableFrom(item.GetType()))
                    {
                        var typedItem = (T)item;
                        if (predicate == null || predicate(typedItem))
                            list.Add(typedItem);
                    }
                    if (recurse && item is Container)
                        RecurseFindItemsByType(item, recurse, list, predicate);
                }
            }
        }

        public T FindItemByType<T>() where T : Item => FindItemByType<T>(true);
        public T FindItemByType<T>(Predicate<T> predicate) where T : Item => FindItemByType(true, predicate);
        public T FindItemByType<T>(bool recurse) where T : Item => FindItemByType<T>(recurse, null);
        public T FindItemByType<T>(bool recurse, Predicate<T> predicate) where T : Item => RecurseFindItemByType(this, recurse, predicate);
        static T RecurseFindItemByType<T>(Item current, bool recurse, Predicate<T> predicate) where T : Item
        {
            if (current != null && current.Items.Count > 0)
            {
                var list = current.Items;
                for (var i = 0; i < list.Count; ++i)
                {
                    var item = list[i];
                    if (typeof(T).IsAssignableFrom(item.GetType()))
                    {
                        var typedItem = (T)item;
                        if (predicate == null || predicate(typedItem))
                            return typedItem;
                    }
                    else if (recurse && item is Container)
                    {
                        var check = RecurseFindItemByType(item, recurse, predicate);
                        if (check != null)
                            return check;
                    }
                }
            }
            return null;
        }

        #endregion

        static bool InTypeList(Item item, Type[] types)
        {
            var t = item.GetType();
            for (var i = 0; i < types.Length; ++i)
                if (types[i].IsAssignableFrom(t))
                    return true;
            return false;
        }

        static void SetSaveFlag(ref SaveFlag flags, SaveFlag toSet, bool setIf)
        {
            if (setIf)
                flags |= toSet;
        }

        static bool GetSaveFlag(SaveFlag flags, SaveFlag toGet) => (flags & toGet) != 0;

        [Flags]
        enum SaveFlag : byte
        {
            None = 0x00000000,
            MaxItems = 0x00000001,
            GumpID = 0x00000002,
            DropSound = 0x00000004,
            LiftOverride = 0x00000008
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(2); // version
            var flags = SaveFlag.None;
            SetSaveFlag(ref flags, SaveFlag.MaxItems, _MaxItems != -1);
            SetSaveFlag(ref flags, SaveFlag.GumpID, _GumpID != -1);
            SetSaveFlag(ref flags, SaveFlag.DropSound, _DropSound != -1);
            SetSaveFlag(ref flags, SaveFlag.LiftOverride, LiftOverride);
            w.Write((byte)flags);
            if (GetSaveFlag(flags, SaveFlag.MaxItems))
                w.WriteEncodedInt(_MaxItems);
            if (GetSaveFlag(flags, SaveFlag.GumpID))
                w.WriteEncodedInt(_GumpID);
            if (GetSaveFlag(flags, SaveFlag.DropSound))
                w.WriteEncodedInt(_DropSound);
        }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            var version = r.ReadInt();
            switch (version)
            {
                case 2:
                    {
                        var flags = (SaveFlag)r.ReadByte();
                        _MaxItems = GetSaveFlag(flags, SaveFlag.MaxItems) ? r.ReadEncodedInt() : -1;
                        _GumpID = GetSaveFlag(flags, SaveFlag.GumpID) ? r.ReadEncodedInt() : -1;
                        _DropSound = GetSaveFlag(flags, SaveFlag.DropSound) ? r.ReadEncodedInt() : -1;
                        LiftOverride = GetSaveFlag(flags, SaveFlag.LiftOverride);
                        break;
                    }
                case 1:
                    {
                        _MaxItems = r.ReadInt();
                        goto case 0;
                    }
                case 0:
                    {
                        if (version < 1)
                            _MaxItems = GlobalMaxItems;
                        _GumpID = r.ReadInt();
                        _DropSound = r.ReadInt();
                        if (_GumpID == DefaultGumpID)
                            _GumpID = -1;
                        if (_DropSound == DefaultDropSound)
                            _DropSound = -1;
                        if (_MaxItems == DefaultMaxItems)
                            _MaxItems = -1;
                        //_Bounds = new Rectangle2D(r.ReadPoint2D(), r.ReadPoint2D());
                        r.ReadPoint2D();
                        r.ReadPoint2D();
                        break;
                    }
            }
            UpdateContainerData();
        }

        public static int GlobalMaxItems { get; set; } = 125;
        public static int GlobalMaxWeight { get; set; } = 400;

        public Container(int itemID)
            : base(itemID)
        {
            _GumpID = -1;
            _DropSound = -1;
            _MaxItems = -1;
            UpdateContainerData();
        }

        public override int GetTotal(TotalType type)
        {
            switch (type)
            {
                case TotalType.Gold: return _TotalGold;
                case TotalType.Items: return _TotalItems;
                case TotalType.Weight: return _TotalWeight;
            }
            return base.GetTotal(type);
        }

        public override void UpdateTotal(Item sender, TotalType type, int delta)
        {
            if (sender != this && delta != 0 && !sender.IsVirtualItem)
            {
                switch (type)
                {
                    case TotalType.Gold:
                        _TotalGold += delta;
                        break;
                    case TotalType.Items:
                        _TotalItems += delta;
                        InvalidateProperties();
                        break;
                    case TotalType.Weight:
                        _TotalWeight += delta;
                        InvalidateProperties();
                        break;
                }
            }
            base.UpdateTotal(sender, type, delta);
        }

        public override void UpdateTotals()
        {
            _TotalGold = 0;
            _TotalItems = 0;
            _TotalWeight = 0;
            var items = _Items;
            if (items == null)
                return;
            for (var i = 0; i < items.Count; ++i)
            {
                var item = items[i];
                item.UpdateTotals();
                if (item.IsVirtualItem)
                    continue;
                _TotalGold += item.TotalGold;
                _TotalItems += item.TotalItems + 1;
                _TotalWeight += item.TotalWeight + item.PileWeight;
            }
        }

        public Container(Serial serial)
            : base(serial) { }

        public virtual bool OnStackAttempt(Mobile from, Item stack, Item dropped) => !CheckHold(from, dropped, true, false) ? false : stack.StackWith(from, dropped);

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (TryDropItem(from, dropped, true))
            {
                from.SendSound(GetDroppedSound(dropped), GetWorldLocation());
                return true;
            }
            else return false;
        }

        public virtual bool TryDropItem(Mobile from, Item dropped, bool sendFullMessage)
        {
            if (!CheckHold(from, dropped, sendFullMessage, true))
                return false;
            var list = Items;
            for (var i = 0; i < list.Count; ++i)
            {
                var item = list[i];
                if (!(item is Container) && item.StackWith(from, dropped, false))
                    return true;
            }
            DropItem(dropped);
            return true;
        }

        public virtual void Destroy()
        {
            var loc = GetWorldLocation();
            var map = Map;
            for (var i = Items.Count - 1; i >= 0; --i)
                if (i < Items.Count)
                {
                    Items[i].SetLastMoved();
                    Items[i].MoveToWorld(loc, map);
                }
            Delete();
        }

        public virtual void DropItem(Item dropped)
        {
            if (dropped == null)
                return;
            AddItem(dropped);
            var bounds = dropped.GetGraphicBounds();
            var ourBounds = Bounds;
            var x = bounds.Width >= ourBounds.Width ? (ourBounds.Width - bounds.Width) / 2 : Utility.Random(ourBounds.Width - bounds.Width);
            var y = bounds.Height >= ourBounds.Height ? (ourBounds.Height - bounds.Height) / 2 : Utility.Random(ourBounds.Height - bounds.Height);
            x += ourBounds.X;
            x -= bounds.X;
            y += ourBounds.Y;
            y -= bounds.Y;
            dropped.Location = new Point3D(x, y, 0);
        }

        public override void OnDoubleClickSecureTrade(Mobile from)
        {
            if (from.InRange(GetWorldLocation(), 2))
            {
                DisplayTo(from);
                var cont = GetSecureTradeCont();
                if (cont != null)
                {
                    var trade = cont.Trade;
                    if (trade != null && trade.From.Mobile == from) DisplayTo(trade.To.Mobile);
                    else if (trade != null && trade.To.Mobile == from) DisplayTo(trade.From.Mobile);
                }
            }
            else from.SendLocalizedMessage(500446); // That is too far away.
        }

        public virtual bool DisplaysContent { get { return true; } }

        public virtual bool CheckContentDisplay(Mobile from)
        {
            if (!DisplaysContent)
                return false;
            var root = RootParent;
            return root == null || root is Item || root == from || from.AccessLevel > AccessLevel.Player ? true : false;
        }

        public override void OnSingleClick(Mobile from)
        {
            base.OnSingleClick(from);
            if (CheckContentDisplay(from))
                LabelTo(from, "({0} items, {1} stones)", TotalItems, TotalWeight);
            //LabelTo(from, 1050044, String.Format("{0}\t{1}", TotalItems, TotalWeight)); // ~1_COUNT~ items, ~2_WEIGHT~ stones
        }

        public List<Mobile> Openers { get; set; }

        public virtual bool IsPublicContainer { get { return false; } }

        public override void OnDelete()
        {
            base.OnDelete();
            Openers = null;
        }

        public virtual void DisplayTo(Mobile to)
        {
            ProcessOpeners(to);
            var ns = to.NetState;
            if (ns == null)
                return;
            to.Send(ns.HighSeas ? (Packet)new ContainerDisplayHS(this) : new ContainerDisplay(this));
            to.Send(ns.ContainerGridLines ? (Packet)new ContainerContent6017(to, this) : new ContainerContent(to, this));
            if (ObjectPropertyList.Enabled)
            {
                var items = this.Items;
                for (var i = 0; i < items.Count; ++i)
                    to.Send(items[i].OPLPacket);
            }
        }

        public void ProcessOpeners(Mobile opener)
        {
            if (!IsPublicContainer)
            {
                var contains = false;
                if (Openers != null)
                {
                    var worldLoc = GetWorldLocation();
                    var map = Map;
                    for (var i = 0; i < Openers.Count; ++i)
                    {
                        var mob = Openers[i];
                        if (mob == opener)
                            contains = true;
                        else
                        {
                            var range = GetUpdateRange(mob);
                            if (mob.Map != map || !mob.InRange(worldLoc, range))
                                Openers.RemoveAt(i--);
                        }
                    }
                }
                if (!contains)
                {
                    if (Openers == null)
                        Openers = new List<Mobile>();
                    Openers.Add(opener);
                }
                else if (Openers != null && Openers.Count == 0)
                    Openers = null;
            }
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (DisplaysContent )//CheckContentDisplay(from))
            {
                if (Core.ML)
                {
                    if (ParentsContain<BankBox>())  //Root Parent is the Mobile.  Parent could be another containter.
                        list.Add(1073841, "{0}\t{1}\t{2}", TotalItems, MaxItems, TotalWeight); // Contents: ~1_COUNT~/~2_MAXCOUNT~ items, ~3_WEIGHT~ stones
                    else
                        list.Add(1072241, "{0}\t{1}\t{2}\t{3}", TotalItems, MaxItems, TotalWeight, MaxWeight); // Contents: ~1_COUNT~/~2_MAXCOUNT~ items, ~3_WEIGHT~/~4_MAXWEIGHT~ stones
                    //TODO: Where do the other clilocs come into play? 1073839 & 1073840?
                }
                else list.Add(1050044, "{0}\t{1}", TotalItems, TotalWeight); // ~1_COUNT~ items, ~2_WEIGHT~ stones
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel > AccessLevel.Player || from.InRange(this.GetWorldLocation(), 2)) DisplayTo(from);
            else from.SendLocalizedMessage(500446); // That is too far away.
        }
    }

    public class ContainerData
    {
        static ContainerData()
        {
            _Table = new Dictionary<int, ContainerData>();
            var path = Path.Combine(Core.BaseDirectory, "Data/containers.cfg");
            if (!File.Exists(path))
            {
                Default = new ContainerData(0x3C, new Rectangle2D(44, 65, 142, 94), 0x48);
                return;
            }
            using (var r = new StreamReader(path))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    try
                    {
                        var split = line.Split('\t');
                        if (split.Length >= 3)
                        {
                            var gumpID = Utility.ToInt32(split[0]);
                            var aRect = split[1].Split(' ');
                            if (aRect.Length < 4)
                                continue;
                            var x = Utility.ToInt32(aRect[0]);
                            var y = Utility.ToInt32(aRect[1]);
                            var width = Utility.ToInt32(aRect[2]);
                            var height = Utility.ToInt32(aRect[3]);
                            var bounds = new Rectangle2D(x, y, width, height);
                            var dropSound = Utility.ToInt32(split[2]);
                            var data = new ContainerData(gumpID, bounds, dropSound);
                            if (Default == null)
                                Default = data;
                            if (split.Length >= 4)
                            {
                                var aIDs = split[3].Split(',');
                                for (var i = 0; i < aIDs.Length; i++)
                                {
                                    var id = Utility.ToInt32(aIDs[i]);
                                    if (_Table.ContainsKey(id))
                                        Console.WriteLine(@"Warning: double ItemID entry in Data\containers.cfg");
                                    else
                                        _Table[id] = data;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            if (Default == null)
                Default = new ContainerData(0x3C, new Rectangle2D(44, 65, 142, 94), 0x48);
        }

        static Dictionary<int, ContainerData> _Table;

        public static ContainerData Default { get; set; }

        public static ContainerData GetData(int itemID)
        {
            _Table.TryGetValue(itemID, out ContainerData data);
            return data != null ? data : Default;
        }

        public int GumpID { get; }
        public Rectangle2D Bounds { get; }
        public int DropSound { get; }

        public ContainerData(int gumpID, Rectangle2D bounds, int dropSound)
        {
            GumpID = gumpID;
            Bounds = bounds;
            DropSound = dropSound;
        }
    }
}