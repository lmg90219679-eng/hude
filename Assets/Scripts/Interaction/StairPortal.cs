using System;
using Escape.Core;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 계단. 각 층에는 계단통이 2개(서쪽, 동쪽) 있고, 각 계단통에 올라가는 계단과 내려가는 계단이 하나씩 있다.
    /// E키로 타면 짝이 되는 계단 앞으로 이동한다. 엘리베이터는 없다.
    /// </summary>
    public class StairPortal : MonoBehaviour, IInteractable
    {
        /// <summary>계단을 탔을 때 알린다. (출발 층, 도착 층, 도착 위치)</summary>
        public static event Action<int, int, Vector3> Used;

        public int floor;
        public int room;
        public bool up;
        public StairPortal partner;
        public Transform exitPoint;     // 이 계단으로 "도착"할 때 서는 자리

        public string GetPrompt(Interactor who)
        {
            if (partner == null) return null;
            return up ? $"E: 위층으로 올라간다 ({TowerLayout.Label(floor + 1)})"
                      : $"E: 아래층으로 내려간다 ({TowerLayout.Label(floor - 1)})";
        }

        public void Interact(Interactor who)
        {
            if (partner == null || partner.exitPoint == null) return;

            int from = floor;
            int to = partner.floor;
            Vector3 arrival = partner.exitPoint.position;

            who.Player.Teleport(arrival, partner.exitPoint.eulerAngles.y);
            NoiseSystem.Emit(arrival, 8f);   // 계단 소리
            who.Inventory.Say(TowerLayout.Label(to));
            Used?.Invoke(from, to, arrival);
        }
    }
}
