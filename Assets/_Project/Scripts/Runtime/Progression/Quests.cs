using System;
using UnityEngine;

namespace IdleArcade
{
    public enum QuestType { Serve, Sell, Build, Upgrade, StationLevel, StationStar, Vip, Wash, Earn, MarketLevel }

    public class QuestDef
    {
        public QuestType type;
        public int target = 1;
        public ItemKind item;
        public string pad;
        public UpgradeId upgrade;
        public StationKind station;
        public string textKey;          // optional explicit text; otherwise built from the type
        public int coins, medals, xp;
    }

    /// <summary>
    /// Quest chain in three chapters (each closes with a chest), then endless generated quests.
    /// One active quest at a time, shown on the HUD card; rewards are claimed by tapping it.
    /// Lessons from the genre: every quest can show the way (tap = arrow and path to the place),
    /// and rewards are worth more than a minute of income at that point of the game.
    /// </summary>
    public static class Quests
    {
        public static event Action Changed;
        public static event Action<int> ChapterDone;

        static QuestDef Q(QuestType t, int target, int coins, int medals, int xp) => new QuestDef { type = t, target = target, coins = coins, medals = medals, xp = xp };
        static QuestDef Sell(ItemKind k, int n, int coins, int medals, int xp) { var q = Q(QuestType.Sell, n, coins, medals, xp); q.item = k; return q; }
        static QuestDef Build(string pad, string key, int coins, int medals, int xp) { var q = Q(QuestType.Build, 1, coins, medals, xp); q.pad = pad; q.textKey = key; return q; }
        static QuestDef Up(UpgradeId id, int lv, int coins, int medals, int xp) { var q = Q(QuestType.Upgrade, lv, coins, medals, xp); q.upgrade = id; return q; }
        static QuestDef StLv(StationKind k, int lv, int coins, int medals, int xp) { var q = Q(QuestType.StationLevel, lv, coins, medals, xp); q.station = k; return q; }
        static QuestDef StStar(StationKind k, int n, int coins, int medals, int xp) { var q = Q(QuestType.StationStar, n, coins, medals, xp); q.station = k; return q; }

        // chapter boundaries: quests [0..8) are chapter 1, [8..18) chapter 2, [18..30) chapter 3
        public static readonly int[] ChapterEnd = { 8, 18, 30 };
        static readonly int[] ChestCoins = { 80, 400, 1200 };
        static readonly int[] ChestMedals = { 2, 3, 5 };

        public static readonly QuestDef[] Chain =
        {
            // chapter 1: opening day (small rewards: early buildings cost 10-90)
            Q(QuestType.Serve, 5, 15, 0, 10),
            Build("tree2", "q.tree2", 20, 0, 10),
            Sell(ItemKind.Apple, 20, 25, 1, 15),
            Build("hire_cashier", "q.hire_cashier", 35, 0, 15),
            Build("oven", "q.oven", 35, 0, 15),
            Sell(ItemKind.Bread, 10, 40, 1, 15),
            Q(QuestType.Earn, 300, 50, 0, 20),
            Build("upgrades", "q.upgrades", 45, 1, 20),
            // chapter 2: a busy market
            Up(UpgradeId.Capacity, 1, 80, 1, 30),
            StLv(StationKind.Apples, 2, 90, 1, 30),
            Q(QuestType.Vip, 2, 100, 1, 30),
            Build("hire_picker", "q.hire_picker", 100, 1, 30),
            Build("terrace", "q.terrace", 120, 1, 35),
            Sell(ItemKind.Pie, 6, 150, 1, 35),
            Q(QuestType.Wash, 8, 150, 1, 35),
            StStar(StationKind.Apples, 1, 200, 1, 40),
            Q(QuestType.Serve, 40, 180, 1, 40),
            Build("hire_baker", "q.hire_baker", 200, 2, 40),
            // chapter 3: the best market in the village
            StLv(StationKind.Bread, 3, 220, 1, 50),
            Build("oven2", "q.oven2", 250, 1, 50),
            Sell(ItemKind.Pie, 40, 260, 2, 50),
            Build("hire_pie_helper", "q.hire_pie_helper", 280, 1, 50),
            Up(UpgradeId.Profit, 3, 300, 2, 60),
            Q(QuestType.Earn, 3000, 320, 2, 60),
            StLv(StationKind.Checkout, 3, 300, 2, 60),
            StStar(StationKind.Bread, 1, 350, 2, 70),
            Q(QuestType.Vip, 8, 380, 2, 70),
            StLv(StationKind.Pies, 5, 400, 2, 70),
            Q(QuestType.MarketLevel, 9, 500, 2, 80),
            StStar(StationKind.Pies, 1, 600, 3, 100),
        };

        public static int Index => SaveSystem.Data.questIndex;
        public static bool InChain => Index < Chain.Length;

        /// <summary>Chapter number (1-based) of the current quest; 0 after the chain.</summary>
        public static int Chapter
        {
            get
            {
                for (int c = 0; c < ChapterEnd.Length; c++) if (Index < ChapterEnd[c]) return c + 1;
                return 0;
            }
        }

        public static void ChapterProgress(out int done, out int total)
        {
            int ch = Chapter;
            if (ch == 0) { done = 0; total = 0; return; }
            int start = ch == 1 ? 0 : ChapterEnd[ch - 2];
            total = ChapterEnd[ch - 1] - start;
            done = Index - start;
        }

        /// <summary>The active quest: from the chain, then generated with growing targets.</summary>
        public static QuestDef Current
        {
            get
            {
                if (InChain) return Chain[Index];
                int n = Index - Chain.Length;      // 0, 1, 2...
                int round = n / 6;
                float grow = 1f + 0.35f * round;
                switch (n % 6)
                {
                    case 0: return Q(QuestType.Serve, R(40 * grow), R(500 * grow), 2, R(80 * grow));
                    case 1: return Sell(ItemKind.Pie, R(30 * grow), R(550 * grow), 2, R(80 * grow));
                    case 2: return Q(QuestType.Earn, R(4000 * grow), R(600 * grow), 2, R(90 * grow));
                    case 3: return Sell(ItemKind.Bread, R(40 * grow), R(500 * grow), 2, R(80 * grow));
                    case 4: return Q(QuestType.Vip, R(6 * grow), R(600 * grow), 3, R(90 * grow));
                    default: return Q(QuestType.Wash, R(20 * grow), R(500 * grow), 2, R(80 * grow));
                }
            }
        }

        static int R(float v) => v >= 100 ? Mathf.RoundToInt(v / 10f) * 10 : Mathf.Max(1, Mathf.RoundToInt(v));

        static long Counter(QuestDef q)
        {
            var d = SaveSystem.Data;
            return q.type switch
            {
                QuestType.Serve => d.customersServed,
                QuestType.Sell => d.sold[(int)q.item],
                QuestType.Vip => d.vipServed,
                QuestType.Wash => d.platesWashed,
                QuestType.Earn => d.coinsCollected,
                _ => 0
            };
        }

        static bool Counts(QuestType t) => t == QuestType.Serve || t == QuestType.Sell || t == QuestType.Vip || t == QuestType.Wash || t == QuestType.Earn;

        /// <summary>Progress towards the target (clamped).</summary>
        public static long Value(QuestDef q)
        {
            long v;
            switch (q.type)
            {
                case QuestType.Build: v = SaveSystem.IsUnlocked(q.pad) ? 1 : 0; break;
                case QuestType.Upgrade: v = Economy.Level(q.upgrade); break;
                case QuestType.StationLevel: v = Stations.Stars(q.station) * Stations.LevelsPerStar + Stations.Level(q.station); break;
                case QuestType.StationStar: v = Stations.Stars(q.station); break;
                case QuestType.MarketLevel: v = SaveSystem.Data.marketLevel; break;
                default: v = Counter(q) - SaveSystem.Data.questStart; break;
            }
            return Math.Min(Math.Max(0, v), q.target);
        }

        public static bool Ready => Value(Current) >= Current.target;

        /// <summary>Snapshot the counters when a quest becomes active, so "sell 20" means 20 more.</summary>
        static void Begin()
        {
            var q = Current;
            SaveSystem.Data.questStart = Counts(q.type) ? Counter(q) : 0;
            Changed?.Invoke();
        }

        /// <summary>Old saves and first start: make sure the snapshot exists.</summary>
        public static void Init()
        {
            if (SaveSystem.Data.questIndex == 0 && SaveSystem.Data.questStart == 0 && SaveSystem.Data.customersServed > 0) Begin();
        }

        public static bool Claim()
        {
            if (!Ready) return false;
            var q = Current;
            Economy.Add(q.coins);
            Stations.AddMedals(q.medals);
            GameEvents.Log($"quest {Index} {q.type} done (+{q.coins} coins, +{q.medals} medals)");
            int finishedChapter = InChain ? Chapter : 0;
            SaveSystem.Data.questIndex++;
            bool chapterClosed = finishedChapter > 0 && Index == ChapterEnd[finishedChapter - 1];
            Begin();
            Progress.AddXp(q.xp, "quest");
            if (chapterClosed)
            {
                int i = finishedChapter - 1;
                Economy.Add(ChestCoins[i]);
                Stations.AddMedals(ChestMedals[i]);
                GameEvents.Log($"chapter {finishedChapter} chest");
                ChapterDone?.Invoke(finishedChapter);
            }
            SaveSystem.Save();
            return true;
        }

        public static int ChestCoinsFor(int chapter) => ChestCoins[chapter - 1];
        public static int ChestMedalsFor(int chapter) => ChestMedals[chapter - 1];

        // ---------------------------------------------------------------- presentation
        public static string Text(QuestDef q)
        {
            if (!string.IsNullOrEmpty(q.textKey)) return Loc.T(q.textKey);
            switch (q.type)
            {
                case QuestType.Serve: return Loc.F("q.serve", q.target);
                case QuestType.Sell: return Loc.F("q.sell", q.target, Loc.T("q.items." + q.item));
                case QuestType.Vip: return Loc.F("q.vip", q.target);
                case QuestType.Wash: return Loc.F("q.wash", q.target);
                case QuestType.Earn: return Loc.F("q.earn", Economy.Format(q.target));
                case QuestType.Upgrade: return Loc.F("q.upgrade", Loc.T(UpgradeKey(q.upgrade)), q.target + 1);
                case QuestType.StationLevel: return Loc.F("q.station_lv", Loc.T("st." + q.station), q.target);
                case QuestType.StationStar: return Loc.F("q.station_star", Loc.T("st." + q.station));
                case QuestType.MarketLevel: return Loc.F("q.level", q.target);
                default: return "";
            }
        }

        static string UpgradeKey(UpgradeId id) => id switch
        {
            UpgradeId.Speed => "upg.speed",
            UpgradeId.Capacity => "upg.capacity",
            UpgradeId.Profit => "upg.profit",
            UpgradeId.HelperSpeed => "upg.h_speed",
            UpgradeId.HelperCapacity => "upg.h_capacity",
            _ => "upg.h_cashier"
        };

        public static Sprite Icon(QuestDef q)
        {
            var C = GameConfig.I;
            switch (q.type)
            {
                case QuestType.Serve: return C.iconCashier;
                case QuestType.Sell: return C.ItemOf(q.item).icon;
                case QuestType.Vip: return C.iconStar;
                case QuestType.Wash: return C.plate.icon;
                case QuestType.Earn: return C.iconCoin;
                case QuestType.Upgrade: return q.upgrade == UpgradeId.Capacity || q.upgrade == UpgradeId.HelperCapacity ? C.iconBag
                                               : q.upgrade == UpgradeId.Profit ? C.iconProfit : q.upgrade == UpgradeId.Cashier ? C.iconCashier : C.iconSpeed;
                case QuestType.StationLevel:
                case QuestType.StationStar: return Stations.Icon(q.station);
                case QuestType.Build:
                {
                    var p = ProgressionManager.I != null ? ProgressionManager.I.Find(q.pad) : null;
                    return p != null && p.icon != null ? p.icon : C.iconBuild;
                }
                default: return C.iconQuest;
            }
        }

        /// <summary>Where the quest is done, for the "show me" arrow. Null when there is no single place.</summary>
        public static Vector3? Place(QuestDef q)
        {
            var C = GameConfig.I;
            switch (q.type)
            {
                case QuestType.Build:
                {
                    var p = ProgressionManager.I != null ? ProgressionManager.I.Find(q.pad) : null;
                    if (p != null && p.Revealed && !SaveSystem.IsUnlocked(q.pad)) return p.transform.position;
                    return null;
                }
                case QuestType.Serve:
                case QuestType.Vip:
                    return Game.I != null && Game.I.checkout != null && Game.I.checkout.isActiveAndEnabled ? Game.I.checkout.ZonePos : (Vector3?)null;
                case QuestType.Earn:
                    return Game.I != null && Game.I.checkout != null && Game.I.checkout.isActiveAndEnabled ? Game.I.checkout.pile.ZonePos : (Vector3?)null;
                case QuestType.Sell:
                {
                    var t = C.ItemOf(q.item);
                    foreach (var s in Station.All) if (s is Shelf sh && sh.isActiveAndEnabled && sh.type == t) return sh.ZonePos;
                    return null;
                }
                case QuestType.Wash:
                    foreach (var s in Station.All) if (s is WashTub w && w.isActiveAndEnabled) return w.ZonePos;
                    return null;
                case QuestType.Upgrade:
                case QuestType.StationLevel:
                case QuestType.StationStar:
                    foreach (var z in UnityEngine.Object.FindObjectsByType<PanelZone>(FindObjectsSortMode.None))
                        if (z.isActiveAndEnabled && z.panel == PanelKind.Upgrades) return z.zone.position;
                    return null;
                default: return null;
            }
        }
    }
}
