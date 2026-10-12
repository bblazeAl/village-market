using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// Market level from experience: customers, buildings, station upgrades and quests all give XP.
    /// Each new level pays coins + a medal and offers a choice of one perk card out of three.
    /// </summary>
    public static class Progress
    {
        static Balance B => GameConfig.I.balance;
        static readonly Queue<int> pending = new Queue<int>();
        static bool showing;

        public static long Xp => SaveSystem.Data.xp;
        public static int Level => SaveSystem.Data.marketLevel;

        /// <summary>XP needed to finish the given level (1-based).</summary>
        public static long Need(int level)
        {
            var t = B.xpPerLevel;
            if (level - 1 < t.Length) return t[level - 1];
            return t[t.Length - 1] + (long)B.xpTailGrowth * (level - t.Length);
        }

        /// <summary>Total XP at which the given level starts.</summary>
        public static long Start(int level)
        {
            long s = 0;
            for (int l = 1; l < level; l++) s += Need(l);
            return s;
        }

        public static int LevelForXp(long xp)
        {
            int l = 1;
            while (xp >= Start(l + 1) && l < 999) l++;
            return l;
        }

        public static int CoinsFor(int level)
        {
            var t = B.levelCoins;
            int i = level - 2;
            if (i < t.Length) return t[Mathf.Max(0, i)];
            return t[t.Length - 1] + 100 * (i - t.Length + 1);
        }

        /// <summary>Old saves had levels by purchases only: start them at the beginning of their level.</summary>
        public static void Migrate()
        {
            var d = SaveSystem.Data;
            if (d.xp >= 0) return;
            d.xp = Start(Mathf.Max(1, d.marketLevel));
        }

        public static void InLevel(out int level, out long have, out long need, out float k)
        {
            level = Level;
            long s = Start(level);
            need = Need(level);
            have = Xp - s;
            k = Mathf.Clamp01((float)have / Mathf.Max(1, need));
        }

        public static void AddXp(long amount, string why)
        {
            if (amount <= 0) return;
            var d = SaveSystem.Data;
            d.xp += amount;
            int lv = LevelForXp(d.xp);
            while (d.marketLevel < lv)
            {
                d.marketLevel++;
                pending.Enqueue(d.marketLevel);
                GameEvents.LevelUp(d.marketLevel);
                GameEvents.Log($"level {d.marketLevel} ({why})");
            }
            ShowNext();
        }

        /// <summary>Level-ups wait while another window is open, then show one by one.</summary>
        public static void ShowNext()
        {
            if (showing || pending.Count == 0) return;
            showing = true;
            int level = pending.Dequeue();
            Tween.Delay(0.9f, () => UIPanels.ShowLevelUp(level, CoinsFor(level)), null, true);
        }

        /// <summary>Called by the level-up window once the player took the reward.</summary>
        public static void Claimed(int level)
        {
            showing = false;
            Stations.AddMedals(B.medalsPerLevel);
            SaveSystem.Save();
            ShowNext();
        }

        public static void Reset()
        {
            pending.Clear();
            showing = false;
        }
    }
}
