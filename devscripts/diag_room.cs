using UnityEngine; using UnityEngine.AI; using System.Linq; using Escape.Core; using Escape.Interaction;
foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)) o.enabled = false;
var sb = new System.Text.StringBuilder();
var targets = new[] { (0, 133), (1, 26), (1, 82), (1, 116), (2, 67), (2, 130), (3, 124) };
foreach (var (f, room) in targets)
{
    Vector3 s0 = TowerLayout.RoomCenter(f, TowerLayout.StartRoom);
    NavMesh.SamplePosition(s0, out var sh, 3f, NavMesh.AllAreas);
    Vector3 c = TowerLayout.RoomCenter(f, room); NavMesh.SamplePosition(c, out var h, 4f, NavMesh.AllAreas);
    var path = new NavMeshPath(); NavMesh.CalculatePath(sh.position, h.position, NavMesh.AllAreas, path);
    var last = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : sh.position;
    var doors = Door.All.Where(d => d.floor == f && (d.roomA == room || d.roomB == room)).ToList();
    sb.AppendLine($"{TowerLayout.Label(f)} 방{room} {RoomThemes.Get(f, room).name} status={path.status} 끝={last:F1} 중심={h.position:F1} 문 {doors.Count}개");
    foreach (var d in doors)
    {
        bool onNav = NavMesh.SamplePosition(d.transform.position, out var dh, 0.5f, NavMesh.AllAreas);
        sb.AppendLine($"   {d.name} 문중심 {d.transform.position:F1} NavMesh위={onNav} 잠김={!string.IsNullOrEmpty(d.lockKeyId)} secret={d.secret}");
    }
}
foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)) o.enabled = true;
return sb.ToString();
