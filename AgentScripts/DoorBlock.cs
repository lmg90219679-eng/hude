using System.Threading.Tasks; using UnityEngine; using System.Linq; using System.Text; using Escape.Interaction; using Escape.Core;
public static class DoorBlock {
  public static async Task<string> Run(string[] names) {
    var sb = new StringBuilder(); var inter = Object.FindAnyObjectByType<Interactor>();
    foreach (var name in names) {
      var d = Door.All.FirstOrDefault(x => x.name == name); if (d == null) { sb.AppendLine(name + " none"); continue; }
      if (!d.IsOpen) d.Interact(inter);
      await Task.Delay(2500); Physics.SyncTransforms();
      var leaf = d.transform.Find("Leaf"); bool alongX = leaf.localScale.x > leaf.localScale.z;
      Vector3 n = alongX ? Vector3.forward : Vector3.right, a = alongX ? Vector3.right : Vector3.forward;
      sb.AppendLine($"{name} pos={d.transform.position:F2} open={d.IsOpen} leaf={leaf.position:F2}");
      foreach (float s in new[]{-1.2f,-0.6f,0f,0.6f,1.2f}) foreach (float l in new[]{-0.45f, 0f, 0.45f}) {
        var pt = d.transform.position + n * s + a * l; pt.y = d.transform.position.y;
        var cols = Physics.OverlapCapsule(pt + Vector3.up * 0.4f, pt + Vector3.up * 1.7f, 0.28f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in cols) sb.AppendLine($"   n={s:F1} a={l:F2}: {c.name} parent={c.transform.parent?.name} center={c.bounds.center:F2} size={c.bounds.size:F2}");
      }
    }
    return sb.ToString();
  }
}
