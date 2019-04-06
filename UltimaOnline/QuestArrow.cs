using UltimaOnline.Network;

namespace UltimaOnline
{
    public class QuestArrow
    {
        public QuestArrow(Mobile m, Mobile t)
        {
            Running = true;
            Mobile = m;
            Target = t;
        }
        public QuestArrow(Mobile m, Mobile t, int x, int y) : this(m, t) => Update(x, y);

        public Mobile Mobile { get; }
        public Mobile Target { get; }
        public bool Running { get; private set; }
        public void Update() => Update(Target.X, Target.Y);
        public void Update(int x, int y)
        {
            if (!Running)
                return;
            var ns = Mobile.NetState;
            if (ns == null)
                return;
            ns.Send(ns.HighSeas ? (Packet)new SetArrowHS(x, y, Target.Serial) : new SetArrow(x, y));
        }

        public void Stop() => Stop(Target.X, Target.Y);
        public void Stop(int x, int y)
        {
            if (!Running)
                return;
            Mobile.ClearQuestArrow();
            var ns = Mobile.NetState;
            if (ns != null)
                ns.Send(ns.HighSeas ? (Packet)new CancelArrowHS(x, y, Target.Serial) : new CancelArrow());
            Running = false;
            OnStop();
        }

        public virtual void OnStop() { }
        public virtual void OnClick(bool rightClick) { }
    }
}