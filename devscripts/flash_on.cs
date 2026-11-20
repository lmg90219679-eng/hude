using UnityEngine; using System.Reflection;
var f = Object.FindAnyObjectByType<Escape.Player.Flashlight>(); if (f == null) return "no flashlight";
typeof(Escape.Player.Flashlight).GetMethod("SetOn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, new object[] { true }); return "on=" + f.IsOn;
