using UnityEngine; using System.Reflection; using Escape.UI; using Escape.Core;
var h = Object.FindAnyObjectByType<HUD>();
typeof(HUD).GetField("bigMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(h, true);
var mt = MapTracker.Instance; var arr = (bool[,])typeof(MapTracker).GetField("visited", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mt);
for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) arr[TowerLayout.GroundFloor, r] = true;
return "ok";
