using System.Collections.Generic;
using Escape.Interaction;
using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 판이 시작될 때 컨테이너(서랍, 책상, 옷장 등) 중 무작위 3곳에 열쇠를, 다른 곳에 조합 아이템을 놓는다.
    /// 위치를 외울 수 없게 매 판 섞는다.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] Container[] containers;
        [SerializeField] int extraBatteries = 3;

        public void SetContainers(Container[] list) => containers = list;

        void Start()
        {
            if (containers == null || containers.Length == 0)
                containers = FindObjectsByType<Container>(FindObjectsSortMode.None);

            var pool = new List<Container>(containers);
            Shuffle(pool);

            var toPlace = new List<ItemType>();
            for (int i = 0; i < Inventory.KeysRequired; i++) toPlace.Add(ItemType.Key);
            toPlace.Add(ItemType.Wire);
            toPlace.Add(ItemType.Cloth);
            toPlace.Add(ItemType.Lighter);
            toPlace.Add(ItemType.Crowbar);
            for (int i = 0; i < extraBatteries; i++) toPlace.Add(ItemType.Battery);

            for (int i = 0; i < pool.Count; i++)
                pool[i].Fill(i < toPlace.Count ? toPlace[i] : (ItemType?)null);
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
