using System.Collections.Generic;
using System.Linq;
using Escape.Core;
using Escape.Interaction;
using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 판이 시작될 때 컨테이너(서랍 등)에 아이템과 방 열쇠를 무작위로 채운다.
    /// 풀 수 있게 보장한다: 잠긴 문의 방 열쇠는 "그 문을 열기 전에 갈 수 있는 방"의 컨테이너에만 넣는다.
    /// (복도는 모두 열려 있고, 일반 문은 열쇠 없이 지나므로, 도달 가능한 방을 문 연결로 계산한다.)
    /// 층마다 탈출 열쇠 조각 1개는 마지막 잠긴 문 너머에 숨긴다.
    /// </summary>
    public class TowerSpawner : MonoBehaviour
    {
        [SerializeField] int batteriesPerFloor = 2;
        [SerializeField] int[] spareFlashlightFloors = { 0, 2, 4 };

        void Start()
        {
            var containers = FindObjectsByType<Container>(FindObjectsSortMode.None);
            var allDoors = FindObjectsByType<Door>(FindObjectsSortMode.None).Where(d => !d.secret && d.roomA != d.roomB).ToArray();

            // 전선은 전원함이 있는 1층 근처에 도움이 되도록 지하1~2층 중 한 곳에 하나만 둔다
            int wireFloor = Random.Range(1, 4);

            for (int f = 0; f < TowerLayout.Floors; f++)
                FillFloor(f, containers.Where(c => c.floor == f).ToList(),
                          allDoors.Where(d => d.floor == f).ToList(), f == wireFloor);
        }

        /// <summary>열려 있는 문(잠기지 않은 문, 또는 이미 열쇠를 둔 문)만 지나서 갈 수 있는 방들.</summary>
        static HashSet<int> Reachable(List<Door> doors, HashSet<Door> passable)
        {
            var reach = new HashSet<int>();
            for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) if (TowerLayout.IsCorridorRoom(r)) reach.Add(r);   // 복도는 모두 이어져 있다
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var d in doors)
                {
                    bool open = string.IsNullOrEmpty(d.lockKeyId) || passable.Contains(d);
                    if (!open) continue;
                    bool a = reach.Contains(d.roomA), b = reach.Contains(d.roomB);
                    if (a == b) continue;
                    reach.Add(a ? d.roomB : d.roomA);
                    grew = true;
                }
            }
            return reach;
        }

        void FillFloor(int floor, List<Container> all, List<Door> doors, bool placeWire)
        {
            foreach (var c in all) c.Fill(null);
            var free = new List<Container>(all);

            // 직원 출입카드: 간호사실의 금속 서랍에 고정 (열면 큰 소음이 난다)
            var metal = free.FirstOrDefault(c => c.metalDrawer);
            if (metal != null) Place(metal, ItemDrop.Item(ItemType.SecurityCard), free);

            var locked = doors.Where(d => !string.IsNullOrEmpty(d.lockKeyId)).OrderBy(d => d.treeOrder).ToList();

            // 탈출 열쇠 조각: 마지막 잠긴 문을 열어야 갈 수 있는 방에 먼저 예약
            if (locked.Count > 0)
            {
                var allButLast = new HashSet<Door>(locked.Take(locked.Count - 1));
                var before = Reachable(doors, allButLast);
                var after = Reachable(doors, new HashSet<Door>(locked));
                after.ExceptWith(before);
                var spot = PickRandom(free, c => after.Contains(c.room)) ?? PickRandom(free, c => true);
                Place(spot, ItemDrop.Item(ItemInfo.PieceForFloor(floor)), free);
            }
            else
            {
                Place(PickRandom(free, c => true), ItemDrop.Item(ItemInfo.PieceForFloor(floor)), free);
            }

            // 방 열쇠: 열리는 순서대로, 그 시점에 갈 수 있는 방의 컨테이너에만
            var opened = new HashSet<Door>();
            foreach (var door in locked)
            {
                var reach = Reachable(doors, opened);
                var spot = PickRandom(free, c => reach.Contains(c.room));
                if (spot == null)
                {
                    Debug.LogWarning($"[TowerSpawner] {TowerLayout.Label(floor)} 열쇠 {door.lockKeyId}를 둘 자리가 없다.");
                }
                else Place(spot, ItemDrop.RoomKey(door.lockKeyId), free);
                opened.Add(door);
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
