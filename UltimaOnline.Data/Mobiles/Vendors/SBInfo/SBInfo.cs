using System;
using System.Collections.Generic;
using UltimaOnline.Items;

namespace UltimaOnline.Mobiles
{
	public abstract class SBInfo
	{
		public static readonly List<SBInfo> Empty = new List<SBInfo>();

		public SBInfo()
		{
		}

		public abstract IShopSellInfo SellInfo { get; }
		public abstract List<GenericBuyInfo> BuyInfo { get; }
	}
}
