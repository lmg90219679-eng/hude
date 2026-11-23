using UnityEngine; using UnityEngine.AI; using System.Linq; using Escape.Player;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var c = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include).First(x => x.name.Contains(name));
var p = Object.FindAnyObjectByType<PlayerController>();
var cam = Camera.main.transform; var f = cam.forward; f.y = 0; f.Normalize();
foreach (float d in new[] { 3.6f, 3f, 2.5f, 2f }) {
  Vector3 q = p.transform.position + f * d;
  if (!NavMesh.SamplePosition(q, out var hit, 0.6f, NavMesh.AllAreas)) continue;
  if (Physics.Linecast(p.transform.position + Vector3.up * 1.5f, hit.position + Vector3.up * 1.5f)) continue;
  c.transform.position = hit.position; c.transform.rotation = Quaternion.LookRotation(-f);
  return "moved " + d;
}
return "no spot";
