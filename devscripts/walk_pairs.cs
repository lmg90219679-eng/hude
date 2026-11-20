using UnityEngine; using System.Text; using Escape.Core; using Escape.Player;
var pc = Object.FindAnyObjectByType<PlayerController>(); var cc = pc.GetComponent<CharacterController>(); var sb = new StringBuilder();
bool Walk(float tx, float tz, int maxIter = 900)
{
    for (int i = 0; i < maxIter; i++)
    {
        Vector3 p = pc.transform.position; Vector3 d = new Vector3(tx - p.x, 0f, tz - p.z);
        if (d.magnitude < 0.12f) return true;
        cc.Move(d.normalized * Mathf.Min(0.07f, d.magnitude) + Vector3.down * 0.12f);
    }
    return false;
}
bool all = true;
for (int n = 0; n < 4; n++)
{
    bool west = n % 2 == 0;
    float X(float x) => west ? x : TowerLayout.SpanX - x;
    float zc = west ? 11f : 63f;
    cc.enabled = false; pc.transform.position = new Vector3(X(5.0f), TowerLayout.FloorY(n) + 0.1f, zc + 0.93f); cc.enabled = true;
    bool ok = Walk(X(0.8f), zc + 0.93f) && Walk(X(0.8f), zc - 0.93f) && Walk(X(5.0f), zc - 0.93f);
    float yUp = pc.transform.position.y; bool okUp = ok && Mathf.Abs(yUp - (TowerLayout.FloorY(n + 1) + 0.1f)) < 0.3f;
    ok = Walk(X(0.8f), zc - 0.93f) && Walk(X(0.8f), zc + 0.93f) && Walk(X(5.0f), zc + 0.93f);
    float yDown = pc.transform.position.y; bool okDown = ok && Mathf.Abs(yDown - (TowerLayout.FloorY(n) + 0.1f)) < 0.3f;
    sb.AppendLine($"{TowerLayout.Label(n)}<->{TowerLayout.Label(n + 1)} ({(west ? "서쪽" : "동쪽")}): 올라가기 {(okUp ? "OK" : "실패")}, 내려오기 {(okDown ? "OK" : "실패")}");
    all &= okUp && okDown;
}
sb.AppendLine(all ? "== 전부 통과 ==" : "== 실패 있음 ==");
return sb.ToString();
