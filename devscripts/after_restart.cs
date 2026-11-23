using UnityEngine; using System.Linq; using Escape.Core; using Escape.Enemy; using Escape.Player; using Escape.Items; using Escape.Interaction;
var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Interactor>();
var start = TowerLayout.RoomCenter(TowerLayout.GroundFloor, TowerLayout.StartRoom);
int fl = TowerLayout.FloorOf(p.transform.position.y);
return $"player {p.transform.position:F1} floor={fl} room={TowerLayout.RoomAt(p.transform.position)} (start room {TowerLayout.StartRoom} @ {start:F1}) state={GameFlow.Instance.State} elapsed={GameFlow.Instance.Elapsed:F1} pieces={inter.Inventory.PieceCount} roomKeys={inter.Inventory.RoomKeyCount} battery={inter.Inventory.Count(ItemType.Battery)} chasersChase={Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include).Count(c => c.Current == ChaserAI.State.Chase)} fade={GameFlow.Instance.FadeAlpha:F2}";
