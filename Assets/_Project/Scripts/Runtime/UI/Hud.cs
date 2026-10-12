using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>
    /// Builds and runs the whole screen-space UI from code: money, market level, objective, toasts,
    /// floating numbers, joystick, off-screen guide arrow, MAX label, version stamp and all panels.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }

        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, safe, floatLayer;
        TextMeshProUGUI moneyText, levelText, levelNum, objectiveText, versionText, maxText;
        int fpsShown = -1, tierShown = -1;
        RectTransform medalPill, medalIcon;
        TextMeshProUGUI medalText, levelXp;
        RectTransform moneyPill, coinIcon, objectiveBox, levelBlock, maxLabel, offArrow;
        Image levelFill, objectiveIcon, offArrowIcon;
        CanvasGroup objectiveGroup;
        long shownMoney;
        float moneyAnimFrom, moneyAnimTo, moneyAnimT = 1f;
        string currentObjective;
        readonly Queue<TextMeshProUGUI> floatPool = new Queue<TextMeshProUGUI>();
        readonly List<(TextMeshProUGUI t, Vector3 world, float age, float life)> floats = new List<(TextMeshProUGUI, Vector3, float, float)>();
        RectTransform toastRt;
        TextMeshProUGUI toastText;
        Image toastIcon;
        CanvasGroup toastGroup;
        float toastTimer;
        Panels panels;

        GameConfig C => GameConfig.I;

        void Awake()
        {
            I = this;
            Build();
            panels = gameObject.AddComponent<Panels>();
            panels.Build(safe, root);
            Feedback.FloatTextHandler = FloatText;
            Feedback.ToastHandler = Toast;
            Economy.MoneyChanged += OnMoney;
            Stations.MedalsChanged += OnMedals;
            Quests.ChapterDone += OnChapterDone;
            Loc.Changed += Relabel;
        }

        void OnDestroy()
        {
            Economy.MoneyChanged -= OnMoney;
            Stations.MedalsChanged -= OnMedals;
            Quests.ChapterDone -= OnChapterDone;
            Loc.Changed -= Relabel;
            if (I == this) I = null;
        }

        void Start()
        {
            shownMoney = Economy.Money;
            moneyText.text = Economy.Format(shownMoney);
            medalText.text = Stations.Medals.ToString();
            medalPill.gameObject.SetActive(Stations.Medals > 0 || Quests.Index > 0);
            Relabel();
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform.parent, false);
            }

            safe = UIFactory.Rect("Safe", root).Stretch();
            safe.gameObject.AddComponent<SafeArea>();

            floatLayer = UIFactory.Rect("Floats", safe).Stretch();

            BuildTopBar();
            BuildObjective();
            BuildQuestCard();
            BuildToast();
            BuildOffscreenArrow();
            BuildMaxLabel();

            var joy = UIFactory.Rect("Joystick", safe).Stretch();
            joy.gameObject.AddComponent<VirtualJoystick>().Build(joy);

            versionText = UIFactory.Text("Version", safe, VersionInfo.Label, 26, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomLeft);
            versionText.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 10), new Vector2(600, 40));
        }

        void BuildTopBar()
        {
            // level block (top-left)
            levelBlock = UIFactory.Rect("Level", safe).Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -26), new Vector2(430, 110));
            var bar = UIFactory.Panel("Bar", levelBlock, new Color(0, 0, 0, 0.38f));
            bar.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, -18), new Vector2(360, 40));
            levelFill = UIFactory.Panel("Fill", bar.rectTransform, UIFactory.Green);
            levelFill.rectTransform.Stretch(5, 5, 5, 5);
            levelFill.type = UnityEngine.UI.Image.Type.Sliced;
            var fillMask = levelFill.gameObject.AddComponent<FillBar>();
            fillMask.Init(levelFill.rectTransform);
            levelXp = UIFactory.Text("Xp", bar.rectTransform, "", 26, Color.white, TextAlignmentOptions.Center, true);
            levelXp.rectTransform.Stretch(0, 0, 0, 2);
            levelText = UIFactory.Text("Label", levelBlock, "", 34, Color.white, TextAlignmentOptions.Left, true);
            levelText.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 24), new Vector2(330, 46));
            var badge = UIFactory.Image("Badge", levelBlock, C.iconStar, Color.white);
            badge.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56, 0), new Vector2(112, 112));
            levelNum = UIFactory.Text("Num", badge.rectTransform, "1", 44, UIFactory.Ink, TextAlignmentOptions.Center);
            levelNum.rectTransform.Stretch(0, 0, 8, 0);

            // money pill (top-centre)
            moneyPill = UIFactory.Rect("Money", safe).Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, -30), new Vector2(300, 96));
            var pillBg = UIFactory.Panel("Bg", moneyPill, new Color(0, 0, 0, 0.42f));
            pillBg.rectTransform.Stretch();
            var coin = UIFactory.Image("Coin", moneyPill, C.iconCoin, Color.white);
            coinIcon = coin.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18, 0), new Vector2(112, 112));
            moneyText = UIFactory.Text("Value", moneyPill, "0", 62, Color.white, TextAlignmentOptions.Center, true);
            moneyText.rectTransform.Stretch(70, 10, 0, 4);

            // medals (left of the gear)
            medalPill = UIFactory.Rect("Medals", safe).Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-142, -36), new Vector2(180, 84));
            var mBg = UIFactory.Panel("Bg", medalPill, new Color(0, 0, 0, 0.42f));
            mBg.rectTransform.Stretch();
            var medal = UIFactory.Image("Medal", medalPill, C.iconMedal, Color.white);
            medalIcon = medal.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14, 4), new Vector2(96, 96));
            medalText = UIFactory.Text("Value", medalPill, "0", 52, Color.white, TextAlignmentOptions.Center, true);
            medalText.rectTransform.Stretch(66, 8, 0, 4);
            medalPill.gameObject.SetActive(false);

            // settings (top-right)
            var gear = UIFactory.Button("Settings", safe, Color.white, new Color(0.75f, 0.75f, 0.8f), () => UIPanels.OpenSettings(), out var face);
            ((RectTransform)gear.transform).Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -28), new Vector2(104, 108));
            var gi = UIFactory.Image("Icon", face, C.iconGear, UIFactory.Ink);
            gi.rectTransform.Stretch(16, 16, 14, 16);
        }

        void BuildObjective()
        {
            objectiveBox = UIFactory.Rect("Objective", safe).Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -158), new Vector2(760, 92));
            objectiveGroup = objectiveBox.gameObject.AddComponent<CanvasGroup>();
            var sh = UIFactory.Panel("Shadow", objectiveBox, new Color(0, 0, 0, 0.18f));
            sh.rectTransform.Stretch(-2, -2, 8, -8);
            var bg = UIFactory.Panel("Bg", objectiveBox, Color.white);
            bg.rectTransform.Stretch();
            objectiveIcon = UIFactory.Image("Icon", objectiveBox, C.iconArrow, Color.white);
            objectiveIcon.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(74, 74));
            objectiveText = UIFactory.Text("Text", objectiveBox, "", 38, UIFactory.Ink, TextAlignmentOptions.Left);
            objectiveText.rectTransform.Stretch(100, 24, 0, 0);
            objectiveGroup.alpha = 0f;
        }

        void BuildToast()
        {
            toastRt = UIFactory.Rect("Toast", safe).Anchor(new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 110));
            toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
            var bg = UIFactory.Panel("Bg", toastRt, new Color(0.18f, 0.12f, 0.08f, 0.82f));
            bg.rectTransform.Stretch();
            toastIcon = UIFactory.Image("Icon", toastRt, C.iconStar, Color.white);
            toastIcon.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(80, 80));
            toastText = UIFactory.Text("Text", toastRt, "", 44, Color.white, TextAlignmentOptions.Center, true);
            toastText.rectTransform.Stretch(110, 30, 0, 0);
            toastGroup.alpha = 0f;
            toastRt.gameObject.SetActive(false);
        }

        void BuildOffscreenArrow()
        {
            offArrow = UIFactory.Rect("OffscreenArrow", safe).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            var bg = UIFactory.Image("Bg", offArrow, C.spriteCircle, UIFactory.Gold);
            bg.rectTransform.Stretch();
            var arrow = UIFactory.Image("Arrow", offArrow, C.iconArrow, Color.white);
            arrow.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 64), new Vector2(56, 56));
            arrow.name = "Pointer";
            offArrowIcon = UIFactory.Image("Icon", offArrow, C.iconBuild, Color.white);
            offArrowIcon.rectTransform.Stretch(18, 18, 18, 18);
            offArrow.gameObject.SetActive(false);
        }

        void BuildMaxLabel()
        {
            maxLabel = UIFactory.Rect("MaxLabel", floatLayer).Anchor(new Vector2(0, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(220, 80));
            maxText = UIFactory.Text("Text", maxLabel, Loc.T("hud.max"), 52, new Color32(255, 92, 70, 255), TextAlignmentOptions.Center, true);
            maxText.rectTransform.Stretch();
            maxLabel.gameObject.SetActive(false);
        }

        void Relabel()
        {
            if (maxText) maxText.text = Loc.T("hud.max");
            RefreshLevel();
            currentObjective = null;
            questShownIndex = -1;
        }

        // ------------------------------------------------------------------ runtime
        void OnMoney(long value, long delta)
        {
            moneyAnimFrom = shownMoney;
            moneyAnimTo = value;
            moneyAnimT = 0f;
            if (delta > 0)
            {
                Tween.UIPunch(moneyPill, 0.08f, 0.18f);
                Tween.UIPunch(coinIcon, 0.25f, 0.2f);
            }
        }

        void Update()
        {
            scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) > 1f ? 1f : 0f;

            // test builds on phones: frame rate and quality step next to the version, readable on a screenshot
            if (Application.isMobilePlatform && MobilePerf.Fps > 0f && (fpsShown != Mathf.RoundToInt(MobilePerf.Fps) || tierShown != MobilePerf.Tier))
            {
                fpsShown = Mathf.RoundToInt(MobilePerf.Fps);
                tierShown = MobilePerf.Tier;
                versionText.text = $"{VersionInfo.Label} · {fpsShown} fps · q{tierShown}";
            }

            if (moneyAnimT < 1f)
            {
                moneyAnimT = Mathf.Min(1f, moneyAnimT + Time.unscaledDeltaTime / 0.35f);
                shownMoney = (long)Mathf.Lerp(moneyAnimFrom, moneyAnimTo, Easing.Evaluate(Ease.OutCubic, moneyAnimT));
                moneyText.text = Economy.Format(shownMoney);
            }

            RefreshLevel();
            UpdateFloats();
            UpdateToast();
            UpdateMaxLabel();
            UpdateOffscreen();
            UpdateQuestCard();
        }

        int lastLevel = -1;
        void RefreshLevel()
        {
            if (levelText == null) return;
            Progress.InLevel(out int lv, out long have, out long need, out float k);
            levelText.text = Loc.F("hud.level", lv);
            levelXp.text = $"{Economy.Format(System.Math.Min(have, need))}/{Economy.Format(need)}";
            levelNum.text = lv.ToString();
            levelFill.GetComponent<FillBar>().SetTarget(k);
            if (lastLevel >= 0 && lv != lastLevel) Tween.UIPunch(levelBlock, 0.12f, 0.3f);
            lastLevel = lv;
        }

        // quest card ----------------------------------------------------------
        RectTransform questCard, questFace;
        CanvasGroup questGroup;
        Image questIcon, questFill, questRewardMedal;
        TextMeshProUGUI questChapter, questText, questProgress, questCoins, questMedals, questClaim;
        RectTransform questBar;
        Button questButton;
        int questShownIndex = -1;
        bool questWasReady, questVisible;

        void BuildQuestCard()
        {
            questCard = UIFactory.Rect("Quest", safe).Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -232), new Vector2(500, 214));
            questGroup = questCard.gameObject.AddComponent<CanvasGroup>();
            questChapter = UIFactory.Text("Chapter", questCard, "", 30, Color.white, TextAlignmentOptions.Left, true);
            questChapter.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, 0), new Vector2(480, 42));
            questButton = UIFactory.Button("Card", questCard, Color.white, new Color32(200, 185, 165, 255), OnQuestTap, out questFace);
            ((RectTransform)questButton.transform).Stretch(0, 0, 46, 0);

            var iconBg = UIFactory.Image("IconBg", questFace, C.spriteCircle, new Color32(255, 236, 200, 255));
            iconBg.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66, 4), new Vector2(104, 104));
            questIcon = UIFactory.Image("Icon", iconBg.rectTransform, C.iconQuest, Color.white);
            questIcon.rectTransform.Stretch(14, 14, 14, 14);

            questText = UIFactory.Text("Text", questFace, "", 32, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            questText.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0, 1), new Vector2(130, -12), new Vector2(356, 76));
            questText.textWrappingMode = TextWrappingModes.Normal;
            questText.fontSizeMin = 24;

            var bar = UIFactory.Panel("Bar", questFace, new Color(0, 0, 0, 0.16f));
            questBar = bar.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 16), new Vector2(180, 36));
            questFill = UIFactory.Panel("Fill", questBar, UIFactory.Green);
            questFill.rectTransform.Stretch(4, 4, 4, 4);
            questFill.gameObject.AddComponent<FillBar>().Init(questFill.rectTransform);
            questProgress = UIFactory.Text("Value", questBar, "", 26, Color.white, TextAlignmentOptions.Center, true);
            questProgress.rectTransform.Stretch(0, 0, 0, 2);

            var coin = UIFactory.Image("Coin", questFace, C.iconCoin, Color.white);
            coin.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(340, 34), new Vector2(44, 44));
            questCoins = UIFactory.Text("Coins", questFace, "", 28, UIFactory.Ink, TextAlignmentOptions.Left);
            questCoins.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(364, 34), new Vector2(62, 40));
            questRewardMedal = UIFactory.Image("Medal", questFace, C.iconMedal, Color.white);
            questRewardMedal.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(446, 36), new Vector2(46, 46));
            questMedals = UIFactory.Text("Medals", questFace, "", 28, UIFactory.Ink, TextAlignmentOptions.Left);
            questMedals.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(470, 34), new Vector2(28, 40));

            questClaim = UIFactory.Text("Claim", questFace, "", 40, Color.white, TextAlignmentOptions.Center, true);
            questClaim.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 8), new Vector2(190, 52));
            questClaim.gameObject.SetActive(false);

            questGroup.alpha = 0f;
            questGroup.blocksRaycasts = false;
        }

        void UpdateQuestCard()
        {
            bool show = Tutorial.I != null && !Tutorial.I.InTutorial && ProgressionManager.I != null;
            if (show != questVisible)
            {
                questVisible = show;
                questGroup.blocksRaycasts = show;
                Tween.Fade(questGroup, show ? 1f : 0f, 0.25f);
                if (show) Tween.PopIn(questCard, Vector3.one, 0.35f);
                questShownIndex = -1;
            }
            if (!show) return;

            var q = Quests.Current;
            if (questShownIndex != Quests.Index)
            {
                if (questShownIndex >= 0) Tween.PopIn(questCard, Vector3.one, 0.35f);
                questShownIndex = Quests.Index;
                questText.text = Quests.Text(q);
                questIcon.sprite = Quests.Icon(q);
                questCoins.text = Economy.Format(q.coins);
                questMedals.text = q.medals.ToString();
                questRewardMedal.gameObject.SetActive(q.medals > 0);
                questMedals.gameObject.SetActive(q.medals > 0);
                RelabelQuestChapter();
            }
            long v = Quests.Value(q);
            bool ready = v >= q.target;
            questFill.GetComponent<FillBar>().SetTarget(q.target > 0 ? (float)v / q.target : 1f);
            questProgress.text = $"{Economy.Format(v)}/{Economy.Format(q.target)}";
            if (ready != questWasReady)
            {
                questWasReady = ready;
                questBar.gameObject.SetActive(!ready);
                questClaim.gameObject.SetActive(ready);
                questClaim.text = Loc.T("q.claim");
                UIFactory.SetButtonColors(questButton, ready ? UIFactory.Green : Color.white, ready ? UIFactory.GreenDark : new Color32(200, 185, 165, 255));
                questText.color = ready ? Color.white : UIFactory.Ink;
                questCoins.color = questMedals.color = ready ? Color.white : UIFactory.Ink;
                if (ready) { AudioManager.Play(Sfx.Tick, 0.7f); Tween.UIPunch(questCard, 0.15f, 0.3f); }
            }
            if (ready)
            {
                float s = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.03f;
                questFace.localScale = new Vector3(s, s, 1f);
            }
            else questFace.localScale = Vector3.one;
        }

        void RelabelQuestChapter()
        {
            int ch = Quests.Chapter;
            if (ch > 0)
            {
                Quests.ChapterProgress(out int done, out int total);
                questChapter.text = Loc.F("q.chapter", ch, done + 1, total);
            }
            else questChapter.text = Loc.T("q.more");
        }

        void OnQuestTap()
        {
            var q = Quests.Current;
            if (Quests.Ready)
            {
                int coins = q.coins;
                if (Quests.Claim())
                {
                    AudioManager.Play(Sfx.CoinsCollect, 1f);
                    if (PlayerController.I)
                    {
                        Fx.CoinBurst(PlayerController.I.transform.position + Vector3.up, 1.1f);
                        Feedback.FloatText("+" + Economy.Format(coins), PlayerController.I.transform.position + Vector3.up * 2.6f, Feedback.Gold, 1.2f);
                    }
                    Tutorial.I?.ClearQuestFocus();
                }
                return;
            }
            var place = Quests.Place(q);
            if (place.HasValue) Tutorial.I?.FocusQuest(place.Value, Quests.Icon(q), Quests.Text(q));
            else Feedback.Toast(Quests.Text(q), Quests.Icon(q));
        }

        void OnMedals(long value, long delta)
        {
            medalText.text = value.ToString();
            if (!medalPill.gameObject.activeSelf && value > 0) { medalPill.gameObject.SetActive(true); Tween.PopIn(medalPill, Vector3.one, 0.35f); }
            if (delta > 0) { Tween.UIPunch(medalPill, 0.12f, 0.2f); Tween.UIPunch(medalIcon, 0.3f, 0.25f); }
        }

        void OnChapterDone(int n) => UIPanels.ShowChapter(n);

        // objective -----------------------------------------------------------
        public void SetObjective(string text, Sprite icon)
        {
            if (text == currentObjective) return;
            currentObjective = text;
            if (string.IsNullOrEmpty(text))
            {
                Tween.Fade(objectiveGroup, 0f, 0.2f);
                return;
            }
            objectiveText.text = text;
            objectiveIcon.sprite = icon != null ? icon : C.iconArrow;
            objectiveGroup.alpha = 0f;
            Tween.Fade(objectiveGroup, 1f, 0.25f);
            objectiveBox.anchoredPosition = new Vector2(0, -130);
            var rt = objectiveBox;
            Tween.Run(0.35f, k => { if (rt) rt.anchoredPosition = new Vector2(0, Mathf.Lerp(-130, -158, k)); }, Ease.OutBack, null, rt, 0f, true);
            float w = Mathf.Clamp(objectiveText.GetPreferredValues(text).x + 150f, 420f, 980f);
            objectiveBox.sizeDelta = new Vector2(w, 92);
        }

        // floats --------------------------------------------------------------
        void FloatText(string text, Vector3 world, Color color, float scale)
        {
            TextMeshProUGUI t = floatPool.Count > 0 ? floatPool.Dequeue() : CreateFloat();
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            t.enableAutoSizing = false;
            t.fontSize = 54 * scale;
            t.rectTransform.localScale = Vector3.zero;
            floats.Add((t, world, 0f, 1.0f));
        }

        TextMeshProUGUI CreateFloat()
        {
            var t = UIFactory.Text("Float", floatLayer, "", 54, Color.white, TextAlignmentOptions.Center, true);
            t.rectTransform.Anchor(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 90));
            return t;
        }

        void UpdateFloats()
        {
            var cam = CameraRig.Cam;
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                f.age += Time.unscaledDeltaTime;
                float k = f.age / f.life;
                if (k >= 1f || cam == null)
                {
                    f.t.gameObject.SetActive(false);
                    floatPool.Enqueue(f.t);
                    floats.RemoveAt(i);
                    continue;
                }
                floats[i] = f;
                Vector3 sp = cam.WorldToScreenPoint(f.world + Vector3.up * (k * 1.2f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(floatLayer, sp, null, out var lp);
                f.t.rectTransform.anchoredPosition = lp + floatLayer.rect.size * 0.5f;
                float s = k < 0.15f ? Easing.Evaluate(Ease.OutBack, k / 0.15f) : 1f;
                f.t.rectTransform.localScale = Vector3.one * s;
                var c = f.t.color;
                c.a = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                f.t.color = c;
            }
        }

        // toast ---------------------------------------------------------------
        void Toast(string text, Sprite icon)
        {
            toastRt.gameObject.SetActive(true);
            toastText.text = text;
            toastIcon.sprite = icon != null ? icon : C.iconStar;
            toastTimer = 2.4f;
            toastGroup.alpha = 1f;
            Tween.PopIn(toastRt, Vector3.one, 0.35f);
        }

        void UpdateToast()
        {
            if (toastTimer <= 0f) return;
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f) Tween.Fade(toastGroup, 0f, 0.3f, () => toastRt.gameObject.SetActive(false));
        }

        // MAX label -------------------------------------------------------------
        float maxPunch;
        public static float LastFullTime = -10f;

        void UpdateMaxLabel()
        {
            var p = PlayerController.I;
            var cam = CameraRig.Cam;
            bool show = p != null && cam != null && p.stack.IsFull && p.stack.Count > 0;
            if (show != maxLabel.gameObject.activeSelf)
            {
                maxLabel.gameObject.SetActive(show);
                if (show) { Tween.PopIn(maxLabel, Vector3.one, 0.3f); AudioManager.Play(Sfx.Max, 0.5f); }
            }
            if (!show) return;
            Vector3 top = p.stack.TopWorld + Vector3.up * 0.45f;
            Vector3 sp = cam.WorldToScreenPoint(top);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(floatLayer, sp, null, out var lp);
            maxLabel.anchoredPosition = lp + floatLayer.rect.size * 0.5f;
            if (Time.time - LastFullTime < 0.1f && Time.time > maxPunch)
            {
                maxPunch = Time.time + 0.6f;
                Tween.UIPunch(maxLabel, 0.25f, 0.25f);
            }
        }

        // off-screen arrow ------------------------------------------------------
        void UpdateOffscreen()
        {
            var g = Guide.I;
            var cam = CameraRig.Cam;
            if (g == null || cam == null || !g.HasTarget || !g.ShowArrow)
            {
                if (offArrow.gameObject.activeSelf) offArrow.gameObject.SetActive(false);
                return;
            }
            Vector3 vp = cam.WorldToViewportPoint(g.TargetPosition);
            bool onScreen = vp.z > 0 && vp.x > 0.06f && vp.x < 0.94f && vp.y > 0.08f && vp.y < 0.86f;
            if (onScreen)
            {
                if (offArrow.gameObject.activeSelf) offArrow.gameObject.SetActive(false);
                return;
            }
            if (!offArrow.gameObject.activeSelf) { offArrow.gameObject.SetActive(true); Tween.PopIn(offArrow, Vector3.one, 0.25f); }
            if (vp.z < 0) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
            Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (c.sqrMagnitude < 0.0001f) c = Vector2.up;
            float m = Mathf.Max(Mathf.Abs(c.x) / 0.42f, Mathf.Abs(c.y) / 0.38f);
            Vector2 edge = c / m;
            Vector2 size = safe.rect.size;
            offArrow.anchoredPosition = new Vector2(edge.x * size.x, edge.y * size.y + Mathf.Sin(Time.unscaledTime * 6f) * 6f);
            float ang = Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg - 90f;
            var pointer = offArrow.Find("Pointer") as RectTransform;
            if (pointer != null)
            {
                pointer.localRotation = Quaternion.Euler(0, 0, ang);
                pointer.anchoredPosition = Quaternion.Euler(0, 0, ang) * new Vector2(0, 70);
            }
            if (g.Icon != null) offArrowIcon.sprite = g.Icon;
        }
    }

    /// <summary>Smoothly animated horizontal fill (width-based so rounded ends stay intact).</summary>
    public class FillBar : MonoBehaviour
    {
        RectTransform rt;
        float shown = -1f, target;
        public void Init(RectTransform r) { rt = r; rt.anchorMax = new Vector2(0f, 1f); }
        public void SetTarget(float k) { target = Mathf.Clamp01(k); if (shown < 0f) shown = target; }
        void Update()
        {
            if (rt == null) return;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.5f);
            rt.anchorMax = new Vector2(Mathf.Max(0.06f, shown), 1f);
            rt.gameObject.SetActive(shown > 0.001f);
        }
    }

    /// <summary>Fits the rect into Screen.safeArea (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last) return;
            last = sa;
            var rt = (RectTransform)transform;
            Vector2 min = sa.position, max = sa.position + sa.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    public static class PlayerHud
    {
        static float lastShelfFull;
        public static void NotifyFull(Carrier c) { Hud.LastFullTime = Time.time; }
        public static void NotifyShelfFull(Shelf s)
        {
            if (Time.time - lastShelfFull < 1.5f) return;
            lastShelfFull = Time.time;
            Feedback.FloatText(Loc.T("hud.full"), s.transform.position + Vector3.up * 1.6f, Feedback.White, 0.8f);
        }
    }

    public static class VersionInfo
    {
        public static string Label => "v" + Application.version + (string.IsNullOrEmpty(BuildStamp.Value) ? "" : " · " + BuildStamp.Value);
    }
}
