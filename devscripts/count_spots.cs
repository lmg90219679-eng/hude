using UnityEngine; using System.Linq; using Escape.Interaction;
var hs = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include);
var g = hs.GroupBy(h => h.name.Split('_')[0]).Select(x => x.Key + ":" + x.Count());
return string.Join(", ", g);
