using System;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Decoupled hooks for juice: UI and camera subscribe, gameplay just calls.</summary>
    public static class Feedback
    {
        public static Action<string, Vector3, Color, float> FloatTextHandler;
        public static Action<string, Sprite> ToastHandler;
        public static Action<float, float> ShakeHandler;
        public static Action<Vector3, float> FocusHandler;

        public static readonly Color Gold = new Color(1f, 0.84f, 0.2f);
        public static readonly Color White = Color.white;
        public static readonly Color Red = new Color(1f, 0.35f, 0.3f);
        public static readonly Color Green = new Color(0.45f, 1f, 0.45f);

        public static void FloatText(string text, Vector3 world, Color color, float scale = 1f) => FloatTextHandler?.Invoke(text, world, color, scale);
        public static void Toast(string text, Sprite icon = null) => ToastHandler?.Invoke(text, icon);
        public static void Shake(float strength = 0.15f, float duration = 0.2f) => ShakeHandler?.Invoke(strength, duration);
        public static void Focus(Vector3 world, float hold = 1.1f) => FocusHandler?.Invoke(world, hold);
    }

    /// <summary>Spawns one-shot particle prefabs and cleans them up.</summary>
    public static class Fx
    {
        public static GameObject Play(GameObject prefab, Vector3 pos, float scale = 1f, float life = 3f, Quaternion? rot = null)
        {
            if (prefab == null) return null;
            var go = UnityEngine.Object.Instantiate(prefab, pos, rot ?? prefab.transform.rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.loop = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.playOnAwake = true;
            }
            UnityEngine.Object.Destroy(go, life);
            return go;
        }

        public static void Dust(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxDust, pos, scale, 2.5f);
        public static void Sparkles(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxSparkles, pos, scale, 3f);
        public static void Confetti(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxConfetti, pos, scale, 4f);
        public static void Heart(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxHeart, pos, scale, 2.5f);
        public static void CoinBurst(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxCoinBurst, pos, scale, 2.5f);
        public static void Smoke(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxSmoke, pos, scale, 3f);
        public static void Splash(Vector3 pos, float scale = 1f) => Play(GameConfig.I.fxSplash, pos, scale, 2f);
    }
}
