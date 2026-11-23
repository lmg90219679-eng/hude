namespace Escape.Items
{
    public enum ItemType
    {
        Wire,
        Battery,
        Cloth,
        Lighter,
        Crowbar,
        Smoke,            // 천 + 라이터 = 연막
        Flashlight,       // 항상 가지고 있는 기본 아이템 (조합 대상)
        SpareFlashlight,  // 서랍에서 찾는 예비 손전등 (배터리가 다하면 교체)
        Piece0,           // 탈출 열쇠 조각: 지하2
        Piece1,           // 지하1
        Piece2,           // 1층
        Piece3,           // 2층
        Piece4,           // 3층
        MasterKey,        // 조각 5개를 합친 탈출 열쇠
        SecurityCard,     // 간호사실의 직원 출입카드 (엘리베이터 잠금 해제)
    }

    public static class ItemInfo
    {
        public static string Label(ItemType t)
        {
            switch (t)
            {
                case ItemType.Wire: return "전선";
                case ItemType.Battery: return "배터리";
                case ItemType.Cloth: return "천";
                case ItemType.Lighter: return "라이터";
                case ItemType.Crowbar: return "쇠막대";
                case ItemType.Smoke: return "연막";
                case ItemType.Flashlight: return "손전등";
                case ItemType.SpareFlashlight: return "예비 손전등";
                case ItemType.Piece0: return "탈출 열쇠 조각 (지하2)";
                case ItemType.Piece1: return "탈출 열쇠 조각 (지하1)";
                case ItemType.Piece2: return "탈출 열쇠 조각 (1층)";
                case ItemType.Piece3: return "탈출 열쇠 조각 (2층)";
                case ItemType.Piece4: return "탈출 열쇠 조각 (3층)";
                case ItemType.MasterKey: return "탈출 열쇠";
                case ItemType.SecurityCard: return "직원 출입카드";
                default: return t.ToString();
            }
        }

        public static bool IsPiece(ItemType t) => t >= ItemType.Piece0 && t <= ItemType.Piece4;

        public static ItemType PieceForFloor(int floor) => (ItemType)((int)ItemType.Piece0 + floor);
    }

    /// <summary>컨테이너 안에 들어갈 물건. 방 열쇠는 keyId로 어느 문의 열쇠인지 구분한다.</summary>
    public class ItemDrop
    {
        public ItemType? Type;      // null이면 방 열쇠
        public string KeyId;        // 방 열쇠일 때 문 번호 (예: "2F-3")

        public string JunkName;     // 값이 있으면 주울 수 없는 잡동사니 (약병, 주사기 등): 서랍 안에 보이기만 한다

        public bool IsJunk => !string.IsNullOrEmpty(JunkName);

        public static ItemDrop Item(ItemType t) => new ItemDrop { Type = t };
        public static ItemDrop RoomKey(string id) => new ItemDrop { Type = null, KeyId = id };
        public static ItemDrop Junk(string name) => new ItemDrop { Type = null, JunkName = name };
    }
}
