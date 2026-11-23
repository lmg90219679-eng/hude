using System.Linq;
using UnityEngine;
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Items/RoomKey.prefab");
var go = Object.Instantiate(prefab);
var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
var info = $"renderers={rs.Length} center={b.center} size={b.size} scale={go.transform.GetChild(0).localScale} active={go.activeSelf} mats={string.Join(",", rs.Select(r => r.sharedMaterial ? r.sharedMaterial.name : "null"))}";
Object.DestroyImmediate(go);
return info;
