using System;
using UltimaOnline;
using UltimaOnline.Items;

namespace UltimaOnline.Items
{
	public class ManaVampireScroll : SpellScroll
	{
		[Constructable]
		public ManaVampireScroll() : this( 1 )
		{
		}

		[Constructable]
		public ManaVampireScroll( int amount ) : base( 52, 0x1F61, amount )
		{
		}

		public ManaVampireScroll( Serial serial ) : base( serial )
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