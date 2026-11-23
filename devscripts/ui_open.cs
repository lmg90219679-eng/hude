using UnityEngine; using System.Reflection; using Escape.UI;
var hud = Object.FindAnyObjectByType<HUD>();
string mode = System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim();
var t = typeof(HUD);
if (mode == "inv") t.GetMethod("SetInventory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, new object[] { true });
if (mode == "map") { t.GetField("bigMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, true); }
if (mode == "none") { t.GetMethod("SetInventory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, new object[] { false }); t.GetField("bigMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, false); }
return mode;
