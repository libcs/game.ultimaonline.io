using System;
using System.Xml;
using UltimaOnline;

namespace UltimaOnline.Regions
{
	public class TownRegion : GuardedRegion
	{
		public TownRegion( XmlElement xml, Map map, Region parent ) : base( xml, map, parent )
		{
		}
	}
}