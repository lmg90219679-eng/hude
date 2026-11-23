using UnityEngine; using System.Linq; using System.Reflection;
foreach (var c in Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include)) c.GraceUntil = 0f;
var f = Object.FindAnyObjectByType<Escape.Player.Flashlight>(); typeof(Escape.Player.Flashlight).GetMethod("SetOn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, new object[]{true});
var p = Object.FindAnyObjectByType<Escape.Player.PlayerController>();
return "hidden=" + p.IsHidden + " light=" + f.IsOn + " pos=" + p.transform.position;
