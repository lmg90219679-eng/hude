using UnityEditor; using UnityEditor.Build.Reporting; using UnityEngine; using System.Linq; using System.Text; using System.IO;
public static class DoBuild {
  public static string Run() {
    var sb = new StringBuilder(); var t0 = System.DateTime.Now;
    Directory.CreateDirectory("Builds/Windows");
    var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    var opt = new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/Windows/Escape.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
    var report = BuildPipeline.BuildPlayer(opt);
    var s = report.summary;
    sb.AppendLine($"result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize / 1048576}MB time={(System.DateTime.Now - t0).TotalMinutes:F1}min");
    foreach (var step in report.steps) foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(10)) sb.AppendLine($"  [{step.name}] {m.content}");
    return sb.ToString();
  }
}
