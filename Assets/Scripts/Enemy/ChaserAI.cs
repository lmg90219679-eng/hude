using System;
using Escape.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Escape.Enemy
{
    /// <summary>
    /// 추적자 AI 뼈대. 순찰 → 소리/시야 감지 → 추적 → 놓치면 수색 → 순찰.
    /// 외형은 나중에 Varco 모델로 교체하므로, 이 스크립트는 모델과 무관하게 동작한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ChaserAI : MonoBehaviour
    {
        public enum State { Patrol, Investigate, Chase, Search }

        /// <summary>플레이어를 잡은 추적자를 넘겨준다 (층마다 추적자가 있어서 누가 잡았는지 알아야 한다).</summary>
        public static event Action<ChaserAI> PlayerCaught;

        [Header("Target")]
        [SerializeField] Transform player;
        [SerializeField] Transform eye;

        [Tooltip("잡히는 연출에서 카메라가 놓일 자리. 추적자 얼굴 앞에 둔다.")]
        [SerializeField] Transform faceCamPoint;

        [Header("Patrol")]
        [SerializeField] Transform[] waypoints;
        [SerializeField] float patrolSpeed = 2.2f;
        [SerializeField] float waypointWait = 1.5f;

        [Header("Sight")]
        [SerializeField] float sightRange = 13f;               // 손전등을 켰을 때 (빛이 눈에 띈다)
        [SerializeField] float unlitSightRange = 4.5f;         // 손전등을 껐을 때: 어둠 속에서는 가까이서만 보인다
        [SerializeField] float crouchSightFactor = 0.6f;       // 웅크리면 더 안 보인다
        [SerializeField] float sightAngle = 100f;     // 전체 시야각
        [SerializeField] LayerMask sightBlockers = ~0;

        [Header("Chase")]
        // 달리기(5.5)보다 살짝 느리게: 직선에서는 간신히 따돌리지만 문, 모서리, 막다른 길에서 따라잡힌다.
        [SerializeField] float chaseSpeed = 5.25f;
        [SerializeField] float loseSightTime = 3.0f;  // 이 시간 동안 못 보면 수색으로
        [SerializeField] float catchDistance = 1.3f;

        [Header("Search")]
        [SerializeField] float investigateSpeed = 3.2f;
        [SerializeField] float searchDuration = 15f;
        [SerializeField] float searchRadius = 7f;

        NavMeshAgent agent;
        Escape.Player.PlayerController playerController;
        Escape.Player.Flashlight playerLight;

        /// <summary>이 시각 전까지는 플레이어를 알아채지 못한다 (판 시작 직후의 안전 시간).</summary>
        public float GraceUntil { get; set; }
        int waypointIndex;
        float waitTimer;
        float lostTimer;
        float searchTimer;
        float searchPointTimer;
        Vector3 lastKnownPosition;

        public State Current { get; private set; } = State.Patrol;

        /// <summary>지금 이 추적자가 있는 층 (0=지하2 ... 4=3층).</summary>
        public int Floor => TowerLayout.FloorOf(transform.position.y);

        /// <summary>다른 층으로 건너가 플레이어를 계속 쫓게 한다. pos는 도착할 위치.</summary>
        public void FollowTo(Vector3 pos, Vector3 playerPos)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            agent.Warp(SnapToNavMesh(pos));
            lastKnownPosition = playerPos;
            EnterChase();
        }
        public Transform FaceCamPoint => faceCamPoint != null ? faceCamPoint : eye;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (eye == null) eye = transform;
            if (player != null)
            {
                playerController = player.GetComponent<Escape.Player.PlayerController>();
                playerLight = player.GetComponentInChildren<Escape.Player.Flashlight>();
            }
        }

        void OnEnable() => NoiseSystem.OnNoise += OnNoiseHeard;
        void OnDisable() => NoiseSystem.OnNoise -= OnNoiseHeard;

        void Start()
        {
            // 다른 층에서 쫓아 내려온 경우(FollowTo)에는 이미 추적 상태라 순찰로 되돌리지 않는다
            if (Current != State.Chase) EnterPatrol();
        }

        void Update()
        {
            if (player == null) return;

            bool seen = CanSeePlayer();

            switch (Current)
            {
                case State.Patrol:
                    if (seen) { EnterChase(); break; }
                    UpdatePatrol();
                    break;

                case State.Investigate:
                    if (seen) { EnterChase(); break; }
                    if (HasArrived()) EnterSearch(agent.destination);
                    break;

                case State.Chase:
                    UpdateChase(seen);
                    break;

                case State.Search:
                    if (seen) { EnterChase(); break; }
                    UpdateSearch();
                    break;
            }
        }

        // ---------- 감지 ----------

        bool CanSeePlayer()
        {
            if (playerController != null && playerController.IsHidden) return false;
            if (Time.time < GraceUntil) return false;
            if (Mathf.Abs(player.position.y - transform.position.y) > 2.5f) return false;   // 다른 층

            Vector3 target = player.position + Vector3.up * 1.0f;
            Vector3 to = target - eye.position;
            float dist = to.magnitude;

            float range = (playerLight != null && !playerLight.IsOn) ? unlitSightRange : sightRange;
            if (playerController != null && playerController.IsCrouching) range *= crouchSightFactor;
            if (dist > range) return false;
            if (Vector3.Angle(eye.forward, to) > sightAngle * 0.5f) return false;

            if (Physics.Raycast(eye.position, to.normalized, out RaycastHit hit, dist, sightBlockers,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.transform == player || hit.transform.IsChildOf(player);
            }
            return true;
        }

        void OnNoiseHeard(Vector3 position, float radius)
        {
            if (Current == State.Chase) return;
            if (Time.time < GraceUntil) return;
            if (Mathf.Abs(position.y - transform.position.y) > 2.5f) return;   // 다른 층 소리는 못 듣는다
            if (Vector3.Distance(transform.position, position) > radius) return;

            lastKnownPosition = position;
            EnterInvestigate(position);
        }

        // ---------- 상태 전환 ----------

        void EnterPatrol()
        {
            Current = State.Patrol;
            agent.speed = patrolSpeed;
            waitTimer = 0f;
            GoToNextWaypoint();
        }

        void EnterInvestigate(Vector3 point)
        {
            Current = State.Investigate;
            agent.speed = investigateSpeed;
            agent.SetDestination(SnapToNavMesh(point));
        }

        void EnterChase()
        {
            Current = State.Chase;
            agent.speed = chaseSpeed;
            lostTimer = 0f;
        }

        void EnterSearch(Vector3 center)
        {
            Current = State.Search;
            agent.speed = investigateSpeed;
            searchTimer = searchDuration;
            searchPointTimer = 0f;
            lastKnownPosition = center;
        }

        // ---------- 상태 업데이트 ----------

        void UpdatePatrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            if (!HasArrived()) return;

            waitTimer += Time.deltaTime;
            if (waitTimer >= waypointWait)
            {
                waitTimer = 0f;
                GoToNextWaypoint();
            }
        }

        void UpdateChase(bool seen)
        {
            if (seen)
            {
                lostTimer = 0f;
                lastKnownPosition = player.position;
            }
            else
            {
                lostTimer += Time.deltaTime;
            }

            agent.SetDestination(SnapToNavMesh(lastKnownPosition));

            bool hidden = playerController != null && playerController.IsHidden;
            if (!hidden && Vector3.Distance(transform.position, player.position) <= catchDistance)
            {
                agent.isStopped = true;
                PlayerCaught?.Invoke(this);
                enabled = false;
                return;
            }

            if (lostTimer >= loseSightTime) EnterSearch(lastKnownPosition);
        }

        void UpdateSearch()
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f) { EnterPatrol(); return; }

            searchPointTimer -= Time.deltaTime;
            if (searchPointTimer <= 0f || HasArrived())
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * searchRadius;
                Vector3 p = lastKnownPosition + new Vector3(r.x, 0f, r.y);
                agent.SetDestination(SnapToNavMesh(p));
                searchPointTimer = 3f;
            }
        }

        // ---------- 유틸 ----------

        void GoToNextWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            agent.SetDestination(SnapToNavMesh(waypoints[waypointIndex].position));
        }

        bool HasArrived()
        {
            return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
        }

        static Vector3 SnapToNavMesh(Vector3 p)
        {
            return NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;
        }

        public void Configure(Transform playerTransform, Transform[] patrolPoints)
        {
            player = playerTransform;
            waypoints = patrolPoints;
        }
    }
}
