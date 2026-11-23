using UnityEngine; using System.Linq; using System.Reflection; using Escape.Core;
var mt = Object.FindAnyObjectByType<MapTracker>();
var arr = (bool[,])typeof(MapTracker).GetField("visited", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mt);
int n = 0;
for (int f = 2; f <= 3; f++)
  for (int r = 0; r < TowerLayout.RoomsPerFloor; r++)
  {
    if (TowerLayout.IsVoidRoom(r)) continue;
    var rc = TowerLayout.RoomRect(r);
    if (rc.center.x < 62f || TowerLayout.IsCorridorRoom(r)) { arr[f, r] = true; n++; }
  }
return "marked " + n;
