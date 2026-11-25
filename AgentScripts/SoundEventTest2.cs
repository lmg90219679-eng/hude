using System.Threading.Tasks; using UnityEngine; using System.Text; using System.Linq; using System.Reflection; using System.Collections.Generic;
using Escape.Core; using Escape.Audio; using Escape.Player; using Escape.Interaction;
public static class SoundEventTest2 {
  static List<AudioSource> Live() => Object.FindObjectsByType<AudioSource>().Where(a => a.gameObject.name.StartsWith("Sfx_")).ToList();
  static async Task Clean() { foreach (var a in Live()) Object.Destroy(a.gameObject); await Task.Delay(150); }
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var p = Object.FindAnyObjectByType<PlayerController>(); var ga = GameAudio.Instance;
    var fNext = typeof(GameAudio).GetField("nextEvent", BindingFlags.Instance | BindingFlags.NonPublic);
    var fHinge = typeof(Door).GetField("hinge", BindingFlags.Instance | BindingFlags.NonPublic);
    var m = typeof(Door).GetMethod("PlayDoorSound", BindingFlags.Instance | BindingFlags.NonPublic);
    var expected = new Dictionary<int, string[]> {
      {0,new[]{"Ev_Moan","Ev_Drip","Ev_Scrape","Ev_Knock","Ev_Slam","Whisper"}}, {1,new[]{"Ev_Scrape","Ev_Knock","Ev_Moan","Ev_Drip"}},
      {2,new[]{"Ev_Moan","Ev_Drip","Ev_Scrape","Ev_Slam"}}, {3,new[]{"Ev_Moan","Ev_Scrape","Ev_Knock"}}, {4,new[]{"Ev_Slam","Ev_Knock"}} };
    foreach (int f in new[] { 4, 3, 2, 1, 0 }) {
      var sc = TowerLayout.RoomCenter(f, TowerLayout.StartRoom); p.Teleport(new Vector3(sc.x, TowerLayout.FloorY(f) + 0.1f, sc.z), 0);
      await Task.Delay(400); await Clean();
      var seen = new HashSet<string>(); float minD = 999, maxD = 0; int count = 0;
      for (int i = 0; i < 50; i++) {
        fNext.SetValue(ga, 0f); await Task.Delay(40);
        foreach (var a in Live()) { if (!seen.Add(a.gameObject.name.Substring(4)) ) { } }
        foreach (var a in Live().Where(a => a.time < 0.15f)) { float d = Vector3.Distance(a.transform.position, p.transform.position); minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d); count++; }
      }
      var exp = expected[f]; var extra = seen.Where(s => !exp.Contains(s)).ToList(); var missing = exp.Where(s => !seen.Contains(s)).ToList();
      sb.AppendLine($"floor {f}: heard={string.Join(",", seen.OrderBy(x=>x))} unexpected=[{string.Join(",", extra)}] missing=[{string.Join(",", missing)}] dist {minD:F1}-{maxD:F1}m");
      await Clean();
    }
    var hinged = Door.All.Where(d => d != null && (Transform)fHinge.GetValue(d) != null).Take(1).ToList();
    var slide = Door.All.Where(d => d != null && (Transform)fHinge.GetValue(d) == null).Take(1).ToList();
    sb.AppendLine($"hinged doors={Door.All.Count(d => d != null && (Transform)fHinge.GetValue(d) != null)} slide doors={Door.All.Count(d => d != null && (Transform)fHinge.GetValue(d) == null)}");
    var q = new[] { new Vector3(0,0,0) };
    foreach (var d in hinged.Concat(slide)) {
      p.Teleport(d.transform.position + Vector3.up * 0.1f + Vector3.right * 1.5f, 0); await Task.Delay(300);
      await Clean(); m.Invoke(d, new object[] { 1f }); await Task.Delay(120);
      sb.AppendLine($"door hinge={((Transform)fHinge.GetValue(d) != null)} -> " + string.Join(",", Live().Select(a => a.gameObject.name + "@" + Vector3.Distance(a.transform.position, p.transform.position).ToString("F0") + "m")));
    }
    await Clean();
    return sb.ToString();
  }
}
