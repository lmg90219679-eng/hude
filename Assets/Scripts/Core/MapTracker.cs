using Escape.Player;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 지도 기록. 플레이어가 들어가 본 방만 기억한다. 가 보지 않은 방은 지도에 나오지 않는다.
    /// </summary>
    public class MapTracker : MonoBehaviour
    {
        public static MapTracker Instance { get; private set; }

        readonly bool[,] visited = new bool[TowerLayout.Floors, TowerLayout.RoomsPerFloor];
        Transform player;

        public int CurrentFloor { get; private set; }
        public int CurrentRoom { get; private set; } = -1;

        void Awake()
        {
            Instance = this;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (player == null) return;
            CurrentFloor = TowerLayout.FloorOf(player.position.y);
            CurrentRoom = TowerLayout.RoomAt(player.position);
            if (CurrentRoom >= 0) visited[CurrentFloor, CurrentRoom] = true;
        }

        public bool Visited(int floor, int room) => visited[floor, room];

        /// <summary>그 층의 방을 하나라도 지나가 봤는가 (엘리베이터는 탐험한 층만 갈 수 있다).</summary>
        public bool VisitedFloor(int floor)
        {
            for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) if (visited[floor, r]) return true;
            return false;
        }
        public Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;
        public float PlayerYaw => player != null ? player.eulerAngles.y : 0f;
    }
}
