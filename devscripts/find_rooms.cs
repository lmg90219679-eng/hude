using UnityEngine; using System.Text; using Escape.Core;
var sb = new StringBuilder();
foreach (var (f, theme) in new[] { (4, "수술실"), (3, "병실"), (1, "영안실"), (2, "구내식당"), (0, "보일러실"), (3, "소아 병실"), (4, "원장실"), (2, "도서실") })
{
    for (int r = 0; r < TowerLayout.RoomsPerFloor; r++)
    {
        var e = RoomThemes.Get(f, r);
        if (e.theme != theme) continue;
        var c = TowerLayout.RoomCenter(f, r);
        sb.AppendLine($"{f} {theme} {e.name} room={r} center=({c.x:F1},{c.y:F1},{c.z:F1}) floor={e.floorTex} wall={e.wallTex} tint={e.tint} light={e.light} props={string.Join(",", e.props)}");
        break;
    }
}
return sb.ToString();
