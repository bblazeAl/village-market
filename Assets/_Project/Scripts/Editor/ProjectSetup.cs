using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace IdleArcade.EditorTools
{
    /// <summary>Player, quality and text settings. Safe to run repeatedly.</summary>
    public static class ProjectSetup
    {
        public const string FontDir = "Assets/_Project/Fonts";

        [MenuItem("IdleArcade/Setup/Configure Project")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Alena";
            PlayerSettings.productName = "Village Market";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.alena.villagemarket");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            ConfigureUrp();
            ImportTmpEssentials();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureUrp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (asset == null) return;
            var so = new SerializedObject(asset);
            void SetInt(string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; }
            void SetFloat(string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; }
            void SetBool(string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; }
            SetInt("m_MSAA", 4);
            SetFloat("m_ShadowDistance", 55f);
            SetInt("m_ShadowCascadeCount", 2);
            SetBool("m_SoftShadowsSupported", true);
            SetInt("m_MainLightShadowmapResolution", 4096);
            SetBool("m_SupportsHDR", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = asset;
            EditorUtility.SetDirty(asset);

            // phones (Android uses the "Mobile" quality level): lighter shadows, slightly lower 3D resolution, UI stays sharp
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (mobile != null)
            {
                so = new SerializedObject(mobile);
                SetInt("m_MSAA", 4);
                SetFloat("m_RenderScale", 0.9f);
                SetFloat("m_ShadowDistance", 45f);
                SetInt("m_ShadowCascadeCount", 1);
                SetBool("m_SoftShadowsSupported", true);
                SetInt("m_MainLightShadowmapResolution", 2048);
                SetBool("m_MainLightShadowsSupported", true);
                SetBool("m_SupportsHDR", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mobile);
            }

            int count = QualitySettings.names.Length;
            for (int i = 0; i < count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = QualitySettings.names[i] == "Mobile" && mobile != null ? mobile : asset;
            }
            QualitySettings.SetQualityLevel(count - 1, false);
        }

        /// <summary>
        /// Browser build for a "open the link and play" page (GitHub Pages): gzip with the JS decompression
        /// fallback (static hosting sends no Content-Encoding headers), cached data, custom full-window template.
        /// </summary>
        public static void ConfigureWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:VillageMarket";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.alena.villagemarket");
            AssetDatabase.SaveAssets();
        }

        /// <summary>Android player settings: portrait phone app, 64-bit (IL2CPP + ARM64), sideloadable APK.</summary>
        public static void ConfigureAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.alena.villagemarket");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            EditorUserBuildSettings.buildAppBundle = false;
            AssetDatabase.SaveAssets();
        }

        static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        /// <summary>Creates the dynamic SDF font asset (Latin + Cyrillic) and two outline material presets.</summary>
        public static (TMP_FontAsset font, Material outline, Material world) BuildFont()
        {
            string ttfPath = $"{FontDir}/Rubik-ExtraBold.ttf";
            string assetPath = $"{FontDir}/Rubik-ExtraBold SDF.asset";
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa == null)
            {
                fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 10, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fa.name = "Rubik-ExtraBold SDF";
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.atlasTextures[0].name = "Rubik Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = "Rubik Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                const string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·×—–…«»№" +
                                     "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
                fa.TryAddCharacters(chars, out _);
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }

            Material MakeVariant(string name, float outline, Color outlineColor, bool underlay)
            {
                string p = $"{FontDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null)
                {
                    m = new Material(fa.material);
                    AssetDatabase.CreateAsset(m, p);
                }
                m.CopyPropertiesFromMaterial(fa.material);
                m.shader = fa.material.shader;
                m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTextures[0]);
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
                m.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                m.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.6f);
                if (underlay)
                {
                    m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
                    m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                    m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
                    m.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline);
                }
                EditorUtility.SetDirty(m);
                return m;
            }

            var outlineMat = MakeVariant("Rubik Outline", 0.22f, new Color32(60, 36, 24, 255), true);
            var worldMat = MakeVariant("Rubik World", 0.25f, new Color32(50, 30, 20, 255), false);
            AssetDatabase.SaveAssets();
            return (fa, outlineMat, worldMat);
        }
    }
}
