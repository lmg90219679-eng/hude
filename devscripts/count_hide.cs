using UnityEngine; using System.Linq; using Escape.Interaction; using Escape.Core;
var cs = Object.FindObjectsByType<Container>(FindObjectsInactive.Include);
var hs = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include);
return $"containers {cs.Length} (byfloor {string.Join(",", Enumerable.Range(0,5).Select(f=>cs.Count(c=>c.floor==f)))}) filled {cs.Count(c=>c.Content!=null)} hiding {hs.Length}";
