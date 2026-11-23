using UnityEngine; using UnityEngine.AI; using System.Linq; using Escape.Player;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var c = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include).First(x => x.name.Contains(name));
var cam = Camera.main; var o = cam.transform.position; var f = cam.transform.forward; f.y = 0; f.Normalize();
for (float yawOff = -60; yawOff <= 60; yawOff += 15) for (float d = 4f; d >= 2f; d -= 0.5f) {
  var dir = Quaternion.Euler(0, yawOff, 0) * f; Vector3 q = o + dir * d; q.y = c.transform.position.y;
  if (!NavMesh.SamplePosition(q, out var hit, 0.6f, NavMesh.AllAreas)) continue;
  var chest = hit.position + Vector3.up * 1.4f;
  if (Physics.Linecast(o, chest) || Physics.Linecast(o, hit.position + Vector3.up * 0.4f)) continue;
  c.transform.position = hit.position; var l = o - hit.position; l.y = 0; c.transform.rotation = Quaternion.LookRotation(l);
  var vp = cam.WorldToViewportPoint(chest); if (vp.z < 0 || vp.x < 0.2f || vp.x > 0.8f) continue;
  return "ok yaw " + yawOff + " d " + d;
}
return "none";
