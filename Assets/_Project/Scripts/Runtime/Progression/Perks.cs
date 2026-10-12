using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    public enum PerkId { Speed, Capacity, Profit, TreeGrow, Bake, HelperSpeed, Patience, Tips, Vip, Checkout }

    public enum Rarity { Common, Rare, Epic }

    /// <summary>One card offered on a market level-up.</summary>
    public struct PerkCard
    {
        public PerkId id;
        public Rarity rarity;
        public float value;
    }

    /// <summary>
    /// Permanent level-up bonuses (the "pick one of three cards" step from long-session idle games).
    /// Values add up per perk; gameplay code asks Perks.Mul / Perks.Add.
    /// </summary>
    public static class Perks
    {
        // value per rarity: common, rare, epic (fractions are percents, Capacity/Tips are whole items/coins)
        static readonly Dictionary<PerkId, float[]> Values = new Dictionary<PerkId, float[]>
        {
            { PerkId.Speed, new[] { 0.05f, 0.10f, 0.16f } },
            { PerkId.Capacity, new[] { 1f, 2f, 3f } },
            { PerkId.Profit, new[] { 0.05f, 0.10f, 0.16f } },
            { PerkId.TreeGrow, new[] { 0.10f, 0.20f, 0.32f } },
            { PerkId.Bake, new[] { 0.10f, 0.20f, 0.32f } },
            { PerkId.HelperSpeed, new[] { 0.08f, 0.15f, 0.24f } },
            { PerkId.Patience, new[] { 0.15f, 0.30f, 0.50f } },
            { PerkId.Tips, new[] { 1f, 2f, 4f } },
            { PerkId.Vip, new[] { 0.25f, 0.50f, 0.80f } },
            { PerkId.Checkout, new[] { 0.10f, 0.20f, 0.32f } },
        };

        public static float Total(PerkId id)
        {
            float v = 0f;
            foreach (var p in SaveSystem.Data.perks) if (p.id == id) v += p.value;
            return v;
        }

        /// <summary>1 + summed percent bonus.</summary>
        public static float Mul(PerkId id) => 1f + Total(id);
        public static int Add(PerkId id) => Mathf.RoundToInt(Total(id));

        public static void Take(PerkCard c)
        {
            SaveSystem.Data.perks.Add(new PerkEntry { id = c.id, value = c.value });
            GameEvents.Log($"perk {c.id} {c.rarity} +{c.value}");
        }

        /// <summary>Only perks that matter right now: no oven bonus before the first oven, etc.</summary>
        static bool Relevant(PerkId id)
        {
            switch (id)
            {
                case PerkId.Bake: return SaveSystem.IsUnlocked("oven");
                case PerkId.HelperSpeed: return HelperManager.I != null && HelperManager.I.Count > 0;
                case PerkId.Tips: return SaveSystem.IsUnlocked("table1");
                case PerkId.Vip: return SaveSystem.Data.marketLevel >= 2;
                case PerkId.Checkout: return SaveSystem.IsUnlocked("counter");
                default: return true;
            }
        }

        /// <summary>Three different cards; better rarities get likelier with the market level.</summary>
        public static PerkCard[] Roll(int level)
        {
            var pool = new List<PerkId>();
            foreach (PerkId id in System.Enum.GetValues(typeof(PerkId))) if (Relevant(id)) pool.Add(id);
            var cards = new PerkCard[3];
            for (int i = 0; i < 3; i++)
            {
                int k = Random.Range(0, pool.Count);
                var id = pool[k];
                pool.RemoveAt(k);
                var r = RollRarity(level);
                cards[i] = new PerkCard { id = id, rarity = r, value = Values[id][(int)r] };
            }
            return cards;
        }

        static Rarity RollRarity(int level)
        {
            float epic = Mathf.Clamp(0.03f + 0.02f * level, 0f, 0.3f);
            float rare = Mathf.Clamp(0.2f + 0.035f * level, 0f, 0.5f);
            float r = Random.value;
            if (r < epic) return Rarity.Epic;
            if (r < epic + rare) return Rarity.Rare;
            return Rarity.Common;
        }

        public static string Describe(PerkCard c)
        {
            string val = c.id == PerkId.Capacity || c.id == PerkId.Tips ? "+" + Mathf.RoundToInt(c.value) : "+" + Mathf.RoundToInt(c.value * 100f) + "%";
            return Loc.F("perk." + c.id + ".d", val);
        }

        public static Sprite Icon(PerkId id)
        {
            var C = GameConfig.I;
            return id switch
            {
                PerkId.Speed => C.iconSpeed,
                PerkId.Capacity => C.iconBag,
                PerkId.Profit => C.iconProfit,
                PerkId.TreeGrow => C.apple.icon,
                PerkId.Bake => C.bread.icon,
                PerkId.HelperSpeed => C.iconHelper,
                PerkId.Patience => C.iconHeart,
                PerkId.Tips => C.iconCoin,
                PerkId.Vip => C.iconStar,
                _ => C.iconCashier
            };
        }
    }
}
