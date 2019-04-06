using System;
using UltimaOnline;

namespace UltimaOnline.Network
{
    public class DisplayHelpTopic : Packet
    {
        public DisplayHelpTopic(int topicID, bool display) : base(0xBF)
        {
            EnsureCapacity(11);

            Stream.Write((short)0x17);
            Stream.Write((byte)1);
            Stream.Write((int)topicID);
            Stream.Write((bool)display);
        }
    }
}