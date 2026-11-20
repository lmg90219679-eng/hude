using UnityEngine; using System.Linq;
var root = GameObject.Find("Tower/HospitalProps");
if (root == null) return "no HospitalProps";
var groups = root.transform.Cast<Transform>().GroupBy(t => t.name.Split('_')[0]).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}");
return $"총 {root.transform.childCount}개 | " + string.Join(", ", groups);
