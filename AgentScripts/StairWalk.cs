using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Reflection;
using Escape.Core; using Escape.Interaction; using Escape.Player; using Escape.Enemy;
public static class StairWalk {
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>(); var cc = p.GetComponent<CharacterController>();
    foreach (var c in Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include)) { c.enabled = false; var a = c.GetComponent<NavMeshAgent>(); if (a) a.enabled = false; }
    foreach (var d in Door.All.Where(x => !x.secret && !x.requiresPower)) { if (string.IsNullOrEmpty(d.lockKeyId)) { if (!d.IsOpen) d.Interact(inter); } }
    int ok = 0, bad = 0;
    foreach (var sd in Door.All.Where(x => x.transform.Find("Hinge") != null)) {
      int f = sd.floor; float fy = TowerLayout.FloorY(f);
      if (!sd.IsOpen) sd.Interact(inter);
      await Task.Delay(1200);
      // 계단통 방 쪽을 향한 방향 (방이 큰 쪽이 홀, 작은 쪽이 계단통: roomB가 새로 열리는 쪽)
      var hall = TowerLayout.RoomCenter(f, sd.roomA); var shaft = TowerLayout.RoomCenter(f, sd.roomB);
      Vector3 toShaft = new Vector3(shaft.x - sd.transform.position.x, 0, shaft.z - sd.transform.position.z); Vector3 dirAxis = Mathf.Abs(toShaft.x) > Mathf.Abs(toShaft.z) ? new Vector3(Mathf.Sign(toShaft.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toShaft.z));
      Vector3 target = sd.transform.position + dirAxis * 1.3f; target.y = fy + 0.1f;
      Vector3 startRaw = sd.transform.position - dirAxis * 3.2f; startRaw.y = fy + 0.1f;
      // 홀 안의 걸을 수 있는 시작점 후보를 여러 개 시험한다 (문 앞 3m 안쪽 여러 자리)
      int reached = 0, tried = 0;
      foreach (var off in new[]{ Vector3.zero, Quaternion.Euler(0,90,0) * dirAxis * 1.2f, Quaternion.Euler(0,-90,0) * dirAxis * 1.2f, -dirAxis * 1.0f }) {
        var st = startRaw + off; if (!NavMesh.SamplePosition(st, out var hs, 1.0f, NavMesh.AllAreas) || !NavMesh.SamplePosition(target, out var ht, 1.5f, NavMesh.AllAreas)) continue;
        tried++;
        var path = new NavMeshPath(); NavMesh.CalculatePath(hs.position, ht.position, NavMesh.AllAreas, path); if (path.status == NavMeshPathStatus.PathInvalid) continue;
        p.Teleport(hs.position + Vector3.up * 0.05f, 0); Physics.SyncTransforms();
        int ci = 1; float best = 1e9f; int stall = 0; bool arrived = false;
        for (int step = 0; step < 900 && ci < path.corners.Length; step++) {
          var cur = p.transform.position; var tgt = path.corners[ci]; var dv = tgt - cur; dv.y = 0;
          if (dv.magnitude < 0.25f) { ci++; continue; }
          cc.Move(dv.normalized * 3f * 0.05f + Vector3.down * 0.02f);
          float rem = Vector3.Distance(new Vector3(p.transform.position.x, 0, p.transform.position.z), new Vector3(ht.position.x, 0, ht.position.z));
          if (rem < best - 0.02f) { best = rem; stall = 0; } else stall++;
          if (stall > 80) break;
          if (rem < 0.7f) { arrived = true; break; }
        }
        if (arrived) reached++;
      }
      if (tried > 0 && reached == tried) ok++; else { bad++; sb.AppendLine($"  {sd.name}: reached {reached}/{tried}"); }
    }
    sb.Insert(0, $"stair doors walk-through ok {ok}, bad {bad}\n");
    return sb.ToString();
  }
}
