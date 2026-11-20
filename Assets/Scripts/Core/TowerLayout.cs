using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 건물 구조 상수. 5개 층(지하2, 지하1, 1층, 2층, 3층). 층 번호(index)는 0=지하2, 1=지하1, 2=1층, 3=2층, 4=3층.
    ///
    /// 평면은 48m x 36m. 긴 복도 두 줄(남쪽, 북쪽)을 두 개의 연결 복도가 이어서 고리(日자)를 이루고,
    /// 복도마다 방이 줄줄이 달려 있다. 계단 홀은 남쪽 복도의 서쪽 끝과 북쪽 복도의 동쪽 끝에 있어서 서로 멀리 떨어져 있다.
    ///
    ///   z 26~36  북쪽 병동  N0(18)  N1(19)  N2(20 시작/승강기)  N3(21)  N4(22)
    ///   z 22~26  북쪽 복도  NC_a(14) NC_b(15) NC_c(16) [동쪽 계단 홀 17]
    ///   z 14~22  중간 병동  M0(9) [연결복도 10] M1(11) [연결복도 12] M2(13)
    ///   z 10~14  남쪽 복도  [서쪽 계단 홀 5] SC_a(6) SC_b(7) SC_c(8)
    ///   z  0~10  남쪽 병동  S0(0)  S1(1)  S2(2 정문 로비)  S3(3)  S4(4)
    /// </summary>
    public static class TowerLayout
    {
        public const int Floors = 5;
        public const float FloorHeight = 4f;
        public const int GroundFloor = 2;          // 1층의 index

        public const float SpanX = 48f, SpanZ = 36f;

        public const int RoomsPerFloor = 23;
        public const int WestStairRoom = 5;        // 남쪽 복도 서쪽 끝 계단 홀
        public const int EastStairRoom = 17;       // 북쪽 복도 동쪽 끝 계단 홀
        public const int CenterRoom = 7;           // 남쪽 복도 가운데 구간
        public const int ExitRoom = 2;             // 1층 정문 로비
        public const int StartRoom = 20;           // 1층 북쪽 가운데 방에서 시작 (계단은 멀리 복도 끝에서 직접 찾아야 한다)
        public const int NurseStationFloor = 3;    // 2층
        public const int NurseStationRoom = 11;    // 중간 병동 가운데 간호사실: 직원 출입카드가 든 금속 서랍이 있다

        static readonly Rect[] Rects =
        {
            new Rect(0, 0, 10, 10), new Rect(10, 0, 8, 10), new Rect(18, 0, 12, 10), new Rect(30, 0, 8, 10), new Rect(38, 0, 10, 10),   // 0..4 남쪽 병동
            new Rect(0, 10, 6, 4), new Rect(6, 10, 11, 4), new Rect(17, 10, 17, 4), new Rect(34, 10, 14, 4),                              // 5..8 남쪽 복도
            new Rect(0, 14, 14, 8), new Rect(14, 14, 3, 8), new Rect(17, 14, 14, 8), new Rect(31, 14, 3, 8), new Rect(34, 14, 14, 8),     // 9..13 중간 병동
            new Rect(0, 22, 17, 4), new Rect(17, 22, 17, 4), new Rect(34, 22, 8, 4), new Rect(42, 22, 6, 4),                              // 14..17 북쪽 복도
            new Rect(0, 26, 10, 10), new Rect(10, 26, 8, 10), new Rect(18, 26, 12, 10), new Rect(30, 26, 8, 10), new Rect(38, 26, 10, 10), // 18..22 북쪽 병동
        };


        static readonly string[] ShortLabels = { "B2", "B1", "1F", "2F", "3F" };
        static readonly string[] Labels = { "지하2", "지하1", "1층", "2층", "3층" };

        public static string Label(int floor) => floor >= 0 && floor < Floors ? Labels[floor] : "?";
        public static string ShortLabel(int floor) => floor >= 0 && floor < Floors ? ShortLabels[floor] : "?";

        public static float FloorY(int floor) => (floor - GroundFloor) * FloorHeight;

        /// <summary>높이(y)로 층 번호를 구한다. 층 바닥 y를 기준으로 가까운 층.</summary>
        public static int FloorOf(float y) =>
            Mathf.Clamp(Mathf.RoundToInt((y - 1.5f) / FloorHeight) + GroundFloor, 0, Floors - 1);

        /// <summary>방의 바닥 영역 (x = 서쪽 끝, y = 남쪽 끝(z), width = 동서 길이, height = 남북 길이)</summary>
        public static Rect RoomRect(int room) => Rects[room];

        public static Vector3 RoomCenter(int floor, int room)
        {
            Rect r = Rects[room];
            return new Vector3(r.center.x, FloorY(floor), r.center.y);
        }

        /// <summary>복도(계단 홀 포함): 방이 아니라 길 구간.</summary>
        public static bool IsCorridorRoom(int room) =>
            (room >= 5 && room <= 8) || room == 10 || room == 12 || (room >= 14 && room <= 17);

        /// <summary>월드 좌표가 속한 방 번호. 건물 밖이면 -1.</summary>
        public static int RoomAt(Vector3 p)
        {
            if (p.x < 0f || p.x >= SpanX || p.z < 0f || p.z >= SpanZ) return -1;
            for (int i = 0; i < Rects.Length; i++)
            {
                Rect r = Rects[i];
                if (p.x >= r.xMin && p.x < r.xMax && p.z >= r.yMin && p.z < r.yMax) return i;
            }
            return -1;
        }
    }
}
