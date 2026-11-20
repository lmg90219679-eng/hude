namespace Escape.Core
{
    /// <summary>
    /// 폐병원의 방 이름 (방 번호는 TowerLayout 평면도 참고). 복도 구간 이름은 층과 상관없이 같고,
    /// 방 이름은 층마다 다르다. 지도 옆에 지금 있는 방의 이름을 보여 주는 데 쓴다.
    /// </summary>
    public static class RoomNames
    {
        // 방 이름을 따로 붙이는 방 번호 순서
        static readonly int[] Named = { 0, 1, 2, 3, 4, 9, 11, 13, 18, 19, 20, 21, 22 };

        // [층][Named 순서]
        static readonly string[][] Names =
        {
            // 지하2
            new[] { "세탁실", "기계실", "보일러실", "창고", "폐기물실", "저수조", "배전반실", "환기실", "비품실", "냉동 창고", "승강기 앞 기계실", "자재 창고", "소각실" },
            // 지하1
            new[] { "영안실", "방사선실", "지하 대기실", "약품 창고", "소독실", "격리실", "병리 검사실", "의료 폐기물실", "시체 안치 대기실", "혈액 보관실", "승강기 앞 대기실", "장비 창고", "부검실" },
            // 1층
            new[] { "약국", "원무과", "정문 로비", "응급 처치실", "접수실", "진료실 1", "대기실", "상담실 2", "물리치료실", "수납 창구", "상담실", "의무 기록실", "경비실" },
            // 2층
            new[] { "처치실", "주사실", "병실 201", "병실 202", "병실 203", "병실 204", "간호사실", "병실 205", "병실 206", "병실 207", "병실 208", "병실 209", "병실 210" },
            // 3층
            new[] { "병실 301", "병실 302", "수술 준비실", "병실 303", "병실 304", "병실 305", "회복실", "병실 306", "병실 307", "VIP 병실", "병실 308", "병실 309", "중환자실" },
        };

        static string Corridor(int room)
        {
            switch (room)
            {
                case 5: return "서쪽 계단 홀";
                case 6: return "남쪽 복도(서)";
                case 7: return "남쪽 복도(중앙)";
                case 8: return "남쪽 복도(동)";
                case 10: return "서쪽 연결 복도";
                case 12: return "동쪽 연결 복도";
                case 14: return "북쪽 복도(서)";
                case 15: return "북쪽 복도(중앙)";
                case 16: return "북쪽 복도(동)";
                case 17: return "동쪽 계단 홀";
                default: return "";
            }
        }

        public static string Get(int floor, int room)
        {
            if (floor < 0 || floor >= Names.Length || room < 0 || room >= TowerLayout.RoomsPerFloor) return "";
            if (TowerLayout.IsCorridorRoom(room)) return Corridor(room);
            for (int i = 0; i < Named.Length; i++)
                if (Named[i] == room) return Names[floor][i];
            return "";
        }
    }
}
