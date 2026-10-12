using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleArcade.EditorTools
{
    /// <summary>Windows / Android player builds with a version stamp (date + git hash) baked into the game.</summary>
    [InitializeOnLoad]
    public static class BuildScript
    {
        const string StampPath = "Assets/_Project/Scripts/Runtime/Core/BuildStamp.cs";
        const string PendingKey = "IdleArcade.PendingWindowsBuild";
        const string TargetKey = PendingKey + ".target";

        // writing the stamp recompiles scripts, which wipes delayCall; resume the build after the reload
        static BuildScript()
        {
            if (SessionState.GetBool(PendingKey, false)) EditorApplication.delayCall += DoBuild;
        }

        [MenuItem("IdleArcade/Build/Windows Player")]
        public static void BuildWindows() => Queue(BuildTarget.StandaloneWindows64);

        [MenuItem("IdleArcade/Build/Android APK")]
        public static void BuildAndroid() => Queue(BuildTarget.Android);

        [MenuItem("IdleArcade/Build/WebGL (browser)")]
        public static void BuildWebGL() => Queue(BuildTarget.WebGL);

        /// <summary>
        /// Command line: Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod IdleArcade.EditorTools.BuildScript.BuildWebGLBatch
        /// (write BuildStamp.cs before launching; batch mode compiles once).
        /// </summary>
        public static void BuildWebGLBatch()
        {
            var report = Run(BuildTarget.WebGL);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static void Queue(BuildTarget target)
        {
            string stamp = DateTime.Now.ToString("dd.MM HH:mm") + " " + GitHash();
            File.WriteAllText(StampPath,
                "namespace IdleArcade\n{\n    /// <summary>Overwritten by the build script with date and git hash.</summary>\n    public static class BuildStamp\n    {\n" +
                $"        public const string Value = \"{stamp}\";\n    }}\n}}\n");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetString(PendingKey + ".stamp", stamp);
            SessionState.SetInt(TargetKey, (int)target);
            AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceSynchronousImport);
            EditorApplication.delayCall += DoBuild;
        }

        static void DoBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += DoBuild; return; }
            if (!SessionState.GetBool(PendingKey, false)) return;
            // wait until the recompiled stamp is live so the build shows the right version
            if (IdleArcade.BuildStamp.Value != SessionState.GetString(PendingKey + ".stamp", "")) { EditorApplication.delayCall += DoBuild; return; }
            SessionState.SetBool(PendingKey, false);
            Run((BuildTarget)SessionState.GetInt(TargetKey, (int)BuildTarget.StandaloneWindows64));
        }

        static BuildReport Run(BuildTarget target)
        {
            bool android = target == BuildTarget.Android;
            bool web = target == BuildTarget.WebGL;
            if (web) ProjectSetup.ConfigureWebGL();
            if (android)
            {
                ProjectSetup.ConfigureAndroid();
                // the Windows user folder has Cyrillic letters; Gradle's prefab step can't load jars from such a path
                Environment.SetEnvironmentVariable("GRADLE_USER_HOME", "C:/dev/_gradle");
            }
            string outDir = Path.GetFullPath(android ? "Builds/Android" : web ? "Builds/WebGL" : "Builds/Windows");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.BootScenePath, SceneBuilder.GameScenePath },
                locationPathName = web ? outDir : Path.Combine(outDir, android ? "VillageMarket.apk" : "VillageMarket.exe"),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            };
            Debug.Log($"[Build] Building {target} player...");
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[Build] OK {report.summary.totalSize / (1024 * 1024)} MB in {report.summary.totalTime.TotalSeconds:0}s -> {opts.locationPathName}");
            else
                Debug.LogError($"[Build] FAILED: {report.summary.result} errors={report.summary.totalErrors}");
            return report;
        }

        static string GitHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath(".")
                };
                using var p = Process.Start(psi);
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                return string.IsNullOrEmpty(s) ? "local" : s;
            }
            catch { return "local"; }
        }
    }
}
