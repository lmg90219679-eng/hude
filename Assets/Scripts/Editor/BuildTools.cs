using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>Windows 실행 파일 빌드. 메뉴: Escape > Build Windows. 결과: Builds/Windows/Escape.exe</summary>
    public static class BuildTools
    {
        const string OutputDir = "Builds/Windows";

        [MenuItem("Escape/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = GetScenes(),
                locationPathName = Path.Combine(OutputDir, "Escape.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[BuildTools] 결과: {summary.result}, 크기 {summary.totalSize / (1024 * 1024)}MB, 오류 {summary.totalErrors}");
        }

        public static void BuildWindowsFromCommandLine()
        {
            BuildWindows();
            EditorApplication.Exit(0);
        }

        static string[] GetScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }
    }
}
