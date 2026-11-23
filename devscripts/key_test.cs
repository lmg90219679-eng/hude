using UnityEngine; using System.Linq; using Escape.Interaction;
var c = Object.FindObjectsByType<Container>(FindObjectsInactive.Include).First(x => x.Content != null && !x.Content.Type.HasValue && !x.Content.IsJunk);
string mode = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
foreach (var l in c.GetComponentsInChildren<Light>(true)) if (l.name == "Glow") l.enabled = mode != "nolight";
foreach (var r in c.GetComponentsInChildren<Renderer>(true)) if (r.name == "RoomKey" || r.transform.IsChildOf(c.GetComponentInChildren<Light>(true).transform.parent)) { if (r.name=="RoomKey") { r.material.SetColor("_EmissionColor", Color.black); } }
return "ok " + mode;
