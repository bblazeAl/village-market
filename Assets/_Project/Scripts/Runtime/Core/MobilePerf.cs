using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IdleArcade
{
    /// <summary>
    /// Phones only: measures the real frame rate and steps graphics down (never back up) while the
    /// device can't keep ~45 FPS. UI is an overlay canvas, so lowering the 3D render scale keeps text sharp.
    /// </summary>
    public class MobilePerf : MonoBehaviour
    {
        public static float Fps { get; private set; }
        public static int Tier { get; private set; }

        const float Window = 2f, Warmup = 4f, LowFps = 45f;
        float acc, elapsed, warm;
        int frames, lowStreak;

        public static void Install()
        {
            if (!Application.isMobilePlatform || FindFirstObjectByType<MobilePerf>() != null) return;
            DontDestroyOnLoad(new GameObject("MobilePerf").AddComponent<MobilePerf>().gameObject);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            warm += dt;
            acc += dt;
            frames++;
            if (acc < Window) return;
            Fps = frames / acc;
            acc = 0f;
            frames = 0;
            if (warm < Warmup) return;
            lowStreak = Fps < LowFps ? lowStreak + 1 : 0;
            // two bad windows in a row; loading hitches alone don't count
            if (lowStreak >= 2 && Tier < 3) { lowStreak = 0; StepDown(); }
        }

        void StepDown()
        {
            Tier++;
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var sun = RenderSettings.sun;
            var cam = CameraRig.Cam;
            UniversalAdditionalCameraData camData = null;
            if (cam != null) cam.TryGetComponent(out camData);
            switch (Tier)
            {
                case 1: // effects
                    if (camData != null) camData.renderPostProcessing = false;
                    if (sun != null && sun.shadows == LightShadows.Soft) sun.shadows = LightShadows.Hard;
                    if (rp != null) rp.msaaSampleCount = 2;
                    break;
                case 2: // 3D resolution
                    if (rp != null) { rp.renderScale = 0.75f; rp.shadowDistance = 30f; }
                    break;
                case 3: // last resort
                    if (rp != null) { rp.renderScale = 0.6f; rp.msaaSampleCount = 1; }
                    if (sun != null) sun.shadows = LightShadows.None;
                    break;
            }
            Debug.Log($"[Perf] {Fps:0} fps -> quality tier {Tier}");
        }
    }
}
