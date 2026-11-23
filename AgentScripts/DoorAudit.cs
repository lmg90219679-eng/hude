using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Collections.Generic; using System.Reflection;
using Escape.Core; using Escape.Interaction; using Escape.Items; using Escape.Player; using Escape.Enemy;
public static class DoorAudit {
  static FieldInfo F(string n) => typeof(Door).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic);
  static string Kind(Door d) => d.secret ? "secret" : d.requiresPower ? "power" : !string.IsNullOrEmpty(d.lockKeyId) ? "locked" : d.transform.Find("Hinge") != null ? "stair" : "normal";
  static Transform Leaf(Door d) => (Transform)F("leaf").GetValue(d);
  static Transform Hinge(Door d) => (Transform)F("hinge").GetValue(d);
  static string ExpectedDoorMat(int floor, Vector3 pos) {
    int room = TowerLayout.RoomAt(pos); if (room < 0 || TowerLayout.IsVoidRoom(room)) return null;
    var env = RoomThemes.Get(floor, room); string file;
    switch (env.wallTex) { case 3: case 10: file = "DoorWood"; break; case 4: case 6: case 9: file = "DoorSteel"; break; case 0: case 1: case 5: case 7: case 8: case 11: file = "DoorWhite"; break; default: file = "DoorRoom"; break; }
    return $"Door_{file}_t{env.tint}";
  }
  static string lastBlock = "";
  static bool Blocked(Vector3 p) {
    var cols = Physics.OverlapCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.7f, 0.28f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.transform.root.GetComponent<PlayerController>() == null).ToArray();
    if (cols.Length > 0) lastBlock = string.Join(",", cols.Select(c => c.name + "(" + c.transform.parent?.name + ") c=" + c.bounds.center.ToString("F1") + " s=" + c.bounds.size.ToString("F1")));
    return cols.Length > 0;
  }
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var fails = new List<string>();
    var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>(); var inv = inter.Inventory;
    foreach (var c in Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include)) { c.enabled = false; var a = c.GetComponent<NavMeshAgent>(); if (a) a.enabled = false; }
    foreach (var t in System.Enum.GetValues(typeof(ItemType)).Cast<ItemType>()) while (t != ItemType.Flashlight && inv.Count(t) > 0) inv.Consume(t);
    var doors = Door.All.ToList(); sb.AppendLine($"doors {doors.Count}");
    int handleOk = 0, handleBad = 0, matOk = 0, matBad = 0, matSkipped = 0;
    foreach (var d in doors) {
      var leaf = Leaf(d); var k = Kind(d);
      if (leaf == null) continue;
      var rend = leaf.GetComponent<Renderer>(); var mf = leaf.GetComponent<MeshFilter>(); string mn = mf.sharedMesh.name; bool alongX = leaf.localScale.x > leaf.localScale.z;
      int hs = mn.EndsWith("_p") ? 1 : -1; Vector3 axis = alongX ? Vector3.right : Vector3.forward; var sd = (Vector3)F("slideDir").GetValue(d);
      float slide = Vector3.Dot(sd, axis);
      var verts = mf.sharedMesh.vertices; var uv = mf.sharedMesh.uv; var nrm = mf.sharedMesh.normals; bool uvOk = true;
      for (int i = 0; i < verts.Length; i++) { bool big = alongX ? Mathf.Abs(nrm[i].z) > 0.5f : Mathf.Abs(nrm[i].x) > 0.5f; if (!big) continue; float a = alongX ? verts[i].x : verts[i].z; if (Mathf.Abs(Mathf.Abs(a) - 0.5f) < 0.01f) { float want = (a * hs > 0) ? 1f : 0f; if (Mathf.Abs(uv[i].x - want) > 0.01f) uvOk = false; } }
      bool opp = hs * Mathf.Sign(slide) < 0;
      if (opp && uvOk) handleOk++; else { handleBad++; fails.Add($"handle {d.name} hs={hs} slide={slide:F1} uv={uvOk}"); }
      if (k == "normal") {
        Vector3 fn = alongX ? Vector3.forward : Vector3.right; var mats = rend.sharedMaterials;
        string e0 = ExpectedDoorMat(d.floor, d.transform.position + fn * 0.7f), e1 = ExpectedDoorMat(d.floor, d.transform.position - fn * 0.7f);
        bool ok0 = e0 == null || (mats.Length > 0 && mats[0].name.StartsWith(e0)), ok1 = e1 == null || (mats.Length > 1 && mats[1].name.StartsWith(e1));
        if (e0 == null || e1 == null) matSkipped++;
        if (ok0 && ok1) matOk++; else { matBad++; fails.Add($"texture {d.name} front {(mats.Length>0?mats[0].name:"-")}/want {e0}, back {(mats.Length>1?mats[1].name:"-")}/want {e1}"); }
      }
    }
    sb.AppendLine($"[look] handle opposite to slide + uv ok {handleOk}, bad {handleBad} / texture matches rooms {matOk}, bad {matBad} (one side void, skipped {matSkipped})");
    int lockOk = 0, lockBad = 0;
    foreach (var d in doors.Where(x => Kind(x) == "locked")) {
      d.Interact(inter); bool stayed = !d.IsOpen && d.IsLocked;
      inv.AddRoomKey(d.lockKeyId); d.Interact(inter); bool opened = d.IsOpen && !d.IsLocked;
      if (stayed && opened) lockOk++; else { lockBad++; fails.Add($"lock {d.name} closedWithoutKey={stayed} openedWithKey={opened}"); }
      d.Interact(inter);
    }
    sb.AppendLine($"[locked] ok {lockOk}, bad {lockBad}");
    await Task.Delay(2500);
    var toOpen = doors.Where(d => Kind(d) != "power").ToList();
    foreach (var d in toOpen) if (!d.IsOpen) d.Interact(inter);
    await Task.Delay(3500);
    int openOk = 0, openBad = 0, passBad = 0;
    foreach (var d in toOpen) {
      var leaf = Leaf(d); var hinge = Hinge(d); bool moved = true;
      if (leaf != null) moved = Vector3.Distance(leaf.localPosition, (Vector3)F("leafStart").GetValue(d)) > ((float)F("slideDistance").GetValue(d)) * 0.95f;
      else if (hinge != null) moved = Quaternion.Angle(Quaternion.identity, hinge.localRotation) > 80f;
      Transform panel = leaf != null ? leaf : d.transform.Find("Hinge/Panel"); bool alongX = panel.localScale.x > panel.localScale.z;
      Vector3 n = alongX ? Vector3.forward : Vector3.right, a = alongX ? Vector3.right : Vector3.forward;
      float wdt = alongX ? panel.localScale.x : panel.localScale.z; bool blocked = false;
      Physics.SyncTransforms();
      foreach (float s in new[]{-1.2f,-0.6f,0f,0.6f,1.2f}) foreach (float l in new[]{-0.45f, 0f, 0.45f}) {
        var pt = d.transform.position + n * s + a * (l * wdt * 0.5f); pt.y = d.transform.position.y;
        if (Blocked(pt)) { blocked = true; break; } }
      if (d.IsOpen && moved) openOk++; else { openBad++; fails.Add($"open {d.name} open={d.IsOpen} moved={moved}"); }
      if (blocked) { passBad++; fails.Add($"blocked {d.name} @ {d.transform.position:F1} by {lastBlock}"); }
    }
    sb.AppendLine($"[open] fully open {openOk}, bad {openBad} / doorway blocked while open {passBad}");
    foreach (var d in toOpen) if (d.IsOpen) d.Interact(inter);
    await Task.Delay(3500);
    int closeOk = 0, closeBad = 0;
    foreach (var d in toOpen) { var leaf = Leaf(d); var hinge = Hinge(d); bool closed = !d.IsOpen;
      if (leaf != null) closed &= Vector3.Distance(leaf.localPosition, (Vector3)F("leafStart").GetValue(d)) < 0.02f; else if (hinge != null) closed &= Quaternion.Angle(Quaternion.identity, hinge.localRotation) < 1f;
      if (closed) closeOk++; else { closeBad++; fails.Add($"close {d.name}"); } }
    sb.AppendLine($"[close] back to closed {closeOk}, bad {closeBad}");
    int chOk = 0, chBad = 0;
    foreach (var d in doors) { bool r = d.TryOpenByChaser(); bool should = !d.IsLocked && !d.secret && Kind(d) != "power"; if (r == should) chOk++; else { chBad++; fails.Add($"chaserOpen {d.name} r={r} should={should}"); } }
    await Task.Delay(3000); int openedN = doors.Count(d => d.IsOpen);
    await Task.Delay(9500); int stillOpen = doors.Count(d => d.IsOpen);
    sb.AppendLine($"[chaser opens] reaction ok {chOk}, bad {chBad} / open after 3s {openedN} -> after 12.5s {stillOpen} (0 if nobody near)");
    sb.AppendLine("failures " + fails.Count + ":"); foreach (var f in fails.Take(40)) sb.AppendLine("  " + f);
    return sb.ToString();
  }
}
