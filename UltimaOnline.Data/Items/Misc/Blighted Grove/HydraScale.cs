using System;
using UltimaOnline;

namespace UltimaOnline.Items
{
    public class HydraScale : Item
    {
        public override int LabelNumber { get { return 1074760; } } // A hydra scale.

        [Constructable]
        public HydraScale() : base(0x26B4)
        {
            LootType = LootType.Blessed;
            Hue = 0xC2; // TODO check
        }

        public HydraScale(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int)0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }
}

