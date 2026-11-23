using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using Escape.Player; using Escape.Core;
public static class ChaseTest {
  public static async Task<string> Run(string name, string tag) {
    var sb = new StringBuilder();
    var c = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include).First(x => x.name == name);
    var p = Object.FindAnyObjectByType<PlayerController>();
    var cam = Camera.main; var o = cam.transform.position; var f = cam.transform.forward; f.y = 0; f.Normalize();
    bool placed = false;
    for (float d = 10f; d >= 5f && !placed; d -= 1f) for (float yo = 0; yo <= 40 && !placed; yo += 10) foreach (float s in new[]{1f,-1f}) {
      var dir = Quaternion.Euler(0, yo * s, 0) * f; var q = o + dir * d; q.y = c.transform.position.y;
      if (!NavMesh.SamplePosition(q, out var hit, 0.6f, NavMesh.AllAreas)) continue;
      if (Physics.Linecast(o, hit.position + Vector3.up * 1.4f)) continue;
      var ag = c.GetComponent<NavMeshAgent>(); ag.Warp(hit.position);
      var l = o - hit.position; l.y = 0; c.transform.rotation = Quaternion.LookRotation(l);
      placed = true; sb.AppendLine($"placed d={d} yaw={yo*s}"); break;
    }
    if (!placed) return "no spot";
    for (int i = 0; i < 14; i++) {
      await Task.Delay(450);
      ScreenCapture.CaptureScreenshot($"C:/Projects/Escape/devscripts/shots/{tag}_{i}.png");
      var gf = GameFlow.Instance;
      sb.AppendLine($"{i} state={c.Current} dist={Vector3.Distance(c.transform.position, p.transform.position):F1} flow={gf.State} fade={gf.FadeAlpha:F2}");
      if (gf.State == GameState.Caught && i > 6) break;
    }
    return sb.ToString();
  }
}
