using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>한 층의 엘리베이터 승강장. 바깥 문 한 쌍과 호출 위치를 가진다.</summary>
    public class ElevatorLanding : MonoBehaviour
    {
        public int floor;
        public ElevatorDoorPair doors;
        public Transform hallPoint;     // 승강장 홀의 바닥 (소리가 나는 위치, 추적자가 달려오는 목적지)
    }
}