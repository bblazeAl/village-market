using System;
using TMPro;
using UnityEngine;

namespace IdleArcade
{
    public enum ItemKind { Apple, Bread, Pie, Plate }

    public enum Sfx
    {
        Pickup, Place, Coin, CoinsCollect, Pay, Unlock, LevelUp, Hire, Upgrade, Checkout, Happy, Click, Open, Close,
        Error, Tick, Max, Step, Plate, Build, Whoosh, Vip, Toggle
    }

    [Serializable]
    public class SfxEntry
    {
        public Sfx id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        public float pitchJitter = 0.06f;
        public float minInterval = 0.03f;
    }

    [Serializable]
    public class Balance
    {
        [Header("Player")]
        public float playerSpeed = 4.4f;
        public float playerSpeedPerLevel = 0.5f;
        public int[] capacityByLevel = { 4, 6, 8, 10, 13, 16 };
        public float[] profitByLevel = { 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f };
        public int[] playerUpgradeCost = { 40, 90, 180, 340, 600 };
        public float playerTransferInterval = 0.085f;

        [Header("Helpers")]
        public float helperSpeed = 2.9f;
        public float helperSpeedPerLevel = 0.45f;
        public int helperCapacity = 3;
        public int helperCapacityPerLevel = 1;
        public float helperTransferInterval = 0.22f;
        public int[] helperUpgradeCost = { 80, 160, 300, 520, 850 };
        public float cashierTime = 1.05f;
        public float cashierTimePerLevel = -0.15f;

        [Header("Production")]
        public float treeGrowInterval = 1.5f;
        public int treeSlots = 7;
        public float ovenCycle = 2.3f;
        public int ovenTrayMax = 8;
        public float pieOvenCycle = 2.2f;
        public int pieOvenInputMax = 10;
        public int pieOvenTrayMax = 6;

        [Header("Customers")]
        public float customerInterval = 4.5f;
        public float customerIntervalMin = 2.1f;
        public float customerSpeed = 2.7f;
        public int customerBaseMax = 3;
        public int customerMaxPerShelf = 2;
        public float playerCheckoutTime = 0.55f;
        public float customerPatience = 28f;
        public float eatTime = 6f;
        public int tableTip = 3;
        public float vipChancePerMinute = 0.6f;
        public float vipMultiplier = 3f;

        [Header("Money")]
        public int coinValue = 2;          // dollars per coin stack on a pile
        public int pileMaxVisual = 36;
        public int startCoins = 40;

        [Header("Pads")]
        public float padFillMin = 0.55f;
        public float padFillMax = 2.0f;
        public float padFillPerCoin = 0.004f;
        public float padStartDelay = 0.2f;

        [Header("Offline")]
        public float offlinePerHelperPerSec = 0.35f;
        public float offlineCapSeconds = 7200f;
        public float offlineMinSeconds = 60f;

        [Header("Market levels (XP)")]
        // XP needed to finish each level, from level 1; past the end the last step grows by xpTailGrowth.
        // Level 10 is a deliberate step (x2.3), like the genre's long-session games.
        public int[] xpPerLevel = { 150, 330, 420, 450, 600, 750, 900, 1050, 2400, 2700 };
        public int xpTailGrowth = 300;
        public int xpPerCustomer = 2;
        public int xpPerVip = 6;
        public int xpPadDivisor = 5;          // building gives cost / divisor
        public int xpStationDivisor = 6;      // station upgrades give cost / divisor
        public int[] levelCoins = { 30, 80, 200, 250, 320, 400, 500, 600, 1000, 1100 };
        public int medalsPerLevel = 1;

        [Header("Station upgrades")]
        public int[] stationBaseCost = { 60, 120, 200, 90 };   // apples, bread, pies, checkout
        public float stationCostGrowth = 1.4f;
        public int stationLevelsPerStar = 5;
        public int[] starMedals = { 4, 8, 12 };
        public float stationSpeedPerLevel = 0.08f;
        public float stationSpeedPerStar = 0.25f;
        public float stationPricePerStar = 0.4f;
        public float checkoutPricePerStar = 0.1f;
    }

    /// <summary>All asset references the runtime needs. Lives in Resources/GameConfig.asset, written by the editor builder.</summary>
    public class GameConfig : ScriptableObject
    {
        static GameConfig instance;
        public static GameConfig I
        {
            get
            {
                if (instance == null) instance = Resources.Load<GameConfig>("GameConfig");
                return instance;
            }
        }

        [Header("Items")]
        public ItemType apple;
        public ItemType bread;
        public ItemType pie;
        public ItemType plate;
        public GameObject coinFlyPrefab;
        public GameObject coinStackPrefab;
        public GameObject basketPrefab;

        [Header("Characters")]
        public GameObject playerModel;
        public GameObject[] customerModels;
        public GameObject[] helperModels;
        public GameObject helperHat;
        public GameObject vipCrown;
        public RuntimeAnimatorController animator;

        [Header("FX")]
        public GameObject fxDust;
        public GameObject fxSparkles;
        public GameObject fxConfetti;
        public GameObject fxLevelUp;
        public GameObject fxHeart;
        public GameObject fxCoinBurst;
        public GameObject fxSmoke;
        public GameObject fxStep;
        public GameObject fxFireworks;
        public GameObject fxSplash;

        [Header("Audio")]
        public SfxEntry[] sfx;
        public AudioClip music;

        [Header("UI")]
        public TMP_FontAsset font;
        public Material fontOutline;     // thick dark outline, for HUD numbers
        public Material fontWorld;       // outline for world-space labels
        public Sprite spriteRound;       // 9-sliced rounded rect
        public Sprite spriteRoundSmall;
        public Sprite spriteCircle;
        public Sprite spriteRing;
        public Sprite spriteRingThin;
        public Sprite spriteShadow;
        public Sprite iconCoin;
        public Sprite iconGear;
        public Sprite iconArrow;
        public Sprite iconSpeed;
        public Sprite iconBag;
        public Sprite iconProfit;
        public Sprite iconHelper;
        public Sprite iconCashier;
        public Sprite iconHeart;
        public Sprite iconLock;
        public Sprite iconStar;
        public Sprite iconCheck;
        public Sprite iconMusic;
        public Sprite iconSound;
        public Sprite iconHand;
        public Sprite iconBuild;
        public Sprite iconSad;
        public Sprite iconTrash;
        public Sprite iconMedal;
        public Sprite iconQuest;
        public Sprite iconChest;
        public Material padRingMaterial;
        public Material padFillMaterial;
        public Material guideLineMaterial;
        public Material blobShadowMaterial;
        public GameObject guideArrowPrefab;

        [Header("Balance")]
        public Balance balance = new Balance();

        public ItemType ItemOf(ItemKind k) => k switch
        {
            ItemKind.Apple => apple,
            ItemKind.Bread => bread,
            ItemKind.Pie => pie,
            _ => plate
        };
    }
}
