using System;
using System.Collections;
using UltimaOnline;
using UltimaOnline.Items;
using UltimaOnline.Mobiles;
using UltimaOnline.Network;

namespace UltimaOnline.Spells
{
	public class NinjaMove : SpecialMove
	{
		public override SkillName MoveSkill{ get{ return SkillName.Ninjitsu; } }

		public override void CheckGain( Mobile m )
		{
			m.CheckSkill( MoveSkill, RequiredSkill - 12.5, RequiredSkill + 37.5 );	//Per five on friday 02/16/07
		}
	}
}