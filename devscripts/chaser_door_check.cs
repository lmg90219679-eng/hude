using UnityEngine;
using UnityEngine.AI;
using Escape.Interaction;
int open = 0, closed = 0, lockedOpen = 0, obstacles = 0;
foreach (var d in Door.All) { if (d.floor != 2) continue; if (d.IsOpen) { open++; if (d.IsLocked) lockedOpen++; } else closed++; if (d.GetComponent<NavMeshObstacle>() != null && d.GetComponent<NavMeshObstacle>().enabled) obstacles++; }
return $"floor2 open={open} closed={closed} lockedOpen={lockedOpen} activeObstacles={obstacles}";
