using UnityEngine; using System.Reflection; using Escape.Player; using Escape.Core;
string[] a = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim().Split(' ');
int f = int.Parse(a[0]), room = int.Parse(a[1]);
var p = Object.FindAnyObjectByType<PlayerController>();
Vector3 c = TowerLayout.RoomCenter(f, room);
p.Teleport(new Vector3(c.x, TowerLayout.FloorY(f) + 0.1f, c.z), 0f);
typeof(PlayerController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, -55f);
return TowerLayout.Label(f) + " " + RoomThemes.Get(f, room).name + " ceil=" + RoomThemes.Get(f, room).ceilTex;
