using System;

namespace UltimaOnline.Items
{
	[FlipableAttribute( 0xFAF, 0xFB0 )]
	[UltimaOnline.Engines.Craft.Anvil]
	public class Anvil : Item
	{
		[Constructable]
		public Anvil() : base( 0xFAF )
		{
			Movable = false;
		}

		public Anvil( Serial serial ) : base( serial )
		{
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 0 ); // version
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadInt();
		}
	}

	[UltimaOnline.Engines.Craft.Forge]
	public class Forge : Item
	{
		[Constructable]
		public Forge() : base( 0xFB1 )
		{
			Movable = false;
		}

		public Forge( Serial serial ) : base( serial )
		{
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 0 ); // version
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadInt();
		}
	}
}