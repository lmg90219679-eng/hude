using System.Collections.Generic;
using System.Linq;
using Escape.Core;
using Escape.Interaction;
using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 판이 시작될 때 컨테이너(서랍 등)에 아이템과 방 열쇠를 무작위로 채운다.
    /// 풀 수 있게 보장한다: 방 열쇠는 "그 문을 열기 전에 갈 수 있는 방"의 컨테이너에만 넣는다.
    /// 층마다 탈출 열쇠 조각 1개는 가장 안쪽(마지막에 열리는) 방에 숨긴다.
    /// </summary>
    public class TowerSpawner : MonoBehaviour
    {
        [SerializeField] int batteriesPerFloor = 2;
        [SerializeField] int[] spareFlashlightFloors = { 0, 2, 4 };

        void Start()
        {
            var containers = FindObjectsByType<Container>(FindObjectsSortMode.None);
            var doors = FindObjectsByType<Door>(FindObjectsSortMode.None)
                .Where(d => !string.IsNullOrEmpty(d.lockKeyId) && d.treeOrder >= 0).ToArray();

            // 전선은 전원함이 있는 1층 근처에 도움이 되도록 지하1~2층 중 한 곳에 하나만 둔다
            int wireFloor = Random.Range(1, 4);

            for (int f = 0; f < TowerLayout.Floors; f++)
                FillFloor(f, containers.Where(c => c.floor == f).ToList(),
                          doors.Where(d => d.floor == f).OrderBy(d => d.treeOrder).ToList(), f == wireFloor);
        }

        void FillFloor(int floor, List<Container> all, List<Door> doors, bool placeWire)
        {
            foreach (var c in all) c.Fill(null);
            var free = new List<Container>(all);

            // 시작부터 갈 수 있는 방: 서쪽 계단방, 가운데 방, 동쪽 계단방
            var reach = new HashSet<int> { TowerLayout.WestStairRoom, TowerLayout.CenterRoom, TowerLayout.EastStairRoom };

            // 탈출 열쇠 조각: 마지막에 열리는 방의 컨테이너에 먼저 예약
            if (doors.Count > 0)
            {
                int deepest = doors[doors.Count - 1].roomB;
                var spot = PickRandom(free, c => c.room == deepest) ?? PickRandom(free, c => true);
                Place(spot, ItemDrop.Item(ItemInfo.PieceForFloor(floor)), free);
            }

            // 방 열쇠: 순서대로, 그 시점에 갈 수 있는 방의 컨테이너에만
            foreach (var door in doors)
            {
                var spot = PickRandom(free, c => reach.Contains(c.room));
                if (spot == null)
                {
                    Debug.LogWarning($"[TowerSpawner] {TowerLayout.Label(floor)} 열쇠 {door.lockKeyId}를 둘 자리가 없다.");
                    continue;
                }
                Place(spot, ItemDrop.RoomKey(door.lockKeyId), free);
                reach.Add(door.roomB);
            }

            // 나머지 아이템은 아무 컨테이너에나
            var extras = new List<ItemType> { ItemType.Cloth, ItemType.Lighter, ItemType.Crowbar };
            for (int i = 0; i < batteriesPerFloor; i++) extras.Add(ItemType.Battery);
            if (placeWire) extras.Add(ItemType.Wire);
            if (System.Array.IndexOf(spareFlashlightFloors, floor) >= 0) extras.Add(ItemType.SpareFlashlight);

            foreach (var t in extras)
            {
                var spot = PickRandom(free, c => true);
                if (spot == null) break;
                Place(spot, ItemDrop.Item(t), free);
            }
        }

        static void Place(Container c, ItemDrop drop, List<Container> free)
        {
            if (c == null) return;
            c.Fill(drop);
            free.Remove(c);
        }

        static Container PickRandom(List<Container> list, System.Func<Container, bool> filter)
        {
            var candidates = list.Where(filter).ToList();
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }
    }
}
