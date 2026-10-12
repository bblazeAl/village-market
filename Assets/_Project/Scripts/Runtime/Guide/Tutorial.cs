using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// First-session walkthrough (coins → till → stand → tree → pick → stock → serve → collect),
    /// then a permanent "advisor" that always names the most useful next action, so the player
    /// never wonders what to do (the top games' "certainty" principle).
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        public static Tutorial I { get; private set; }

        public MoneyPile startCoins;
        public Checkout checkout;
        public AppleTree firstTree;
        public Shelf appleStand;

        enum Step { CollectCoins, BuildCounter, BuildStand, BuildTree, PickApples, StockStand, Serve, CollectMoney, Done }
        Step step;
        float think;
        string lastKey;
        float suggestionHold;

        GameConfig C => GameConfig.I;
        Hud H => Hud.I;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            step = SaveSystem.Data.tutorialDone ? Step.Done : (Step)Mathf.Clamp(SaveSystem.Data.tutorialStep, 0, (int)Step.Done);
            GameEvents.OnMoneyCollected += OnMoneyCollected;
        }

        void OnDisable() { GameEvents.OnMoneyCollected -= OnMoneyCollected; }

        void OnMoneyCollected(long v, MoneyPile p)
        {
            if (p == startCoins && !SaveSystem.Data.startCoinsTaken)
            {
                SaveSystem.Data.startCoinsTaken = true;
                SaveSystem.Save();
            }
        }

        public void Restart()
        {
            SaveSystem.Data.tutorialDone = false;
            SaveSystem.Data.tutorialStep = 0;
            step = Step.CollectCoins;
            lastKey = null;
        }

        public bool InTutorial => step != Step.Done;

        void Update()
        {
            if (H == null || Guide.I == null) return;
            think -= Time.deltaTime;
            if (think > 0f) return;
            think = 0.2f;

            if (step != Step.Done) RunTutorial();
            else if (!RunQuestFocus()) RunAdvisor();
        }

        // ------------------------------------------------------------------ quest "show me"
        float focusUntil;
        Vector3 focusPos;
        Sprite focusIcon;
        string focusText;

        /// <summary>Tapping an unfinished quest card: arrow and path to the place for a few seconds.</summary>
        public void FocusQuest(Vector3 pos, Sprite icon, string text)
        {
            focusPos = pos;
            focusIcon = icon;
            focusText = text;
            focusUntil = Time.unscaledTime + 7f;
            lastKey = null;
            think = 0f;
            if (CameraRig.I != null && !CameraRig.I.IsOnScreen(pos, 0.12f)) Feedback.Focus(pos, 0.8f);
        }

        public void ClearQuestFocus() { focusUntil = 0f; }

        bool RunQuestFocus()
        {
            if (Time.unscaledTime > focusUntil) return false;
            var player = PlayerController.I;
            if (Quests.Ready || (player != null && Vector3.Distance(player.transform.position, focusPos) < 1.3f)) { focusUntil = 0f; return false; }
            Show("questfocus", focusText, focusIcon, focusPos, true, true);
            return true;
        }

        void Advance(Step s)
        {
            step = s;
            SaveSystem.Data.tutorialStep = (int)s;
            if (s == Step.Done)
            {
                SaveSystem.Data.tutorialDone = true;
                GameEvents.Log("tutorial done");
            }
            SaveSystem.Save();
            lastKey = null;
        }

        void Show(string key, string text, Sprite icon, Vector3? target, bool arrow, bool line)
        {
            if (key != lastKey) { lastKey = key; }
            H.SetObjective(text, icon);
            if (target.HasValue) Guide.I.Point(target.Value, icon, arrow, line);
            else Guide.I.Clear();
        }

        PayPad Pad(string id) => ProgressionManager.I != null ? ProgressionManager.I.Find(id) : null;

        bool PadDone(string id) => SaveSystem.IsUnlocked(id);

        void ShowPad(string id)
        {
            var p = Pad(id);
            if (p == null || !p.Revealed) { H.SetObjective(null, null); Guide.I.Clear(); return; }
            Show("pad." + id, Loc.F("obj.build", Loc.T(p.titleKey)), p.icon, p.transform.position, true, true);
        }

        // ------------------------------------------------------------------ tutorial
        void RunTutorial()
        {
            var player = PlayerController.I;
            switch (step)
            {
                case Step.CollectCoins:
                    if (SaveSystem.Data.startCoinsTaken) { Advance(Step.BuildCounter); break; }
                    Show("coins", Loc.T("obj.collect_coins"), C.iconCoin, startCoins.transform.position, true, true);
                    break;
                case Step.BuildCounter:
                    if (PadDone("counter")) { Advance(Step.BuildStand); break; }
                    ShowPad("counter");
                    break;
                case Step.BuildStand:
                    if (PadDone("apple_stand")) { Advance(Step.BuildTree); break; }
                    ShowPad("apple_stand");
                    break;
                case Step.BuildTree:
                    if (PadDone("tree1")) { Advance(Step.PickApples); break; }
                    ShowPad("tree1");
                    break;
                case Step.PickApples:
                {
                    int have = player.stack.CountOf(C.apple);
                    if (have >= 3 || (have > 0 && firstTree.Available == 0) || appleStand.Stock >= 2) { Advance(Step.StockStand); break; }
                    Show("pick", Loc.T("obj.pick_apples"), C.apple.icon, firstTree.ZonePos, true, true);
                    break;
                }
                case Step.StockStand:
                    if (appleStand.Stock >= 2 && !player.stack.Has(C.apple) || appleStand.Stock >= 3) { Advance(Step.Serve); break; }
                    if (!player.stack.Has(C.apple) && appleStand.Stock < 2) { step = Step.PickApples; break; }
                    Show("stock", Loc.T("obj.stock_stand"), C.apple.icon, appleStand.ZonePos, true, true);
                    break;
                case Step.Serve:
                    if (SaveSystem.Data.customersServed >= 1) { Advance(Step.CollectMoney); break; }
                    if (appleStand.Stock < 1 && CustomerManager.I != null && CustomerManager.I.CountWaitingFor(appleStand) > 0 && !player.stack.Has(C.apple))
                    {
                        Show("pick", Loc.T("obj.pick_apples"), C.apple.icon, firstTree.ZonePos, true, true);
                        break;
                    }
                    if (player.stack.Has(C.apple) && appleStand.Stock < 2)
                    {
                        Show("stock", Loc.T("obj.stock_stand"), C.apple.icon, appleStand.ZonePos, true, true);
                        break;
                    }
                    Show("serve", Loc.T("obj.serve"), C.iconCashier, checkout.ZonePos, true, true);
                    break;
                case Step.CollectMoney:
                    if (!checkout.pile.HasMoney) { Advance(Step.Done); break; }
                    Show("money", Loc.T("obj.collect_money"), C.iconCoin, checkout.pile.ZonePos, true, true);
                    break;
            }
        }

        // ------------------------------------------------------------------ advisor
        void RunAdvisor()
        {
            var player = PlayerController.I;
            if (player == null) return;
            bool idle = player.IdleTime > 2.5f;
            suggestionHold -= 0.2f;

            var pm = ProgressionManager.I;
            // 1. something to build right now
            var pad = pm.NextPad(true);
            if (pad != null)
            {
                // hire pads already read as an action ("Hire cashier"); others get "Build: ..."
                string text = pad.action == PadAction.Hire ? Loc.T(pad.titleKey) : Loc.F("obj.build", Loc.T(pad.titleKey));
                Show("pad." + pad.id, text, pad.icon, pad.transform.position, true, idle);
                return;
            }
            // 2. customers waiting at an unattended till
            if (checkout.isActiveAndEnabled && checkout.HasWaitingCustomer && !checkout.HasCashier && !checkout.PlayerAttending)
            {
                Show("serve", Loc.T("obj.serve"), C.iconCashier, checkout.ZonePos, true, idle);
                return;
            }
            // 3. money piling up while the next goal needs it
            var next = pm.NextPad(false);
            long pile = checkout.pile.Amount;
            if (pile > 0 && (next == null || pile + Economy.Money >= next.Remaining || pile >= 25))
            {
                Show("money", Loc.T("obj.collect_money"), C.iconCoin, checkout.pile.ZonePos, idle, idle);
                return;
            }
            // 4. carrying something: deliver it
            var top = player.stack.TopType;
            if (top != null)
            {
                if (top == C.plate)
                {
                    var tub = FindStation<WashTub>();
                    if (tub != null) { Show("wash", Loc.T("obj.wash"), C.plate.icon, tub.ZonePos, idle, idle); return; }
                }
                else
                {
                    var conv = FindStation<Converter>();
                    if (top == C.apple && conv != null && conv.inTray.Count < 2 && FindShelf(C.apple)?.Stock > 4)
                    {
                        Show("feed", Loc.T("obj.feed_oven"), C.apple.icon, conv.ZonePos, idle, idle);
                        return;
                    }
                    var shelf = FindShelf(top);
                    var bin = FindStation<TrashBin>();
                    if (shelf != null && !shelf.NeedsStock && player.stack.IsFull && bin != null && AnyHungryShelfExcept(top))
                    {
                        Show("trash", Loc.T("obj.trash"), C.iconTrash, bin.ZonePos, true, idle);
                        return;
                    }
                    if (shelf != null)
                    {
                        Show("stock." + top.name, Loc.F("obj.stock", Loc.T(top.nameKey)), top.icon, shelf.ZonePos, idle, idle);
                        return;
                    }
                }
            }
            // 5. an empty shelf with people waiting
            Shelf hungry = null;
            int waiting = 0;
            foreach (var s in Station.All)
            {
                if (!(s is Shelf sh) || !sh.isActiveAndEnabled || sh.Stock > 1) continue;
                int w = CustomerManager.I != null ? CustomerManager.I.CountWaitingFor(sh) : 0;
                if (w > waiting) { waiting = w; hungry = sh; }
            }
            if (hungry != null)
            {
                Station src = SourceFor(hungry.type, out string key);
                if (src != null)
                {
                    Vector3 pos = src is Converter cv && hungry.type == C.pie ? cv.OutZonePos : src.ZonePos;
                    Show("src." + hungry.type.name, Loc.T(key), hungry.type.icon, pos, idle, idle);
                    return;
                }
            }
            // 6. dirty tables
            if (TableManager.TotalDirty() > 0)
            {
                var t = TableManager.DirtiestTable(player.transform.position);
                Show("clean", Loc.T("obj.clean"), C.plate.icon, t.ZonePos, idle, idle);
                return;
            }
            // 7. saving up
            if (next != null)
            {
                Show("save." + next.id, Loc.F("obj.save_up", Loc.T(next.titleKey)), next.icon, next.transform.position, false, false);
                return;
            }
            Show("done", Loc.T("obj.all_done"), C.iconStar, null, false, false);
        }

        Station SourceFor(ItemType t, out string key)
        {
            key = "obj.pick_apples";
            if (t == C.apple)
            {
                AppleTree best = null;
                foreach (var s in Station.All) if (s is AppleTree tr && tr.isActiveAndEnabled && (best == null || tr.Available > best.Available)) best = tr;
                return best;
            }
            if (t == C.bread)
            {
                key = "obj.take_bread";
                Oven best = null;
                foreach (var s in Station.All) if (s is Oven o && o.isActiveAndEnabled && (best == null || o.tray.Count > best.tray.Count)) best = o;
                return best;
            }
            if (t == C.pie)
            {
                var conv = FindStation<Converter>();
                if (conv == null) return null;
                key = conv.outTray.Count > 0 ? "obj.take_pies" : "obj.feed_oven";
                if (conv.outTray.Count == 0)
                {
                    key = "obj.pick_apples";
                    return SourceFor(C.apple, out _);
                }
                return conv;
            }
            return null;
        }

        static bool AnyHungryShelfExcept(ItemType t)
        {
            foreach (var s in Station.All)
                if (s is Shelf sh && sh.isActiveAndEnabled && sh.type != t && sh.Stock == 0 && CustomerManager.I != null && CustomerManager.I.CountWaitingFor(sh) > 0) return true;
            return false;
        }

        static T FindStation<T>() where T : Station
        {
            foreach (var s in Station.All) if (s is T t && t.isActiveAndEnabled) return t;
            return null;
        }

        static Shelf FindShelf(ItemType t)
        {
            Shelf best = null;
            foreach (var s in Station.All)
                if (s is Shelf sh && sh.isActiveAndEnabled && sh.type == t && (best == null || sh.Fill < best.Fill)) best = sh;
            return best;
        }
    }
}
