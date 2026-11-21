using UnityEngine;
using UnityEngine.AI;
using Escape.Interaction;
using Escape.Enemy;
var sb = new System.Text.StringBuilder();
var doors = Door.All;
Door plain = null, locked = null;
foreach (var d in doors) { if (d.floor == 2 && !d.IsOpen) { if (!d.IsLocked && !d.secret && plain == null) plain = d; if (d.IsLocked && locked == null) locked = d; } }
sb.AppendLine($"doors={doors.Count} plain={plain != null} locked={locked != null}");
var ch = Object.FindFirstObjectByType<ChaserAI>();
sb.AppendLine($"chaser={ch.name} opener={ch.GetComponent<ChaserDoorOpener>() != null}");
if (plain != null)
{
    var agent = ch.GetComponent<NavMeshAgent>();
    agent.Warp(plain.transform.position + new Vector3(1.5f, 0.05f, 0));
}
return sb.ToString();
