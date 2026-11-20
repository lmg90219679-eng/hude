using UnityEngine; using System.IO; using System.Collections.Generic; using Escape.Core;
var saved = new List<(Renderer r, Material m)>();
var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", new Color(0.9f, 0.2f, 0.15f));
var blue = new Material(Shader.Find("Universal Render Pipeline/Unlit")); blue.SetColor("_BaseColor", new Color(0.2f, 0.6f, 0.95f));
foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
{
    if (r.gameObject.name == "Wall") { saved.Add((r, r.sharedMaterial)); r.sharedMaterial = red; }
    else if (r.gameObject.name == "Leaf" || r.gameObject.name == "Panel") { saved.Add((r, r.sharedMaterial)); r.sharedMaterial = blue; }
}
int f = int.Parse(File.ReadAllText("C:/Projects/Escape/devscripts/floor.txt").Trim());
float y = TowerLayout.FloorY(f);
var camGo = new GameObject("TopCam"); var cam = camGo.AddComponent<Camera>();
cam.orthographic = true; cam.orthographicSize = 38.5f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 3.6f;
cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
cam.transform.position = new Vector3(50f, y + 3.4f, 37f); cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
var lg = new GameObject("TopLight").AddComponent<Light>(); lg.type = LightType.Directional; lg.intensity = 1.6f; lg.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
int W = 1600, H = 1184;
var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
File.WriteAllBytes($"C:/Projects/Escape/devscripts/plan_f{f}.png", tex.EncodeToPNG());
RenderTexture.active = null; Object.DestroyImmediate(tex); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lg.gameObject);
foreach (var (r, m) in saved) if (r != null) r.sharedMaterial = m;
return "ok";
