using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Collections.Generic; using System.Reflection;
using Escape.Core; using Escape.Interaction; using Escape.Player; using Escape.Enemy;
public static class DoorShots {
  const int TW = 112, TH = 140;
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>();
    foreach (var c in Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include)) { c.enabled = false; var a = c.GetComponent<NavMeshAgent>(); if (a) a.enabled = false; }
    foreach (var o in Object.FindObjectsByType<ChaserDoorOpener>(FindObjectsInactive.Include)) o.enabled = false;
    foreach (var d in Door.All) if (d.IsOpen) d.Interact(inter);
    await Task.Delay(3000);
    var camGo = new GameObject("DoorShotCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 55; cam.nearClipPlane = 0.05f; cam.farClipPlane = 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.enabled = false;
    var lightGo = new GameObject("DoorShotLight"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 9; light.intensity = 25; light.shadows = LightShadows.None;
    var rt = new RenderTexture(TW, TH, 24); cam.targetTexture = rt; var tex = new Texture2D(TW, TH, TextureFormat.RGB24, false);
    int flagged = 0, total = 0;
    for (int f = 0; f < TowerLayout.Floors; f++) {
      var sc = TowerLayout.RoomCenter(f, TowerLayout.StartRoom); p.Teleport(new Vector3(sc.x, TowerLayout.FloorY(f) + 0.1f, sc.z), 0); await Task.Delay(900);
      var doors = Door.All.Where(d => d.floor == f).OrderBy(d => d.name).ToList(); int cols = 16; int rows = Mathf.CeilToInt(doors.Count * 2f / cols);
      var sheet = new Texture2D(cols * TW, rows * TH, TextureFormat.RGB24, false); int idx = 0;
      foreach (var d in doors) {
        var panel = d.transform.Find("Leaf") ?? d.transform.Find("Hinge/Panel"); bool alongX = panel.localScale.x > panel.localScale.z; Vector3 n = alongX ? Vector3.forward : Vector3.right;
        foreach (float side in new[]{1f, -1f}) {
          var pos = d.transform.position + n * side * 2.2f + Vector3.up * 1.4f; var look = d.transform.position + Vector3.up * 1.2f;
          camGo.transform.position = pos; camGo.transform.LookAt(look); lightGo.transform.position = pos - n * side * 0.2f;
          cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, TW, TH), 0, 0); tex.Apply(); RenderTexture.active = null;
          var px = tex.GetPixels(); float lum = 0; int mag = 0; foreach (var c in px) { lum += (c.r + c.g + c.b) / 3f; if (c.r > 0.75f && c.b > 0.75f && c.g < 0.35f) mag++; }
          lum /= px.Length; total++;
          if (lum < 0.02f || lum > 0.9f || mag > px.Length * 0.01f) { flagged++; sb.AppendLine($"  flag {d.name} side {side:F0} lum={lum:F2} magenta={mag}"); }
          int col = idx % cols, row = idx / cols; sheet.SetPixels(col * TW, (rows - 1 - row) * TH, TW, TH, px); idx++;
        }
      }
      sheet.Apply(); System.IO.File.WriteAllBytes($"C:/Projects/Escape/devscripts/shots/doors_f{f}.png", sheet.EncodeToPNG()); Object.Destroy(sheet);
      sb.AppendLine($"floor {f}: {doors.Count} doors -> sheet {cols * TW}x{rows * TH}");
    }
    Object.Destroy(camGo); Object.Destroy(lightGo); rt.Release();
    sb.Insert(0, $"rendered {total} door faces, flagged {flagged}\n");
    return sb.ToString();
  }
}
