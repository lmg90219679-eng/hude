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

        public static event Action PlayerCaught;

        [Header("Target")]
        [SerializeField] Transform player;
        [SerializeField] Transform eye;

        [Header("Patrol")]
        [SerializeField] Transform[] waypoints;
        [SerializeField] float patrolSpeed = 2f;
        [SerializeField] float waypointWait = 1.5f;

        [Header("Sight")]
        [SerializeField] float sightRange = 15f;
        [SerializeField] float sightAngle = 100f;     // 전체 시야각
        [SerializeField] LayerMask sightBlockers = ~0;

        [Header("Chase")]
        [SerializeField] float chaseSpeed = 4.2f;
        [SerializeField] float loseSightTime = 2.5f;  // 이 시간 동안 못 보면 수색으로
        [SerializeField] float catchDistance = 1.2f;

        [Header("Search")]
        [SerializeField] float investigateSpeed = 3f;
        [SerializeField] float searchDuration = 8f;
        [SerializeField] float searchRadius = 6f;

        NavMeshAgent agent;
        Escape.Player.PlayerController playerController;
        int waypointIndex;
        float waitTimer;
        float lostTimer;
        float searchTimer;
        float searchPointTimer;
        Vector3 lastKnownPosition;

        public State Current { get; private set; } = State.Patrol;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (eye == null) eye = transform;
            if (player != null) playerController = player.GetComponent<Escape.Player.PlayerController>();
        }

        void OnEnable() => NoiseSystem.OnNoise += OnNoiseHeard;
        void OnDisable() => NoiseSystem.OnNoise -= OnNoiseHeard;

        void Start()
        {
            EnterPatrol();
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

            Vector3 target = player.position + Vector3.up * 1.0f;
            Vector3 to = target - eye.position;
            float dist = to.magnitude;
            if (dist > sightRange) return false;
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
                PlayerCaught?.Invoke();
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
