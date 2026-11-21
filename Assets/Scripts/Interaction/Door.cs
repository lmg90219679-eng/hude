using Escape.Core;
using Escape.Items;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Escape.Interaction
{
    /// <summary>
    /// 문 한 짝. 두 가지 방식이 있다.
    /// - 미닫이(leaf): 문짝이 벽 속으로 미끄러져 들어가며 열린다 (일반 문, 숨겨진 문).
    /// - 여닫이(hinge): 경첩을 축으로 문짝이 돌아서 열린다 (계단 비상구).
    /// 문은 모두 상호작용 키(F)로 열고 닫는다.
    /// lockKeyId가 있으면 잠긴 문이다: 그 문의 방 열쇠가 있으면 열 수 있고,
    /// 쇠막대가 있으면 강제로 열 수 있다 (쇠막대는 소모되고 소음이 크다).
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("미닫이")]
        [SerializeField] Transform leaf;
        [SerializeField] Vector3 slideDir = Vector3.right;   // 열릴 때 문짝이 움직이는 방향 (벽을 따라)
        [SerializeField] float slideDistance = 1.9f;
        [SerializeField] float slideSpeed = 2.2f;            // m/s

        [Header("여닫이")]
        [SerializeField] Transform hinge;
        [SerializeField] float openAngle = 95f;              // 부호로 열리는 방향이 정해진다
        [SerializeField] float swingSpeed = 3.5f;            // 초당 배속 (도 = 이 값 x 60)

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

        public Vector3 obstacleSize;       // 씬 빌더가 채운다: 잠긴 문이 추적자의 길을 막는 상자 크기
        public static readonly List<Door> All = new List<Door>();
        NavMeshObstacle blocker;
        bool open;
        bool unlocked;
        float slideProgress;               // 0 = 닫힘, 1 = 활짝 열림 (미닫이)
        float angle;                       // 여닫이
        Vector3 leafStart;

        public bool IsLocked => !string.IsNullOrEmpty(lockKeyId) && !unlocked;
        public bool IsOpen => open;

        void Awake()
        {
            if (leaf != null) leafStart = leaf.localPosition;
            // 잠긴 문, 숨겨진 문, 전력이 필요한 문은 추적자가 지나가지 못한다 (길찾기에서 막는다)
            if (obstacleSize != Vector3.zero && (!string.IsNullOrEmpty(lockKeyId) || secret || requiresPower))
            {
                blocker = gameObject.AddComponent<NavMeshObstacle>();
                blocker.shape = NavMeshObstacleShape.Box;
                blocker.center = Vector3.up * 1.3f;
                blocker.size = obstacleSize;
                blocker.carving = true;
                blocker.carveOnlyStationary = false;
            }
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>추적자가 문을 연다. 잠기거나 숨겨진 문은 열지 못한다.</summary>
        public bool TryOpenByChaser()
        {
            if (IsLocked || secret || PowerBlocked) return false;
            open = true;
            return true;
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
            if (blocker != null) blocker.enabled = IsLocked || PowerBlocked || (secret && !open);

            // 단서: 문이 닫혀 있고, (전력이 필요한 문이면) 발전기가 복구됐을 때만 빛이 새어 나온다
            if (leak != null)
            {
                bool powered = !requiresPower || (PowerGrid.Instance != null && PowerGrid.Instance.Complete);
                leak.SetActive(!open && powered);
            }

            if (hinge != null)
            {
                float target = open ? openAngle : 0f;
                angle = Mathf.MoveTowards(angle, target, swingSpeed * 60f * Time.deltaTime);
                hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
            }

            if (leaf != null)
            {
                float rate = slideSpeed / Mathf.Max(0.1f, slideDistance);
                slideProgress = Mathf.MoveTowards(slideProgress, open ? 1f : 0f, rate * Time.deltaTime);
                leaf.localPosition = leafStart + slideDir.normalized * (slideDistance * slideProgress);
            }
        }
    }
}
