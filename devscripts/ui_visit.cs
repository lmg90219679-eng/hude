using UnityEngine; using System.Linq; using System.Reflection; using Escape.Player; using Escape.Core;
var p = Object.FindAnyObjectByType<PlayerController>();
var rooms = Enumerable.Range(0, TowerLayout.RoomsPerFloor).Where(r => !TowerLayout.IsVoidRoom(r)).OrderBy(r => TowerLayout.RoomRect(r).center.x + TowerLayout.RoomRect(r).center.y).Take(40).ToList();
var f = TowerLayout.GroundFloor;
var sb = new System.Text.StringBuilder();
foreach (var r in rooms) { var c = TowerLayout.RoomCenter(f, r); p.Teleport(new Vector3(c.x, TowerLayout.FloorY(f) + 0.1f, c.z), 0f); sb.Append(r + ","); }
return sb.ToString();
