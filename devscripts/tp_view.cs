using UnityEngine; using System.Reflection; using Escape.Player;
var p = Object.FindAnyObjectByType<PlayerController>();
var parts = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/tp.txt").Trim().Split(' ');
float x = float.Parse(parts[0]), z = float.Parse(parts[1]), yaw = float.Parse(parts[2]);
float y = parts.Length > 3 ? float.Parse(parts[3]) : 0.1f;
float pitch = parts.Length > 4 ? float.Parse(parts[4]) : 2f;
p.Teleport(new Vector3(x, y, z), yaw);
typeof(PlayerController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, pitch);
return "ok";
