using UnityEngine; using System.Linq; using Escape.Core; using Escape.Interaction;
var sb = new System.Text.StringBuilder();
var all = Door.All.ToList();
sb.AppendLine($"Door.All = {all.Count}");
string Kind(Door d) => d.secret ? "secret" : d.requiresPower ? "power" : !string.IsNullOrEmpty(d.lockKeyId) ? "locked" : d.transform.Find("Hinge") != null ? "stair(hinge)" : "normal";
foreach (var g in all.GroupBy(Kind)) sb.AppendLine($"  {g.Key}: {g.Count()}");
sb.AppendLine("by floor: " + string.Join(", ", Enumerable.Range(0,5).Select(f => $"{f}:{all.Count(d => d.floor == f)}")));
sb.AppendLine("name samples: " + string.Join(" | ", all.Take(6).Select(d => d.name)));
sb.AppendLine("ExitDoor: " + Object.FindObjectsByType<ExitDoor>(FindObjectsInactive.Include).Length + ", StairPortal: " + StairPortal.All.Count);
sb.AppendLine("children of first door: " + string.Join(",", Enumerable.Range(0, all[0].transform.childCount).Select(i => all[0].transform.GetChild(i).name)));
// 문처럼 이름 붙은 다른 오브젝트
var other = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name.ToLower().Contains("door") && t.GetComponent<Door>() == null && t.parent != null && t.parent.GetComponent<Door>() == null).GroupBy(t => System.Text.RegularExpressions.Regex.Replace(t.name, @"[_\d].*$", "")).Select(g => $"{g.Key}:{g.Count()}");
sb.AppendLine("other door-named: " + string.Join(", ", other));
return sb.ToString();
