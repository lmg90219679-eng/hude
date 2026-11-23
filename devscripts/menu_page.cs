using UnityEngine; using System.Reflection;
int p = int.Parse(System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim());
var m = Object.FindAnyObjectByType<Escape.UI.MainMenu>(); var f = typeof(Escape.UI.MainMenu).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic);
f.SetValue(m, System.Enum.ToObject(f.FieldType, p)); return "page " + p;
