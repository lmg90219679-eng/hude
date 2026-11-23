using UnityEngine; using UnityEngine.AI; using System.Linq; using Escape.Core; using Escape.Interaction;
var hs = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include).First(h => h.name == "Locker" && Vector3.Distance(h.transform.position, new Vector3(28, 4, 28)) < 2f);
var ex = hs.transform.Find("ExitPoint"); var hp = hs.transform.Find("HidePoint");
int room = TowerLayout.RoomAt(hs.transform.position); var r = TowerLayout.RoomRect(room);
bool onNav = NavMesh.SamplePosition(ex.position, out var h, 2f, NavMesh.AllAreas);
return $"locker={hs.transform.position:F1} exit={ex.position:F1} room={room} rect={r} 방이름={RoomThemes.Get(TowerLayout.FloorOf(hs.transform.position.y), room).name} exitInRoom={r.Contains(new Vector2(ex.position.x, ex.position.z))} nav={onNav} navPos={h.position:F1} dist={Vector3.Distance(h.position, ex.position):F2}";
