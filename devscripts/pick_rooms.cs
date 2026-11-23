using System.Linq; using Escape.Core;
var rng = new System.Random(77); var res = new System.Collections.Generic.List<string>();
for (int f = 0; f < TowerLayout.Floors; f++) {
  var rooms = Enumerable.Range(0, TowerLayout.RoomsPerFloor).Where(r => !TowerLayout.IsVoidRoom(r) && !TowerLayout.IsCorridorRoom(r) && TowerLayout.RoomRect(r).width >= 6f).OrderBy(_ => rng.Next()).Take(3);
  foreach (var r in rooms) res.Add($"{f} {r}");
}
return string.Join(";", res);
