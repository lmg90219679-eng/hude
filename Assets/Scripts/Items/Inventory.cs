using System;
using System.Collections.Generic;
using Escape.Player;
using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 인벤토리와 조합. 조합 규칙:
    ///   배터리 + 손전등 = 손전등 충전
    ///   예비 손전등 + 손전등 = 손전등 교체 (배터리 가득)
    ///   천 + 라이터 = 연막 (G키로 던져 추적자를 유인)
    ///   탈출 열쇠 조각 5개 = 탈출 열쇠 1개 (조각 두 개를 골라 합친다)
    ///   쇠막대 + 잠긴 문 = 강제 개방 (Door에서 처리)
    ///   전선 + 전원함 = 전원 복구 (PowerBox에서 처리)
    /// 방 열쇠는 문마다 하나씩 있고, 어느 문의 열쇠인지 id로 구분한다.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public const int PiecesRequired = 5;

        [SerializeField] Flashlight flashlight;
        [SerializeField] float batteryRechargeSeconds = 300f;

        readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();
        readonly HashSet<string> roomKeys = new HashSet<string>();

        public event Action Changed;
        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = -100f;

        public bool HasMasterKey => Has(ItemType.MasterKey);
        public int RoomKeyCount => roomKeys.Count;
        public IEnumerable<string> RoomKeyIds => roomKeys;

        public int PieceCount
        {
            get
            {
                int n = 0;
                for (var t = ItemType.Piece0; t <= ItemType.Piece4; t++) if (Has(t)) n++;
                return n;
            }
        }

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

        public void AddRoomKey(string id)
        {
            roomKeys.Add(id);
            Say($"방 열쇠 획득 ({id})");
            Changed?.Invoke();
        }

        public bool HasRoomKey(string id) => roomKeys.Contains(id);

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

            if (Matches(a, b, ItemType.SpareFlashlight, ItemType.Flashlight))
            {
                if (!Has(ItemType.SpareFlashlight)) return Fail();
                Consume(ItemType.SpareFlashlight);
                if (flashlight != null) flashlight.FullRecharge();
                Say("예비 손전등으로 교체했다");
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

            if (ItemInfo.IsPiece(a) && ItemInfo.IsPiece(b) && a != b)
            {
                if (PieceCount < PiecesRequired)
                {
                    Say($"조각이 더 필요하다 ({PieceCount}/{PiecesRequired})");
                    return false;
                }
                for (var t = ItemType.Piece0; t <= ItemType.Piece4; t++) Consume(t);
                Add(ItemType.MasterKey);
                Say("탈출 열쇠를 완성했다! 1층 정문으로 가자");
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
