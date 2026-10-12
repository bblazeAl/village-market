using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>Loading screen: title, animated progress bar and a tip while the game scene loads.</summary>
    public class Boot : MonoBehaviour
    {
        public string gameScene = "Game";
        public Color top = new Color32(120, 196, 255, 255);
        public Color bottom = new Color32(255, 214, 150, 255);

        RectTransform barFill;
        float shown;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            GameSettings.Apply();
            BuildUI(out var tip);
            int t = Random.Range(1, 6);
            tip.text = Loc.T("tip." + t);

            float minTime = 1.6f, start = Time.unscaledTime;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0)
            {
                AudioListener.volume = 0f;
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "autotest");
                System.IO.Directory.CreateDirectory(dir);
                StartCoroutine(BootShot(System.IO.Path.Combine(dir, "boot.png")));
            }
            var op = SceneManager.LoadSceneAsync(gameScene);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - start < minTime)
            {
                float target = Mathf.Min(op.progress / 0.9f, (Time.unscaledTime - start) / minTime);
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.5f);
                barFill.anchorMax = new Vector2(Mathf.Max(0.04f, shown), 1f);
                yield return null;
            }
            barFill.anchorMax = Vector2.one;
            yield return new WaitForSecondsRealtime(0.15f);
            op.allowSceneActivation = true;
        }

        IEnumerator BootShot(string path)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            ScreenCapture.CaptureScreenshot(path);
        }

        void BuildUI(out TextMeshProUGUI tip)
        {
            var C = GameConfig.I;
            var canvasGo = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            var root = (RectTransform)canvasGo.transform;

            // vertical gradient background
            var bg = UIFactory.Rect("Bg", root).Stretch();
            var raw = bg.gameObject.AddComponent<RawImage>();
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            raw.texture = tex;

            var logo = UIFactory.Image("Logo", root, C.iconStar, Color.white);
            logo.rectTransform.Anchor(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(240, 240));
            logo.gameObject.AddComponent<Spin>().speed = 18f;
            var title = UIFactory.Text("Title", root, Loc.T("game.title"), 118, Color.white, TextAlignmentOptions.Center, true);
            title.rectTransform.Anchor(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 160));
            var sub = UIFactory.Text("Sub", root, Loc.T("game.subtitle").ToUpperInvariant(), 52, new Color32(255, 240, 200, 255), TextAlignmentOptions.Center, true);
            sub.rectTransform.Anchor(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(600, 80));
            Tween.Run(0.6f, k => { if (title) title.rectTransform.localScale = Vector3.one * k; }, Ease.OutBack, null, title, 0f, true);

            var bar = UIFactory.Panel("Bar", root, new Color(0, 0, 0, 0.3f));
            bar.rectTransform.Anchor(new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 56));
            var fill = UIFactory.Panel("Fill", bar.rectTransform, UIFactory.Green);
            barFill = fill.rectTransform;
            barFill.Stretch(6, 6, 6, 6);
            barFill.anchorMax = new Vector2(0.04f, 1f);
            var loading = UIFactory.Text("Loading", root, Loc.T("loading"), 44, Color.white, TextAlignmentOptions.Center, true);
            loading.rectTransform.Anchor(new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(700, 60));
            tip = UIFactory.Text("Tip", root, "", 38, new Color32(90, 60, 40, 255), TextAlignmentOptions.Center);
            tip.rectTransform.Anchor(new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(900, 120));
            tip.textWrappingMode = TextWrappingModes.Normal;
            var ver = UIFactory.Text("Version", root, VersionInfo.Label, 26, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.BottomLeft);
            ver.rectTransform.Anchor(Vector2.zero, Vector2.zero, new Vector2(18, 10), new Vector2(600, 40));
        }
    }
}
