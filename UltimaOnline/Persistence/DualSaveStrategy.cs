using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

using UltimaOnline;
using UltimaOnline.Guilds;

namespace UltimaOnline
{
    public sealed class DualSaveStrategy : StandardSaveStrategy
    {
        public override string Name
        {
            get { return "Dual"; }
        }

        public DualSaveStrategy()
        {
        }

        public override void Save(SaveMetrics metrics, bool permitBackgroundWrite)
        {
            this.PermitBackgroundWrite = permitBackgroundWrite;

            Thread saveThread = new Thread(delegate ()
            {
                SaveItems(metrics);
            });

            saveThread.Name = "Item Save Subset";
            saveThread.Start();

            SaveMobiles(metrics);
            SaveGuilds(metrics);

            saveThread.Join();

            if (permitBackgroundWrite && UseSequentialWriters)  //If we're permitted to write in the background, but we don't anyways, then notify.
                World.NotifyDiskWriteComplete();
        }
    }
}