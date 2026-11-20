using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 건물 구조 상수. 5개 층(지하2, 지하1, 1층, 2층, 3층), 층마다 3x3 방(각 8m).
    /// 층 번호(index)는 0=지하2, 1=지하1, 2=1층, 3=2층, 4=3층.
    /// 방 번호는 j * 3 + i (i = x 방향 0..2, j = z 방향 0..2).
    /// </summary>
    public static class TowerLayout
    {
        public const int Floors = 5;
        public const int Grid = 3;
        public const float Cell = 8f;
        public const float FloorHeight = 4f;
        public const int GroundFloor = 2;          // 1층의 index

        public const int RoomsPerFloor = Grid * Grid;
        public const int WestStairRoom = 3;        // (0,1)
        public const int CenterRoom = 4;           // (1,1)
        public const int EastStairRoom = 5;        // (2,1)
        public const int ExitRoom = 1;             // (1,0) 1층 정문이 있는 방
        public const int StartRoom = 3;            // 1층 서쪽 계단방에서 시작
        public const int NurseStationFloor = 3;    // 2층
        public const int NurseStationRoom = 6;     // (0,2) 간호사실: 직원 출입카드가 든 금속 서랍이 있다

        static readonly string[] Labels = { "지하2", "지하1", "1층", "2층", "3층" };
        static readonly string[] ShortLabels = { "B2", "B1", "1F", "2F", "3F" };

        public static string Label(int floor) => floor >= 0 && floor < Floors ? Labels[floor] : "?";
        public static string ShortLabel(int floor) => floor >= 0 && floor < Floors ? ShortLabels[floor] : "?";

        public static float FloorY(int floor) => (floor - GroundFloor) * FloorHeight;

        /// <summary>높이(y)로 층 번호를 구한다. 층 바닥 y를 기준으로 가까운 층.</summary>
        public static int FloorOf(float y) =>
            Mathf.Clamp(Mathf.RoundToInt((y - 1.5f) / FloorHeight) + GroundFloor, 0, Floors - 1);

        public static int RoomIndex(int i, int j) => j * Grid + i;
        public static int RoomX(int room) => room % Grid;
        public static int RoomZ(int room) => room / Grid;

        public static Vector3 RoomCenter(int floor, int room) =>
            new Vector3(RoomX(room) * Cell + Cell * 0.5f, FloorY(floor), RoomZ(room) * Cell + Cell * 0.5f);

        /// <summary>월드 좌표가 속한 방 번호. 건물 밖이면 -1.</summary>
        public static int RoomAt(Vector3 p)
        {
            int i = Mathf.FloorToInt(p.x / Cell);
            int j = Mathf.FloorToInt(p.z / Cell);
            if (i < 0 || i >= Grid || j < 0 || j >= Grid) return -1;
            return RoomIndex(i, j);
        }
    }
}
