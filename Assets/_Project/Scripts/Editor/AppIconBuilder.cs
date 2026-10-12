using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IdleArcade.EditorTools
{
    /// <summary>App icon: the heroine's portrait on a warm background, set as the default player icon.</summary>
    public static class AppIconBuilder
    {
        public const string IconPath = AssetBuilder.Gen + "/AppIcon.png";
        const int Size = 1024;

        [MenuItem("IdleArcade/Build/App Icon")]
        public static void Build()
        {
            var prefab = SyntyLib.Find(AssetBuilder.PlayerModel);
            // cheerful thumbs-up (Synty emotes) if the pack is present, plain idle otherwise
            var clip = FindClip("A_POLY_EMOT_Happy_ThumbsUp_Femn");
            float time = 1.6f;
            if (clip == null) { clip = FindClip("Idle_Loop"); time = 0.4f; }
            var ch = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // clip.SampleAnimation poses humanoids in edit mode (AnimationMode sampling does not); the root may move, bounds follow it
            if (clip != null) clip.SampleAnimation(ch, time);
            var proxy = BakePose(ch);
            ch.SetActive(false);
            // sampling puts the root at the world origin, i.e. inside the open scene; shoot far below it
            proxy.transform.position += new Vector3(0f, -600f, 0f);
            var subject = proxy;

            var b = SyntyLib.RenderBounds(subject);
            float h = b.size.y;
            // head-and-shoulders portrait, slightly from the left and above, like store icons of the genre
            var focus = new Vector3(b.center.x, b.max.y - h * 0.4f, b.center.z);
            var camGo = new GameObject("~AppIconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            var rot = Quaternion.Euler(8f, 162f, 0f);
            cam.transform.rotation = rot;
            float dist = h * 0.47f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = focus - rot * Vector3.forward * dist;

            var key = new GameObject("~AppIconKey").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.35f; key.color = new Color(1f, 0.96f, 0.88f);
            key.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            var rim = new GameObject("~AppIconRim").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = 0.8f; rim.color = new Color(1f, 0.85f, 0.6f);
            rim.transform.rotation = Quaternion.Euler(20f, 20f, 0f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;
            var prevAmbient = RenderSettings.ambientLight; var prevMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.58f, 0.6f);
            cam.Render();
            RenderSettings.ambientMode = prevMode; RenderSettings.ambientLight = prevAmbient;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var fg = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            fg.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            fg.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(key.gameObject); Object.DestroyImmediate(rim.gameObject);
            Object.DestroyImmediate(proxy);
            Object.DestroyImmediate(ch);

            var outTex = Compose(fg);
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, outTex.EncodeToPNG());
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
        }

        // warm sunny radial background + soft dark outline around the character, no transparency (launchers mask it)
        static Texture2D Compose(Texture2D fg)
        {
            var src = fg.GetPixels32();
            var outline = new float[Size * Size];
            const int r = 10;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (src[y * Size + x].a > 8) continue;
                    float best = 0f;
                    for (int dy = -r; dy <= r && best < 1f; dy += 2)
                        for (int dx = -r; dx <= r; dx += 2)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= Size || sy >= Size || dx * dx + dy * dy > r * r) continue;
                            if (src[sy * Size + sx].a > 128) { best = 1f; break; }
                        }
                    outline[y * Size + x] = best;
                }
            var inner = new Color(1f, 0.86f, 0.42f);
            var outer = new Color(0.98f, 0.55f, 0.26f);
            var line = new Color(0.24f, 0.14f, 0.09f);
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - Size * 0.5f) / Size, dy = (y - Size * 0.58f) / Size;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.6f);
                    var c = Color.Lerp(inner, outer, t * t);
                    int i = y * Size + x;
                    c = Color.Lerp(c, line, outline[i]);
                    Color f = src[i];
                    c = Color.Lerp(c, new Color(f.r, f.g, f.b, 1f), f.a);
                    c.a = 1f;
                    px[i] = c;
                }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // edit-mode cameras render skinned meshes in bind pose, so bake the current pose into static meshes
        static GameObject BakePose(GameObject ch)
        {
            var proxy = new GameObject("~AppIconPose");
            foreach (var smr in ch.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = new Mesh();
                smr.BakeMesh(m, true);
                var part = new GameObject(smr.name);
                part.transform.SetParent(proxy.transform, false);
                part.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = m;
                part.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            }
            foreach (var mr in ch.GetComponentsInChildren<MeshRenderer>())
            {
                var part = Object.Instantiate(mr.gameObject, mr.transform.position, mr.transform.rotation, proxy.transform);
                part.transform.localScale = mr.transform.lossyScale;
            }
            return proxy;
        }

        static AnimationClip FindClip(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
