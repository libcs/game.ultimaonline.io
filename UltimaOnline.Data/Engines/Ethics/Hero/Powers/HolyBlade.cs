using System;
using System.Collections.Generic;
using System.Text;

namespace UltimaOnline.Ethics.Hero
{
    public sealed class HolyBlade : Power
    {
        public HolyBlade()
        {
            m_Definition = new PowerDefinition(
                    10,
                    "Holy Blade",
                    "Erstok Reyam",
                    ""
                );
        }

        public override void BeginInvoke(Player from)
        {
        }
    }
}
