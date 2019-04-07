using System;
using System.Collections;
using System.Threading;
using UltimaOnline.Accounting;
using UltimaOnline.Engines.ConPVP;
using UltimaOnline.Factions;
using UltimaOnline.Mobiles;
using UltimaOnline.Network;

namespace UltimaOnline.Engines.Reports
{
    public class Reports
    {
        public static bool Enabled = false;

        public static void Initialize()
        {
            if (!Enabled)
                return;
            _StatsHistory = new SnapshotHistory();
            _StatsHistory.Load();
            StaffHistory = new StaffHistory();
            StaffHistory.Load();
            var now = DateTime.UtcNow;
            var date = now.Date;
            var timeOfDay = now.TimeOfDay;
            _GenerateTime = date + TimeSpan.FromHours(Math.Ceiling(timeOfDay.TotalHours));
            Timer.DelayCall(TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(0.5), new TimerCallback(CheckRegenerate));
        }

        static DateTime _GenerateTime;

        public static void CheckRegenerate()
        {
            if (DateTime.UtcNow < _GenerateTime)
                return;
            Generate();
            _GenerateTime += TimeSpan.FromHours(1.0);
        }

        static SnapshotHistory _StatsHistory;

        public static StaffHistory StaffHistory { get; private set; }

        public static void Generate()
        {
            var ss = new Snapshot
            {
                TimeStamp = DateTime.UtcNow
            };
            FillSnapshot(ss);
            _StatsHistory.Snapshots.Add(ss);
            StaffHistory.QueueStats.Add(new QueueStatus(Help.PageQueue.List.Count));
            ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateOutput), ss);
        }

        static void UpdateOutput(object state)
        {
            //_StatsHistory.Save();
            //StaffHistory.Save();
            //var renderer = new HtmlRenderer("stats", (Snapshot)state, _StatsHistory);
            //renderer.Render();
            //renderer.Upload();

            //renderer = new HtmlRenderer("staff", StaffHistory);
            //renderer.Render();
            //renderer.Upload();
        }

        public static void FillSnapshot(Snapshot ss)
        {
            ss.Children.Add(CompileGeneralStats());
            ss.Children.Add(CompilePCByDL());
            ss.Children.Add(CompileTop15());
            ss.Children.Add(CompileDislikedArenas());
            ss.Children.Add(CompileStatChart());
            var obs = CompileSkillReports();
            for (var i = 0; i < obs.Length; ++i)
                ss.Children.Add(obs[i]);
            obs = CompileFactionReports();
            for (var i = 0; i < obs.Length; ++i)
                ss.Children.Add(obs[i]);
        }

        public static Report CompileGeneralStats()
        {
            var report = new Report("General Stats", "200");
            report.Columns.Add("50%", "left");
            report.Columns.Add("50%", "left");
            int npcs = 0, players = 0;
            foreach (var mob in World.Mobiles.Values)
            {
                if (mob.Player) ++players;
                else ++npcs;
            }
            report.Items.Add("NPCs", npcs, "N0");
            report.Items.Add("Players", players, "N0");
            report.Items.Add("Clients", NetState.Instances.Count, "N0");
            report.Items.Add("Accounts", Accounts.Count, "N0");
            report.Items.Add("Items", World.Items.Count, "N0");
            return report;
        }

        static Chart CompilePCByDL()
        {
            var chart = new BarGraph("Player Count By Dueling Level", "graphs_pc_by_dl", 5, "Dueling Level", "Players", BarGraphRenderMode.Bars);
            var lastLevel = -1;
            ChartItem lastItem = null;
            var ladder = Ladder.Instance;
            if (ladder != null)
            {
                var entries = ladder.ToArrayList();
                for (var i = entries.Count - 1; i >= 0; --i)
                {
                    var entry = (LadderEntry)entries[i];
                    var level = Ladder.GetLevel(entry.Experience);
                    if (lastItem == null || level != lastLevel)
                    {
                        chart.Items.Add(lastItem = new ChartItem(level.ToString(), 1));
                        lastLevel = level;
                    }
                    else lastItem.Value++;
                }
            }
            return chart;
        }

        static Report CompileTop15()
        {
            var report = new Report("Top 15 Duelists", "80%");
            report.Columns.Add("6%", "center", "Rank");
            report.Columns.Add("6%", "center", "Level");
            report.Columns.Add("6%", "center", "Guild");
            report.Columns.Add("70%", "left", "Name");
            report.Columns.Add("6%", "center", "Wins");
            report.Columns.Add("6%", "center", "Losses");
            var ladder = Ladder.Instance;
            if (ladder != null)
            {
                var entries = ladder.ToArrayList();
                for (var i = 0; i < entries.Count && i < 15; ++i)
                {
                    var entry = (LadderEntry)entries[i];
                    var level = Ladder.GetLevel(entry.Experience);
                    var guild = string.Empty;
                    if (entry.Mobile.Guild != null)
                        guild = entry.Mobile.Guild.Abbreviation;
                    var item = new ReportItem();
                    item.Values.Add(LadderGump.Rank(entry.Index + 1));
                    item.Values.Add(level.ToString(), "N0");
                    item.Values.Add(guild);
                    item.Values.Add(entry.Mobile.Name);
                    item.Values.Add(entry.Wins.ToString(), "N0");
                    item.Values.Add(entry.Losses.ToString(), "N0");
                    report.Items.Add(item);
                }
            }
            return report;
        }

        static Chart CompileDislikedArenas()
        {
            var chart = new PieChart("Most Disliked Arenas", "graphs_arenas_disliked", false);
            var prefs = Preferences.Instance;
            if (prefs != null)
            {
                var arenas = Arena.Arenas;
                for (var i = 0; i < arenas.Count; ++i)
                {
                    var arena = arenas[i];
                    var name = arena.Name;
                    if (name != null)
                        chart.Items.Add(name, 0);
                }
                var entries = prefs.Entries;
                for (var i = 0; i < entries.Count; ++i)
                {
                    var entry = (PreferencesEntry)entries[i];
                    var list = entry.Disliked;
                    for (var j = 0; j < list.Count; ++j)
                    {
                        var disliked = (string)list[j];
                        for (var k = 0; k < chart.Items.Count; ++k)
                        {
                            var item = chart.Items[k];
                            if (item.Name == disliked)
                            {
                                ++item.Value;
                                break;
                            }
                        }
                    }
                }
            }
            return chart;
        }

        public static Chart CompileStatChart()
        {
            var chart = new PieChart("Stat Distribution", "graphs_strdexint_distrib", true);
            var strItem = new ChartItem("Strength", 0);
            var dexItem = new ChartItem("Dexterity", 0);
            var intItem = new ChartItem("Intelligence", 0);
            foreach (var mob in World.Mobiles.Values)
                if (mob.RawStatTotal == mob.StatCap && mob is PlayerMobile)
                {
                    strItem.Value += mob.RawStr;
                    dexItem.Value += mob.RawDex;
                    intItem.Value += mob.RawInt;
                }
            chart.Items.Add(strItem);
            chart.Items.Add(dexItem);
            chart.Items.Add(intItem);
            return chart;
        }

        public class SkillDistribution : IComparable
        {
            public SkillInfo _Skill;
            public int _NumberOfGMs;

            public SkillDistribution(SkillInfo skill) => _Skill = skill;
            public int CompareTo(object obj) => ((SkillDistribution)obj)._NumberOfGMs - _NumberOfGMs;
        }

        public static SkillDistribution[] GetSkillDistribution()
        {
            var skip = Core.ML ? 0 : Core.SE ? 1 : Core.AOS ? 3 : 6;
            var distribs = new SkillDistribution[SkillInfo.Table.Length - skip];
            for (var i = 0; i < distribs.Length; ++i)
                distribs[i] = new SkillDistribution(SkillInfo.Table[i]);
            foreach (var mob in World.Mobiles.Values)
                if (mob.SkillsTotal >= 1500 && mob.SkillsTotal <= 7200 && mob is PlayerMobile)
                {
                    var skills = mob.Skills;
                    for (var i = 0; i < skills.Length - skip; ++i)
                    {
                        var skill = skills[i];
                        if (skill.BaseFixedPoint >= 1000)
                            distribs[i]._NumberOfGMs++;
                    }
                }
            return distribs;
        }

        public static PersistableObject[] CompileSkillReports()
        {
            var distribs = GetSkillDistribution();
            Array.Sort(distribs);
            return new PersistableObject[] { CompileSkillChart(distribs), CompileSkillReport(distribs) };
        }

        public static Report CompileSkillReport(SkillDistribution[] distribs)
        {
            var report = new Report("Skill Report", "300");
            report.Columns.Add("70%", "left", "Name");
            report.Columns.Add("30%", "center", "GMs");
            for (var i = 0; i < distribs.Length; ++i)
                report.Items.Add(distribs[i]._Skill.Name, distribs[i]._NumberOfGMs, "N0");
            return report;
        }

        public static Chart CompileSkillChart(SkillDistribution[] distribs)
        {
            var chart = new PieChart("GM Skill Distribution", "graphs_skill_distrib", true);
            for (var i = 0; i < 12; ++i)
                chart.Items.Add(distribs[i]._Skill.Name, distribs[i]._NumberOfGMs);
            var rem = 0;
            for (var i = 12; i < distribs.Length; ++i)
                rem += distribs[i]._NumberOfGMs;
            chart.Items.Add("Other", rem);
            return chart;
        }

        public static PersistableObject[] CompileFactionReports() => new PersistableObject[] { CompileFactionMembershipChart(), CompileFactionReport(), CompileFactionTownReport(), CompileSigilReport(), CompileFactionLeaderboard() };

        public static Chart CompileFactionMembershipChart()
        {
            var chart = new PieChart("Faction Membership", "graphs_faction_membership", true);
            var factions = Faction.Factions;
            for (var i = 0; i < factions.Count; ++i)
                chart.Items.Add(factions[i].Definition.FriendlyName, factions[i].Members.Count);
            return chart;
        }

        public static Report CompileFactionLeaderboard()
        {
            var report = new Report("Faction Leaderboard", "60%");
            report.Columns.Add("28%", "center", "Name");
            report.Columns.Add("28%", "center", "Faction");
            report.Columns.Add("28%", "center", "Office");
            report.Columns.Add("16%", "center", "Kill Points");
            var list = new ArrayList();
            var factions = Faction.Factions;
            for (var i = 0; i < factions.Count; ++i)
            {
                var faction = factions[i];
                list.AddRange(faction.Members);
            }
            list.Sort();
            list.Reverse();
            for (var i = 0; i < list.Count && i < 15; ++i)
            {
                var pl = (PlayerState)list[i];
                string office;
                if (pl.Faction.Commander == pl.Mobile) office = "Commanding Lord";
                else if (pl.Finance != null) office = $"{pl.Finance.Definition.FriendlyName} Finance Minister";
                else if (pl.Sheriff != null) office = $"{pl.Sheriff.Definition.FriendlyName} Sheriff";
                else office = string.Empty;
                var item = new ReportItem();
                item.Values.Add(pl.Mobile.Name);
                item.Values.Add(pl.Faction.Definition.FriendlyName);
                item.Values.Add(office);
                item.Values.Add(pl.KillPoints.ToString(), "N0");
                report.Items.Add(item);
            }
            return report;
        }

        public static Report CompileFactionReport()
        {
            var report = new Report("Faction Statistics", "80%");
            report.Columns.Add("20%", "center", "Name");
            report.Columns.Add("20%", "center", "Commander");
            report.Columns.Add("15%", "center", "Members");
            report.Columns.Add("15%", "center", "Merchants");
            report.Columns.Add("15%", "center", "Kill Points");
            report.Columns.Add("15%", "center", "Silver");
            var factions = Faction.Factions;
            for (var i = 0; i < factions.Count; ++i)
            {
                var faction = factions[i];
                var members = faction.Members;
                var totalKillPoints = 0;
                var totalMerchants = 0;
                for (var j = 0; j < members.Count; ++j)
                {
                    totalKillPoints += members[j].KillPoints;
                    if (members[j].MerchantTitle != MerchantTitle.None)
                        ++totalMerchants;
                }
                var item = new ReportItem();
                item.Values.Add(faction.Definition.FriendlyName);
                item.Values.Add(faction.Commander == null ? string.Empty : faction.Commander.Name);
                item.Values.Add(faction.Members.Count.ToString(), "N0");
                item.Values.Add(totalMerchants.ToString(), "N0");
                item.Values.Add(totalKillPoints.ToString(), "N0");
                item.Values.Add(faction.Silver.ToString(), "N0");
                report.Items.Add(item);
            }
            return report;
        }

        public static Report CompileSigilReport()
        {
            var report = new Report("Faction Town Sigils", "50%");
            report.Columns.Add("35%", "center", "Town");
            report.Columns.Add("35%", "center", "Controller");
            report.Columns.Add("30%", "center", "Capturable");
            var sigils = Sigil.Sigils;
            for (var i = 0; i < sigils.Count; ++i)
            {
                var sigil = sigils[i];
                var controller = "Unknown";
                if (sigil.RootParent is Mobile mob)
                {
                    var faction = Faction.Find(mob);
                    if (faction != null)
                        controller = faction.Definition.FriendlyName;
                }
                else if (sigil.LastMonolith != null && sigil.LastMonolith.Faction != null)
                    controller = sigil.LastMonolith.Faction.Definition.FriendlyName;
                var item = new ReportItem();
                item.Values.Add(sigil.Town == null ? string.Empty : sigil.Town.Definition.FriendlyName);
                item.Values.Add(controller);
                item.Values.Add(sigil.IsPurifying ? "No" : "Yes");
                report.Items.Add(item);
            }
            return report;
        }

        public static Report CompileFactionTownReport()
        {
            var report = new Report("Faction Towns", "80%");
            report.Columns.Add("20%", "center", "Name");
            report.Columns.Add("20%", "center", "Owner");
            report.Columns.Add("17%", "center", "Sheriff");
            report.Columns.Add("17%", "center", "Finance Minister");
            report.Columns.Add("13%", "center", "Silver");
            report.Columns.Add("13%", "center", "Prices");
            var towns = Town.Towns;
            for (var i = 0; i < towns.Count; ++i)
            {
                var town = towns[i];
                var prices = "Normal";
                if (town.Tax < 0) prices = "{town.Tax}%";
                else if (town.Tax > 0) prices = $"+{town.Tax}%";
                var item = new ReportItem();
                item.Values.Add(town.Definition.FriendlyName);
                item.Values.Add(town.Owner == null ? "Neutral" : town.Owner.Definition.FriendlyName);
                item.Values.Add(town.Sheriff == null ? string.Empty : town.Sheriff.Name);
                item.Values.Add(town.Finance == null ? string.Empty : town.Finance.Name);
                item.Values.Add(town.Silver.ToString(), "N0");
                item.Values.Add(prices);
                report.Items.Add(item);
            }
            return report;
        }
    }
}
