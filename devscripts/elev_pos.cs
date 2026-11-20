using UnityEngine; using Escape.Interaction;
var landings = Object.FindObjectsByType<ElevatorLanding>(FindObjectsInactive.Include);
return $"landings={landings.Length} first={landings[0].transform.position} ctrl={(Object.FindAnyObjectByType<ElevatorController>() != null)}";
