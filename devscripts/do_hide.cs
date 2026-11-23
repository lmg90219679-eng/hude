using UnityEngine; using System.Linq; using Escape.Interaction;
string want = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var h = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include).Where(x => x.name.StartsWith(want) && x.name.EndsWith("_1F")).First();
h.Interact(Object.FindAnyObjectByType<Interactor>());
return "hidden=" + Object.FindAnyObjectByType<Escape.Player.PlayerController>().IsHidden;
