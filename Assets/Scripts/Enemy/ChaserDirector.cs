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

        ChaserAI[] chasers;
        PlayerController player;

        void Awake()
        {
            chasers = FindObjectsByType<ChaserAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            player = FindFirstObjectByType<PlayerController>();

            // 판 시작 직후에는 주변을 살필 시간을 준다
            foreach (var c in chasers) c.GraceUntil = Time.time + startGraceSeconds;
        }

        void OnEnable() => StairPortal.Used += OnStairUsed;
        void OnDisable() => StairPortal.Used -= OnStairUsed;

        void Update()
        {
            if (player == null) return;
            int playerFloor = TowerLayout.FloorOf(player.transform.position.y);
            foreach (var c in chasers)
            {
                if (c == null) continue;
                bool want = Mathf.Abs(c.Floor - playerFloor) <= 1;
                if (c.gameObject.activeSelf != want) c.gameObject.SetActive(want);
            }
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
