using UnityEngine; using System.Linq; using System.Reflection; using Escape.Enemy; using Escape.Items; using Escape.Interaction;
var c = Object.FindObjectsByType<ChaserAI>(FindObjectsInactive.Include).First();
float F(string n) => (float)typeof(ChaserAI).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
var conts = Object.FindObjectsByType<Container>(FindObjectsInactive.Include);
int bat = conts.Count(x => x.Content != null && x.Content.Type == ItemType.Battery);
var fl = Object.FindAnyObjectByType<Escape.Player.Flashlight>();
fl.GetType().GetMethod("SetOn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fl, new object[]{true});
float r0 = fl.BatteryRatio;
return $"diff={Escape.Core.GameSettings.Difficulty} patrol={F("patrolSpeed"):F2} chase={F("chaseSpeed"):F2} sight={F("sightRange"):F1} unlit={F("unlitSightRange"):F1} search={F("searchDuration"):F1} grace={c.GraceUntil - Time.time:F1}s batteries={bat} drain/s={Escape.Core.GameSettings.BatteryDrain}";
