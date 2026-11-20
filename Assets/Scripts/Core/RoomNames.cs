namespace Escape.Core
{
    /// <summary>방 이름. 층과 방 번호로 정해지는 환경(RoomThemes)의 이름을 그대로 쓴다. 지도 옆에 지금 있는 방의 이름을 보여 주는 데 쓴다.</summary>
    public static class RoomNames
    {
        public static string Get(int floor, int room) => RoomThemes.Get(floor, room).name ?? "";
    }
}
