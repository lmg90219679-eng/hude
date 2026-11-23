using UnityEngine; using System.Linq;
var root = GameObject.Find("Tower/HospitalProps").transform.Cast<Transform>();
return string.Join(", ", root.GroupBy(t => t.name.Substring(0, t.name.LastIndexOf('_'))).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count()));
