using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleArcade
{
    /// <summary>Scene bootstrap: load save, restore world, autosave, offline earnings, reset.</summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public MoneyPile startCoins;
        public Transform playerSpawn;
        public PlayerController player;
        public Checkout checkout;

        float autosave;
        public static bool Autotest { get; private set; }
        public static float AutotestTimeScale { get; private set; } = 1f;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            if (Application.isMobilePlatform) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            MobilePerf.Install();
            ParseArgs();
            if (Autotest) SaveSystem.Slot = "save_autotest";
            if (Autotest && HasArg("-fresh") && !freshDone) { SaveSystem.Wipe(); freshDone = true; }
            else SaveSystem.Load();
            Progress.Migrate();
            Progress.Reset();
            GameSettings.Apply();
            Time.timeScale = 1f;
        }

        static bool freshDone;

        void OnDestroy()
        {
            if (I == this) I = null;
            Pool.Clear();
        }

        void Start()
        {
            ProgressionManager.I.Restore();
            Quests.Init();
            if (!SaveSystem.Data.startCoinsTaken) startCoins.AddSilent(GameConfig.I.balance.startCoins);
            if (SaveSystem.Data.pileMoney > 0 && checkout != null) checkout.pile.AddSilent(SaveSystem.Data.pileMoney);
            SaveSystem.Data.pileMoney = 0;
            player.Teleport(playerSpawn.position, playerSpawn.eulerAngles.y);
            CameraRig.I.SnapToTarget();
            Economy.Notify();

            double away = SaveSystem.SecondsSinceLastSave();
            var B = GameConfig.I.balance;
            int helpers = HelperManager.I != null ? HelperManager.I.Count : 0;
            if (!Autotest && helpers > 0 && away >= B.offlineMinSeconds)
            {
                long amount = (long)(Math.Min(away, B.offlineCapSeconds) * helpers * B.offlinePerHelperPerSec * Economy.ProfitMultiplier);
                if (amount > 0) Tween.Delay(0.8f, () => UIPanels.ShowOffline(amount), this, true);
            }

            if (Autotest) gameObject.AddComponent<AutoPilot>();
            GameEvents.Log("game start");
        }

        void Update()
        {
            SaveSystem.Data.playTime += Time.unscaledDeltaTime * (Time.timeScale > 0f ? Time.timeScale : 0f);
            autosave -= Time.unscaledDeltaTime;
            if (autosave <= 0f)
            {
                autosave = 8f;
                SaveNow();
            }
        }

        public void SaveNow()
        {
            long piles = 0;
            if (checkout != null && checkout.isActiveAndEnabled) piles += checkout.pile.Amount;
            SaveSystem.Data.pileMoney = piles;
            SaveSystem.Save();
            SaveSystem.Data.pileMoney = 0;
        }

        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() { SaveNow(); }

        public void ResetProgress()
        {
            SaveSystem.Wipe();
            Progress.Reset();
            Time.timeScale = 1f;
            InputReader.Blocked = false;
            GameEvents.ClearAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------------ command line
        static string[] args;
        void ParseArgs()
        {
            args = Environment.GetCommandLineArgs();
            Autotest = HasArg("-autotest");
            var ts = ArgValue("-timescale");
            if (ts != null && float.TryParse(ts, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                AutotestTimeScale = Mathf.Clamp(v, 0.25f, 8f);
        }

        public static bool HasArg(string a)
        {
            if (args == null) return false;
            foreach (var s in args) if (string.Equals(s, a, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string ArgValue(string a)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], a, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
