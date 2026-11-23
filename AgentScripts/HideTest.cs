using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Reflection;
using Escape.Player; using Escape.Core; using Escape.Interaction; using Escape.Enemy;
public static class HideTest {
  static string[] Tag = { "B2", "B1", "1F", "2F", "3F" };
  public static async Task<string> Run() {
    var sb = new StringBuilder(); int fail = 0, total = 0;
    var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>();
    foreach (var c in Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include)) c.GraceUntil = 0f;
    var fl = Object.FindAnyObjectByType<Flashlight>(); typeof(Flashlight).GetMethod("SetOn", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fl, new object[]{true});
    var all = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include);
    var groups = all.GroupBy(h => System.Text.RegularExpressions.Regex.Replace(h.name, @"_?(B2|B1|1F|2F|3F|\d+).*$", "")).OrderBy(g => g.Key);
    foreach (var g in groups) {
      var list = g.ToList(); var pick = list.Count <= 6 ? list : list.GroupBy(h => TowerLayout.FloorOf(h.transform.position.y)).Select(x => x.First()).Take(4).ToList();
      int shot = 0;
      foreach (var h in pick) {
        total++;
        var hp = h.transform.Find("HidePoint"); var ex = h.transform.Find("ExitPoint");
        if (hp == null || ex == null) { sb.AppendLine($"FAIL {h.name} no points"); fail++; continue; }
        int f = TowerLayout.FloorOf(hp.position.y); float fy = TowerLayout.FloorY(f);
        var fwd = ex.position - hp.position; fwd.y = 0; fwd.Normalize();
        Vector3 stand = hp.position + fwd * 2.0f; stand.y = fy + 0.1f;
        p.Teleport(stand, Quaternion.LookRotation(-fwd).eulerAngles.y);
        await Task.Delay(350);
        Physics.SyncTransforms();
        // 1. 눈높이에서 숨는 곳 쪽으로 쏘면 그 소품이 맞는지
        var eye = stand + Vector3.up * 1.6f; var tgt = hp.position + Vector3.up * 0.8f;
        bool aim = false; string hitName = "-";
        var hits = Physics.RaycastAll(eye, (tgt - eye).normalized, 4f, ~0, QueryTriggerInteraction.Ignore).Where(x => x.collider.transform.root != p.transform.root).OrderBy(x => x.distance).ToArray();
        if (hits.Length > 0) { aim = hits[0].collider.GetComponentInParent<HidingSpot>() == h; hitName = hits[0].collider.name; }
        // 2. 들어가기
        inter.Player.EnterHiding(hp.position, ex.position, hp.rotation, -1f);   // eyeHeight는 아래에서 HidingSpot으로 다시
        p.ExitHiding();
        await Task.Delay(100);
        p.Teleport(stand, Quaternion.LookRotation(-fwd).eulerAngles.y); await Task.Delay(150);
        h.Interact(inter);
        bool hidden = p.IsHidden; float camH = Camera.main.transform.position.y - fy;
        var room = TowerLayout.RoomAt(hp.position); bool inRoom = room >= 0 && TowerLayout.RoomRect(room).Contains(new Vector2(p.transform.position.x, p.transform.position.z));
        // 3. 추적자가 못 보는지
        var ch = Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include).FirstOrDefault(x => x.name == $"Chaser_{Tag[f]}_0");
        string seen = "n/a";
        if (ch != null) {
          bool placed = false;
          for (float d = 4.5f; d >= 3f && !placed; d -= 0.5f) for (int a = 0; a < 360 && !placed; a += 30) {
            var q = hp.position + Quaternion.Euler(0, a, 0) * Vector3.forward * d; q.y = fy;
            if (!NavMesh.SamplePosition(q, out var nh, 0.5f, NavMesh.AllAreas)) continue;
            if (Physics.Linecast(hp.position + Vector3.up * 1.2f, nh.position + Vector3.up * 1.4f)) continue;
            ch.GetComponent<NavMeshAgent>().Warp(nh.position); var l = hp.position - nh.position; l.y = 0; ch.transform.rotation = Quaternion.LookRotation(l); placed = true;
          }
          await Task.Delay(1100);
          seen = placed ? (ch.Current == ChaserAI.State.Chase ? "SEEN" : "unseen") : "noplace";
          if (shot++ == 0) { ScreenCapture.CaptureScreenshot($"C:/Projects/Escape/devscripts/shots/hide_{g.Key}.png"); await Task.Delay(200); }
          if (GameFlow.Instance.State == GameState.Caught) { sb.AppendLine($"FAIL {h.name} caught while hidden"); fail++; return sb.ToString(); }
          // 추적자를 멀리 치운다
          ch.GetComponent<NavMeshAgent>().Warp(NavMesh.SamplePosition(hp.position + Vector3.right * 14f, out var far, 6f, NavMesh.AllAreas) ? far.position : ch.transform.position);
        }
        // 4. 나오기
        p.ExitHiding(); await Task.Delay(120); Physics.SyncTransforms();
        var pos = p.transform.position;
        bool mesh = NavMesh.SamplePosition(pos, out var eh, 0.9f, NavMesh.AllAreas);
        bool blocked = Physics.OverlapCapsule(pos + Vector3.up * 0.4f, pos + Vector3.up * 1.5f, 0.28f, ~0, QueryTriggerInteraction.Ignore).Any(x => x.transform.root != p.transform.root);
        var cc = p.GetComponent<CharacterController>(); var before = p.transform.position; cc.Move(fwd * 0.6f); var moved = (p.transform.position - before).magnitude;
        bool ok = aim && hidden && inRoom && seen != "SEEN" && !blocked && moved > 0.3f && camH < 1.9f;
        if (!ok) fail++;
        sb.AppendLine($"{(ok ? "ok  " : "FAIL")} {h.name} f={f} aim={aim}({hitName}) hidden={hidden} inRoom={inRoom} camH={camH:F2} {seen} exitMesh={mesh} blocked={blocked} moved={moved:F2}");
      }
    }
    sb.AppendLine($"total {total} fail {fail}");
    return sb.ToString();
  }
}
