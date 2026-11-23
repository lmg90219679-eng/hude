using UnityEngine; using System.Linq; using Escape.Interaction;
var parts = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/door_name.txt").Split('|');
var pos = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
var d = Door.All.OrderBy(x => Vector3.Distance(x.transform.position, pos)).First();
return d.name + " IsOpen=" + d.IsOpen;
