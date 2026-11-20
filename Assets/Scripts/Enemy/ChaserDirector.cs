using System.Collections;
using Escape.Core;
using Escape.Interaction;
using Escape.Player;
using UnityEngine;

namespace Escape.Enemy
{
    /// <summary>
    /// 층마다 있는 추적자를 관리한다.
    /// - 플레이어와 두 층 이상 떨어진 추적자는 꺼 둔다 (성능).
    /// - 추적 중인 추적자 가까이에서 계단을 타면, 잠시 뒤 그 추적자가 계단으로 따라온다.
    /// </summary>
    public class ChaserDirector : MonoBehaviour
    {
        [SerializeField] float followDelay = 2.5f;
        [SerializeField] float followRange = 18f;
        [SerializeField] float startGraceSeconds = 30f;

        public static ChaserDirector Instance { get; private set; }
        public bool HuntMode { get; private set; }

        ChaserAI[] chasers;
        PlayerController player;

        void Awake()
        {
            Instance = this;
            chasers = FindObjectsByType<ChaserAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            player = FindFirstObjectByType<PlayerController>();

            // 판 시작 직후에는 주변을 살필 시간을 준다
            foreach (var c in chasers) c.GraceUntil = Time.time + startGraceSeconds;
        }

        int lastPlayerFloor = -1;

        /// <summary>도착한 층에서 플레이어와 가장 가까운 계단 입구 자리.</summary>
        static Vector3 NearestStairArrival(int floor, Vector3 from)
        {
            Vector3 best = from;
            float bestDist = float.MaxValue;
            foreach (var s in StairPortal.All)
            {
                if (s == null || s.floor != floor || s.exitPoint == null) continue;
                float d = Vector3.Distance(from, s.exitPoint.position);
                if (d < bestDist) { bestDist = d; best = s.exitPoint.position; }
            }
            return best;
        }

        void Update()
        {
            if (player == null) return;
            int playerFloor = TowerLayout.FloorOf(player.transform.position.y);

            // 플레이어가 계단을 걸어 다른 층으로 넘어가면, 가까이서 쫓던 추적자가 계단 입구로 따라온다
            if (lastPlayerFloor >= 0 && playerFloor != lastPlayerFloor)
                OnStairUsed(lastPlayerFloor, playerFloor, NearestStairArrival(playerFloor, player.transform.position));
            lastPlayerFloor = playerFloor;

            foreach (var c in chasers)
            {
                if (c == null) continue;
                bool want = HuntMode || Mathf.Abs(c.Floor - playerFloor) <= 1;
                if (c.gameObject.activeSelf != want) c.gameObject.SetActive(want);
            }
        }

        /// <summary>마지막 차단기: 모든 추적자가 추격 모드로 바뀌어 건물 전체를 돌아다닌다.</summary>
        public void EnterHuntMode()
        {
            if (HuntMode) return;
            HuntMode = true;
            foreach (var c in chasers)
            {
                if (c == null) continue;
                c.gameObject.SetActive(true);
                c.SetHunt(true);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnStairUsed(int from, int to, Vector3 arrival)
        {
            foreach (var c in chasers)
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.Floor != from || c.Current != ChaserAI.State.Chase) continue;
                if (Vector3.Distance(c.transform.position, player.transform.position) > followRange) continue;
                StartCoroutine(Follow(c, arrival));
            }
        }

        IEnumerator Follow(ChaserAI chaser, Vector3 arrival)
        {
            yield return new WaitForSeconds(followDelay);
            if (chaser == null) yield break;
            chaser.gameObject.SetActive(true);
            chaser.FollowTo(arrival, player.transform.position);
        }
    }
}
