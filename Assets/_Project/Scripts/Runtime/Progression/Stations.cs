using System;
using UnityEngine;

namespace IdleArcade
{
    public enum StationKind { Apples = 0, Bread = 1, Pies = 2, Checkout = 3 }

    /// <summary>
    /// The long ladder on top of the short upgrades: each station type levels up for coins; a full bar
    /// (5 levels) is crowned with a star bought with medals. Levels speed the station up, a star also
    /// makes its product dearer. Medals come only from quests, chapters and market levels.
    /// </summary>
    public static class Stations
    {
        public static event Action Changed;
        public static event Action<long, long> MedalsChanged;   // (newValue, delta)

        static Balance B => GameConfig.I.balance;
        public static int MaxStars => B.starMedals.Length;
        public static int LevelsPerStar => B.stationLevelsPerStar;

        public static readonly StationKind[] All = { StationKind.Apples, StationKind.Bread, StationKind.Pies, StationKind.Checkout };

        public static string RequiredPad(StationKind k) => k switch
        {
            StationKind.Apples => "tree1",
            StationKind.Bread => "oven",
            StationKind.Pies => "pie_oven",
            _ => "counter"
        };

        public static bool Built(StationKind k) => SaveSystem.IsUnlocked(RequiredPad(k));
        public static int Level(StationKind k) => SaveSystem.Data.stationLevel[(int)k];
        public static int Stars(StationKind k) => SaveSystem.Data.stationStars[(int)k];
        public static bool BarFull(StationKind k) => Level(k) >= LevelsPerStar;
        public static bool Maxed(StationKind k) => Stars(k) >= MaxStars && BarFull(k);

        /// <summary>Coin price of the next level, or -1 when the bar is full (a star is due) or maxed.</summary>
        public static int LevelCost(StationKind k)
        {
            if (BarFull(k)) return -1;
            int step = Stars(k) * LevelsPerStar + Level(k);
            float c = B.stationBaseCost[(int)k] * Mathf.Pow(B.stationCostGrowth, step);
            return Mathf.Max(5, Mathf.RoundToInt(c / 5f) * 5);
        }

        /// <summary>Medal price of the next star, or -1 when the bar isn't full yet or all stars are earned.</summary>
        public static int StarCost(StationKind k)
        {
            if (!BarFull(k) || Stars(k) >= MaxStars) return -1;
            return B.starMedals[Stars(k)];
        }

        public static bool TryLevel(StationKind k)
        {
            int c = LevelCost(k);
            if (c < 0 || !Built(k) || !Economy.TrySpend(c)) return false;
            SaveSystem.Data.stationLevel[(int)k]++;
            Progress.AddXp(Mathf.Max(1, c / Mathf.Max(1, B.xpStationDivisor)), "station " + k);
            GameEvents.Log($"station {k} -> lv{Level(k)} star{Stars(k)}");
            Changed?.Invoke();
            return true;
        }

        public static bool TryStar(StationKind k)
        {
            int c = StarCost(k);
            if (c < 0 || !TrySpendMedals(c)) return false;
            SaveSystem.Data.stationStars[(int)k]++;
            // after the last star the bar stays full (shown as maxed); otherwise a new bar starts
            if (Stars(k) < MaxStars) SaveSystem.Data.stationLevel[(int)k] = 0;
            GameEvents.Log($"station {k} STAR {Stars(k)}");
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- medals
        public static int Medals => SaveSystem.Data.medals;

        public static void AddMedals(int n)
        {
            if (n == 0) return;
            SaveSystem.Data.medals += n;
            MedalsChanged?.Invoke(SaveSystem.Data.medals, n);
        }

        static bool TrySpendMedals(int n)
        {
            if (SaveSystem.Data.medals < n) return false;
            SaveSystem.Data.medals -= n;
            MedalsChanged?.Invoke(SaveSystem.Data.medals, -n);
            return true;
        }

        // ---------------------------------------------------------------- effects
        /// <summary>Production / service speed multiplier from levels and stars.</summary>
        /// <summary>Counts every level ever bought, so a new star never makes the station slower than before it.</summary>
        public static float SpeedMul(StationKind k)
        {
            int levels = Stars(k) >= MaxStars ? Stars(k) * LevelsPerStar : Stars(k) * LevelsPerStar + Level(k);
            return 1f + B.stationSpeedPerLevel * levels + B.stationSpeedPerStar * Stars(k);
        }

        /// <summary>Price multiplier for a product: its station's stars, plus the checkout's stars for everything.</summary>
        public static float PriceMul(ItemType t)
        {
            var C = GameConfig.I;
            float m = 1f + B.checkoutPricePerStar * Stars(StationKind.Checkout);
            if (t == C.apple) m *= 1f + B.stationPricePerStar * Stars(StationKind.Apples);
            else if (t == C.bread) m *= 1f + B.stationPricePerStar * Stars(StationKind.Bread);
            else if (t == C.pie) m *= 1f + B.stationPricePerStar * Stars(StationKind.Pies);
            return m;
        }

        public static Sprite Icon(StationKind k)
        {
            var C = GameConfig.I;
            return k switch
            {
                StationKind.Apples => C.apple.icon,
                StationKind.Bread => C.bread.icon,
                StationKind.Pies => C.pie.icon,
                _ => C.iconCashier
            };
        }
    }
}
