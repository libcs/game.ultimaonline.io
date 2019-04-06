using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UltimaOnline
{
    public class AggressorInfo
    {
        static Queue<AggressorInfo> _pool = new Queue<AggressorInfo>();
        Mobile _attacker, _defender;
        DateTime _lastCombatTime;
        bool _canReportMurder;
        bool _reported;
        bool _criminalAggression;
        bool _queued;

        AggressorInfo(Mobile attacker, Mobile defender, bool criminal)
        {
            _attacker = attacker;
            _defender = defender;
            _canReportMurder = criminal;
            _criminalAggression = criminal;
            Refresh();
        }

        public static AggressorInfo Create(Mobile attacker, Mobile defender, bool criminal)
        {
            AggressorInfo info;
            if (_pool.Count > 0)
            {
                info = _pool.Dequeue();
                info._attacker = attacker;
                info._defender = defender;
                info._canReportMurder = criminal;
                info._criminalAggression = criminal;
                info._queued = false;
                info.Refresh();
            }
            else
                info = new AggressorInfo(attacker, defender, criminal);
            return info;
        }

        public void Free()
        {
            if (_queued)
                return;
            _queued = true;
            _pool.Enqueue(this);
        }

        public static TimeSpan ExpireDelay { get; set; } = TimeSpan.FromMinutes(2.0);

        public static void DumpAccess()
        {
            using (var op = new StreamWriter("warnings.log", true))
            {
                op.WriteLine("Warning: Access to queued AggressorInfo:");
                op.WriteLine(new StackTrace());
                op.WriteLine();
                op.WriteLine();
            }
        }

        public bool Expired
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _attacker.Deleted || _defender.Deleted || DateTime.UtcNow >= (_lastCombatTime + ExpireDelay);
            }
        }

        public bool CriminalAggression
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _criminalAggression;
            }
            set
            {
                if (_queued)
                    DumpAccess();
                _criminalAggression = value;
            }
        }

        public Mobile Attacker
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _attacker;
            }
        }

        public Mobile Defender
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _defender;
            }
        }

        public DateTime LastCombatTime
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _lastCombatTime;
            }
        }

        public bool Reported
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _reported;
            }
            set
            {
                if (_queued)
                    DumpAccess();
                _reported = value;
            }
        }

        public bool CanReportMurder
        {
            get
            {
                if (_queued)
                    DumpAccess();
                return _canReportMurder;
            }
            set
            {
                if (_queued)
                    DumpAccess();
                _canReportMurder = value;
            }
        }

        public void Refresh()
        {
            if (_queued)
                DumpAccess();
            _lastCombatTime = DateTime.UtcNow;
            _reported = false;
        }
    }
}