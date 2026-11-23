using UnityEngine; using System.Linq;
var cs = Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include);
return string.Join("\n", cs.Where(c=>c.name.EndsWith("_0")).Select(c => { var rs = c.GetComponentsInChildren<Renderer>(true).Where(r=>r is SkinnedMeshRenderer || r is MeshRenderer).ToArray(); if (rs.Length==0) return c.name+" no renderer"; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return $"{c.name} rootY={c.transform.position.y:F2} boundsMinY={b.min.y:F2} maxY={b.max.y:F2} h={b.size.y:F2}"; }));
