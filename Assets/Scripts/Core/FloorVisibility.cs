using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 건물이 커서 모든 층을 다 그리면 느리다. 플레이어가 있는 층과 위아래 한 층만 그리고,
    /// 조명은 같은 범위 안에서 가까운 것만 켠다. (층을 가로지르는 계단통, 승강로는 항상 그린다.)
    /// 화면 밖 층의 충돌체와 길찾기는 그대로라서 추적자는 영향을 받지 않는다.
    /// </summary>
    public class FloorVisibility : MonoBehaviour
    {
        [SerializeField] float lightDistance = 34f;
        [SerializeField] int floorsAround = 1;

        readonly List<Renderer>[] byFloor = new List<Renderer>[TowerLayout.Floors];
        readonly bool[] shown = new bool[TowerLayout.Floors];
        readonly List<(Light light, int floor, PowerLight power)> lights = new List<(Light, int, PowerLight)>();
        Transform player;
        int lastFloor = int.MinValue;
        float nextLightUpdate;

        static int FloorFromY(float y) =>
            Mathf.Clamp(Mathf.FloorToInt((y + 1f) / TowerLayout.FloorHeight) + TowerLayout.GroundFloor, 0, TowerLayout.Floors - 1);

        void Start()
        {
            for (int f = 0; f < byFloor.Length; f++) { byFloor[f] = new List<Renderer>(); shown[f] = true; }

            var root = GameObject.Find("Tower");
            if (root == null) { enabled = false; return; }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                if (b.size.y > 5.5f) continue;   // 층을 가로지르는 것(계단통 벽, 승강로)은 항상 그린다
                byFloor[FloorFromY(b.center.y)].Add(r);
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                var pl = l.GetComponent<PowerLight>();
                if (!l.enabled && pl == null) continue;   // 처음부터 꺼 둔 조명(정문 전원 조명 등)은 건드리지 않는다
                lights.Add((l, FloorFromY(l.transform.position.y - 1.5f), pl));
            }

            var pc = FindFirstObjectByType<Escape.Player.PlayerController>();
            if (pc != null) player = pc.transform;
        }

        void Update()
        {
            if (player == null) return;
            int pf = TowerLayout.FloorOf(player.position.y);

            if (pf != lastFloor)
            {
                lastFloor = pf;
                for (int f = 0; f < byFloor.Length; f++)
                {
                    bool want = Mathf.Abs(f - pf) <= floorsAround;
                    if (shown[f] == want) continue;
                    shown[f] = want;
                    foreach (var r in byFloor[f]) if (r != null) r.enabled = want;
                }
            }

            if (Time.time >= nextLightUpdate)
            {
                nextLightUpdate = Time.time + 0.5f;
                Vector3 pp = player.position;
                float maxSq = lightDistance * lightDistance;
                foreach (var (light, floor, power) in lights)
                {
                    if (light == null) continue;
                    bool want = Mathf.Abs(floor - pf) <= floorsAround && (light.transform.position - pp).sqrMagnitude < maxSq && (power == null || power.IsOn);
                    if (light.enabled != want) light.enabled = want;
                }
            }
        }
    }
}
