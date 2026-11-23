using UnityEngine; using System.Linq; using Escape.Core;
string name = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var t = GameObject.Find("Tower/HospitalProps").transform.Cast<Transform>().First(x => x.name.StartsWith(name));
foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
int f = TowerLayout.FloorOf(t.position.y); int room = TowerLayout.RoomAt(t.position);
Vector3 c = TowerLayout.RoomCenter(f, room); Vector3 dir = c - t.position; dir.y = 0; dir.Normalize();
var go = GameObject.Find("InspectCam"); if (go == null) go = new GameObject("InspectCam");
var cam = go.GetComponent<Camera>(); if (cam == null) cam = go.AddComponent<Camera>();
cam.nearClipPlane = 0.05f; cam.fieldOfView = 55; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;

Vector3 tgt = t.position + Vector3.up * 0.9f;
go.transform.position = tgt + dir * 3.2f + Vector3.up * 0.9f;
go.transform.LookAt(tgt);
var lg = go.GetComponentInChildren<Light>(); if (lg == null) lg = new GameObject("L").AddComponent<Light>(); lg.transform.SetParent(go.transform, false); lg.type = LightType.Point; lg.range = 8; lg.intensity = 6;
cam.depth = 50; 
return $"{t.name} room dir toward center {dir} cam {go.transform.position}";
