using UnityEngine; using System.Linq; using System.Reflection; using Escape.Player; using Escape.Core; using Escape.Interaction; using Escape.Items;
string want = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var all = Object.FindObjectsByType<Container>(FindObjectsInactive.Include);
Container c = null;
foreach (var x in all) {
  if (x.Content == null) continue;
  string n = x.Content.IsJunk ? x.Content.JunkName : (x.Content.Type.HasValue ? x.Content.Type.Value.ToString() : "RoomKey");
  if (n == want && x.floor == 2) { c = x; break; }
}
if (c == null) return "none " + want + " ; filled=" + all.Count(a=>a.Content!=null) + "/" + all.Length;
var p = Object.FindAnyObjectByType<PlayerController>();
Vector3 stand = c.transform.position + c.transform.forward * 1.05f; stand.y = c.transform.position.y + 0.05f;
Vector3 look = c.transform.position - stand; look.y = 0;
p.Teleport(stand, Quaternion.LookRotation(look).eulerAngles.y);
typeof(PlayerController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, 35f);
c.Interact(Object.FindAnyObjectByType<Interactor>());
return c.name + " " + n(c);
string n(Container k) => k.Content.IsJunk ? k.Content.JunkName : k.Content.Type.HasValue ? k.Content.Type.Value.ToString() : "RoomKey";
