using UnityEngine; using System.Linq; using System.Text; using Escape.Interaction; using Escape.Core;
var sb = new StringBuilder();
foreach (var name in new[] { "NormalDoor_1F_115-25", "NormalDoor_1F_36-1", "NormalDoor_B2_36-1", "NormalDoor_B2_115-25", "NormalDoor_1F_58-8" }) {
  var d = Door.All.FirstOrDefault(x => x.name == name); if (d == null) { sb.AppendLine(name + " none"); continue; }
  var leaf = d.transform.Find("Leaf"); bool alongX = leaf.localScale.x > leaf.localScale.z;
  Vector3 n = alongX ? Vector3.forward : Vector3.right, a = alongX ? Vector3.right : Vector3.forward;
  sb.AppendLine($"{name} pos={d.transform.position:F1} alongX={alongX} leafW={(alongX ? leaf.localScale.x : leaf.localScale.z):F2} rooms {d.roomA}/{d.roomB} leafPos={leaf.position:F1} open={d.IsOpen}");
  foreach (float s in new[]{-1.2f,-0.6f,0f,0.6f,1.2f}) foreach (float l in new[]{-0.9f, 0f, 0.9f}) {
    var pt = d.transform.position + n * s + a * l; pt.y = d.transform.position.y;
    var cols = Physics.OverlapCapsule(pt + Vector3.up * 0.4f, pt + Vector3.up * 1.7f, 0.28f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.transform != leaf).ToArray();
    foreach (var c in cols) sb.AppendLine($"   n={s:F1} a={l:F1}: {c.name} (parent {c.transform.parent?.name}) bounds {c.bounds.center:F1} size {c.bounds.size:F1}");
  }
}
return sb.ToString();
