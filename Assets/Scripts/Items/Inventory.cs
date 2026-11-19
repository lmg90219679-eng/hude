using System;
using System.Collections.Generic;
using Escape.Player;
using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 인벤토리와 조합. 조합 규칙(4개):
    ///   배터리 + 손전등 = 손전등 충전
    ///   천 + 라이터 = 연막 (G키로 던져 추적자를 유인)
    ///   쇠막대 + 잠긴 문 = 강제 개방 (LockedDoor에서 처리)
    ///   전선 + 전원함 = 전원 복구 (PowerBox에서 처리)
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public const int KeysRequired = 3;

        [SerializeField] Flashlight flashlight;
        [SerializeField] float batteryRechargeSeconds = 90f;

        readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();

        public event Action Changed;
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = -100f;

        public int KeyCount => Count(ItemType.Key);
        public bool HasAllKeys => KeyCount >= KeysRequired;

        void Awake()
        {
            if (flashlight == null) flashlight = GetComponentInChildren<Flashlight>();
        }

        public int Count(ItemType t) => t == ItemType.Flashlight ? 1 : (items.TryGetValue(t, out int n) ? n : 0);
        public bool Has(ItemType t) => Count(t) > 0;

        public void Add(ItemType t, int n = 1)
        {
            items[t] = Count(t) + n;
            Say($"{ItemInfo.Label(t)} 획득");
            Changed?.Invoke();
        }

        public bool Consume(ItemType t, int n = 1)
        {
            if (Count(t) < n) return false;
            items[t] = Count(t) - n;
            Changed?.Invoke();
            return true;
        }

        public void Say(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.time;
        }

        /// <summary>두 아이템을 조합한다. 성공하면 true.</summary>
        public bool TryCombine(ItemType a, ItemType b)
        {
            if (Matches(a, b, ItemType.Battery, ItemType.Flashlight))
            {
                if (!Has(ItemType.Battery)) return Fail();
                Consume(ItemType.Battery);
                if (flashlight != null) flashlight.Recharge(batteryRechargeSeconds);
                Say("손전등을 충전했다");
                return true;
            }

            if (Matches(a, b, ItemType.Cloth, ItemType.Lighter))
            {
                if (!Has(ItemType.Cloth) || !Has(ItemType.Lighter)) return Fail();
                Consume(ItemType.Cloth);
                Consume(ItemType.Lighter);
                Add(ItemType.Smoke);
                Say("연막을 만들었다 (G키로 던진다)");
                return true;
            }

            return Fail();
        }

        bool Fail()
        {
            Say("조합할 수 없다");
            return false;
        }

        static bool Matches(ItemType a, ItemType b, ItemType x, ItemType y) =>
            (a == x && b == y) || (a == y && b == x);

        public IEnumerable<ItemType> Owned()
        {
            yield return ItemType.Flashlight;
            foreach (var kv in items)
                if (kv.Value > 0) yield return kv.Key;
        }
    }
}
