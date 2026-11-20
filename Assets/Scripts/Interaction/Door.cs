using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 좌우로 미끄러져 열리는 양문(자동문 같은 움직임). 두 짝(leafA, leafB)이 가운데에서 만나고, 열리면 벽 쪽으로 갈라진다.
    /// 문은 모두 상호작용 키(F)로 열고 닫는다.
    /// lockKeyId가 있으면 잠긴 문이다: 그 문의 방 열쇠가 있으면 열 수 있고,
    /// 쇠막대가 있으면 강제로 열 수 있다 (쇠막대는 소모되고 소음이 크다).
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform leafA;          // -slideAxis 쪽 문짝
        [SerializeField] Transform leafB;          // +slideAxis 쪽 문짝
        [SerializeField] Vector3 slideAxis = Vector3.right;
        [SerializeField] float slideDistance = 1.5f;
        [SerializeField] float speed = 2.2f;       // m/s
        [SerializeField] float noiseRadius = NoiseSystem.DoorRadius;

        [Header("잠금 (씬 빌더가 채운다)")]
        public string lockKeyId;           // 비어 있으면 잠기지 않은 문
        public int floor;
        public int roomA;                  // 문이 이어주는 두 방 (roomB가 새로 열리는 쪽)
        public int roomB;
        public int treeOrder = -1;         // 층 안에서 열리는 순서 (열쇠가 풀리는 순서)
        public bool requiresPower;         // 발전기(차단기 3개)가 복구돼야 열린다 (엘리베이터 홀의 숨겨진 문)
        public bool secret;                // 숨겨진 문: 벽처럼 보이고 지도에 나오지 않는다

        [SerializeField] GameObject leak;   // 숨겨진 문 아래로 새어 나오는 빛 (찾는 단서)

        bool open;
        bool unlocked;
        float progress;                    // 0 = 닫힘, 1 = 활짝 열림
        Vector3 startA, startB;

        public bool IsLocked => !string.IsNullOrEmpty(lockKeyId) && !unlocked;
        public bool IsOpen => open;

        void Awake()
        {
            if (leafA != null) startA = leafA.localPosition;
            if (leafB != null) startB = leafB.localPosition;
        }

        bool PowerBlocked => requiresPower && (PowerGrid.Instance == null || !PowerGrid.Instance.Complete);

        public string GetPrompt(Interactor who)
        {
            if (PowerBlocked) return null;
            if (IsLocked)
            {
                if (who.Inventory.HasRoomKey(lockKeyId)) return "F: 열쇠로 문을 연다";
                if (who.Inventory.Has(ItemType.Crowbar)) return "F: 쇠막대로 문을 강제로 연다 (큰 소음)";
                return "잠겨 있다";
            }
            if (secret && !open) return "F: 벽의 틈을 밀어 본다";
            return open ? "F: 문을 닫는다" : "F: 문을 연다";
        }

        public void Interact(Interactor who)
        {
            if (PowerBlocked) return;
            if (IsLocked)
            {
                if (who.Inventory.HasRoomKey(lockKeyId))
                {
                    unlocked = true;
                    who.Inventory.Say("문을 열었다");
                    NoiseSystem.Emit(transform.position, noiseRadius);
                }
                else if (who.Inventory.Consume(ItemType.Crowbar))
                {
                    unlocked = true;
                    who.Inventory.Say("문을 강제로 열었다");
                    NoiseSystem.Emit(transform.position, noiseRadius * 1.6f);
                }
                else
                {
                    who.Inventory.Say("문이 잠겨 있다");
                    return;
                }
                open = true;
                return;
            }

            NoiseSystem.Emit(transform.position, noiseRadius);
            open = !open;
        }

        void Update()
        {
            // 단서: 문이 닫혀 있고, (전력이 필요한 문이면) 발전기가 복구됐을 때만 빛이 새어 나온다
            if (leak != null)
            {
                bool powered = !requiresPower || (PowerGrid.Instance != null && PowerGrid.Instance.Complete);
                leak.SetActive(!open && powered);
            }

            float rate = speed / Mathf.Max(0.1f, slideDistance);
            progress = Mathf.MoveTowards(progress, open ? 1f : 0f, rate * Time.deltaTime);

            Vector3 axis = slideAxis.normalized;
            if (leafA != null) leafA.localPosition = startA - axis * (slideDistance * progress);
            if (leafB != null) leafB.localPosition = startB + axis * (slideDistance * progress);
        }
    }
}
