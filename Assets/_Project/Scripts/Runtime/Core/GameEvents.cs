using System;

namespace IdleArcade
{
    /// <summary>Gameplay event bus used by the tutorial, the advisor, analytics-style logging and the autotest.</summary>
    public static class GameEvents
    {
        public static event Action<ItemType, Carrier> OnItemPicked;
        public static event Action<ItemType, Carrier, Station> OnItemDropped;
        public static event Action<Customer, long> OnCustomerServed;
        public static event Action<long, MoneyPile> OnMoneyCollected;
        public static event Action<PayPad> OnPadUnlocked;
        public static event Action<PayPad> OnPadRevealed;
        public static event Action<PicnicTable> OnTableDirty;
        public static event Action<int> OnLevelUp;
        public static event Action<string> OnLog;

        public static void ItemPicked(ItemType t, Carrier c) => OnItemPicked?.Invoke(t, c);
        public static void ItemDropped(ItemType t, Carrier c, Station s) => OnItemDropped?.Invoke(t, c, s);
        public static void CustomerServed(Customer c, long paid) => OnCustomerServed?.Invoke(c, paid);
        public static void MoneyCollected(long v, MoneyPile p) => OnMoneyCollected?.Invoke(v, p);
        public static void PadUnlocked(PayPad p) => OnPadUnlocked?.Invoke(p);
        public static void PadRevealed(PayPad p) => OnPadRevealed?.Invoke(p);
        public static void TableDirty(PicnicTable t) => OnTableDirty?.Invoke(t);
        public static void LevelUp(int lv) => OnLevelUp?.Invoke(lv);
        public static void Log(string s) => OnLog?.Invoke(s);

        public static void ClearAll()
        {
            OnItemPicked = null; OnItemDropped = null; OnCustomerServed = null; OnMoneyCollected = null;
            OnPadUnlocked = null; OnPadRevealed = null; OnTableDirty = null; OnLevelUp = null; OnLog = null;
        }
    }
}
