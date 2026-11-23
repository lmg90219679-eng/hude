using UnityEngine; using System.Linq; using Escape.Interaction;
var d = Door.All.First(x => x.name == "NormalDoor_1F_133-25");
var mf = d.transform.Find("Leaf").GetComponent<MeshFilter>().sharedMesh;
var v = mf.vertices; var n = mf.normals; var uv = mf.uv;
var sb = new System.Text.StringBuilder();
for (int i = 0; i < v.Length; i++) if (Mathf.Abs(n[i].z) > 0.5f) sb.AppendLine($"v={v[i]} n={n[i]} uv={uv[i]}");
sb.AppendLine("sub0 tris=" + mf.GetTriangles(0).Length + " sub1 tris=" + mf.GetTriangles(1).Length);
return sb.ToString();
