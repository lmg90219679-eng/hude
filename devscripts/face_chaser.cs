using UnityEngine; using System.Linq; using System.Reflection; using Escape.Player;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var c = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include).First(x => x.name.Contains(name));
var p = Object.FindAnyObjectByType<PlayerController>();
var d = c.transform.position - p.transform.position; d.y = 0;
p.Teleport(p.transform.position, Quaternion.LookRotation(d).eulerAngles.y);
typeof(PlayerController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, 3f);
return "dist " + d.magnitude + " pos " + c.transform.position;
