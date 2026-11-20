using UnityEngine; using System.Linq; using System.Collections.Generic; using Escape.Core;
int n = TowerLayout.RoomsPerFloor;
var adj = new Dictionary<int, List<int>>(); for (int i = 0; i < n; i++) adj[i] = new List<int>();
foreach (var e in TowerLayout.Edges) { adj[e.a].Add(e.b); adj[e.b].Add(e.a); }
var seen = new HashSet<int> { TowerLayout.StartRoom }; var q = new Queue<int>(); q.Enqueue(TowerLayout.StartRoom);
while (q.Count > 0) { int c = q.Dequeue(); foreach (int o in adj[c]) if (seen.Add(o)) q.Enqueue(o); }
var unreachable = Enumerable.Range(0, n).Where(i => !TowerLayout.IsVoidRoom(i) && !seen.Contains(i)).ToList();
int voids = Enumerable.Range(0, n).Count(TowerLayout.IsVoidRoom);
bool ok = TowerLayout.IsLayoutLocked(out string reason);
return $"방 {TowerLayout.RealRoomsPerFloor}개/층 x5층 = {TowerLayout.RealRoomsPerFloor * 5}, 없앤 자리 {voids}, 도달 불가 {unreachable.Count} {string.Join(",", unreachable)}, 잠금 {(ok ? "일치" : reason)}";
