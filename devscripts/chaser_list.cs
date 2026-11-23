using UnityEngine; using System.Linq; using UnityEngine.AI;
var sb = new System.Text.StringBuilder();
foreach (var c in Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include)) {
  var ag = c.GetComponent<NavMeshAgent>(); var an = c.GetComponentInChildren<Animator>();
  sb.AppendLine($"{c.name} floor={c.Floor} state={c.Current} pos={c.transform.position:F1} agent={(ag!=null&&ag.enabled)} onMesh={(ag!=null&&ag.isOnNavMesh)} anim={(an!=null?an.runtimeAnimatorController?.name:"none")} speed={(ag!=null?ag.velocity.magnitude:0):F2}");
}
var gf = Escape.Core.GameFlow.Instance; sb.AppendLine("flow=" + (gf==null?"null":gf.State.ToString()+" t="+gf.Elapsed));
return sb.ToString();
