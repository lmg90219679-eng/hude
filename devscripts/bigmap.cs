using UnityEngine; using System.Reflection; using Escape.UI; using Escape.Core; using Escape.Player;
var h = Object.FindAnyObjectByType<HUD>();
typeof(HUD).GetField("bigMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(h, true);
// 지도에 보이도록 여러 방을 방문한 것으로 만든다 (시험용)
var mt = MapTracker.Instance; var arr = (bool[,])typeof(MapTracker).GetField("visited", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mt);
for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) if (r % 3 != 0) arr[TowerLayout.GroundFloor, r] = true;
return "ok";
