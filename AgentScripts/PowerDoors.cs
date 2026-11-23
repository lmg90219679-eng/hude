using System.Threading.Tasks; using UnityEngine; using System.Linq; using System.Text; using System.Reflection;
using Escape.Core; using Escape.Interaction; using Escape.Player;
public static class PowerDoors {
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var inter = Object.FindAnyObjectByType<Interactor>(); var grid = PowerGrid.Instance;
    var targets = Door.All.Where(d => d.secret || d.requiresPower).ToList();
    foreach (var d in targets) if (d.IsOpen) d.Interact(inter);
    await Task.Delay(2500);
    int beforeOpen = 0; foreach (var d in targets) { d.Interact(inter); if (d.IsOpen) beforeOpen++; }
    sb.AppendLine($"before power: of {targets.Count} secret/power doors, {beforeOpen} opened (expected: only the non-power secret door)");
    foreach (var d in targets) if (d.IsOpen) d.Interact(inter);
    await Task.Delay(2500);
    grid.TryRaise(0); grid.TryRaise(1); grid.TryRaise(2);
    sb.AppendLine("power complete = " + grid.Complete);
    foreach (var d in targets) if (!d.IsOpen) d.Interact(inter);
    await Task.Delay(3500); Physics.SyncTransforms();
    int ok = 0;
    foreach (var d in targets) {
      var leaf = (Transform)typeof(Door).GetField("leaf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(d); bool alongX = leaf.localScale.x > leaf.localScale.z;
      Vector3 n = alongX ? Vector3.forward : Vector3.right, a = alongX ? Vector3.right : Vector3.forward; bool blocked = false;
      foreach (float s in new[]{-1.2f,-0.6f,0f,0.6f,1.2f}) foreach (float l in new[]{-0.45f,0f,0.45f}) {
        var pt = d.transform.position + n * s + a * l; pt.y = d.transform.position.y;
        if (Physics.OverlapCapsule(pt + Vector3.up * 0.4f, pt + Vector3.up * 1.7f, 0.28f, ~0, QueryTriggerInteraction.Ignore).Any(c => c.transform.root.GetComponent<PlayerController>() == null)) blocked = true; }
      if (d.IsOpen && !blocked) ok++; else sb.AppendLine($"  {d.name} open={d.IsOpen} blocked={blocked}");
    }
    sb.AppendLine($"after power: open and clear {ok}/{targets.Count}");
    return sb.ToString();
  }
}
