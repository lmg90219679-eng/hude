using UnityEngine; using Escape.Player; using Escape.Core;
var p = Object.FindAnyObjectByType<PlayerController>();
p.Teleport(new Vector3(31.5f, TowerLayout.FloorY(2) + 0.1f, 40f), 40f);
return "ok";
