using System.Threading.Tasks; using UnityEngine; using System.Linq; using System.Text; using Escape.Player; using Escape.Core; using Escape.Interaction;
public static class ExitTest {
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var p = Object.FindAnyObjectByType<PlayerController>(); var cc = p.GetComponent<CharacterController>();
    var all = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include);
    var targets = new[]{ "FoldingScreen_3F","HospitalBed_3F","LaundryCart_B2","LaundryCart_2F","Locker" };
    int bad = 0, n = 0;
    foreach (var h in all.Where(x => targets.Any(t => x.name == t))) {
      var ex = h.transform.Find("ExitPoint"); var hp = h.transform.Find("HidePoint"); if (ex == null) continue;
      int f = TowerLayout.FloorOf(ex.position.y); float fy = TowerLayout.FloorY(f);
      p.Teleport(ex.position + Vector3.up * 0.05f, 0); await Task.Delay(250); Physics.SyncTransforms();
      float best = 0; var o = p.transform.position;
      for (int a = 0; a < 360; a += 45) { p.Teleport(o, 0); Physics.SyncTransforms(); var d = Quaternion.Euler(0, a, 0) * Vector3.forward; cc.Move(d * 1.0f); best = Mathf.Max(best, (p.transform.position - o).magnitude); }
      n++; if (best < 0.9f) { bad++; sb.AppendLine($"TRAPPED? {h.name} @ {h.transform.position:F0} best move {best:F2}"); }
    }
    sb.AppendLine($"checked {n} trapped {bad}");
    return sb.ToString();
  }
}
