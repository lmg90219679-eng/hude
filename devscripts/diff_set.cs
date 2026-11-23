int d = int.Parse(System.IO.File.ReadAllText("C:/Projects/Escape/devscripts/prop.txt").Trim());
Escape.Core.GameSettings.Difficulty = (Escape.Core.Difficulty)d;
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Tower.unity"); return "diff " + d;
