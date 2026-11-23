using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using Escape.Core; using Escape.Interaction; using Escape.Items;
public static class PieceReach {
  public static async Task<string> Run() {
    var sb = new StringBuilder();
    foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include)) o.enabled = false; await Task.Delay(1500);
    foreach (var c in Object.FindObjectsByType<Container>(FindObjectsInactive.Include).Where(c => c.Content != null && c.Content.Type.HasValue && ItemInfo.IsPiece(c.Content.Type.Value))) {
      int f = c.floor; var s = TowerLayout.RoomCenter(f, TowerLayout.StartRoom);
      var from = new Vector3(s.x, TowerLayout.FloorY(f) + 0.1f, s.z);
      bool a = NavMesh.SamplePosition(from, out var ha, 4f, NavMesh.AllAreas); bool b = NavMesh.SamplePosition(c.transform.position, out var hb, 4f, NavMesh.AllAreas);
      var p = new NavMeshPath(); NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p);
      var end = p.corners.Length > 0 ? p.corners[p.corners.Length-1] : Vector3.zero;
      sb.AppendLine($"{c.Content.Type} f{f} room{c.room} {TowerLayout.RoomAt(c.transform.position)} pos={c.transform.position:F1} snap={b} snapDist={(b ? Vector3.Distance(hb.position, c.transform.position) : -1):F2} path={p.status} endGap={Vector3.Distance(new Vector3(end.x,0,end.z), new Vector3(hb.position.x,0,hb.position.z)):F1}");
    }
    return sb.ToString();
  }
}
