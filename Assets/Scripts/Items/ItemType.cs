namespace Escape.Items
{
    public enum ItemType
    {
        Key,
        Wire,
        Battery,
        Cloth,
        Lighter,
        Crowbar,
        Smoke,       // 천 + 라이터 = 연막
        Flashlight,  // 항상 가지고 있는 기본 아이템 (조합 대상)
    }

    public static class ItemInfo
    {
        public static string Label(ItemType t)
        {
            switch (t)
            {
                case ItemType.Key: return "열쇠";
                case ItemType.Wire: return "전선";
                case ItemType.Battery: return "배터리";
                case ItemType.Cloth: return "천";
                case ItemType.Lighter: return "라이터";
                case ItemType.Crowbar: return "쇠막대";
                case ItemType.Smoke: return "연막";
                case ItemType.Flashlight: return "손전등";
                default: return t.ToString();
            }
        }
    }
}
