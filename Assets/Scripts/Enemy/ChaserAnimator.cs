using UnityEngine;
using UnityEngine.AI;

namespace Escape.Enemy
{
    /// <summary>
    /// 추적자 애니메이션. 추적 중이면 달리기, 그 외에는 걷기. 멈춰 있으면 동작을 멈춘다.
    /// Animator 컨트롤러에는 bool 파라미터 "Run"이 있고, Walk/Run 두 상태가 있다.
    /// </summary>
    public class ChaserAnimator : MonoBehaviour
    {
        static readonly int RunParam = Animator.StringToHash("Run");

        [SerializeField] Animator animator;
        [SerializeField] ChaserAI ai;
        [SerializeField] NavMeshAgent agent;
        [SerializeField] float movingThreshold = 0.15f;

        void Awake()
        {
            if (ai == null) ai = GetComponent<ChaserAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (animator == null || ai == null || agent == null) return;

            bool moving = agent.velocity.magnitude > movingThreshold;
            animator.SetBool(RunParam, ai.Current == ChaserAI.State.Chase);
            animator.speed = moving ? 1f : 0f;
        }
    }
}
