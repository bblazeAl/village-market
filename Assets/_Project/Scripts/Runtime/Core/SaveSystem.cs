using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleArcade
{
    [Serializable]
    public class PadProgress
    {
        public string id;
        public int paid;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public long money;
        public List<string> unlocked = new List<string>();
        public List<PadProgress> partial = new List<PadProgress>();
        public int upSpeed, upCapacity, upProfit, upHelperSpeed, upHelperCapacity, upCashier;
        public bool tutorialDone;
        public int tutorialStep;
        public bool startCoinsTaken;
        public float playTime;
        public int customersServed;
        public long totalEarned;
        public int marketLevel = 1;
        public bool demoCompleteShown;
        public long pileMoney;
        public string lastSaveUtc;

        // progression v2: XP levels, level-up perks, station stars, quests
        public long xp = -1;                  // -1 = old save, derived from marketLevel on load
        public int medals;
        public List<PerkEntry> perks = new List<PerkEntry>();
        public int[] stationLevel = new int[4];
        public int[] stationStars = new int[4];
        public int questIndex;
        public long questStart;
        public int vipServed;
        public int[] sold = new int[4];       // by ItemKind
        public int platesWashed;
        public long coinsCollected;
    }

    [Serializable]
    public class PerkEntry
    {
        public PerkId id;
        public float value;
    }

    /// <summary>JSON save with atomic write (tmp + replace) and a backup copy.</summary>
    public static class SaveSystem
    {
        public static SaveData Data { get; private set; } = new SaveData();
        /// <summary>
        /// File name without extension. The editor and the autotest bot use their own files: they share
        /// persistentDataPath with the player's build, and a test wipe must never touch the player's progress.
        /// </summary>
#if UNITY_EDITOR
        public static string Slot = "save_editor";
#else
        public static string Slot = "save";
#endif
        static string PathMain => System.IO.Path.Combine(Application.persistentDataPath, Slot + ".json");
        static string PathTmp => PathMain + ".tmp";
        static string PathBak => PathMain + ".bak";

        public static bool HasSave => File.Exists(PathMain) || File.Exists(PathBak);

#if UNITY_WEBGL && !UNITY_EDITOR
        // Browsers have no real file system: the save lives in PlayerPrefs (IndexedDB), flushed on every save.
        static string PrefsKey => "save." + Slot;

        public static void Load() => Data = Parse(PlayerPrefs.GetString(PrefsKey, ""), PrefsKey) ?? new SaveData();

        public static void Save()
        {
            Data.lastSaveUtc = DateTime.UtcNow.ToString("o");
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static void Wipe()
        {
            Data = new SaveData();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
#else
        public static void Load()
        {
            Data = TryRead(PathMain) ?? TryRead(PathBak) ?? new SaveData();
        }

        static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Parse(File.ReadAllText(path), path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] read failed {path}: {e.Message}");
                return null;
            }
        }

#endif

        static SaveData Parse(string json, string source)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null) return null;
                d.unlocked ??= new List<string>();
                d.partial ??= new List<PadProgress>();
                d.perks ??= new List<PerkEntry>();
                if (d.stationLevel == null || d.stationLevel.Length < 4) d.stationLevel = new int[4];
                if (d.stationStars == null || d.stationStars.Length < 4) d.stationStars = new int[4];
                if (d.sold == null || d.sold.Length < 4) d.sold = new int[4];
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] read failed {source}: {e.Message}");
                return null;
            }
        }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        public static void Save()
        {
            try
            {
                Data.lastSaveUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(PathTmp, json);
                if (File.Exists(PathMain))
                {
                    File.Copy(PathMain, PathBak, true);
                    File.Delete(PathMain);
                }
                File.Move(PathTmp, PathMain);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] write failed: {e.Message}");
            }
        }

        public static void Wipe()
        {
            Data = new SaveData();
            try
            {
                if (File.Exists(PathMain)) File.Delete(PathMain);
                if (File.Exists(PathBak)) File.Delete(PathBak);
            }
            catch (Exception e) { Debug.LogWarning($"[Save] wipe failed: {e.Message}"); }
        }
#endif

        public static double SecondsSinceLastSave()
        {
            if (string.IsNullOrEmpty(Data.lastSaveUtc)) return 0;
            if (!DateTime.TryParse(Data.lastSaveUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return 0;
            return Math.Max(0, (DateTime.UtcNow - t).TotalSeconds);
        }

        public static bool IsUnlocked(string id) => Data.unlocked.Contains(id);

        public static int GetPaid(string id)
        {
            foreach (var p in Data.partial) if (p.id == id) return p.paid;
            return 0;
        }

        public static void SetPaid(string id, int paid)
        {
            foreach (var p in Data.partial) if (p.id == id) { p.paid = paid; return; }
            Data.partial.Add(new PadProgress { id = id, paid = paid });
        }
    }

    /// <summary>Per-device preferences that survive a progress reset.</summary>
    public static class GameSettings
    {
        public static bool Music { get => PlayerPrefs.GetInt("set.music", 1) == 1; set { PlayerPrefs.SetInt("set.music", value ? 1 : 0); Changed?.Invoke(); } }
        public static bool Sound { get => PlayerPrefs.GetInt("set.sound", 1) == 1; set { PlayerPrefs.SetInt("set.sound", value ? 1 : 0); Changed?.Invoke(); } }
        public static Lang Language
        {
            get => (Lang)PlayerPrefs.GetInt("set.lang", (int)Loc.SystemDefault());
            set { PlayerPrefs.SetInt("set.lang", (int)value); Loc.Set(value); Changed?.Invoke(); }
        }
        public static event Action Changed;
        public static void Apply() => Loc.Set(Language);
    }
}
