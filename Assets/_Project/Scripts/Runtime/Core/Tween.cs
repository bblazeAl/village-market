using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    public enum Ease { Linear, OutQuad, InQuad, InOutQuad, OutCubic, InCubic, OutBack, InBack, OutElastic, InOutSine, OutBounce }

    public static class Easing
    {
        public static float Evaluate(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InCubic: return t * t * t;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case Ease.InBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f || t >= 1f) return t;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1f / d1) return n1 * t * t;
                    if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                    if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                    t -= 2.625f / d1; return n1 * t * t + 0.984375f;
                }
                default: return t;
            }
        }
    }

    /// <summary>Tiny allocation-light tween engine. One runner object lives for the whole game.</summary>
    public sealed class Tween
    {
        internal float duration, elapsed, delay;
        internal Ease ease;
        internal Action<float> onUpdate;
        internal Action onComplete;
        internal UnityEngine.Object owner;
        internal bool unscaled;
        internal bool killed;
        internal int id;

        public bool IsAlive => !killed;
        public void Kill() { killed = true; }

        // ---------- runner ----------
        static readonly List<Tween> active = new List<Tween>(256);
        static readonly List<Tween> pending = new List<Tween>(64);
        static TweenRunner runner;
        static int nextId = 1;

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("~TweenRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            runner = go.AddComponent<TweenRunner>();
        }

        public static Tween Run(float duration, Action<float> onUpdate, Ease ease = Ease.OutQuad, Action onComplete = null,
            UnityEngine.Object owner = null, float delay = 0f, bool unscaled = false)
        {
            EnsureRunner();
            var t = new Tween
            {
                duration = Mathf.Max(0.0001f, duration), ease = ease, onUpdate = onUpdate, onComplete = onComplete,
                owner = owner, delay = delay, unscaled = unscaled, id = nextId++
            };
            pending.Add(t);
            return t;
        }

        public static Tween Delay(float seconds, Action action, UnityEngine.Object owner = null, bool unscaled = false)
            => Run(seconds, null, Ease.Linear, action, owner, 0f, unscaled);

        public static void KillOwner(UnityEngine.Object owner)
        {
            if (owner == null) return;
            for (int i = 0; i < active.Count; i++) if (active[i].owner == owner) active[i].killed = true;
            for (int i = 0; i < pending.Count; i++) if (pending[i].owner == owner) pending[i].killed = true;
        }

        internal static void Tick()
        {
            if (pending.Count > 0) { active.AddRange(pending); pending.Clear(); }
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var t = active[i];
                bool ownerGone = !ReferenceEquals(t.owner, null) && t.owner == null;
                if (t.killed || ownerGone) { active.RemoveAt(i); continue; }
                float step = t.unscaled ? udt : dt;
                if (t.delay > 0f) { t.delay -= step; continue; }
                t.elapsed += step;
                float p = Mathf.Clamp01(t.elapsed / t.duration);
                try { t.onUpdate?.Invoke(Easing.Evaluate(t.ease, p)); }
                catch (Exception e) { Debug.LogException(e); t.killed = true; }
                if (p >= 1f && !t.killed)
                {
                    t.killed = true;
                    active.RemoveAt(i);
                    try { t.onComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        // ---------- helpers ----------
        public static Tween Scale(Transform tr, Vector3 to, float dur, Ease ease = Ease.OutBack, Action done = null, float delay = 0f)
        {
            Vector3 from = tr.localScale;
            return Run(dur, k => { if (tr) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, done, tr, delay);
        }

        public static Tween PopIn(Transform tr, Vector3 to, float dur = 0.35f, float delay = 0f, Action done = null)
        {
            tr.localScale = Vector3.zero;
            return Scale(tr, to, dur, Ease.OutBack, done, delay);
        }

        public static Tween PopOut(Transform tr, float dur = 0.2f, Action done = null)
            => Scale(tr, Vector3.zero, dur, Ease.InBack, done);

        /// <summary>Squash-and-stretch punch around a base scale.</summary>
        public static Tween Punch(Transform tr, Vector3 baseScale, float strength = 0.18f, float dur = 0.28f)
        {
            return Run(dur, k =>
            {
                if (!tr) return;
                float s = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f) * strength;
                tr.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s * 0.6f), baseScale.z * (1f + s));
            }, Ease.Linear, () => { if (tr) tr.localScale = baseScale; }, tr);
        }

        public static Tween UIPunch(RectTransform tr, float strength = 0.15f, float dur = 0.22f)
        {
            return Run(dur, k =>
            {
                if (!tr) return;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * strength;
                tr.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear, () => { if (tr) tr.localScale = Vector3.one; }, tr, 0f, true);
        }

        /// <summary>Flies a transform along an arc to a (possibly moving) world target.</summary>
        public static Tween Arc(Transform tr, Func<Vector3> target, float height, float dur, Action done = null, Ease ease = Ease.InOutSine,
            Func<Quaternion> targetRot = null, float scaleFrom = -1f, float scaleTo = -1f)
        {
            Vector3 start = tr.position;
            Quaternion startRot = tr.rotation;
            Vector3 baseScale = tr.localScale;
            return Run(dur, k =>
            {
                if (!tr) return;
                Vector3 end = target();
                Vector3 p = Vector3.LerpUnclamped(start, end, k);
                p.y += Mathf.Sin(k * Mathf.PI) * height;
                tr.position = p;
                if (targetRot != null) tr.rotation = Quaternion.Slerp(startRot, targetRot(), k);
                if (scaleFrom >= 0f) tr.localScale = baseScale.normalized * baseScale.magnitude * Mathf.Lerp(scaleFrom, scaleTo, k);
            }, ease, done, tr);
        }

        public static Tween MoveLocal(Transform tr, Vector3 to, float dur, Ease ease = Ease.OutQuad, Action done = null)
        {
            Vector3 from = tr.localPosition;
            return Run(dur, k => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, k); }, ease, done, tr);
        }

        public static Tween Fade(CanvasGroup cg, float to, float dur, Action done = null, bool unscaled = true)
        {
            float from = cg.alpha;
            return Run(dur, k => { if (cg) cg.alpha = Mathf.LerpUnclamped(from, to, k); }, Ease.OutQuad, done, cg, 0f, unscaled);
        }
    }

    internal sealed class TweenRunner : MonoBehaviour
    {
        void Update() => Tween.Tick();
    }
}
