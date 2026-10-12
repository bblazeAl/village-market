using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace IdleArcade
{
    /// <summary>
    /// Autotest bot (run the build with -autotest [-fresh] [-timescale 3]). Plays greedily like a
    /// focused player, logs every unlock with game time, takes screenshots, quits when the demo ends.
    /// Used to verify the first-10-minutes pacing without a human.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public static bool KeepPlaying;   // editor runs: same as -long
        readonly StringBuilder log = new StringBuilder();
        string dir;
        float gameTime, nextShot = 5f, decide, pathTimer;
        Vector3 target;
        string goal = "";
        readonly List<Vector3> corners = new List<Vector3>();
        int cornerIndex;
        NavMeshPath path;
        float stayUntil;
        bool done;
        float timeLimit = 1200f;
        int shotIndex;

        GameConfig C => GameConfig.I;

        void Start()
        {
            dir = Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "??_*.png")) File.Delete(f);
            path = new NavMeshPath();
            Time.timeScale = Game.AutotestTimeScale;
            AudioListener.volume = 0f;   // tests run silently
            AudioListener.pause = true;
            GameEvents.OnLog += Log;
            GameEvents.OnCustomerServed += (c, paid) => { served++; };
            Log($"autotest start timescale={Time.timeScale} version={VersionInfo.Label}");
            var lim = Game.ArgValue("-limit");
            if (lim != null && float.TryParse(lim, out var l)) timeLimit = l;
        }

        int served;
        bool clickPending;
        int fpsFrames;
        float fpsTime;

        void OnDestroy()
        {
            GameEvents.OnLog -= Log;
            InputReader.Override = null;
        }

        void Log(string s)
        {
            string line = $"[{Economy.FormatTime(gameTime)}] {s} | money={Economy.Money} served={SaveSystem.Data.customersServed} lv={SaveSystem.Data.marketLevel}";
            log.AppendLine(line);
            Debug.Log("[Autotest] " + line);
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString());
        }

        void Shot(string name)
        {
            string file = Path.Combine(dir, $"{shotIndex++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(file);
        }

        void Update()
        {
            if (done) return;
            if (UIPanels.AnyModalOpen)
            {
                // close level-up / offline / demo popups like a player would
                if (!clickPending)
                {
                    clickPending = true;
                    Tween.Delay(0.6f, () => { clickPending = false; ClickModal(); }, this, true);
                }
                if (Time.timeScale == 0f) gameTime += Time.unscaledDeltaTime;
                return;
            }
            if (Time.timeScale > 0f && Time.timeScale != Game.AutotestTimeScale) Time.timeScale = Game.AutotestTimeScale;
            gameTime += Time.deltaTime;

            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            if (fpsTime >= 10f)
            {
                Log($"fps {fpsFrames / fpsTime:0.0} customers={CustomerManager.I?.ActiveCount} helpers={HelperManager.I?.Count}");
                fpsFrames = 0;
                fpsTime = 0f;
            }
            if (gameTime >= nextShot)
            {
                Shot($"t{Mathf.RoundToInt(gameTime):0000}");
                nextShot += 30f;
            }

            // -long keeps playing past the demo end to check the quest / level / star progression
            bool stopAtDemo = !Game.HasArg("-long") && !KeepPlaying;
            if ((stopAtDemo && SaveSystem.Data.demoCompleteShown) || gameTime > timeLimit)
            {
                done = true;
                var d = SaveSystem.Data;
                Log($"SUMMARY xp={d.xp} quest={d.questIndex} medals={d.medals} perks={d.perks.Count} stars={string.Join(",", d.stationStars)} levels={string.Join(",", d.stationLevel)} vip={d.vipServed}");
                Log(stopAtDemo && d.demoCompleteShown ? "DEMO COMPLETE" : "TIME LIMIT");
                Shot("final");
                Tween.Delay(1.5f, () => Application.Quit(), this, true);
                return;
            }

            decide -= Time.deltaTime;
            if (decide <= 0f)
            {
                decide = 0.25f;
                Decide();
            }
            Steer();
        }

        void ClickModal()
        {
            if (!UIPanels.AnyModalOpen) return;
            if (UIPanels.LevelUpOpen)
            {
                int best = UIPanels.BestCard();
                UIPanels.PickCard(best);
                return;
            }
            var btns = FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
            foreach (var b in btns)
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                var n = b.transform.parent != null ? b.transform.parent.name : "";
                if (n == "Offline" || n == "Demo" || n == "Chapter")
                {
                    if (n == "Demo" && b.GetComponentInChildren<TMPro.TMP_Text>().text == Loc.T("demo.restart")) continue;
                    b.onClick.Invoke();
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ decisions
        void Go(Vector3 p, string why, float stay = 0f)
        {
            if (why != goal) Log("goal: " + why);
            goal = why;
            if ((p - target).sqrMagnitude > 0.04f) { target = p; pathTimer = 0f; }
            if (stay > 0f) stayUntil = Mathf.Max(stayUntil, gameTime + stay);
        }

        void Decide()
        {
            var player = PlayerController.I;
            var pm = ProgressionManager.I;
            var co = Game.I.checkout;
            var stack = player.stack;
            if (gameTime < stayUntil && NearTarget(0.5f)) return;

            if (!SaveSystem.Data.startCoinsTaken) { Go(Game.I.startCoins.transform.position, "start coins"); return; }

            // a player taps the glowing quest card as soon as it is ready
            if (Quests.Ready && Tutorial.I != null && !Tutorial.I.InTutorial) Quests.Claim();

            // buy upgrades when standing at a board would be cheap enough
            if (TryUpgrades(player)) return;

            var pad = pm.NextPad(true);
            if (pad != null) { Go(pad.transform.position, "pad " + pad.id, 0.5f); return; }

            if (co.isActiveAndEnabled && co.HasWaitingCustomer && !co.HasCashier) { Go(co.ZonePos, "serve", 1.2f); return; }

            var next = pm.NextPad(false);
            long pile = co.isActiveAndEnabled ? co.pile.Amount : 0;
            if (pile > 0 && (pile >= 20 || next == null || Economy.Money + pile >= next.Remaining)) { Go(co.pile.ZonePos, "collect"); return; }

            // plates first if nobody else does it
            bool pieHelper = false;
            if (HelperManager.I != null) foreach (var h in HelperManager.I.Helpers) if (h.role == HelperRole.PieHelper) pieHelper = true;
            if (stack.Has(C.plate))
            {
                var tub = Find<WashTub>();
                if (tub != null && (stack.IsFull || TableManager.TotalDirty() == 0)) { Go(tub.ZonePos, "wash"); return; }
            }
            if (!pieHelper && TableManager.TotalDirty() > 0 && !stack.IsFull && (stack.IsEmpty || stack.TopType == C.plate))
            {
                var t = TableManager.DirtiestTable(player.transform.position);
                Go(t.ZonePos, "clean"); return;
            }

            // deliver what we carry
            if (!stack.IsEmpty)
            {
                var top = stack.TopType;
                if (top == C.apple)
                {
                    var conv = Find<Converter>();
                    var pieShelf = FindShelf(C.pie);
                    var appleShelf = FindShelf(C.apple);
                    if (conv != null && conv.inTray.Count < 3 && (appleShelf == null || appleShelf.Stock >= 4) && pieShelf != null && pieShelf.Stock < 3)
                    { Go(conv.ZonePos, "feed pie oven"); return; }
                }
                var shelf = FindShelf(top);
                if (shelf != null && shelf.NeedsStock && (stack.IsFull || SourceEmpty(top))) { Go(shelf.ZonePos, "stock " + top.name); return; }
                var bin = Find<TrashBin>();
                if (bin != null && stack.IsFull && shelf != null && !shelf.NeedsStock && OtherShelfStarving(top)) { Go(bin.ZonePos, "dump " + top.name, 2.5f); return; }
                if (shelf != null && !shelf.NeedsStock && stack.IsFull) { Go(co.isActiveAndEnabled ? co.ZonePos : shelf.ZonePos, "serve (shelf full)", 1f); return; }
            }

            // gather the most needed product
            Shelf needy = null;
            float worst = float.MaxValue;
            foreach (var s in Station.All)
            {
                if (!(s is Shelf sh) || !sh.isActiveAndEnabled) continue;
                int waiting = CustomerManager.I != null ? CustomerManager.I.CountWaitingFor(sh) : 0;
                float score = sh.Fill - waiting * 0.25f;
                if (score < worst) { worst = score; needy = sh; }
            }
            if (needy != null && !stack.IsFull)
            {
                var src = Source(needy.type, out var pos);
                if (src != null) { Go(pos, "gather " + needy.type.name); return; }
            }
            if (!stack.IsEmpty)
            {
                var shelf = FindShelf(stack.TopType);
                if (shelf != null) { Go(shelf.ZonePos, "stock " + stack.TopType.name); return; }
            }
            if (co.isActiveAndEnabled) Go(co.ZonePos, "serve (idle)", 1f);
        }

        bool TryUpgrades(PlayerController player)
        {
            foreach (var z in FindObjectsByType<PanelZone>(FindObjectsSortMode.None))
            {
                if (!z.isActiveAndEnabled) continue;
                if (z.panel == PanelKind.Upgrades && TryStations(player, z)) return true;
                var ids = z.panel == PanelKind.Upgrades
                    ? new[] { UpgradeId.Capacity, UpgradeId.Speed, UpgradeId.Profit }
                    : new[] { UpgradeId.HelperSpeed, UpgradeId.HelperCapacity, UpgradeId.Cashier };
                foreach (var id in ids)
                {
                    int c = Economy.Cost(id);
                    if (c < 0) continue;
                    bool essential = (id == UpgradeId.Capacity && Economy.Level(id) < 2) || (id == UpgradeId.Speed && Economy.Level(id) < 1)
                                     || (id == UpgradeId.HelperSpeed && Economy.Level(id) < 1);
                    if (essential && Economy.Money >= c)
                    {
                        if (Station.InZone(player.transform.position, z.zone, z.radius))
                        {
                            Economy.TryUpgrade(id);
                            Log($"upgrade {id} -> {Economy.Level(id)}");
                        }
                        else Go(z.zone.position, "upgrade " + id, 0.6f);
                        return true;
                    }
                    var next = ProgressionManager.I.NextPad(false);
                    // only spend if it doesn't delay the next unlock much
                    bool cheap = next == null || Economy.Money - c >= next.Remaining * 0.6f || c <= 60;
                    if (Economy.Money >= c && cheap)
                    {
                        if (Station.InZone(player.transform.position, z.zone, z.radius))
                        {
                            Economy.TryUpgrade(id);
                            Log($"upgrade {id} -> {Economy.Level(id)}");
                        }
                        else Go(z.zone.position, "upgrade " + id, 0.6f);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Stars whenever medals allow; levels when they don't delay the next building much.</summary>
        bool TryStations(PlayerController player, PanelZone z)
        {
            var next = ProgressionManager.I.NextPad(false);
            foreach (var k in Stations.All)
            {
                if (!Stations.Built(k) || Stations.Maxed(k)) continue;
                int star = Stations.StarCost(k);
                bool want;
                if (star > 0) want = Stations.Medals >= star;
                else
                {
                    int c = Stations.LevelCost(k);
                    want = Economy.Money >= c && (next == null || Economy.Money - c >= next.Remaining * 0.6f || c <= 50);
                }
                if (!want) continue;
                if (Station.InZone(player.transform.position, z.zone, z.radius))
                {
                    if (star > 0) Stations.TryStar(k); else Stations.TryLevel(k);
                }
                else Go(z.zone.position, "station " + k, 0.6f);
                return true;
            }
            return false;
        }

        static bool OtherShelfStarving(ItemType t)
        {
            foreach (var s in Station.All)
                if (s is Shelf sh && sh.isActiveAndEnabled && sh.type != t && sh.Stock == 0 && CustomerManager.I != null && CustomerManager.I.CountWaitingFor(sh) > 0) return true;
            return false;
        }

        bool SourceEmpty(ItemType t)
        {
            var s = Source(t, out _);
            if (s is AppleTree tr) return tr.Available == 0;
            if (s is Oven o) return o.tray.Count == 0;
            if (s is Converter cv) return cv.outTray.Count == 0;
            return true;
        }

        Station Source(ItemType t, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (t == C.apple)
            {
                AppleTree best = null;
                foreach (var s in Station.All) if (s is AppleTree tr && tr.isActiveAndEnabled && (best == null || tr.Available > best.Available)) best = tr;
                if (best != null && best.Available > 0) { pos = best.ZonePos; return best; }
                return null;
            }
            if (t == C.bread)
            {
                Oven best = null;
                foreach (var s in Station.All) if (s is Oven o && o.isActiveAndEnabled && (best == null || o.tray.Count > best.tray.Count)) best = o;
                if (best != null && best.tray.Count > 0) { pos = best.ZonePos; return best; }
                return null;
            }
            if (t == C.pie)
            {
                var conv = Find<Converter>();
                if (conv == null) return null;
                if (conv.outTray.Count > 0) { pos = conv.OutZonePos; return conv; }
                return Source(C.apple, out pos);
            }
            return null;
        }

        static T Find<T>() where T : Station
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

        // ------------------------------------------------------------------ steering
        bool NearTarget(float r)
        {
            var p = PlayerController.I.transform.position;
            Vector3 d = target - p;
            d.y = 0;
            return d.magnitude < r;
        }

        Vector3 lastPos;
        float stuckTime, sidestepTime;
        float sidestepSign = 1f;

        void Steer()
        {
            var p = PlayerController.I.transform.position;
            // stuck on a corner? sidestep for a moment
            bool wantsMove = InputReader.Override.HasValue && InputReader.Override.Value.sqrMagnitude > 0.05f;
            if (wantsMove && (p - lastPos).sqrMagnitude < 0.0004f) stuckTime += Time.deltaTime; else stuckTime = 0f;
            lastPos = p;
            if (stuckTime > 0.35f) { sidestepTime = 0.45f; stuckTime = 0f; sidestepSign = -sidestepSign; pathTimer = 0f; }
            if (sidestepTime > 0f && InputReader.Override.HasValue)
            {
                sidestepTime -= Time.deltaTime;
                var o = InputReader.Override.Value;
                InputReader.Override = new Vector2(-o.y, o.x) * sidestepSign + o * 0.3f;
                return;
            }
            pathTimer -= Time.deltaTime;
            if (pathTimer <= 0f)
            {
                pathTimer = 0.5f;
                corners.Clear();
                cornerIndex = 0;
                if (NavMesh.SamplePosition(p, out var a, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out var b, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 0)
                {
                    corners.AddRange(path.corners);
                    corners[corners.Count - 1] = new Vector3(target.x, corners[corners.Count - 1].y, target.z);
                }
                else corners.Add(target);
            }
            if (corners.Count == 0) { InputReader.Override = Vector2.zero; return; }
            while (cornerIndex < corners.Count - 1)
            {
                Vector3 dd = corners[cornerIndex] - p; dd.y = 0;
                if (dd.magnitude < 0.35f) cornerIndex++; else break;
            }
            Vector3 dest = corners[Mathf.Min(cornerIndex, corners.Count - 1)];
            Vector3 dir = dest - p;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (cornerIndex >= corners.Count - 1 && dist < 0.3f) { InputReader.Override = Vector2.zero; return; }
            var cam = CameraRig.I.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            dir /= Mathf.Max(dist, 0.001f);
            float speedK = cornerIndex >= corners.Count - 1 ? Mathf.Clamp01(dist / 0.8f) * 0.7f + 0.3f : 1f;
            InputReader.Override = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, fwd)) * speedK;
        }
    }
}
