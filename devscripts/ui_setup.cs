using UnityEngine; using System.Linq; using System.Reflection; using Escape.Items; using Escape.UI; using Escape.Player; using Escape.Core;
var inv = Object.FindAnyObjectByType<Inventory>();
inv.Add(ItemType.Battery); inv.Add(ItemType.Battery); inv.Add(ItemType.Cloth); inv.Add(ItemType.Lighter); inv.Add(ItemType.Crowbar); inv.Add(ItemType.SpareFlashlight);
inv.Add(ItemType.Piece0); inv.Add(ItemType.Piece2); inv.Add(ItemType.SecurityCard); inv.Add(ItemType.Wire);
inv.AddRoomKey("1F-3"); inv.AddRoomKey("1F-7"); inv.AddRoomKey("2F-1");
return "ok";
