using UnityEngine; using System.Linq;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var t = GameObject.Find("Tower/HospitalProps").transform.Cast<Transform>().First(x => x.name.StartsWith(name));
var rs = t.GetComponentsInChildren<Renderer>(true);
var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
return $"{t.name} pos={t.position} active={t.gameObject.activeInHierarchy} renderers={rs.Length} enabled={rs.Count(r=>r.enabled)} boundsCenter={b.center} size={b.size} scale={t.GetChild(0).localScale} mats={string.Join(",", rs.Select(r=>r.sharedMaterial? r.sharedMaterial.name:"null"))}";
