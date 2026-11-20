using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 여닫는 문. 열고 닫을 때 소음이 난다.
    /// lockKeyId가 있으면 잠긴 문이다: 그 문의 방 열쇠가 있으면 열 수 있고,
    /// 쇠막대가 있으면 강제로 열 수 있다 (쇠막대는 소모되고 소음이 크다).
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform hinge;
        [SerializeField] float openAngle = 95f;
        [SerializeField] float speed = 4f;
        [SerializeField] float noiseRadius = NoiseSystem.DoorRadius;

        [Header("잠금 (씬 빌더가 채운다)")]
        public string lockKeyId;           // 비어 있으면 잠기지 않은 문
        public int floor;
        public int roomA;                  // 문이 이어주는 두 방 (roomB가 새로 열리는 쪽)
        public int roomB;
        public int treeOrder = -1;         // 층 안에서 열리는 순서 (열쇠가 풀리는 순서)
        public bool requiresPower;         // 발전기(차단기 3개)가 복구돼야 열린다 (엘리베이터 홀의 숨겨진 문)
        public bool secret;                // 숨겨진 문: 벽처럼 보이고 지도에 나오지 않는다

        bool open;
        bool unlocked;
        float angle;

        public bool IsLocked => !string.IsNullOrEmpty(lockKeyId) && !unlocked;
        public bool IsOpen => open;

        void Awake()
        {
            if (hinge == null) hinge = transform;
        }

        bool PowerBlocked => requiresPower && (PowerGrid.Instance == null || !PowerGrid.Instance.Complete);

        public string GetPrompt(Interactor who)
        {
            if (PowerBlocked) return null;
            if (IsLocked)
            {
                if (who.Inventory.HasRoomKey(lockKeyId)) return "E: 열쇠로 문을 연다";
                if (who.Inventory.Has(ItemType.Crowbar)) return "E: 쇠막대로 문을 강제로 연다 (큰 소음)";
                return "잠겨 있다";
            }
            if (secret && !open) return "E: 벽의 틈을 밀어 본다";
            return open ? "E: 문을 닫는다" : "E: 문을 연다";
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
            float target = open ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, speed * 60f * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
