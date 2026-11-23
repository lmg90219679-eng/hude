using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Reflection; using Escape.Player;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var c = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include).FirstOrDefault(x => x.name.Contains(name));
if (c == null) return "none";
c.enabled = false; var ag = c.GetComponent<NavMeshAgent>(); if (ag) ag.enabled = false;
Vector3 t = c.transform.position;
var p = Object.FindAnyObjectByType<PlayerController>();
for (int i = 0; i < 16; i++) {
  float a = i * 22.5f * Mathf.Deg2Rad; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
  foreach (float dist in new[] { 3.2f, 4f, 2.6f }) {
    Vector3 q = t + d * dist;
    if (!NavMesh.SamplePosition(q, out var hit, 0.5f, NavMesh.AllAreas)) continue;
    if (Physics.Linecast(t + Vector3.up * 1.2f, hit.position + Vector3.up * 1.2f)) continue;
    Vector3 look = t - hit.position; look.y = 0;
    p.Teleport(hit.position, Quaternion.LookRotation(look).eulerAngles.y);
    c.transform.rotation = Quaternion.LookRotation(-look);
    typeof(PlayerController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, 3f);
    return c.name + " dist " + dist;
  }
}
return "no clear spot";
