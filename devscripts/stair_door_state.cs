using UnityEngine; using System.Linq; using System.Text; using Escape.Interaction; using Escape.Core;
var sb = new StringBuilder();
foreach (var d in Door.All.Where(x => x.transform.Find("Hinge") != null)) {
  var h = d.transform.Find("Hinge"); var pn = h.Find("Panel"); var col = pn.GetComponent<Collider>();
  sb.AppendLine($"{d.name} pos={d.transform.position:F1} open={d.IsOpen} hingePos={h.position:F2} hingeEuler={h.localEulerAngles.y:F0} panelCenter={col.bounds.center:F2} size={col.bounds.size:F2} scale={pn.localScale:F2}");
}
return sb.ToString();
