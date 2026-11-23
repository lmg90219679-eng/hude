using UnityEngine; using System.Linq; using Escape.Player; using Escape.Core;
int idx = int.Parse(System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim());
var rooms = Enumerable.Range(0, TowerLayout.RoomsPerFloor).Where(r => !TowerLayout.IsVoidRoom(r)).OrderBy(r => TowerLayout.RoomRect(r).center.x + TowerLayout.RoomRect(r).center.y).ToList();
if (idx >= rooms.Count) return "end";
var p = Object.FindAnyObjectByType<PlayerController>(); var f = TowerLayout.GroundFloor; var c = TowerLayout.RoomCenter(f, rooms[idx]);
p.Teleport(new Vector3(c.x, TowerLayout.FloorY(f) + 0.1f, c.z), 0f);
return rooms[idx].ToString();
