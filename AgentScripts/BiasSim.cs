using System.Threading.Tasks; using UnityEngine; using System.Linq; using System.Text; using System.Collections.Generic; using System.Reflection;
using Escape.Core; using Escape.Interaction; using Escape.Items;
public static class BiasSim {
  public static async Task<string> Run(float[] biases, int repeats) {
    var sb = new StringBuilder(); var sp = Object.FindAnyObjectByType<TowerSpawner>();
    var fBias = typeof(TowerSpawner).GetField("chainBias", BindingFlags.Instance | BindingFlags.NonPublic);
    var start = typeof(TowerSpawner).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
    var unl = typeof(Door).GetField("unlocked", BindingFlags.Instance | BindingFlags.NonPublic);
    var s1 = TowerLayout.RoomCenter(2, TowerLayout.StartRoom); var start1F = new Vector3(s1.x, TowerLayout.FloorY(2) + 0.1f, s1.z);
    foreach (float b in biases) {
      var totals = new List<float>(); var perFloor = new float[5];
      for (int r = 0; r < repeats; r++) {
        fBias.SetValue(sp, b); start.Invoke(sp, null);
        foreach (var d in Door.All) unl.SetValue(d, false);
        await Task.Delay(400);
        var keys = new HashSet<string>(); float tot = 0;
        for (int f = 0; f < 5; f++) {
          // 층 시작: 계단 홀(복도 끝) 대신 시작 방 기준. 층 사이 비교용
          var c = TowerLayout.RoomCenter(f, TowerLayout.StartRoom); var st = new Vector3(c.x, TowerLayout.FloorY(f) + 0.1f, c.z);
          var res = await TimeBudget.Explore(f, st, keys); perFloor[f] += res.t; tot += res.t;
          foreach (var d in Door.All.Where(d => d.floor == f)) unl.SetValue(d, false);
        }
        totals.Add(tot);
      }
      sb.AppendLine($"chainBias {b:F1}: 탐색 합계 평균 {totals.Average()/60f:F1}분 (최소 {totals.Min()/60f:F1}, 최대 {totals.Max()/60f:F1}) | 층별 평균(분) " + string.Join(" ", perFloor.Select(x => (x/repeats/60f).ToString("F1"))));
    }
    return sb.ToString();
  }
}
