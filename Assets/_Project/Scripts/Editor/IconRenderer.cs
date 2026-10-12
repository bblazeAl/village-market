using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IdleArcade.EditorTools
{
    /// <summary>Renders 3D props into transparent sprites with a chunky dark outline (matches the UI icon style).</summary>
    public static class IconRenderer
    {
        public const string Dir = "Assets/_Project/Generated/Icons";

        public static Sprite Render(GameObject source, string name, Vector3 viewEuler, int size = 256, float outlinePx = 7f)
        {
            Directory.CreateDirectory(Dir);
            var stage = new GameObject("~IconStage");
            stage.transform.position = new Vector3(0f, -400f, 0f);
            var obj = Object.Instantiate(source, stage.transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.SetActive(true);
            foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;

            var b = SyntyLib.RenderBounds(obj);
            var camGo = new GameObject("~IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.orthographic = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            var rot = Quaternion.Euler(viewEuler);
            float radius = b.extents.magnitude;
            cam.transform.rotation = rot;
            cam.transform.position = b.center - rot * Vector3.forward * (radius * 4f + 1f);

            // fit ortho size to the projected bounds
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var lp = cam.transform.InverseTransformPoint(c);
                maxX = Mathf.Max(maxX, Mathf.Abs(lp.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(lp.y));
            }
            cam.orthographicSize = Mathf.Max(maxX, maxY) * 1.12f;

            var lightGo = new GameObject("~IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var fill = new GameObject("~IconFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.color = new Color(0.8f, 0.88f, 1f);
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);

            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;
            var prevAmbient = RenderSettings.ambientLight;
            var prevMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            cam.Render();
            RenderSettings.ambientMode = prevMode;
            RenderSettings.ambientLight = prevAmbient;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            AddOutline(tex, outlinePx, new Color32(60, 38, 26, 255));

            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(fill.gameObject);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Un-premultiplies the render and adds a smooth outline by dilating alpha.</summary>
        static void AddOutline(Texture2D tex, float radius, Color32 outline)
        {
            int w = tex.width, h = tex.height;
            var src = tex.GetPixels();
            // URP writes premultiplied-ish edges; restore straight colour
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c.a > 0.001f && c.a < 1f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; }
                src[i] = c;
            }
            var dst = new Color[src.Length];
            int r = Mathf.CeilToInt(radius);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float best = 0f;
                for (int dy = -r; dy <= r && best < 1f; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= h) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= w) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > radius + 0.5f) continue;
                        float a = src[yy * w + xx].a * Mathf.Clamp01(radius + 0.5f - d);
                        if (a > best) best = a;
                    }
                }
                var s = src[y * w + x];
                Color o = outline;
                o.a = best;
                // composite source over outline
                float outA = s.a + o.a * (1f - s.a);
                Color rgb = outA > 0f ? (new Color(s.r, s.g, s.b) * s.a + new Color(o.r, o.g, o.b) * o.a * (1f - s.a)) / outA : Color.clear;
                dst[y * w + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
            }
            tex.SetPixels(dst);
            tex.Apply();
        }
    }
}
