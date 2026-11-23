using UnityEngine; using System.Linq; using Escape.Interaction;
var p = Object.FindAnyObjectByType<Escape.Player.PlayerController>().transform.position;
var d = Door.All.Where(x => !x.secret && !x.IsLocked && !x.IsOpen && Vector3.Distance(x.transform.position, p) > 12f && Mathf.Abs(x.transform.position.y - p.y) < 1.5f).FirstOrDefault();
if (d == null) return "no door";
bool ok = d.TryOpenByChaser();
System.IO.File.WriteAllText("C:/Projects/Escape/devscripts/door_name.txt", d.name + "|" + d.transform.position.x + "|" + d.transform.position.y + "|" + d.transform.position.z);
return d.name + " opened=" + ok + " IsOpen=" + d.IsOpen;
