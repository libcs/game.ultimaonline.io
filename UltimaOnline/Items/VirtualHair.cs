using System;
using UltimaOnline;
using UltimaOnline.Network;

namespace UltimaOnline
{
	public abstract class BaseHairInfo
	{
		private int m_ItemID;
		private int m_Hue;

		[CommandProperty( AccessLevel.GameMaster )]
		public int ItemID { get { return m_ItemID; } set { m_ItemID = value; } }

		[CommandProperty( AccessLevel.GameMaster )]
		public int Hue { get { return m_Hue; } set { m_Hue = value; } }

		protected BaseHairInfo( int itemid )
			: this( itemid, 0 )
		{
		}

		protected BaseHairInfo( int itemid, int hue )
		{
			m_ItemID = itemid;
			m_Hue = hue;
		}

		protected BaseHairInfo( GenericReader reader )
		{
			int version = reader.ReadInt();

			switch( version )
			{
				case 0:
				{
					m_ItemID = reader.ReadInt();
					m_Hue = reader.ReadInt();
					break;
				}
			}
		}

		public virtual void Serialize( GenericWriter writer )
		{
			writer.Write( (int)0 ); //version
			writer.Write( (int)m_ItemID );
			writer.Write( (int)m_Hue );
		}
	}

	public class HairInfo : BaseHairInfo
	{
		public HairInfo( int itemid )
			: base( itemid, 0 )
		{
		}

		public HairInfo( int itemid, int hue )
			: base( itemid, hue )
		{
		}

		public HairInfo( GenericReader reader )
			: base( reader )
		{
		}

		public static int FakeSerial( Mobile parent )
		{
			return (0x7FFFFFFF - 0x400 - (parent.Serial * 4));
		}
	}

	public class FacialHairInfo : BaseHairInfo
	{
		public FacialHairInfo( int itemid )
			: base( itemid, 0 )
		{
		}

		public FacialHairInfo( int itemid, int hue )
			: base( itemid, hue )
		{
		}

		public FacialHairInfo( GenericReader reader )
			: base( reader )
		{
		}

		public static int FakeSerial( Mobile parent )
		{
			return (0x7FFFFFFF - 0x400 - 1 - (parent.Serial * 4));
		}
	}

	public sealed class HairEquipUpdate : Packet
	{
		public HairEquipUpdate( Mobile parent )
			: base( 0x2E, 15 )
		{
			int hue = parent.HairHue;

			if( parent.SolidHueOverride >= 0 )
				hue = parent.SolidHueOverride;

			int hairSerial = HairInfo.FakeSerial( parent );

			Stream.Write( (int)hairSerial );
			Stream.Write( (short)parent.HairItemID );
			Stream.Write( (byte)0 );
			Stream.Write( (byte)Layer.Hair );
			Stream.Write( (int)parent.Serial );
			Stream.Write( (short)hue );
		}
	}

	public sealed class FacialHairEquipUpdate : Packet
	{
		public FacialHairEquipUpdate( Mobile parent )
			: base( 0x2E, 15 )
		{
			int hue = parent.FacialHairHue;

			if( parent.SolidHueOverride >= 0 )
				hue = parent.SolidHueOverride;

			int hairSerial = FacialHairInfo.FakeSerial( parent );

			Stream.Write( (int)hairSerial );
			Stream.Write( (short)parent.FacialHairItemID );
			Stream.Write( (byte)0 );
			Stream.Write( (byte)Layer.FacialHair );
			Stream.Write( (int)parent.Serial );
			Stream.Write( (short)hue );
		}
	}

	public sealed class RemoveHair : Packet
	{
		public RemoveHair( Mobile parent )
			: base( 0x1D, 5 )
		{
			Stream.Write( (int)HairInfo.FakeSerial( parent ) );
		}
	}

	public sealed class RemoveFacialHair : Packet
	{
		public RemoveFacialHair( Mobile parent )
			: base( 0x1D, 5 )
		{
			Stream.Write( (int)FacialHairInfo.FakeSerial( parent ) );
		}
	}
}