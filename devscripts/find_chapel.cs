using System.Linq; using Escape.Core;
var res = new System.Collections.Generic.List<string>();
for (int f = 0; f < TowerLayout.Floors; f++) for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) { if (TowerLayout.IsVoidRoom(r)) continue; var e = RoomThemes.Get(f, r); if (e.name.Contains("예배")) res.Add($"{f}:{r}:{e.name}:{string.Join("/", e.props ?? new string[0])}"); }
return res.Count + " " + string.Join(" | ", res);
