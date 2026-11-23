using UnityEngine; using UnityEngine.AI; using System.Linq; using System.Text; using System.Collections.Generic;
using Escape.Core; using Escape.Interaction; using Escape.Items; using Escape.Player;
public static class EscapeTest {
  public static string Run() {
    var sb = new StringBuilder();
    var conts = Object.FindObjectsByType<Container>(FindObjectsInactive.Include);
    var doors = Door.All.ToList();
    // ---- A. 풀 수 있는지 (열쇠 → 잠긴 문 → 방 → 서랍 → 열쇠 ...) ----
    var have = new HashSet<string>(); var pieces = new HashSet<ItemType>(); var reach = new HashSet<(int,int)>();
    var rooms = new HashSet<(int,int)>();
    for (int f = 0; f < TowerLayout.Floors; f++) for (int r = 0; r < 200; r++) { try { if (TowerLayout.RoomRect(r).width > 0 && (TowerLayout.IsCorridorRoom(r) || r == TowerLayout.StartRoom)) reach.Add((f, r)); } catch { break; } }
    (int a, int b, Door d)[] Edges() => doors.Where(d => !d.secret).Select(d => {
      var panel = d.transform.Find("Leaf") ?? d.transform.Find("Hinge/Panel"); if (panel == null) return (-1, -1, d);
      bool alongX = panel.localScale.x > panel.localScale.z; var n = alongX ? Vector3.forward : Vector3.right;
      return (TowerLayout.RoomAt(d.transform.position + n * 1.4f), TowerLayout.RoomAt(d.transform.position - n * 1.4f), d); }).Where(e => e.Item1 >= 0 && e.Item2 >= 0).ToArray();
    var edges = Edges();
    bool changed = true; int guard = 0;
    while (changed && guard++ < 200) {
      changed = false;
      foreach (var e in edges) {
        int f = e.d.floor; bool locked = !string.IsNullOrEmpty(e.d.lockKeyId);
        bool open = !locked || have.Contains(e.d.lockKeyId);
        if (!open) continue;
        if (reach.Contains((f, e.a)) && reach.Add((f, e.b))) changed = true;
        if (reach.Contains((f, e.b)) && reach.Add((f, e.a))) changed = true;
      }
      foreach (var c in conts) {
        if (c.Content == null || !reach.Contains((c.floor, c.room))) continue;
        if (c.Content.Type.HasValue) { var t = c.Content.Type.Value; if (ItemInfo.IsPiece(t) && pieces.Add(t)) changed = true; }
        else if (c.Content.KeyId != null && have.Add(c.Content.KeyId)) changed = true;
      }
    }
    int lockedCount = doors.Count(d => !string.IsNullOrEmpty(d.lockKeyId));
    var lockedUnopened = doors.Where(d => !string.IsNullOrEmpty(d.lockKeyId) && !have.Contains(d.lockKeyId)).ToList();
    var allPieces = conts.Where(c => c.Content != null && c.Content.Type.HasValue && ItemInfo.IsPiece(c.Content.Type.Value)).Select(c => $"{c.Content.Type}@f{c.floor}r{c.room}").ToList();
    sb.AppendLine($"[풀 수 있나] 잠긴 문 {lockedCount}개, 열쇠를 얻을 수 있는 문 {lockedCount - lockedUnopened.Count}개, 못 여는 문 {lockedUnopened.Count}");
    sb.AppendLine($"  조각 위치: {string.Join(", ", allPieces)}");
    sb.AppendLine($"  열쇠로 모을 수 있는 조각 {pieces.Count}/5");
    // ---- B. 조합 ----
    var player = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>(); var inv = inter.Inventory;
    foreach (var t in System.Enum.GetValues(typeof(ItemType)).Cast<ItemType>()) { while (inv.Count(t) > 0 && t != ItemType.Flashlight) inv.Consume(t); }
    string R(string label, bool ok, string msg) => $"  {(ok ? "ok  " : "FAIL")} {label} -> {msg}";
    inv.Add(ItemType.Battery); bool r1 = inv.TryCombine(ItemType.Battery, ItemType.Flashlight); sb.AppendLine(R("배터리+손전등", r1 && !inv.Has(ItemType.Battery), inv.LastMessage));
    inv.Add(ItemType.SpareFlashlight); bool r2 = inv.TryCombine(ItemType.Flashlight, ItemType.SpareFlashlight); sb.AppendLine(R("예비 손전등+손전등", r2 && !inv.Has(ItemType.SpareFlashlight), inv.LastMessage));
    inv.Add(ItemType.Cloth); inv.Add(ItemType.Lighter); bool r3 = inv.TryCombine(ItemType.Cloth, ItemType.Lighter); sb.AppendLine(R("천+라이터", r3 && inv.Has(ItemType.Smoke) && !inv.Has(ItemType.Cloth), inv.LastMessage));
    bool r4 = inv.TryCombine(ItemType.Wire, ItemType.Crowbar); sb.AppendLine(R("엉뚱한 조합은 거절", !r4, inv.LastMessage));
    inv.Add(ItemType.Piece0); inv.Add(ItemType.Piece1); inv.Add(ItemType.Piece2); inv.Add(ItemType.Piece3);
    bool r5 = inv.TryCombine(ItemType.Piece0, ItemType.Piece1); sb.AppendLine(R("조각 4개는 합쳐지지 않음", !r5 && !inv.HasMasterKey, inv.LastMessage));
    inv.Add(ItemType.Piece4); bool r6 = inv.TryCombine(ItemType.Piece0, ItemType.Piece1); sb.AppendLine(R("조각 5개 → 탈출 열쇠", r6 && inv.HasMasterKey && inv.PieceCount == 0, inv.LastMessage));
    inv.Consume(ItemType.MasterKey);
    // ---- C. 정문 ----
    var exit = Object.FindAnyObjectByType<ExitDoor>(FindObjectsInactive.Include);
    sb.AppendLine(exit == null ? "FAIL 정문(ExitDoor) 없음" : $"  정문 @ {exit.transform.position:F1} floor {TowerLayout.FloorOf(exit.transform.position.y)} room {TowerLayout.RoomAt(exit.transform.position)}");
    if (exit != null) {
      var s = TowerLayout.RoomCenter(TowerLayout.FloorOf(exit.transform.position.y), TowerLayout.StartRoom);
      var sp = new Vector3(s.x, exit.transform.position.y, s.z);
      bool a1 = NavMesh.SamplePosition(sp, out var h1, 3f, NavMesh.AllAreas), a2 = NavMesh.SamplePosition(exit.transform.position, out var h2, 3f, NavMesh.AllAreas);
      var path = new NavMeshPath(); bool pathOk = a1 && a2 && NavMesh.CalculatePath(h1.position, h2.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
      sb.AppendLine(R("시작 방에서 정문까지 길이 있음", pathOk, $"path {path.status}"));
      sb.AppendLine("  prompt(no key) = " + exit.GetPrompt(inter));
      exit.Interact(inter); sb.AppendLine(R("열쇠 없이는 안 열림", GameFlow.Instance.State == GameState.Playing, inv.LastMessage));
      inv.Add(ItemType.MasterKey); sb.AppendLine("  prompt(key) = " + exit.GetPrompt(inter));
      exit.Interact(inter); sb.AppendLine(R("열쇠가 있으면 클리어", GameFlow.Instance.State == GameState.Cleared, $"state {GameFlow.Instance.State} time {GameFlow.Instance.ClearTime:F1}"));
    }
    return sb.ToString();
  }
}
