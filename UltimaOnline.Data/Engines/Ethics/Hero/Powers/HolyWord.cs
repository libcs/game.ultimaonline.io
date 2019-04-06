using System;
using System.Collections.Generic;
using System.Text;

namespace UltimaOnline.Ethics.Hero
{
    public sealed class HolyWord : Power
    {
        public HolyWord()
        {
            m_Definition = new PowerDefinition(
                    100,
                    "Holy Word",
                    "Erstok Oostrac",
                    ""
                );
        }

        public override void BeginInvoke(Player from)
        {
        }
    }
}
