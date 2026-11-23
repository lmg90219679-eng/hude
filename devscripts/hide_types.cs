using UnityEngine; using System.Linq; using Escape.Interaction; using Escape.Core;
var hs = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include);
var g = hs.GroupBy(h => System.Text.RegularExpressions.Regex.Replace(h.name, @"_?(B2|B1|1F|2F|3F|\d+).*$", "")).OrderBy(x => x.Key);
return string.Join("\n", g.Select(x => $"{x.Key} n={x.Count()} e.g. {x.First().name} @ {x.First().transform.position:F0}"));
