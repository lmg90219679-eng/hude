using UnityEngine; using System.Linq; using Escape.Core; using Escape.Enemy; using Escape.Player; using Escape.Items;
var p = Object.FindAnyObjectByType<PlayerController>(); var inter = Object.FindAnyObjectByType<Escape.Interaction.Interactor>();
inter.Inventory.Add(ItemType.Piece0); inter.Inventory.Add(ItemType.Battery); inter.Inventory.AddRoomKey("B2-1");
var rc = TowerLayout.RoomCenter(4, TowerLayout.StartRoom); p.Teleport(new Vector3(rc.x, TowerLayout.FloorY(4) + 0.1f, rc.z), 0f);
var c = Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include).First(x => x.name.StartsWith("Chaser_3F"));
c.gameObject.SetActive(true);
ChaserAI.ForceCatch(c, p.transform.position + p.transform.forward * 1.2f, Quaternion.LookRotation(-p.transform.forward));
return "caught on 3F at " + p.transform.position + " pieces=" + inter.Inventory.PieceCount + " keys=" + inter.Inventory.RoomKeyCount;
