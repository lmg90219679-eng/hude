using UnityEngine; using System.Linq; using Escape.Interaction;
var c = Object.FindObjectsByType<Container>(FindObjectsInactive.Include).First(x => x.Content != null && !x.Content.Type.HasValue && !x.Content.IsJunk);
var rs = c.GetComponentsInChildren<Renderer>(true).Where(r => r.transform.parent != null && r.gameObject.name != null && r.GetComponentInParent<Escape.Items.ItemVisualSettings>() == null).ToArray();
var sb = new System.Text.StringBuilder();
foreach (var r in c.GetComponentsInChildren<Renderer>(true)) { if (!r.transform.IsChildOf(c.transform)) continue; var m = r.sharedMaterial; sb.AppendLine(r.name + " | " + (m? m.shader.name + " base=" + (m.HasProperty("_BaseColor")? m.GetColor("_BaseColor").ToString():"-") + " emis=" + (m.HasProperty("_EmissionColor")? m.GetColor("_EmissionColor").ToString():"-") + " map=" + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap")!=null) : "null")); }
return sb.ToString();
