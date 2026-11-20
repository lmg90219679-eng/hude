using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 건물 구조. 5개 층(지하2, 지하1, 1층, 2층, 3층), 층 번호(index)는 0=지하2, 1=지하1, 2=1층, 3=2층, 4=3층.
    ///
    /// 평면은 100m x 74m. 6줄의 방 줄(band)과 그 사이의 긴 복도 5줄(폭 4m)이 번갈아 놓이고,
    /// 안쪽 네 줄의 방 줄은 두 개의 세로 연결 복도(폭 3m)가 가로지른다. 복도는 서로 이어져 고리를 이룬다.
    /// 방 줄마다 방이 폭 4.5~12m로 제각각 쪼개져 있고, 일부는 옆방과 문으로 이어진 안방 구조다.
    /// 계단 홀은 남쪽 첫째 복도의 서쪽 끝과 북쪽 다섯째 복도의 동쪽 끝에 있어서 건물 반대편 끝에 떨어져 있다.
    ///
    /// 한 층에 방(방, 복도, 연결 복도, 계단 홀)이 130개쯤 있고 그중 진짜 방이 100개 이상이다.
    /// 방 번호는 층이 달라도 같은 자리다. 방 하나의 환경(테마, 바닥, 벽, 소품)은 층과 방 번호로 정해진다.
    /// </summary>
    public static class TowerLayout
    {
        public const int Floors = 5;
        public const float FloorHeight = 4f;
        public const int GroundFloor = 2;          // 1층의 index

        public const float SpanX = 100f, SpanZ = 74f;

        public enum Kind { Room, Corridor, Connector, Hall }

        public struct DoorEdge
        {
            public int a, b;
            public bool vertical;    // 세로 벽(x 일정)인가
            public float x, z;       // 문 중심
            public bool open;        // 문 없이 뚫린 이음매
            public float w;          // open일 때의 너비
            public bool stair;       // 계단 홀의 비상구
        }

        // ---------- 생성된 평면 ----------
        static readonly List<Rect> rects = new List<Rect>();
        static readonly List<Kind> kinds = new List<Kind>();
        static readonly List<int> bands = new List<int>();
        static readonly List<DoorEdge> edges = new List<DoorEdge>();

        public static int WestStairRoom { get; private set; }
        public static int EastStairRoom { get; private set; }
        public static int CenterRoom { get; private set; }       // 가운데 복도 구간
        public static int ExitRoom { get; private set; }         // 1층 정문 로비
        public static int StartRoom { get; private set; }        // 시작 방 (승강기 승강장이 뒤에 있다)
        public const int NurseStationFloor = 3;                  // 2층
        public static int NurseStationRoom { get; private set; } // 직원 출입카드가 든 금속 서랍이 있는 간호사실
        public static int[] ChaserStartRooms { get; private set; }

        public static int RoomsPerFloor => rects.Count;
        public static int RealRoomsPerFloor { get; private set; }
        public static IReadOnlyList<DoorEdge> Edges => edges;

        static readonly string[] ShortLabels = { "B2", "B1", "1F", "2F", "3F" };
        static readonly string[] Labels = { "지하2", "지하1", "1층", "2층", "3층" };

        public static string Label(int floor) => floor >= 0 && floor < Floors ? Labels[floor] : "?";
        public static string ShortLabel(int floor) => floor >= 0 && floor < Floors ? ShortLabels[floor] : "?";

        public static float FloorY(int floor) => (floor - GroundFloor) * FloorHeight;

        /// <summary>높이(y)로 층 번호를 구한다. 층 바닥 y를 기준으로 가까운 층.</summary>
        public static int FloorOf(float y) =>
            Mathf.Clamp(Mathf.RoundToInt((y - 1.5f) / FloorHeight) + GroundFloor, 0, Floors - 1);

        /// <summary>방의 바닥 영역 (x = 서쪽 끝, y = 남쪽 끝(z), width = 동서 길이, height = 남북 길이)</summary>
        public static Rect RoomRect(int room) => rects[room];
        public static Kind KindOf(int room) => kinds[room];
        public static int BandOf(int room) => bands[room];

        public static Vector3 RoomCenter(int floor, int room)
        {
            Rect r = rects[room];
            return new Vector3(r.center.x, FloorY(floor), r.center.y);
        }

        /// <summary>복도, 연결 복도, 계단 홀: 방이 아니라 길 구간.</summary>
        public static bool IsCorridorRoom(int room) => kinds[room] != Kind.Room;

        /// <summary>월드 좌표가 속한 방 번호. 건물 밖이면 -1.</summary>
        public static int RoomAt(Vector3 p)
        {
            if (p.x < 0f || p.x >= SpanX || p.z < 0f || p.z >= SpanZ) return -1;
            // 방 줄(band)과 복도 줄(row)은 z로 바로 갈린다. 줄 안에서만 찾는다.
            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];
                if (p.z < r.yMin || p.z >= r.yMax) continue;
                if (p.x >= r.xMin && p.x < r.xMax) return i;
            }
            return -1;
        }

        // ---------- 생성 ----------
        static readonly float[] RowZ0 = { 9f, 22f, 35f, 48f, 61f };         // 복도 줄 (높이 4)
        static readonly float[] BandZ0 = { 0f, 13f, 26f, 39f, 52f, 65f };   // 방 줄 (깊이 9)
        const float RowH = 4f, BandH = 9f;
        static readonly float[] ConnX = { 30f, 66f };                       // 세로 연결 복도 중심 (폭 3)
        const float ConnW = 3f;

        static int Add(Rect r, Kind k, int band)
        {
            rects.Add(r); kinds.Add(k); bands.Add(band);
            return rects.Count - 1;
        }

        static float PickWidth(System.Random rng)
        {
            double v = rng.NextDouble();
            if (v < 0.05) return new[] { 9f, 10.5f, 12f }[rng.Next(3)];
            return new[] { 4.5f, 4.7f, 4.9f, 5f, 5.2f, 5.4f }[rng.Next(6)];
        }

        static List<(float a, float b)> Split(System.Random rng, float x0, float x1)
        {
            var res = new List<(float, float)>();
            float x = x0;
            while (x < x1 - 0.01f)
            {
                float rem = x1 - x;
                float w = PickWidth(rng);
                if (rem - w < 4.2f) w = rem;
                res.Add((x, x + w));
                x += w;
            }
            return res;
        }

        static TowerLayout()
        {
            var rng = new System.Random(77031);

            // 1) 복도 줄: 연결 복도와 만나는 자리에서 구간으로 나눈다
            float[] breaks = { 0f, ConnX[0] - ConnW / 2f, ConnX[0] + ConnW / 2f, ConnX[1] - ConnW / 2f, ConnX[1] + ConnW / 2f, SpanX };
            var rowZones = new List<(float x0, float x1, int id)>[5];
            for (int row = 0; row < 5; row++)
            {
                var xs = new List<float>(breaks);
                if (row == 0) xs.Insert(1, 6f);                       // 서쪽 계단 홀
                if (row == 4) xs.Insert(xs.Count - 1, SpanX - 6f);    // 동쪽 계단 홀
                rowZones[row] = new List<(float, float, int)>();
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    bool hall = (row == 0 && i == 0) || (row == 4 && i == xs.Count - 2);
                    int id = Add(new Rect(xs[i], RowZ0[row], xs[i + 1] - xs[i], RowH), hall ? Kind.Hall : Kind.Corridor, -1);
                    rowZones[row].Add((xs[i], xs[i + 1], id));
                    if (hall && row == 0) WestStairRoom = id;
                    if (hall && row == 4) EastStairRoom = id;
                }
            }
            CenterRoom = rowZones[2][2].id;

            // 2) 세로 연결 복도: 안쪽 네 줄의 방 줄을 가로지른다
            var connZone = new Dictionary<(int band, int ci), int>();
            for (int b = 1; b <= 4; b++)
                for (int ci = 0; ci < 2; ci++)
                    connZone[(b, ci)] = Add(new Rect(ConnX[ci] - ConnW / 2f, BandZ0[b], ConnW, BandH), Kind.Connector, b);

            // 3) 방: 방 줄마다 폭이 제각각인 방으로 쪼갠다. 로비, 시작 방, 간호사실은 따로 자리를 잡는다.
            var roomsInBand = new List<(float x0, float x1, int id)>[6];
            for (int b = 0; b < 6; b++)
            {
                roomsInBand[b] = new List<(float, float, int)>();
                var segs = new List<(float a, float b)>();
                if (b == 0 || b == 5) segs.Add((0f, SpanX));
                else { segs.Add((0f, breaks[1])); segs.Add((breaks[2], breaks[3])); segs.Add((breaks[4], SpanX)); }

                // 예약 방 (x 범위)
                (float a, float b, string tag)? reserved = null;
                if (b == 0) reserved = (42f, 58f, "lobby");
                if (b == 5) reserved = (45f, 55f, "start");
                if (b == 3) reserved = (43f, 53f, "nurse");

                foreach (var seg in segs)
                {
                    var pieces = new List<(float a, float b, bool res)>();
                    if (reserved.HasValue && reserved.Value.a >= seg.a - 0.01f && reserved.Value.b <= seg.b + 0.01f)
                    {
                        if (reserved.Value.a > seg.a + 0.01f) foreach (var s in Split(rng, seg.a, reserved.Value.a)) pieces.Add((s.a, s.b, false));
                        pieces.Add((reserved.Value.a, reserved.Value.b, true));
                        if (reserved.Value.b < seg.b - 0.01f) foreach (var s in Split(rng, reserved.Value.b, seg.b)) pieces.Add((s.a, s.b, false));
                    }
                    else foreach (var s in Split(rng, seg.a, seg.b)) pieces.Add((s.a, s.b, false));

                    foreach (var p in pieces)
                    {
                        int id = Add(new Rect(p.a, BandZ0[b], p.b - p.a, BandH), Kind.Room, b);
                        roomsInBand[b].Add((p.a, p.b, id));
                        if (p.res)
                        {
                            if (reserved.Value.tag == "lobby") ExitRoom = id;
                            else if (reserved.Value.tag == "start") StartRoom = id;
                            else NurseStationRoom = id;
                        }
                    }
                }
            }
            RealRoomsPerFloor = 0;
            for (int i = 0; i < kinds.Count; i++) if (kinds[i] == Kind.Room) RealRoomsPerFloor++;

            // 4) 통로: 복도 이음매(뚫림), 계단 홀 비상구, 방 문
            for (int row = 0; row < 5; row++)
            {
                var z = rowZones[row];
                float zc = RowZ0[row] + RowH / 2f;
                for (int i = 0; i < z.Count - 1; i++)
                {
                    bool stairDoor = (row == 0 && i == 0) || (row == 4 && i == z.Count - 2);
                    edges.Add(new DoorEdge { a = z[i].id, b = z[i + 1].id, vertical = true, x = z[i].x1, z = zc, open = !stairDoor, w = RowH, stair = stairDoor });
                }
            }
            for (int b = 1; b <= 4; b++)
                for (int ci = 0; ci < 2; ci++)
                {
                    int cz = connZone[(b, ci)];
                    // 아래 복도 줄(b-1), 위 복도 줄(b)의 교차 구간과 이어진다
                    foreach (int row in new[] { b - 1, b })
                    {
                        int inter = -1;
                        foreach (var rz in rowZones[row]) if (rz.x0 < ConnX[ci] && ConnX[ci] < rz.x1) inter = rz.id;
                        float zEdge = row == b - 1 ? BandZ0[b] : BandZ0[b] + BandH;
                        edges.Add(new DoorEdge { a = cz, b = inter, vertical = false, x = ConnX[ci], z = zEdge, open = true, w = ConnW });
                    }
                }

            for (int b = 0; b < 6; b++)
            {
                var list = roomsInBand[b];
                for (int i = 0; i < list.Count; i++)
                {
                    var rm = list[i];
                    // 복도 문: 방 줄의 남쪽/북쪽 복도 구간 중 겹치는 폭이 가장 큰 구간에 문을 낸다
                    foreach (int row in new[] { b - 1, b })
                    {
                        if (row < 0 || row > 4) continue;
                        float doorZ = row == b - 1 ? BandZ0[b] : BandZ0[b] + BandH;
                        int best = -1; float bestLo = 0f, bestHi = 0f, bestLen = 0f;
                        foreach (var rz in rowZones[row])
                        {
                            if (kinds[rz.id] == Kind.Hall) continue;
                            float lo = Mathf.Max(rm.x0, rz.x0), hi = Mathf.Min(rm.x1, rz.x1);
                            if (hi - lo > bestLen) { bestLen = hi - lo; best = rz.id; bestLo = lo; bestHi = hi; }
                        }
                        if (best < 0 || bestLen < 2.8f) continue;
                        float cx = (bestLo + bestHi) * 0.5f + ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0f, bestLen - 2.8f) * 0.7f;
                        edges.Add(new DoorEdge { a = rm.id, b = best, vertical = false, x = cx, z = doorZ });
                    }
                    // 연결 복도 문
                    if (b >= 1 && b <= 4)
                        for (int ci = 0; ci < 2; ci++)
                        {
                            float cl = ConnX[ci] - ConnW / 2f, cr = ConnX[ci] + ConnW / 2f;
                            float dz = BandZ0[b] + BandH * 0.5f + ((float)rng.NextDouble() - 0.5f) * 3f;
                            if (Mathf.Abs(rm.x1 - cl) < 0.01f) edges.Add(new DoorEdge { a = rm.id, b = connZone[(b, ci)], vertical = true, x = cl, z = dz });
                            if (Mathf.Abs(rm.x0 - cr) < 0.01f) edges.Add(new DoorEdge { a = rm.id, b = connZone[(b, ci)], vertical = true, x = cr, z = dz });
                        }
                    // 옆방과 이어지는 문 (안방 구조)
                    if (i + 1 < list.Count && Mathf.Abs(list[i + 1].x0 - rm.x1) < 0.01f)
                        edges.Add(new DoorEdge { a = rm.id, b = list[i + 1].id, vertical = true, x = rm.x1, z = BandZ0[b] + BandH * 0.5f + ((float)rng.NextDouble() - 0.5f) * 3f });
                }
            }

            // 순찰 추적자의 층별 출발 복도 구간
            ChaserStartRooms = new[] { rowZones[3][0].id, rowZones[1][4].id, rowZones[0][5].id, rowZones[2][0].id, rowZones[1][0].id };
        }
    }
}
