using UnityEngine; using UnityEngine.AI;
int n = 0; foreach (var c in Object.FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include)) { c.enabled = false; var a = c.GetComponent<NavMeshAgent>(); if (a) a.enabled = false; n++; }
return "frozen " + n;
