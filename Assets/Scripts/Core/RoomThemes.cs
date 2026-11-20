using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>방 하나의 환경: 테마(이름, 소품), 바닥·벽·천장 질감과 색조, 조명, 파손 정도. 층과 방 번호로 정해지고, 모든 방이 서로 다르다.</summary>
    public struct RoomEnv
    {
        public string name;       // 층 안에서 겹치지 않는 이름 (예: "병실 214")
        public string theme;      // 테마 이름 (예: "병실")
        public string[] props;    // 이 테마가 즐겨 놓는 소품 모델
        public int floorTex;      // 0..FloorStyles-1
        public int wallTex;       // 0..WallStyles-1
        public int tint;          // 0..Tints-1 (바닥·벽·천장에 곱하는 색조)
        public int light;         // 0..LightColors-1 (방 조명 색)
        public int flicker;       // 0 안정, 1 깜박임
        public int debris;        // 0..3 파편·잔해 정도
        public bool windowed;     // 바깥벽에 창이 있으면 낸다
        public int signature;     // 위 요소의 조합 (겹치지 않게 맞춘다)
    }

    public static class RoomThemes
    {
        public const int FloorStyles = 8, WallStyles = 6, Tints = 6, LightColors = 5;

        public static readonly Color[] TintColors =
        {
            new Color(1.00f, 1.00f, 1.00f), new Color(0.85f, 1.00f, 0.88f), new Color(0.88f, 0.92f, 1.00f),
            new Color(1.00f, 0.88f, 0.80f), new Color(0.95f, 0.95f, 0.80f), new Color(0.80f, 0.78f, 0.90f),
        };

        public static readonly Color[] LightColorTable =
        {
            new Color(0.75f, 0.82f, 1.00f), new Color(1.00f, 0.80f, 0.55f), new Color(0.65f, 1.00f, 0.80f),
            new Color(1.00f, 0.60f, 0.55f), new Color(0.90f, 0.90f, 1.00f),
        };

        struct Theme
        {
            public string name; public string[] props; public int[] floors; public int[] walls; public bool windowed;
            public Theme(string n, string[] p, int[] f, int[] w, bool win = true) { name = n; props = p; floors = f; walls = w; windowed = win; }
        }

        static string[] P(params string[] p) => p;
        static int[] I(params int[] v) => v;

        // 층마다 테마 목록. 같은 테마가 여러 번 나올 수 있다 (병실처럼). 모든 방은 이 중 하나를 갖는다.
        // 바닥/벽 번호: 바닥 0 이끼 1 파편 2 낡은 타일 3 얼룩 콘크리트 4 낡은 마룻바닥 5 녹색 리놀륨 6 곰팡이 카펫 7 핏자국 타일
        //               벽  0 벗겨진 페인트 1 타일 2 푸른 페인트 3 나무 패널 4 낙서 콘크리트 5 빛바랜 벽지
        static readonly Theme[][] Pools =
        {
            // ---------- 지하2 ----------
            new[]
            {
                new Theme("세탁실", P("WashingMachine", "MetalShelf", "MedicineCart"), I(2, 3), I(1, 4), false),
                new Theme("보일러실", P("Boiler", "MetalShelf", "OxygenTank"), I(3, 1), I(4, 0), false),
                new Theme("기계실", P("Boiler", "OxygenTank", "MetalShelf"), I(3, 1), I(4, 1), false),
                new Theme("배전실", P("MetalShelf", "FilingCabinet", "OxygenTank"), I(3, 2), I(4, 0), false),
                new Theme("창고", P("MetalShelf", "MetalShelf", "MedicineCart"), I(3, 1), I(0, 4), false),
                new Theme("창고", P("MetalShelf", "Wheelchair", "Gurney"), I(1, 3), I(0, 1), false),
                new Theme("폐기물실", P("MetalShelf", "Wheelchair", "OxygenTank"), I(1, 7), I(4, 0), false),
                new Theme("냉동 창고", P("MorgueUnit", "MetalShelf"), I(2, 7), I(1, 4), false),
                new Theme("환기실", P("OxygenTank", "MetalShelf", "Boiler"), I(3, 2), I(4, 1), false),
                new Theme("수조실", P("Sink", "OxygenTank", "MetalShelf"), I(2, 0), I(1, 0), false),
                new Theme("직원 휴게실", P("WaitingBench", "OfficeDesk", "Bookshelf"), I(4, 6), I(3, 5), false),
                new Theme("정비실", P("MetalShelf", "OfficeDesk", "MedicineCart"), I(3, 1), I(2, 4), false),
                new Theme("탈의실", P("SteelLocker", "WaitingBench", "Sink"), I(2, 3), I(1, 2), false),
                new Theme("취사장", P("KitchenCounter", "DiningTable", "MetalShelf"), I(2, 5), I(1, 0), false),
            },
            // ---------- 지하1 ----------
            new[]
            {
                new Theme("영안실", P("MorgueUnit", "ExamTable", "FoldingScreen"), I(2, 7), I(1, 2), false),
                new Theme("안치실", P("MorgueUnit", "MorgueUnit", "MetalShelf"), I(2, 3), I(1, 4), false),
                new Theme("방사선실", P("XRayMachine", "FoldingScreen", "MedicineCart"), I(2, 5), I(1, 2), false),
                new Theme("MRI실", P("XRayMachine", "ExamTable", "OxygenTank"), I(5, 2), I(2, 1), false),
                new Theme("검사실", P("ExamTable", "MedicineCart", "FoldingScreen"), I(5, 2), I(2, 0), false),
                new Theme("병리실", P("ExamTable", "Sink", "MetalShelf"), I(2, 7), I(1, 0), false),
                new Theme("소독실", P("Sink", "MetalShelf", "MedicineCart"), I(2, 5), I(1, 2), false),
                new Theme("격리실", P("HospitalBed", "FoldingScreen", "IVStand"), I(7, 1), I(0, 2), false),
                new Theme("혈액 보관실", P("MetalShelf", "IVStand", "MedicineCart"), I(2, 5), I(1, 2), false),
                new Theme("약품 창고", P("MetalShelf", "MetalShelf", "MedicineCart"), I(3, 1), I(0, 4), false),
                new Theme("중앙 공급실", P("MetalShelf", "MedicineCart", "Gurney"), I(3, 5), I(2, 4), false),
                new Theme("장비 창고", P("Gurney", "Wheelchair", "OxygenTank"), I(3, 1), I(0, 4), false),
                new Theme("부검실", P("ExamTable", "SurgicalLamp", "Sink"), I(7, 2), I(1, 0), false),
                new Theme("기록 보관실", P("FilingCabinet", "FilingCabinet", "Bookshelf"), I(4, 6), I(3, 5), false),
            },
            // ---------- 1층 ----------
            new[]
            {
                new Theme("접수실", P("NurseCounter", "WaitingBench", "FilingCabinet"), I(2, 5), I(2, 5)),
                new Theme("약국", P("MetalShelf", "NurseCounter", "MedicineCart"), I(5, 2), I(2, 1)),
                new Theme("응급 처치실", P("Gurney", "IVStand", "MedicineCart"), I(7, 2), I(0, 1)),
                new Theme("진료실", P("OfficeDesk", "ExamTable", "FoldingScreen"), I(4, 5), I(2, 5)),
                new Theme("진료실", P("OfficeDesk", "ExamTable", "Bookshelf"), I(6, 4), I(3, 2)),
                new Theme("상담실", P("OfficeDesk", "WaitingBench", "Bookshelf"), I(6, 4), I(5, 3)),
                new Theme("원무과", P("OfficeDesk", "FilingCabinet", "FilingCabinet"), I(5, 6), I(2, 5)),
                new Theme("수납 창구", P("NurseCounter", "FilingCabinet", "WaitingBench"), I(2, 5), I(2, 5)),
                new Theme("의무 기록실", P("FilingCabinet", "FilingCabinet", "Bookshelf"), I(4, 6), I(3, 5)),
                new Theme("물리치료실", P("ExamTable", "Wheelchair", "FoldingScreen"), I(5, 4), I(2, 0)),
                new Theme("대기실", P("WaitingBench", "WaitingBench", "Wheelchair"), I(1, 0), I(0, 5)),
                new Theme("매점", P("KitchenCounter", "MetalShelf", "WaitingBench"), I(2, 5), I(5, 2)),
                new Theme("구내식당", P("DiningTable", "KitchenCounter", "DiningTable"), I(2, 4), I(1, 0)),
                new Theme("경비실", P("OfficeDesk", "Bookshelf", "FilingCabinet"), I(3, 6), I(4, 3)),
                new Theme("도서실", P("Bookshelf", "Bookshelf", "DiningTable"), I(4, 6), I(3, 5)),
                new Theme("예배실", P("WaitingBench", "WaitingBench", "WaitingBench"), I(4, 0), I(5, 0)),
            },
            // ---------- 2층 ----------
            new[]
            {
                new Theme("병실", P("HospitalBed", "HospitalBed", "IVStand", "FoldingScreen"), I(0, 1, 4), I(0, 2, 5)),
                new Theme("병실", P("HospitalBed", "HospitalBed", "HospitalBed", "IVStand"), I(5, 2, 7), I(2, 0, 1)),
                new Theme("병실", P("HospitalBed", "IVStand", "MedicineCart", "FoldingScreen"), I(1, 4, 6), I(5, 0, 3)),
                new Theme("병실", P("HospitalBed", "HospitalBed", "Wheelchair", "IVStand"), I(0, 5, 7), I(2, 4, 0)),
                new Theme("병실", P("HospitalBed", "HospitalBed", "OxygenTank", "FoldingScreen"), I(1, 3, 6), I(0, 5, 1)),
                new Theme("소아 병실", P("Crib", "Crib", "IVStand", "FoldingScreen"), I(5, 6, 0), I(5, 2, 0)),
                new Theme("처치실", P("ExamTable", "MedicineCart", "IVStand"), I(2, 5, 7), I(1, 2, 0)),
                new Theme("주사실", P("MedicineCart", "IVStand", "WaitingBench"), I(5, 2, 1), I(2, 0, 5)),
                new Theme("면회실", P("WaitingBench", "DiningTable", "Bookshelf"), I(4, 6, 1), I(3, 5, 0)),
                new Theme("회복실", P("HospitalBed", "Gurney", "IVStand"), I(5, 7, 1), I(2, 0, 1)),
                new Theme("격리 병실", P("HospitalBed", "FoldingScreen", "OxygenTank"), I(7, 1, 3), I(0, 4, 2)),
                new Theme("샤워실", P("Sink", "Wheelchair", "MetalShelf"), I(2, 7, 1), I(1, 0, 2)),
                new Theme("급식실", P("KitchenCounter", "DiningTable", "MetalShelf"), I(2, 5, 4), I(1, 0, 5)),
                new Theme("의국", P("OfficeDesk", "Bookshelf", "HospitalBed"), I(4, 6, 5), I(3, 5, 2)),
                new Theme("환자 휴게실", P("WaitingBench", "DiningTable", "Wheelchair"), I(6, 4, 1), I(5, 3, 0)),
                new Theme("린넨실", P("MetalShelf", "MetalShelf", "WashingMachine"), I(3, 2, 1), I(1, 0, 4)),
            },
            // ---------- 3층 ----------
            new[]
            {
                new Theme("병실", P("HospitalBed", "HospitalBed", "IVStand", "FoldingScreen"), I(1, 4, 0), I(0, 2, 5)),
                new Theme("병실", P("HospitalBed", "IVStand", "Wheelchair", "MedicineCart"), I(5, 6, 7), I(2, 4, 0)),
                new Theme("병실", P("HospitalBed", "HospitalBed", "OxygenTank", "FoldingScreen"), I(0, 3, 1), I(5, 0, 1)),
                new Theme("병실", P("HospitalBed", "HospitalBed", "HospitalBed", "IVStand"), I(2, 5, 4), I(0, 2, 3)),
                new Theme("수술실", P("OperatingTable", "SurgicalLamp", "MedicineCart", "Sink"), I(7, 2, 5), I(1, 2, 0), false),
                new Theme("수술실", P("OperatingTable", "SurgicalLamp", "OxygenTank", "IVStand"), I(2, 7, 5), I(1, 2, 4), false),
                new Theme("수술 준비실", P("Sink", "MedicineCart", "SurgicalLamp", "Gurney"), I(2, 5, 7), I(1, 2, 0), false),
                new Theme("중환자실", P("HospitalBed", "IVStand", "OxygenTank", "FoldingScreen"), I(5, 7, 1), I(2, 0, 1)),
                new Theme("마취실", P("OxygenTank", "MedicineCart", "Gurney", "SurgicalLamp"), I(5, 2, 7), I(2, 1, 0), false),
                new Theme("원장실", P("OfficeDesk", "Bookshelf", "FilingCabinet", "WaitingBench"), I(4, 6, 5), I(3, 5, 2)),
                new Theme("회의실", P("DiningTable", "Bookshelf", "FilingCabinet"), I(6, 4, 5), I(3, 5, 0)),
                new Theme("연구실", P("ExamTable", "Bookshelf", "MetalShelf", "Sink"), I(5, 2, 3), I(1, 2, 0)),
                new Theme("정신과 격리실", P("HospitalBed", "FoldingScreen"), I(7, 1, 3), I(0, 4, 2), false),
                new Theme("VIP 병실", P("HospitalBed", "WaitingBench", "Wheelchair", "Bookshelf"), I(4, 6, 5), I(5, 3, 2)),
                new Theme("분만실", P("HospitalBed", "Crib", "IVStand", "Sink"), I(5, 7, 2), I(2, 1, 0)),
                new Theme("재활실", P("ExamTable", "Wheelchair", "FoldingScreen", "WaitingBench"), I(4, 6, 5), I(5, 0, 2)),
            },
        };

        // 특별한 방 이름 (층, 방): 정문 로비, 시작 방, 간호사실, 승강기 앞
        static string SpecialName(int floor, int room, out string[] props)
        {
            props = null;
            if (room == TowerLayout.ExitRoom)
            {
                if (floor == TowerLayout.GroundFloor) { props = P("NurseCounter", "WaitingBench", "WaitingBench", "Wheelchair"); return "정문 로비"; }
                props = P("WaitingBench", "WaitingBench", "Wheelchair", "MetalShelf");
                return "중앙 홀";
            }
            if (room == TowerLayout.StartRoom)
            {
                props = P("OfficeDesk", "WaitingBench", "Bookshelf", "FilingCabinet");
                return floor == TowerLayout.GroundFloor ? "상담실" : "승강기 앞 대기실";
            }
            if (room == TowerLayout.NurseStationRoom)
            {
                props = P("NurseCounter", "MetalShelf", "MedicineCart", "FilingCabinet");
                return floor == TowerLayout.NurseStationFloor ? "간호사실" : "처치 데스크";
            }
            return null;
        }

        static RoomEnv[,] envs;

        static int Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        static void Build()
        {
            if (envs != null) return;
            int rooms = TowerLayout.RoomsPerFloor;
            envs = new RoomEnv[TowerLayout.Floors, rooms];
            var used = new HashSet<long>();

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                var counters = new Dictionary<string, int>();
                int number = (f == 0 ? 0 : f == 1 ? 100 : f == 2 ? 100 : f == 3 ? 200 : 300);
                int roomNo = 0;
                for (int r = 0; r < rooms; r++)
                {
                    var env = new RoomEnv();
                    if (TowerLayout.IsCorridorRoom(r))
                    {
                        env.theme = "복도";
                        env.name = CorridorName(r);
                        env.props = new string[0];
                        int h = Hash(f, r, 11);
                        env.floorTex = new[] { 1, 3, 2, 0 }[h % 4];
                        env.wallTex = new[] { 0, 5, 2, 4 }[(h >> 4) % 4];
                        env.tint = (h >> 8) % Tints;
                        env.light = (h >> 12) % LightColors;
                        env.flicker = (h >> 16) % 5 == 0 ? 1 : 0;
                        env.debris = 2;
                        env.windowed = false;
                        envs[f, r] = env;
                        continue;
                    }

                    roomNo++;
                    string special = SpecialName(f, r, out var specialProps);
                    var pool = Pools[f];
                    int th = Hash(f, r, 3) % pool.Length;
                    Theme t = pool[th];

                    env.theme = special ?? t.name;
                    env.props = specialProps ?? t.props;
                    env.windowed = special == null ? t.windowed : f >= 2;
                    if (special != null) env.name = special;
                    else
                    {
                        counters.TryGetValue(t.name, out int c);
                        counters[t.name] = c + 1;
                        env.name = f >= 2 ? $"{t.name} {number + roomNo}" : $"{t.name} {(f == 0 ? "B2" : "B1")}-{c + 1}";
                    }

                    // 바닥/벽/색조/조명 조합이 다른 방과 겹치지 않게 고른다
                    for (int attempt = 0; attempt < 400; attempt++)
                    {
                        int h = Hash(f * 1000 + r, attempt, 97);
                        env.floorTex = t.floors[h % t.floors.Length];
                        env.wallTex = t.walls[(h >> 3) % t.walls.Length];
                        env.tint = (h >> 6) % Tints;
                        env.light = (h >> 9) % LightColors;
                        env.flicker = (h >> 12) % 6 == 0 ? 1 : 0;
                        env.debris = (h >> 14) % 4;
                        long key = ((((long)env.floorTex * 8 + env.wallTex) * 8 + env.tint) * 8 + env.light) * 8 + env.debris;
                        key = key * 100 + Mathf.Abs(env.theme.GetHashCode() % 97);
                        if (used.Add(key)) break;
                    }
                    env.signature = Hash(env.floorTex, env.wallTex * 8 + env.tint, env.light * 8 + env.debris);
                    envs[f, r] = env;
                }
            }
        }

        static string CorridorName(int room)
        {
            if (room == TowerLayout.WestStairRoom) return "서쪽 계단 홀";
            if (room == TowerLayout.EastStairRoom) return "동쪽 계단 홀";
            Rect r = TowerLayout.RoomRect(room);
            if (TowerLayout.KindOf(room) == TowerLayout.Kind.Connector)
                return (r.center.x < TowerLayout.SpanX * 0.5f ? "서쪽" : "동쪽") + $" 연결 복도 {TowerLayout.BandOf(room)}";
            int row = Mathf.RoundToInt((r.yMin - 9f) / 13f) + 1;
            return $"복도 {row} (x{Mathf.RoundToInt(r.center.x)})";
        }

        public static RoomEnv Get(int floor, int room)
        {
            Build();
            if (floor < 0 || floor >= TowerLayout.Floors || room < 0 || room >= TowerLayout.RoomsPerFloor) return default;
            return envs[floor, room];
        }
    }
}
