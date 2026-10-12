using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// Owns the unlock graph: which pads are visible, what each pad builds, market levels, demo end.
    /// Pads are authored in the scene by the world builder; their order is the intended play order.
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager I { get; private set; }

        public PayPad[] pads;

        float recheck;
        public int TasksDone { get; private set; }
        public int TaskTotal { get; private set; }

        void Awake()
        {
            I = this;
            pads = pads.OrderBy(p => p.order).ToArray();
        }

        void OnDestroy() { if (I == this) I = null; }

        public PayPad Find(string id)
        {
            foreach (var p in pads) if (p.id == id) return p;
            return null;
        }

        public void Restore()
        {
            foreach (var p in pads)
            {
                foreach (var r in p.reveals) if (r) r.SetActive(false);
                p.Init(SaveSystem.GetPaid(p.id));
                p.Hide();
            }
            foreach (var p in pads)
            {
                if (!SaveSystem.IsUnlocked(p.id)) continue;
                ApplyUnlock(p, true);
            }
            RecountTasks();
            RefreshReveals(true);
        }

        bool RequirementMet(string req)
        {
            if (req == "coins") return SaveSystem.Data.startCoinsTaken;
            if (req.StartsWith("served:")) return SaveSystem.Data.customersServed >= int.Parse(req.Substring(7));
            return SaveSystem.IsUnlocked(req);
        }

        void RefreshReveals(bool instant)
        {
            PayPad focusPad = null;
            foreach (var p in pads)
            {
                if (p.Revealed || SaveSystem.IsUnlocked(p.id)) continue;
                bool ok = SaveSystem.Data.customersServed >= p.requiresServed;
                if (p.requires != null) foreach (var r in p.requires) ok &= RequirementMet(r);
                if (!ok) continue;
                p.Reveal(instant);
                if (!instant && p.focusOnReveal && focusPad == null) focusPad = p;
            }
            if (focusPad != null && CameraRig.I != null && !CameraRig.I.IsOnScreen(focusPad.transform.position, 0.12f))
                Feedback.Focus(focusPad.transform.position, 1.0f);
        }

        void Update()
        {
            recheck -= Time.deltaTime;
            if (recheck <= 0f) { recheck = 0.5f; RefreshReveals(false); }
        }

        public void OnPadPaid(PayPad p)
        {
            if (!SaveSystem.Data.unlocked.Contains(p.id)) SaveSystem.Data.unlocked.Add(p.id);
            SaveSystem.SetPaid(p.id, p.cost);
            p.Complete(false);
            ApplyUnlock(p, false);
            AudioManager.Play(p.action == PadAction.Hire ? Sfx.Hire : Sfx.Unlock, 0.8f);
            Feedback.Shake(0.12f, 0.2f);
            GameEvents.PadUnlocked(p);
            GameEvents.Log($"unlock {p.id} cost={p.cost}");
            RecountTasks();
            Tween.Delay(0.6f, () => RefreshReveals(false), this);
            Progress.AddXp(Mathf.Max(5, p.cost / Mathf.Max(1, GameConfig.I.balance.xpPadDivisor)), "build " + p.id);
            SaveSystem.Save();
            if (pads.All(x => SaveSystem.IsUnlocked(x.id)) && !SaveSystem.Data.demoCompleteShown)
            {
                SaveSystem.Data.demoCompleteShown = true;
                Tween.Delay(2.8f, () => UIPanels.ShowDemoComplete(), this);
            }
        }

        void ApplyUnlock(PayPad p, bool instant)
        {
            if (instant) p.Complete(true);
            if (p.hides != null)
                foreach (var h in p.hides)
                {
                    if (!h) continue;
                    if (instant) h.SetActive(false);
                    else Tween.Scale(h.transform, Vector3.zero, 0.3f, Ease.InBack, () => h.SetActive(false));
                }
            float delay = 0f;
            foreach (var r in p.reveals)
            {
                if (!r) continue;
                Build(r, instant, delay);
                delay += 0.12f;
            }
            if (p.action == PadAction.Hire && HelperManager.I != null)
            {
                Vector3 post = p.hirePost != null ? p.hirePost.position : p.transform.position;
                HelperManager.I.Hire(p.hireRole, post, instant);
                if (!instant) Feedback.Toast(Loc.T("toast.hired"), GameConfig.I.iconHelper);
            }
        }

        static void Build(GameObject r, bool instant, float delay)
        {
            Vector3 s = r.transform.localScale;
            r.SetActive(true);
            var tree = r.GetComponentInChildren<AppleTree>();
            if (instant)
            {
                if (tree) tree.GrowInitial(tree.Capacity, 0f);
                return;
            }
            r.transform.localScale = Vector3.zero;
            Tween.Scale(r.transform, s, 0.5f, Ease.OutBack, null, delay);
            Tween.Delay(delay, () =>
            {
                if (!r) return;
                Fx.Dust(r.transform.position, 1.2f);
                Fx.Sparkles(r.transform.position + Vector3.up * 1.0f, 1f);
                AudioManager.Play(Sfx.Build, 0.5f);
            }, r);
            if (tree) tree.GrowInitial(3, delay + 0.5f);
        }

        void RecountTasks()
        {
            TaskTotal = pads.Count(p => p.countsAsTask);
            TasksDone = pads.Count(p => p.countsAsTask && SaveSystem.IsUnlocked(p.id));
            // the market level comes from XP now (Progress); buildings only feed it
        }

        /// <summary>Cheapest revealed pad, for the advisor.</summary>
        public PayPad NextPad(bool affordableOnly)
        {
            PayPad best = null;
            foreach (var p in pads)
            {
                if (!p.Revealed || p.IsDone || SaveSystem.IsUnlocked(p.id)) continue;
                if (affordableOnly && Economy.Money < p.Remaining) continue;
                if (best == null || p.Remaining < best.Remaining || (p.Remaining == best.Remaining && p.order < best.order)) best = p;
            }
            return best;
        }

        public IEnumerable<PayPad> RevealedPads()
        {
            foreach (var p in pads) if (p.Revealed && !p.IsDone && !SaveSystem.IsUnlocked(p.id)) yield return p;
        }
    }
}
