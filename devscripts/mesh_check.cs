using UnityEngine; using UnityEngine.AI;
var sb = new System.Text.StringBuilder();
foreach (var c in Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include)) {
  var ag = c.GetComponent<NavMeshAgent>();
  bool near = NavMesh.SamplePosition(c.transform.position, out var h, 1.5f, NavMesh.AllAreas);
  sb.AppendLine($"{c.name} active={c.gameObject.activeSelf} enabled={c.enabled} onMesh={ag.isOnNavMesh} sample={near} dy={(near?h.position.y-c.transform.position.y:0):F2} tri={NavMesh.CalculateTriangulation().vertices.Length}");
}
return sb.ToString();
