using System.Threading.Tasks; using UnityEngine; using System.Text; using System.Linq; using System.Reflection;
using Escape.Core; using Escape.Audio; using Escape.Player;
public static class FloorAudioTest {
  public static async Task<string> Run() {
    var sb = new StringBuilder(); var p = Object.FindAnyObjectByType<PlayerController>(); var ga = GameAudio.Instance;
    var beds = (AudioSource[])typeof(GameAudio).GetField("floorBeds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ga);
    sb.AppendLine("beds: " + string.Join(",", beds.Select(b => b == null ? "null" : b.clip.name + (b.isPlaying ? "(playing)" : "(stopped)"))));
    int events = 0;
    foreach (int f in new[] { 4, 3, 2, 1, 0 }) {
      var sc = TowerLayout.RoomCenter(f, TowerLayout.StartRoom); p.Teleport(new Vector3(sc.x, TowerLayout.FloorY(f) + 0.1f, sc.z), 0);
      await Task.Delay(4500);
      sb.AppendLine($"floor {f}: volumes " + string.Join(" ", beds.Select(b => b.volume.ToString("F2"))));
    }
    events = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.gameObject.name.StartsWith("Sfx_Ev_") || a.gameObject.name == "Sfx_Whisper");
    sb.AppendLine("event sources alive now: " + events);
    return sb.ToString();
  }
}
