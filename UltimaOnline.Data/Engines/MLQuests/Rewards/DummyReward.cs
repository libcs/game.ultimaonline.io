using System;
using UltimaOnline;
using UltimaOnline.Mobiles;
using UltimaOnline.Gumps;
using System.Collections.Generic;

namespace UltimaOnline.Engines.MLQuests.Rewards
{
    public class DummyReward : BaseReward
    {
        public DummyReward(TextDefinition name)
            : base(name)
        {
        }

        protected override int LabelHeight { get { return 180; } }

        public override void AddRewardItems(PlayerMobile pm, List<Item> rewards)
        {
        }
    }
}
