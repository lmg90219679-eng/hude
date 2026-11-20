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
        public const int StartRoom = 7;            // 1층 북쪽 가운데 방에서 시작 (계단은 복도 양 끝 방화문 너머에서 직접 찾아야 한다)
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

        // 병원 평면 (24m x 24m): 가운데 4m 폭 복도(z 10~14)를 사이에 두고 남쪽 병동, 북쪽 병동이 있다.
        // 줄마다 방 너비가 달라서 방 크기가 제각각이다. 복도 줄의 양 끝 방(3, 5)은 방화문 너머의 계단 홀.
        //   남쪽 병동 (z 0~10) : 0 (x 0~7) | 1 정문 로비 (x 7~17) | 2 (x 17~24)
        //   복도 줄  (z 10~14) : 3 서쪽 계단 홀 (x 0~6) | 4 중앙 복도 (x 6~18) | 5 동쪽 계단 홀 (x 18~24)
        //   북쪽 병동 (z 14~24): 6 (x 0~9) | 7 (x 9~15, 뒤에 비밀 승강기) | 8 (x 15~24)
        public const float Span = Grid * Cell;
        static readonly float[] ZBounds = { 0f, 10f, 14f, 24f };
        static readonly float[][] XBounds =
        {
            new[] { 0f, 7f, 17f, 24f },
            new[] { 0f, 6f, 18f, 24f },
            new[] { 0f, 9f, 15f, 24f },
        };

        /// <summary>방의 바닥 영역 (x = 서쪽 끝, y = 남쪽 끝(z), width = 동서 길이, height = 남북 길이)</summary>
        public static Rect RoomRect(int room)
        {
            int i = RoomX(room), j = RoomZ(room);
            float x0 = XBounds[j][i], x1 = XBounds[j][i + 1];
            float z0 = ZBounds[j], z1 = ZBounds[j + 1];
            return new Rect(x0, z0, x1 - x0, z1 - z0);
        }

        public static Vector3 RoomCenter(int floor, int room)
        {
            Rect r = RoomRect(room);
            return new Vector3(r.center.x, FloorY(floor), r.center.y);
        }

        /// <summary>월드 좌표가 속한 방 번호. 건물 밖이면 -1.</summary>
        public static int RoomAt(Vector3 p)
        {
            if (p.x < 0f || p.x >= Span || p.z < 0f || p.z >= Span) return -1;
            int j = p.z < ZBounds[1] ? 0 : (p.z < ZBounds[2] ? 1 : 2);
            var xs = XBounds[j];
            int i = p.x < xs[1] ? 0 : (p.x < xs[2] ? 1 : 2);
            return RoomIndex(i, j);
        }
    }
}
