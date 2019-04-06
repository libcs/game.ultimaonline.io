using System;
using UltimaOnline.Network;

namespace UltimaOnline.Prompts
{
    public abstract class Prompt
    {
        private int m_Serial;
        private static int m_Serials;

        public int Serial
        {
            get
            {
                return m_Serial;
            }
        }

        protected Prompt()
        {
            do
            {
                m_Serial = ++m_Serials;
            } while (m_Serial == 0);
        }

        public virtual void OnCancel(Mobile from)
        {
        }

        public virtual void OnResponse(Mobile from, string text)
        {
        }
    }
}