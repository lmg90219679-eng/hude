using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Collections.Generic;
using Escape.Core; using Escape.Interaction; using Escape.Items;
public static class TimeBudget {
  const float Walk = 3.0f, OpenDrawer = 2.5f, OpenDoor = 2.0f, Stair = 6f;
  public static float PathLen(Vector3 a, Vector3 b, out bool ok) {
    ok = false;
    if (!NavMesh.SamplePosition(a, out var ha, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 3f, NavMesh.AllAreas)) return 0;
    var p = new NavMeshPath(); if (!NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p) || p.status == NavMeshPathStatus.PathInvalid) return 0;
    if (p.status == NavMeshPathStatus.PathPartial) { var end = p.corners[p.corners.Length-1]; if (Vector2.Distance(new Vector2(end.x, end.z), new Vector2(b.x, b.z)) > 3.5f || Mathf.Abs(end.y - b.y) > 2f) return 0; }
    ok = true; float L = 0; for (int i = 1; i < p.corners.Length; i++) L += Vector3.Distance(p.corners[i-1], p.corners[i]); return L;
  }
  static StairPortal Portal(int floor, bool up, bool west) => StairPortal.All.FirstOrDefault(s => s.floor == floor && s.up == up && (s.transform.position.x < 50f) == west);
  public static async Task<(float t, int opened, int doors, bool gotPiece)> Explore(int f, Vector3 start, HashSet<string> keys) {
    var conts = Object.FindObjectsByType<Container>(FindObjectsInactive.Include).Where(c => c.floor == f).ToList();
    var doors = Door.All.Where(d => d.floor == f && !string.IsNullOrEmpty(d.lockKeyId) && !d.secret).ToList();
    var done = new HashSet<Container>(); var unlocked = new HashSet<Door>(); float t = 0; Vector3 pos = start; int opened = 0, nd = 0; bool piece = false;
    while (!piece) {
      Container best = null; float bl = 1e9f;
      foreach (var c in conts) { if (done.Contains(c)) continue; float L = PathLen(pos, c.transform.position, out bool ok); if (ok && L < bl) { bl = L; best = c; } }
      if (best == null) break;
      t += bl / Walk + OpenDrawer; pos = best.transform.position; done.Add(best); opened++;
      var drop = best.Content; if (drop == null) continue;
      if (drop.Type.HasValue && ItemInfo.IsPiece(drop.Type.Value)) piece = true;
      else if (!drop.Type.HasValue && drop.KeyId != null) {
        keys.Add(drop.KeyId);
        foreach (var d in doors.Where(d => keys.Contains(d.lockKeyId) && !unlocked.Contains(d))) { unlocked.Add(d); nd++; t += OpenDoor; typeof(Door).GetField("unlocked", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(d, true); }
        await Task.Delay(150);
      }
    }
    return (t, opened, nd, piece);
  }
  static float Leg(Vector3 from, Vector3 to) { float L = PathLen(from, to, out bool ok); return ok ? L / Walk : -1; }
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var keys = new HashSet<string>(); float total = 0;
    var s1 = TowerLayout.RoomCenter(2, TowerLayout.StartRoom); Vector3 start1F = new Vector3(s1.x, TowerLayout.FloorY(2) + 0.1f, s1.z);
    var exitDoor = Object.FindAnyObjectByType<ExitDoor>(FindObjectsInactive.Include).transform.position;
    // 탐험 순서: 1F -> B1 -> B2 -> (B1 -> 1F) -> 2F -> 3F -> (2F -> 1F -> 정문)
    Vector3 Arr(StairPortal p) => p.partner.exitPoint.position;
    var explore = new (int f, Vector3 start, string name)[] {
      (2, start1F, "1F"), (1, Arr(Portal(2, false, false)), "B1"), (0, Arr(Portal(1, false, true)), "B2"),
      (3, Arr(Portal(2, true, true)), "2F"), (4, Arr(Portal(3, true, false)), "3F") };
    var res = new Dictionary<string, (float t, int o, int d, bool p)>();
    foreach (var e in explore) { var r = await Explore(e.f, e.start, keys); res[e.name] = (r.t, r.opened, r.doors, r.gotPiece); total += r.t; sb.AppendLine($"  {e.name}: 탐색 {r.t/60f:F1}분 (서랍 {r.opened}개, 문 {r.doors}개, 조각 {(r.gotPiece ? "획득" : "못 찾음")})"); }
    // 층 이동과 복귀 (탐험이 끝난 층에서 계단까지 걷기): 모든 문이 열려 있다고 보고 계산
    foreach (var d in Door.All.Where(d => !string.IsNullOrEmpty(d.lockKeyId))) typeof(Door).GetField("unlocked", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(d, true); await Task.Delay(300);
    float legs = 0; int stairs = 0;
    legs += Leg(Arr(Portal(2, false, false)) , Portal(2, false, false).transform.position) * 0; // 자리 표시
    var seq = new List<(Vector3 a, Vector3 b)> {
      (Vector3.zero, Vector3.zero) }; seq.Clear();
    // 1F 탐험 후 -> 동쪽 아래 계단 (탐험 종료 지점은 모르므로 시작 방 기준으로 대신)
    seq.Add((start1F, Portal(2, false, false).transform.position));
    seq.Add((Arr(Portal(2, false, false)), Portal(1, false, true).transform.position));      // B1: 동 -> 서 (B2로)
    seq.Add((Arr(Portal(1, false, true)), Portal(0, true, true).transform.position));        // B2: 도착 -> 위로(서)
    seq.Add((Arr(Portal(0, true, true)), Portal(1, true, false).transform.position));         // B1: 서 -> 동 (1F로)
    seq.Add((Arr(Portal(1, true, false)), Portal(2, true, true).transform.position));         // 1F: 동 -> 서 (2F로)
    seq.Add((Arr(Portal(2, true, true)), Portal(3, true, false).transform.position));         // 2F: 서 -> 동 (3F로)
    seq.Add((Arr(Portal(3, true, false)), Portal(4, false, false).transform.position));       // 3F: 도착 -> 아래(동)
    seq.Add((Arr(Portal(4, false, false)), Portal(3, false, true).transform.position));       // 2F: 동 -> 서 (1F로)
    seq.Add((Arr(Portal(3, false, true)), exitDoor));                                           // 1F: 서 -> 정문
    foreach (var s in seq) { float l = Leg(s.a, s.b); if (l < 0) { sb.AppendLine("  경로 실패"); continue; } legs += l; stairs++; }
    legs += (stairs - 1) * Stair;
    total += legs;
    sb.AppendLine($"  층 사이 이동과 복귀 걷기: {legs/60f:F1}분 (계단 {stairs-1}회 포함)");
    sb.AppendLine($"  합계(추격·숨기·헤맴 제외): {total/60f:F1}분");
    return sb.ToString();
  }
}
