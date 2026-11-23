using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Collections.Generic; using Escape.Core; using Escape.Interaction;

var sb = new System.Text.StringBuilder();
foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)) o.enabled = false;
Physics.SyncTransforms();

// ---- 소품 수집 ----
var props = new List<(string name, Bounds b, int floor)>();
void Add(Transform t)
{
    var col = t.GetComponent<BoxCollider>();
    if (col == null) return;
    props.Add((t.name, col.bounds, TowerLayout.FloorOf(col.bounds.center.y)));
}
var hp = GameObject.Find("Tower/HospitalProps"); if (hp != null) foreach (Transform t in hp.transform) Add(t);
var cl = GameObject.Find("Tower/RoomClutter"); if (cl != null) foreach (Transform t in cl.transform) Add(t);
foreach (var c in Object.FindObjectsByType<Container>(FindObjectsInactive.Include))
{
    var bc = c.GetComponentInChildren<BoxCollider>(); if (bc == null) continue;
    props.Add((c.name, bc.bounds, TowerLayout.FloorOf(bc.bounds.center.y)));
}
foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(x => x.name == "Locker")) Add(t);
sb.AppendLine($"소품 {props.Count}개 수집");

// ---- 1. 소품끼리 겹침 ----
var overlaps = new List<string>();
for (int i = 0; i < props.Count; i++)
    for (int j = i + 1; j < props.Count; j++)
    {
        if (props[i].floor != props[j].floor) continue;
        var a = props[i].b; var b = props[j].b;
        a.Expand(-0.1f); b.Expand(-0.1f);
        if (a.Intersects(b)) overlaps.Add($"{props[i].name}@{props[i].b.center:F0} x {props[j].name}@{props[j].b.center:F0}");
    }
sb.AppendLine($"[겹침] {overlaps.Count}쌍");
foreach (var s in overlaps.Take(8)) sb.AppendLine("  " + s);

// ---- 2. 방 밖으로 삐져나감 ----
var outside = new List<string>();
foreach (var p in props)
{
    int room = TowerLayout.RoomAt(p.b.center);
    if (room < 0) { outside.Add($"{p.name}@{p.b.center:F0} (방 없음)"); continue; }
    var r = TowerLayout.RoomRect(room);
    const float tol = 0.08f;
    if (p.b.min.x < r.xMin - tol || p.b.max.x > r.xMax + tol || p.b.min.z < r.yMin - tol || p.b.max.z > r.yMax + tol)
        outside.Add($"{p.name}@{p.b.center:F0} 방{room}");
}
sb.AppendLine($"[방 밖] {outside.Count}개");
foreach (var s in outside.Take(8)) sb.AppendLine("  " + s);

// ---- 3. 문 앞이 막힘 ----
var blocked = new List<string>(); int doorCount = 0;
foreach (var d in Door.All)
{
    var leaf = d.transform.Find("Leaf") ?? d.transform.Find("Hinge");
    if (leaf == null) continue;
    doorCount++;
    var panel = d.transform.Find("Leaf") ?? d.transform.Find("Hinge/Panel");
    bool alongX = panel.localScale.x > panel.localScale.z;
    Vector3 n = alongX ? Vector3.forward : Vector3.right;
    float w = alongX ? panel.localScale.x : panel.localScale.z;
    foreach (float side in new[] { 1f, -1f })
    {
        var center = d.transform.position + n * side * 0.9f + Vector3.up * 1f;
        var size = alongX ? new Vector3(w * 0.9f, 2f, 1.3f) : new Vector3(1.3f, 2f, w * 0.9f);
        var zone = new Bounds(center, size);
        foreach (var p in props)
            if (zone.Intersects(p.b)) { blocked.Add($"{d.name} 면{(side > 0 ? "+" : "-")} <- {p.name}@{p.b.center:F0}"); break; }
    }
}
sb.AppendLine($"[문 앞 막힘] {blocked.Count}곳 (문 {doorCount}개 검사)");
foreach (var s in blocked.Take(10)) sb.AppendLine("  " + s);

// ---- 4. 문 양쪽이 걸어서 닿는지 (잠긴 문은 열쇠로만 지나므로 문 앞쪽(복도 쪽)까지만 본다) ----
var unreachable = new List<string>();
var startPos = new Dictionary<int, Vector3>();
for (int f = 0; f < TowerLayout.Floors; f++)
{
    Vector3 s0 = TowerLayout.RoomCenter(f, TowerLayout.StartRoom);
    if (NavMesh.SamplePosition(new Vector3(s0.x, TowerLayout.FloorY(f) + 0.1f, s0.z), out var sh, 3f, NavMesh.AllAreas)) startPos[f] = sh.position;
}
bool Reach(int f, Vector3 target)
{
    if (!startPos.ContainsKey(f)) return false;
    if (!NavMesh.SamplePosition(new Vector3(target.x, TowerLayout.FloorY(f) + 0.1f, target.z), out var h, 1.2f, NavMesh.AllAreas)) return false;
    if (Mathf.Abs(h.position.y - TowerLayout.FloorY(f)) > 1.5f) return false;
    var path = new NavMeshPath();
    NavMesh.CalculatePath(startPos[f], h.position, NavMesh.AllAreas, path);
    return path.status == NavMeshPathStatus.PathComplete;
}
foreach (var d in Door.All)
{
    if (d.secret || d.requiresPower) continue;
    var panel = d.transform.Find("Leaf") ?? d.transform.Find("Hinge/Panel"); if (panel == null) continue;
    bool alongX = panel.localScale.x > panel.localScale.z;
    Vector3 n = alongX ? Vector3.forward : Vector3.right;
    bool locked = !string.IsNullOrEmpty(d.lockKeyId);
    foreach (float side in new[] { 1f, -1f })
    {
        var p = d.transform.position + n * side * 1.4f;
        int room = TowerLayout.RoomAt(p);
        if (locked && room >= 0 && !TowerLayout.IsCorridorRoom(room)) continue;   // 잠긴 문 너머는 열쇠로만
        if (!Reach(d.floor, p)) unreachable.Add($"{d.name} 면{(side > 0 ? "+" : "-")} 방{room}({(room >= 0 ? RoomThemes.Get(d.floor, room).name : "?")}) @{p:F0}");
    }
}
sb.AppendLine($"[문 양쪽 도달 불가] {unreachable.Count}곳");
foreach (var s in unreachable.Take(15)) sb.AppendLine("  " + s);

// ---- 5. 숨는 곳 출구 ----
var badExit = new List<string>();
foreach (var hs in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include))
{
    var ex = hs.transform.Find("ExitPoint"); if (ex == null) continue;
    if (!NavMesh.SamplePosition(ex.position, out var eh, 0.9f, NavMesh.AllAreas)) badExit.Add($"{hs.name}@{hs.transform.position:F0}");
}
sb.AppendLine($"[숨는 곳 출구가 걸을 수 없는 자리] {badExit.Count}개");
foreach (var s in badExit.Take(10)) sb.AppendLine("  " + s);

foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)) o.enabled = true;
return sb.ToString();
