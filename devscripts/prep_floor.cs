using UnityEngine; using System.Reflection; using Escape.Core;
int f = int.Parse(System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim());
foreach (var c in Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include)) c.GraceUntil = 0f;
var fl = Object.FindAnyObjectByType<Escape.Player.Flashlight>(); typeof(Escape.Player.Flashlight).GetMethod("SetOn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fl, new object[]{true});
var p = Object.FindAnyObjectByType<Escape.Player.PlayerController>();
var rc = TowerLayout.RoomCenter(f, TowerLayout.StartRoom);
p.Teleport(new Vector3(rc.x, TowerLayout.FloorY(f) + 0.1f, rc.z), 0f);
return "floor " + f + " at " + p.transform.position;
