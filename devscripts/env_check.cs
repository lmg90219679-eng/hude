using UnityEngine; using System.Collections.Generic; using Escape.Core;
var sigs = new HashSet<string>(); int dup = 0, total = 0; var names = new HashSet<string>(); int nameDup = 0;
for (int f = 0; f < TowerLayout.Floors; f++)
    for (int r = 0; r < TowerLayout.RoomsPerFloor; r++)
    {
        if (TowerLayout.IsVoidRoom(r)) continue;
        var e = RoomThemes.Get(f, r);
        if (!names.Add($"{f}:{e.name}")) nameDup++;
        if (TowerLayout.IsCorridorRoom(r)) continue;
        total++;
        if (!sigs.Add($"{e.theme}|{e.floorTex}|{e.wallTex}|{e.tint}|{e.light}|{e.debris}")) dup++;
    }
return $"방 {total}개(5개 층), 환경 중복 {dup}, 같은 층 이름 중복 {nameDup}";
