using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    public static class UIPanels
    {
        internal static Panels Impl;
        public static bool AnyModalOpen => Impl != null && Impl.ModalOpen;
        public static void Open(PanelKind k) => Impl?.OpenSheet(k);
        public static void Close(PanelKind k) => Impl?.CloseSheet(k);
        public static void OpenSettings() => Impl?.OpenSettings();
        public static void ShowLevelUp(int level, int reward) => Impl?.ShowLevelUp(level, reward);
        public static void ShowDemoComplete() => Impl?.ShowDemoComplete();
        public static void ShowOffline(long amount) => Impl?.ShowOffline(amount);
        public static void ShowChapter(int n) => Impl?.ShowChapter(n);
        public static bool LevelUpOpen => Impl != null && Impl.LevelUpOpen;
        public static void PickCard(int i) => Impl?.PickCard(i);
        public static int BestCard() => Impl != null ? Impl.BestCard() : 0;
    }

    /// <summary>All pop-up UI: upgrade sheets, settings, level-up banner, offline earnings, demo end.</summary>
    public class Panels : MonoBehaviour
    {
        RectTransform safe, root;
        GameConfig C => GameConfig.I;

        // sheet
        RectTransform sheet;
        TextMeshProUGUI sheetTitle;
        PanelKind? sheetKind;
        readonly List<UpgradeRow> rows = new List<UpgradeRow>();
        RectTransform rowsRoot;

        // modals
        RectTransform dim;
        CanvasGroup dimGroup;
        RectTransform settings, confirm, levelUp, demo, offline, chapter;
        public bool ModalOpen { get; private set; }

        class UpgradeRow
        {
            public UpgradeId id;
            public bool isStation;
            public StationKind station;
            public RectTransform rt;
            public TextMeshProUGUI title, desc, cost, level;
            public Button buy;
            public Image icon, coin, medal;
            public Image[] pips, stars;
        }

        public void Build(RectTransform safeRoot, RectTransform canvasRoot)
        {
            UIPanels.Impl = this;
            safe = safeRoot;
            root = canvasRoot;
            BuildSheet();
            dim = UIFactory.Rect("Dim", root).Stretch();
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0.05f, 0.03f, 0.02f, 0.6f);
            dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            dim.gameObject.SetActive(false);
            BuildSettings();
            BuildConfirm();
            BuildLevelUp();
            BuildDemo();
            BuildOffline();
            BuildChapter();
        }

        void OnDestroy() { if (UIPanels.Impl == this) UIPanels.Impl = null; }

        // ================================================================ sheet (upgrades: hero / stations tabs; helpers)
        const float RowH = 176f, RowGap = 18f;
        bool stationsTab;
        Button tabHero, tabStations;
        TextMeshProUGUI tabHeroLabel, tabStationsLabel;
        Image tabDot;

        void BuildSheet()
        {
            sheet = UIFactory.Rect("UpgradeSheet", safe).Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1000, 700));
            var shadow = UIFactory.Panel("Shadow", sheet, new Color(0, 0, 0, 0.25f));
            shadow.rectTransform.Stretch(-4, -4, 10, -14);
            var bg = UIFactory.Panel("Bg", sheet, UIFactory.Cream);
            bg.rectTransform.Stretch();
            var header = UIFactory.Panel("Header", sheet, UIFactory.Orange);
            header.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(560, 104));
            sheetTitle = UIFactory.Text("Title", header.rectTransform, "", 54, Color.white, TextAlignmentOptions.Center, true);
            sheetTitle.rectTransform.Stretch(0, 0, 0, 8);

            tabHero = UIFactory.Button("TabHero", sheet, UIFactory.Orange, UIFactory.OrangeDark, () => SetTab(false), out var hf);
            ((RectTransform)tabHero.transform).Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-200, -110), new Vector2(380, 84));
            tabHeroLabel = UIFactory.Text("Label", hf, "", 40, Color.white, TextAlignmentOptions.Center, true);
            tabHeroLabel.rectTransform.Stretch(8, 8, 0, 2);
            tabStations = UIFactory.Button("TabStations", sheet, UIFactory.Grey, UIFactory.GreyDark, () => SetTab(true), out var sf);
            ((RectTransform)tabStations.transform).Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(200, -110), new Vector2(380, 84));
            tabStationsLabel = UIFactory.Text("Label", sf, "", 40, Color.white, TextAlignmentOptions.Center, true);
            tabStationsLabel.rectTransform.Stretch(8, 8, 0, 2);
            tabDot = UIFactory.Image("Dot", tabStations.transform, C.spriteCircle, UIFactory.Red);
            tabDot.rectTransform.Anchor(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -10), new Vector2(34, 34));

            rowsRoot = UIFactory.Rect("Rows", sheet).Stretch(30, 30, 80, 30);
            var v = rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = RowGap;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
            for (int i = 0; i < 4; i++) rows.Add(CreateRow(rowsRoot));
            sheet.gameObject.SetActive(false);
        }

        UpgradeRow CreateRow(Transform parent)
        {
            var r = new UpgradeRow();
            r.rt = UIFactory.Rect("Row", parent);
            var le = r.rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RowH;
            var bg = UIFactory.Panel("Bg", r.rt, Color.white);
            bg.rectTransform.Stretch();
            var iconBg = UIFactory.Image("IconBg", r.rt, C.spriteCircle, new Color32(255, 236, 200, 255));
            iconBg.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, 0), new Vector2(132, 132));
            r.icon = UIFactory.Image("Icon", iconBg.rectTransform, C.iconSpeed, Color.white);
            r.icon.rectTransform.Stretch(18, 18, 18, 18);
            r.title = UIFactory.Text("Title", r.rt, "", 42, UIFactory.Ink, TextAlignmentOptions.Left);
            r.title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -18), new Vector2(460, 54));
            r.desc = UIFactory.Text("Desc", r.rt, "", 30, new Color32(130, 100, 80, 255), TextAlignmentOptions.Left);
            r.desc.fontSizeMin = 20;
            r.desc.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -70), new Vector2(460, 40));
            r.stars = new Image[3];
            for (int i = 0; i < r.stars.Length; i++)
            {
                r.stars[i] = UIFactory.Image("Star" + i, r.rt, C.iconStar, Color.white);
                r.stars[i].rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(178 + i * 46, -68), new Vector2(44, 44));
            }
            r.pips = new Image[Mathf.Max(Economy.MaxUpgradeLevel, Stations.LevelsPerStar)];
            for (int i = 0; i < r.pips.Length; i++)
            {
                var p = UIFactory.Panel("Pip" + i, r.rt, new Color32(225, 215, 200, 255));
                p.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(178 + i * 70, 22), new Vector2(60, 26));
                r.pips[i] = p;
            }
            r.level = UIFactory.Text("Lv", r.rt, "", 30, UIFactory.Ink, TextAlignmentOptions.Left);
            r.level.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(538, 14), new Vector2(120, 40));
            var row = r;
            r.buy = UIFactory.Button("Buy", r.rt, UIFactory.Green, UIFactory.GreenDark, () => Buy(row), out var face);
            ((RectTransform)r.buy.transform).Anchor(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(250, 124));
            r.coin = UIFactory.Image("Coin", face, C.iconCoin, Color.white);
            r.coin.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(64, 64));
            r.medal = UIFactory.Image("Medal", face, C.iconMedal, Color.white);
            r.medal.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 2), new Vector2(70, 70));
            r.cost = UIFactory.Text("Cost", face, "", 46, Color.white, TextAlignmentOptions.Center, true);
            r.cost.rectTransform.Stretch(80, 10, 0, 0);
            return r;
        }

        static readonly UpgradeId[] PlayerUps = { UpgradeId.Speed, UpgradeId.Capacity, UpgradeId.Profit };
        static readonly UpgradeId[] HelperUps = { UpgradeId.HelperSpeed, UpgradeId.HelperCapacity, UpgradeId.Cashier };

        public void OpenSheet(PanelKind k)
        {
            sheetKind = k;
            if (k != PanelKind.Upgrades) stationsTab = false;
            LayoutSheet();
            sheet.gameObject.SetActive(true);
            Tween.KillOwner(sheet);
            float h = sheet.sizeDelta.y;
            sheet.anchoredPosition = new Vector2(0, -h);
            var s = sheet;
            Tween.Run(0.35f, t => { if (s) s.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-h, 30, t)); }, Ease.OutBack, null, s, 0f, true);
            AudioManager.Play(Sfx.Open, 0.6f);
            Economy.MoneyChanged -= OnMoneyChanged;
            Economy.MoneyChanged += OnMoneyChanged;
            Stations.MedalsChanged -= OnMoneyChanged;
            Stations.MedalsChanged += OnMoneyChanged;
        }

        void SetTab(bool stations)
        {
            if (stationsTab == stations) return;
            stationsTab = stations;
            LayoutSheet();
        }

        /// <summary>Row binding, tab look and sheet height for the current panel/tab.</summary>
        void LayoutSheet()
        {
            bool upgrades = sheetKind == PanelKind.Upgrades;
            bool tabs = upgrades;
            sheetTitle.text = Loc.T(upgrades ? "upg.title" : "helpers.title");
            tabHero.gameObject.SetActive(tabs);
            tabStations.gameObject.SetActive(tabs);
            tabHeroLabel.text = Loc.T("tab.hero");
            tabStationsLabel.text = Loc.T("tab.stations");
            UIFactory.SetButtonColors(tabHero, !stationsTab ? UIFactory.Orange : UIFactory.Grey, !stationsTab ? UIFactory.OrangeDark : UIFactory.GreyDark);
            UIFactory.SetButtonColors(tabStations, stationsTab ? UIFactory.Orange : UIFactory.Grey, stationsTab ? UIFactory.OrangeDark : UIFactory.GreyDark);

            int count;
            if (upgrades && stationsTab)
            {
                count = Stations.All.Length;
                for (int i = 0; i < count; i++) { rows[i].isStation = true; rows[i].station = Stations.All[i]; }
            }
            else
            {
                var ids = upgrades ? PlayerUps : HelperUps;
                count = ids.Length;
                for (int i = 0; i < count; i++) { rows[i].isStation = false; rows[i].id = ids[i]; }
            }
            for (int i = 0; i < rows.Count; i++) rows[i].rt.gameObject.SetActive(i < count);
            float top = tabs ? 160f : 80f;
            rowsRoot.offsetMax = new Vector2(-30, -top);
            float h = top + count * RowH + (count - 1) * RowGap + 30f;
            sheet.sizeDelta = new Vector2(1000, h);
            RefreshRows();
        }

        void OnMoneyChanged(long v, long d) { if (sheetKind.HasValue) RefreshRows(); }

        public void CloseSheet(PanelKind k)
        {
            if (sheetKind != k) return;
            sheetKind = null;
            Economy.MoneyChanged -= OnMoneyChanged;
            Stations.MedalsChanged -= OnMoneyChanged;
            var s = sheet;
            float h = sheet.sizeDelta.y;
            Tween.KillOwner(s);
            Tween.Run(0.22f, t => { if (s) s.anchoredPosition = new Vector2(0, Mathf.Lerp(30, -h - 20, t)); }, Ease.InQuad,
                () => { if (s && !sheetKind.HasValue) s.gameObject.SetActive(false); }, s, 0f, true);
            AudioManager.Play(Sfx.Close, 0.5f);
        }

        Sprite IconFor(UpgradeId id) => id switch
        {
            UpgradeId.Speed => C.iconSpeed,
            UpgradeId.Capacity => C.iconBag,
            UpgradeId.Profit => C.iconProfit,
            UpgradeId.HelperSpeed => C.iconSpeed,
            UpgradeId.HelperCapacity => C.iconBag,
            _ => C.iconCashier
        };

        static string Key(UpgradeId id) => id switch
        {
            UpgradeId.Speed => "upg.speed",
            UpgradeId.Capacity => "upg.capacity",
            UpgradeId.Profit => "upg.profit",
            UpgradeId.HelperSpeed => "upg.h_speed",
            UpgradeId.HelperCapacity => "upg.h_capacity",
            _ => "upg.h_cashier"
        };

        static readonly Color32 PipOff = new Color32(225, 215, 200, 255);
        static readonly Color32 StarOff = new Color32(90, 70, 60, 70);

        void RefreshRows()
        {
            foreach (var r in rows)
            {
                if (!r.rt.gameObject.activeSelf) continue;
                if (r.isStation) RefreshStationRow(r);
                else RefreshUpgradeRow(r);
            }
            if (tabDot != null) tabDot.gameObject.SetActive(!stationsTab && AnyStationAffordable());
        }

        void RefreshUpgradeRow(UpgradeRow r)
        {
            int lv = Economy.Level(r.id);
            int cost = Economy.Cost(r.id);
            r.title.text = Loc.T(Key(r.id));
            r.desc.text = Loc.T(Key(r.id) + ".d");
            r.desc.rectTransform.anchoredPosition = new Vector2(178, -70);
            r.desc.rectTransform.sizeDelta = new Vector2(460, 40);
            foreach (var s in r.stars) s.gameObject.SetActive(false);
            r.icon.sprite = IconFor(r.id);
            for (int i = 0; i < r.pips.Length; i++)
            {
                r.pips[i].gameObject.SetActive(i < Economy.MaxUpgradeLevel);
                r.pips[i].color = i < lv ? (Color)UIFactory.Green : PipOff;
            }
            r.level.text = Loc.F("upg.level", lv + 1);
            bool maxed = cost < 0;
            bool afford = !maxed && Economy.Money >= cost;
            SetBuy(r, maxed ? Loc.T("upg.maxed") : Economy.Format(cost), !maxed, false, afford, false);
        }

        void RefreshStationRow(UpgradeRow r)
        {
            var k = r.station;
            r.icon.sprite = Stations.Icon(k);
            r.title.text = Loc.T("st." + k);
            int stars = Stations.Stars(k);
            for (int i = 0; i < r.stars.Length; i++)
            {
                r.stars[i].gameObject.SetActive(true);
                r.stars[i].color = i < stars ? Color.white : (Color)StarOff;
            }
            r.desc.rectTransform.anchoredPosition = new Vector2(178 + 3 * 46 + 10, -70);
            r.desc.rectTransform.sizeDelta = new Vector2(320, 40);
            int lv = Stations.Level(k);
            for (int i = 0; i < r.pips.Length; i++)
            {
                r.pips[i].gameObject.SetActive(i < Stations.LevelsPerStar);
                r.pips[i].color = i < lv ? (Color)UIFactory.Gold : PipOff;
            }
            if (!Stations.Built(k))
            {
                var pad = ProgressionManager.I != null ? ProgressionManager.I.Find(Stations.RequiredPad(k)) : null;
                r.desc.text = Loc.F("st.locked", pad != null ? Loc.T(pad.titleKey) : "");
                r.level.text = "";
                SetBuy(r, "", false, false, false, true);
                return;
            }
            float speed = Stations.SpeedMul(k);
            r.desc.text = k == StationKind.Checkout
                ? Loc.F("st.desc.checkout", speed.ToString("0.0"), Mathf.RoundToInt(GameConfig.I.balance.checkoutPricePerStar * 100 * stars))
                : Loc.F("st.desc", speed.ToString("0.0"), (1f + GameConfig.I.balance.stationPricePerStar * stars).ToString("0.0"));
            r.level.text = Loc.F("upg.level", stars * Stations.LevelsPerStar + lv);
            if (Stations.Maxed(k)) { SetBuy(r, Loc.T("upg.maxed"), false, false, false, false); return; }
            int starCost = Stations.StarCost(k);
            if (starCost > 0)
            {
                bool ok = Stations.Medals >= starCost;
                SetBuy(r, starCost.ToString(), false, true, ok, false, true);
                return;
            }
            int cost = Stations.LevelCost(k);
            SetBuy(r, Economy.Format(cost), true, false, Economy.Money >= cost, false);
        }

        void SetBuy(UpgradeRow r, string text, bool coin, bool medal, bool active, bool locked, bool star = false)
        {
            r.cost.text = locked ? "" : text;
            r.coin.gameObject.SetActive(coin);
            r.medal.gameObject.SetActive(medal);
            r.cost.rectTransform.offsetMin = new Vector2(coin || medal ? 80 : 10, 0);
            r.buy.interactable = active;
            Color face = active ? (star ? UIFactory.Gold : (Color)UIFactory.Green) : UIFactory.Grey;
            Color lip = active ? (star ? (Color)UIFactory.OrangeDark : UIFactory.GreenDark) : UIFactory.GreyDark;
            UIFactory.SetButtonColors(r.buy, face, lip);
            if (locked)
            {
                r.medal.gameObject.SetActive(false);
                r.coin.gameObject.SetActive(false);
                r.cost.text = Loc.T("st.soon");
            }
        }

        static bool AnyStationAffordable()
        {
            foreach (var k in Stations.All)
            {
                if (!Stations.Built(k) || Stations.Maxed(k)) continue;
                int sc = Stations.StarCost(k);
                if (sc > 0 ? Stations.Medals >= sc : Economy.Money >= Stations.LevelCost(k)) return true;
            }
            return false;
        }

        void Buy(UpgradeRow r)
        {
            bool ok;
            if (r.isStation)
            {
                bool star = Stations.StarCost(r.station) > 0;
                ok = star ? Stations.TryStar(r.station) : Stations.TryLevel(r.station);
                if (ok && star)
                {
                    Feedback.Toast(Loc.F("toast.star", Loc.T("st." + r.station)), C.iconStar);
                    if (PlayerController.I) Fx.Confetti(PlayerController.I.transform.position + Vector3.up * 2.5f, 1f);
                }
            }
            else
            {
                ok = Economy.TryUpgrade(r.id);
                if (ok) GameEvents.Log($"upgrade {r.id} -> {Economy.Level(r.id)}");
            }
            if (ok)
            {
                AudioManager.Play(Sfx.Upgrade, 0.8f);
                Tween.UIPunch(r.rt, 0.06f, 0.2f);
                if (PlayerController.I) Fx.Sparkles(PlayerController.I.transform.position + Vector3.up, 0.8f);
                SaveSystem.Save();
            }
            else AudioManager.Play(Sfx.Error, 0.5f);
            RefreshRows();
        }

        // ================================================================ modal helpers
        RectTransform Modal(string name, Vector2 size, string titleKey, out TextMeshProUGUI title)
        {
            var m = UIFactory.Rect(name, root).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var sh = UIFactory.Panel("Shadow", m, new Color(0, 0, 0, 0.3f));
            sh.rectTransform.Stretch(-4, -4, 12, -16);
            var bg = UIFactory.Panel("Bg", m, UIFactory.Cream);
            bg.rectTransform.Stretch();
            var header = UIFactory.Panel("Header", m, UIFactory.Orange);
            header.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(Mathf.Min(size.x - 120, 620), 110));
            title = UIFactory.Text("Title", header.rectTransform, Loc.T(titleKey), 56, Color.white, TextAlignmentOptions.Center, true);
            title.rectTransform.Stretch(0, 0, 0, 8);
            m.gameObject.SetActive(false);
            return m;
        }

        void ShowModal(RectTransform m)
        {
            ModalOpen = true;
            InputReader.Blocked = true;
            dim.gameObject.SetActive(true);
            dim.SetAsLastSibling();
            m.SetAsLastSibling();
            dimGroup.alpha = 0f;
            Tween.Fade(dimGroup, 1f, 0.2f);
            m.gameObject.SetActive(true);
            m.localScale = Vector3.zero;
            Tween.Run(0.35f, t => { if (m) m.localScale = Vector3.one * t; }, Ease.OutBack, null, m, 0f, true);
            Time.timeScale = 0f;
            AudioManager.Play(Sfx.Open, 0.6f);
        }

        void HideModal(RectTransform m, Action after = null)
        {
            Tween.Run(0.18f, t => { if (m) m.localScale = Vector3.one * (1f - t); }, Ease.InQuad, () =>
            {
                if (m) m.gameObject.SetActive(false);
                bool other = settings.gameObject.activeSelf || confirm.gameObject.activeSelf || levelUp.gameObject.activeSelf
                             || demo.gameObject.activeSelf || offline.gameObject.activeSelf || chapter.gameObject.activeSelf;
                if (!other)
                {
                    ModalOpen = false;
                    InputReader.Blocked = false;
                    Time.timeScale = 1f;
                    Tween.Fade(dimGroup, 0f, 0.15f, () => { if (!ModalOpen) dim.gameObject.SetActive(false); });
                }
                after?.Invoke();
            }, m, 0f, true);
            AudioManager.Play(Sfx.Close, 0.5f);
        }

        Button BigButton(Transform parent, string text, Color face, Color lip, Vector2 pos, Vector2 size, Action click, out TextMeshProUGUI label)
        {
            var b = UIFactory.Button("Btn", parent, face, lip, click, out var f);
            ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            label = UIFactory.Text("Label", f, text, 46, Color.white, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch(10, 10, 0, 4);
            return b;
        }

        // ================================================================ settings
        TextMeshProUGUI musicVal, soundVal, langVal;
        Button musicBtn, soundBtn;
        readonly List<(TextMeshProUGUI t, string key)> settingLabels = new List<(TextMeshProUGUI, string)>();
        TextMeshProUGUI settingsTitle;

        void BuildSettings()
        {
            settings = Modal("Settings", new Vector2(860, 1120), "set.title", out settingsTitle);
            float y = 300;
            musicBtn = SettingRow(settings, "set.music", C.iconMusic, ref y, () => { GameSettings.Music = !GameSettings.Music; RefreshSettings(); }, out musicVal);
            soundBtn = SettingRow(settings, "set.sound", C.iconSound, ref y, () => { GameSettings.Sound = !GameSettings.Sound; RefreshSettings(); }, out soundVal);
            SettingRow(settings, "set.lang", C.iconStar, ref y, () =>
            {
                GameSettings.Language = GameSettings.Language == Lang.EN ? Lang.RU : Lang.EN;
                RefreshSettings();
            }, out langVal);
            BigButton(settings, Loc.T("set.tutorial"), UIFactory.Sky, new Color32(40, 110, 200, 255), new Vector2(0, -150), new Vector2(620, 120), () =>
            {
                HideModal(settings, () => Tutorial.I?.Restart());
            }, out var tl);
            settingLabels.Add((tl, "set.tutorial"));
            BigButton(settings, Loc.T("set.reset"), UIFactory.Red, UIFactory.RedDark, new Vector2(0, -290), new Vector2(620, 120), () =>
            {
                ShowModal(confirm);
            }, out var rl);
            settingLabels.Add((rl, "set.reset"));
            BigButton(settings, Loc.T("set.close"), UIFactory.Green, UIFactory.GreenDark, new Vector2(0, -450), new Vector2(420, 120), () => HideModal(settings), out var cl);
            settingLabels.Add((cl, "set.close"));
            var ver = UIFactory.Text("Version", settings, VersionInfo.Label, 28, new Color32(150, 120, 100, 255), TextAlignmentOptions.Center);
            ver.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(700, 40));
        }

        Button SettingRow(RectTransform parent, string key, Sprite icon, ref float y, Action toggle, out TextMeshProUGUI value)
        {
            var row = UIFactory.Rect("Row_" + key, parent).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(720, 120));
            var bg = UIFactory.Panel("Bg", row, Color.white);
            bg.rectTransform.Stretch();
            var ic = UIFactory.Image("Icon", row, icon, Color.white);
            ic.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(76, 76));
            var label = UIFactory.Text("Label", row, Loc.T(key), 44, UIFactory.Ink, TextAlignmentOptions.Left);
            label.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(330, 80));
            settingLabels.Add((label, key));
            var b = UIFactory.Button("Toggle", row, UIFactory.Green, UIFactory.GreenDark, toggle, out var f);
            ((RectTransform)b.transform).Anchor(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(240, 96));
            value = UIFactory.Text("Value", f, "", 40, Color.white, TextAlignmentOptions.Center, true);
            value.rectTransform.Stretch(6, 6, 0, 2);
            y -= 140;
            return b;
        }

        void RefreshSettings()
        {
            musicVal.text = Loc.T(GameSettings.Music ? "set.on" : "set.off");
            soundVal.text = Loc.T(GameSettings.Sound ? "set.on" : "set.off");
            langVal.text = Loc.T("set.lang.value");
            UIFactory.SetButtonColors(musicBtn, GameSettings.Music ? UIFactory.Green : UIFactory.Grey, GameSettings.Music ? UIFactory.GreenDark : UIFactory.GreyDark);
            UIFactory.SetButtonColors(soundBtn, GameSettings.Sound ? UIFactory.Green : UIFactory.Grey, GameSettings.Sound ? UIFactory.GreenDark : UIFactory.GreyDark);
            settingsTitle.text = Loc.T("set.title");
            foreach (var (t, k) in settingLabels) t.text = Loc.T(k);
        }

        public void OpenSettings()
        {
            if (ModalOpen) return;
            RefreshSettings();
            ShowModal(settings);
        }

        // ================================================================ confirm reset
        TextMeshProUGUI confirmText, confirmYes, confirmNo, confirmTitle;
        void BuildConfirm()
        {
            confirm = Modal("Confirm", new Vector2(820, 640), "set.reset", out confirmTitle);
            confirmText = UIFactory.Text("Text", confirm, "", 42, UIFactory.Ink, TextAlignmentOptions.Center);
            confirmText.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(700, 200));
            confirmText.textWrappingMode = TextWrappingModes.Normal;
            BigButton(confirm, "", UIFactory.Red, UIFactory.RedDark, new Vector2(0, -90), new Vector2(560, 116), () =>
            {
                Game.I.ResetProgress();
            }, out confirmYes);
            BigButton(confirm, "", UIFactory.Green, UIFactory.GreenDark, new Vector2(0, -230), new Vector2(560, 116), () => HideModal(confirm), out confirmNo);
        }

        void OnEnable() { Loc.Changed += RelabelConfirm; }
        void OnDisable() { Loc.Changed -= RelabelConfirm; }
        void RelabelConfirm()
        {
            if (confirmText == null) return;
            confirmTitle.text = Loc.T("set.reset");
            confirmText.text = Loc.T("set.reset.confirm");
            confirmYes.text = Loc.T("set.yes");
            confirmNo.text = Loc.T("set.no");
        }

        // ================================================================ level up: reward + pick one of three perk cards
        TextMeshProUGUI levelTitle, levelReward, levelPick, rerollLabel;
        Button rerollBtn;
        int pendingReward, pendingLevel;
        bool rerollUsed, cardPicked;
        PerkCard[] offered;
        readonly List<(Button b, Image strip, Image icon, TextMeshProUGUI title, TextMeshProUGUI desc, TextMeshProUGUI rarity)> cardViews
            = new List<(Button, Image, Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)>();

        static readonly Color32[] RarityColor = { new Color32(120, 190, 90, 255), new Color32(70, 150, 240, 255), new Color32(180, 90, 230, 255) };
        static readonly Color32[] RarityDark = { new Color32(80, 140, 60, 255), new Color32(40, 100, 190, 255), new Color32(120, 50, 170, 255) };

        public bool LevelUpOpen => levelUp != null && levelUp.gameObject.activeSelf && !cardPicked;

        void BuildLevelUp()
        {
            levelUp = Modal("LevelUp", new Vector2(940, 1190), "levelup.title", out levelTitle);
            var star = UIFactory.Image("Star", levelUp, C.iconStar, Color.white);
            star.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-320, -150), new Vector2(110, 110));
            star.gameObject.AddComponent<Spin>().speed = 25f;
            levelReward = UIFactory.Text("Reward", levelUp, "", 46, UIFactory.Ink, TextAlignmentOptions.Center);
            levelReward.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(40, -150), new Vector2(600, 100));
            levelPick = UIFactory.Text("Pick", levelUp, "", 40, new Color32(130, 100, 80, 255), TextAlignmentOptions.Center);
            levelPick.rectTransform.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(820, 60));
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = UIFactory.Button("Card" + i, levelUp, Color.white, new Color32(215, 200, 180, 255), () => PickCard(idx), out var face);
                ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -400 - i * 240), new Vector2(840, 210));
                var strip = UIFactory.Panel("Strip", face, RarityColor[0]);
                strip.rectTransform.Stretch(0, 814, 0, 0);
                var iconBg = UIFactory.Image("IconBg", face, C.spriteCircle, new Color32(255, 236, 200, 255));
                iconBg.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120, 0), new Vector2(150, 150));
                var icon = UIFactory.Image("Icon", iconBg.rectTransform, C.iconStar, Color.white);
                icon.rectTransform.Stretch(22, 22, 22, 22);
                var title = UIFactory.Text("Title", face, "", 44, UIFactory.Ink, TextAlignmentOptions.Left);
                title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(220, -26), new Vector2(580, 56));
                var desc = UIFactory.Text("Desc", face, "", 36, new Color32(110, 85, 65, 255), TextAlignmentOptions.Left);
                desc.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(220, -86), new Vector2(580, 50));
                var rarity = UIFactory.Text("Rarity", face, "", 30, RarityColor[0], TextAlignmentOptions.Left, true);
                rarity.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(220, 18), new Vector2(400, 44));
                cardViews.Add((b, strip, icon, title, desc, rarity));
            }
            rerollBtn = BigButton(levelUp, "", UIFactory.Sky, new Color32(40, 110, 200, 255), new Vector2(0, -505), new Vector2(520, 110), Reroll, out rerollLabel);
        }

        public void ShowLevelUp(int level, int reward)
        {
            pendingReward = reward;
            pendingLevel = level;
            rerollUsed = false;
            cardPicked = false;
            levelTitle.text = Loc.F("levelup.title", level);
            levelReward.text = Loc.F("levelup.reward2", Economy.Format(reward), GameConfig.I.balance.medalsPerLevel);
            levelPick.text = Loc.T("levelup.pick");
            rerollLabel.text = Loc.T("levelup.reroll");
            offered = Perks.Roll(level);
            FillCards();
            rerollBtn.gameObject.SetActive(true);
            AudioManager.Play(Sfx.LevelUp, 0.9f);
            if (PlayerController.I) Fx.Confetti(PlayerController.I.transform.position + Vector3.up * 3f, 1.5f);
            Tween.Delay(0.35f, () => ShowModal(levelUp), this, true);
        }

        void FillCards()
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                var v = cardViews[i];
                var c = offered[i];
                int r = (int)c.rarity;
                v.strip.color = RarityColor[r];
                v.icon.sprite = Perks.Icon(c.id);
                v.title.text = Loc.T("perk." + c.id);
                v.desc.text = Perks.Describe(c);
                v.rarity.text = Loc.T("rarity." + c.rarity);
                v.rarity.color = RarityColor[r];
                UIFactory.SetButtonColors(v.b, Color.white, RarityDark[r]);
                v.b.interactable = true;
                var rt = (RectTransform)v.b.transform;
                rt.localScale = Vector3.zero;
                Tween.Run(0.3f, k => { if (rt) rt.localScale = Vector3.one * k; }, Ease.OutBack, null, rt, 0.15f + i * 0.08f, true);
            }
        }

        void Reroll()
        {
            if (rerollUsed || cardPicked) return;
            rerollUsed = true;
            offered = Perks.Roll(pendingLevel);
            FillCards();
            rerollBtn.gameObject.SetActive(false);
            AudioManager.Play(Sfx.Whoosh, 0.6f);
        }

        /// <summary>Takes a card, pays the level reward and closes. Also used by the autotest bot.</summary>
        public void PickCard(int i)
        {
            if (cardPicked || offered == null || i < 0 || i >= offered.Length) return;
            cardPicked = true;
            foreach (var v in cardViews) v.b.interactable = false;
            Perks.Take(offered[i]);
            Tween.UIPunch((RectTransform)cardViews[i].b.transform, 0.12f, 0.25f);
            AudioManager.Play(Sfx.Upgrade, 0.9f);
            int level = pendingLevel;
            Tween.Delay(0.35f, () =>
            {
                HideModal(levelUp, () => Progress.Claimed(level));
                if (pendingReward > 0)
                {
                    Economy.Add(pendingReward);
                    if (PlayerController.I) Fx.CoinBurst(PlayerController.I.transform.position + Vector3.up, 1.2f);
                    AudioManager.Play(Sfx.CoinsCollect, 1f);
                    pendingReward = 0;
                }
            }, this, true);
        }

        /// <summary>Index of the strongest offered card by a simple preference (for the autotest bot).</summary>
        public int BestCard()
        {
            if (offered == null) return 0;
            int best = 0;
            for (int i = 1; i < offered.Length; i++) if (offered[i].rarity > offered[best].rarity) best = i;
            return best;
        }

        // ================================================================ chapter chest
        TextMeshProUGUI chapterTitle, chapterText, chapterBtn;
        void BuildChapter()
        {
            chapter = Modal("Chapter", new Vector2(860, 820), "chapter.title", out chapterTitle);
            var chest = UIFactory.Image("Chest", chapter, C.iconChest, Color.white);
            chest.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(260, 260));
            chapterText = UIFactory.Text("Text", chapter, "", 46, UIFactory.Ink, TextAlignmentOptions.Center);
            chapterText.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(760, 130));
            chapterText.textWrappingMode = TextWrappingModes.Normal;
            BigButton(chapter, "", UIFactory.Green, UIFactory.GreenDark, new Vector2(0, -270), new Vector2(520, 130), () =>
            {
                HideModal(chapter);
                if (PlayerController.I) Fx.CoinBurst(PlayerController.I.transform.position + Vector3.up, 1.4f);
                AudioManager.Play(Sfx.CoinsCollect, 1f);
            }, out chapterBtn);
        }

        public void ShowChapter(int n)
        {
            chapterTitle.text = Loc.F("chapter.title", n);
            chapterText.text = Loc.F("chapter.text", Economy.Format(Quests.ChestCoinsFor(n)), Quests.ChestMedalsFor(n));
            chapterBtn.text = Loc.T("offline.collect");
            AudioManager.Play(Sfx.LevelUp, 1f);
            if (PlayerController.I) Fx.Confetti(PlayerController.I.transform.position + Vector3.up * 3f, 1.5f);
            Tween.Delay(0.4f, () => ShowModal(chapter), this, true);
        }

        // ================================================================ demo complete
        TextMeshProUGUI demoTitle, demoText, demoStats, demoCont, demoRestart;
        void BuildDemo()
        {
            demo = Modal("Demo", new Vector2(900, 900), "demo.title", out demoTitle);
            demoText = UIFactory.Text("Text", demo, "", 44, UIFactory.Ink, TextAlignmentOptions.Center);
            demoText.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(780, 180));
            demoText.textWrappingMode = TextWrappingModes.Normal;
            demoStats = UIFactory.Text("Stats", demo, "", 34, new Color32(130, 100, 80, 255), TextAlignmentOptions.Center);
            demoStats.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 120));
            demoStats.textWrappingMode = TextWrappingModes.Normal;
            BigButton(demo, "", UIFactory.Green, UIFactory.GreenDark, new Vector2(0, -130), new Vector2(560, 124), () => HideModal(demo), out demoCont);
            BigButton(demo, "", UIFactory.Orange, UIFactory.OrangeDark, new Vector2(0, -280), new Vector2(560, 124), () => Game.I.ResetProgress(), out demoRestart);
        }

        public void ShowDemoComplete()
        {
            demoTitle.text = Loc.T("demo.title");
            demoText.text = Loc.T("demo.text");
            var d = SaveSystem.Data;
            demoStats.text = Loc.F("demo.stats", Economy.FormatTime(d.playTime), d.customersServed, Economy.Format(d.totalEarned));
            demoCont.text = Loc.T("demo.continue");
            demoRestart.text = Loc.T("demo.restart");
            AudioManager.Play(Sfx.LevelUp, 1f);
            if (PlayerController.I) Fx.Play(C.fxFireworks, PlayerController.I.transform.position + Vector3.up * 4f, 1f, 6f);
            GameEvents.Log("demo complete");
            ShowModal(demo);
        }

        // ================================================================ offline
        TextMeshProUGUI offTitle, offText, offAmount, offBtn;
        long offlineAmount;
        void BuildOffline()
        {
            offline = Modal("Offline", new Vector2(860, 760), "offline.title", out offTitle);
            offText = UIFactory.Text("Text", offline, "", 40, UIFactory.Ink, TextAlignmentOptions.Center);
            offText.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(760, 120));
            offText.textWrappingMode = TextWrappingModes.Normal;
            var coin = UIFactory.Image("Coin", offline, C.iconCoin, Color.white);
            coin.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, 10), new Vector2(130, 130));
            offAmount = UIFactory.Text("Amount", offline, "", 84, UIFactory.Ink, TextAlignmentOptions.Left);
            offAmount.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-30, 10), new Vector2(400, 120));
            BigButton(offline, "", UIFactory.Green, UIFactory.GreenDark, new Vector2(0, -220), new Vector2(520, 130), () =>
            {
                HideModal(offline);
                Economy.Add(offlineAmount);
                AudioManager.Play(Sfx.CoinsCollect, 1f);
                offlineAmount = 0;
            }, out offBtn);
        }

        public void ShowOffline(long amount)
        {
            offlineAmount = amount;
            offTitle.text = Loc.T("offline.title");
            offText.text = Loc.T("offline.text");
            offAmount.text = Economy.Format(amount);
            offBtn.text = Loc.T("offline.collect");
            ShowModal(offline);
        }

        void Start() { RelabelConfirm(); }
    }

    public class Spin : MonoBehaviour
    {
        public float speed = 30f;
        void Update() { transform.Rotate(0, 0, speed * Time.unscaledDeltaTime); }
    }
}
