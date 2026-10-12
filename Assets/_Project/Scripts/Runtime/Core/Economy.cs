using System;
using UnityEngine;

namespace IdleArcade
{
    public enum UpgradeId { Speed, Capacity, Profit, HelperSpeed, HelperCapacity, Cashier }

    /// <summary>Money and upgrade levels. Everything is persisted through SaveSystem.Data.</summary>
    public static class Economy
    {
        public const int MaxUpgradeLevel = 5;
        public static event Action<long, long> MoneyChanged;   // (newValue, delta)
        public static event Action<UpgradeId> Upgraded;

        static Balance B => GameConfig.I.balance;
        public static long Money => SaveSystem.Data.money;

        public static void Add(long amount, bool countAsEarned = true)
        {
            if (amount == 0) return;
            SaveSystem.Data.money += amount;
            if (countAsEarned && amount > 0) SaveSystem.Data.totalEarned += amount;
            MoneyChanged?.Invoke(SaveSystem.Data.money, amount);
        }

        public static bool TrySpend(long amount)
        {
            if (amount <= 0) return true;
            if (SaveSystem.Data.money < amount) return false;
            SaveSystem.Data.money -= amount;
            MoneyChanged?.Invoke(SaveSystem.Data.money, -amount);
            return true;
        }

        public static void Notify() => MoneyChanged?.Invoke(Money, 0);

        public static int Level(UpgradeId id) => id switch
        {
            UpgradeId.Speed => SaveSystem.Data.upSpeed,
            UpgradeId.Capacity => SaveSystem.Data.upCapacity,
            UpgradeId.Profit => SaveSystem.Data.upProfit,
            UpgradeId.HelperSpeed => SaveSystem.Data.upHelperSpeed,
            UpgradeId.HelperCapacity => SaveSystem.Data.upHelperCapacity,
            _ => SaveSystem.Data.upCashier
        };

        static void SetLevel(UpgradeId id, int v)
        {
            switch (id)
            {
                case UpgradeId.Speed: SaveSystem.Data.upSpeed = v; break;
                case UpgradeId.Capacity: SaveSystem.Data.upCapacity = v; break;
                case UpgradeId.Profit: SaveSystem.Data.upProfit = v; break;
                case UpgradeId.HelperSpeed: SaveSystem.Data.upHelperSpeed = v; break;
                case UpgradeId.HelperCapacity: SaveSystem.Data.upHelperCapacity = v; break;
                default: SaveSystem.Data.upCashier = v; break;
            }
        }

        public static bool IsHelperUpgrade(UpgradeId id) => id == UpgradeId.HelperSpeed || id == UpgradeId.HelperCapacity || id == UpgradeId.Cashier;

        public static int Cost(UpgradeId id)
        {
            int lv = Level(id);
            if (lv >= MaxUpgradeLevel) return -1;
            var table = IsHelperUpgrade(id) ? B.helperUpgradeCost : B.playerUpgradeCost;
            int c = table[Mathf.Clamp(lv, 0, table.Length - 1)];
            if (id == UpgradeId.Profit) c = Mathf.RoundToInt(c * 1.25f / 5f) * 5;
            return c;
        }

        public static bool TryUpgrade(UpgradeId id)
        {
            int c = Cost(id);
            if (c < 0 || !TrySpend(c)) return false;
            SetLevel(id, Level(id) + 1);
            Upgraded?.Invoke(id);
            return true;
        }

        public static float PlayerSpeed => (B.playerSpeed + B.playerSpeedPerLevel * Level(UpgradeId.Speed)) * Perks.Mul(PerkId.Speed);
        public static int PlayerCapacity => B.capacityByLevel[Mathf.Clamp(Level(UpgradeId.Capacity), 0, B.capacityByLevel.Length - 1)] + Perks.Add(PerkId.Capacity);
        public static float ProfitMultiplier => B.profitByLevel[Mathf.Clamp(Level(UpgradeId.Profit), 0, B.profitByLevel.Length - 1)] * Perks.Mul(PerkId.Profit);
        public static float HelperSpeed => (B.helperSpeed + B.helperSpeedPerLevel * Level(UpgradeId.HelperSpeed)) * Perks.Mul(PerkId.HelperSpeed);
        public static int HelperCapacity => B.helperCapacity + B.helperCapacityPerLevel * Level(UpgradeId.HelperCapacity);
        public static float CashierTime => Mathf.Max(0.35f, B.cashierTime + B.cashierTimePerLevel * Level(UpgradeId.Cashier));

        public static string Format(long v)
        {
            if (v >= 1_000_000) return (v / 1_000_000f).ToString(v >= 10_000_000 ? "0" : "0.#") + "M";
            if (v >= 10_000) return (v / 1000f).ToString(v >= 100_000 ? "0" : "0.#") + "K";
            return v.ToString();
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
