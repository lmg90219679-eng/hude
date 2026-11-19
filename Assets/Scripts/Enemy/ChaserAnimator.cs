using UnityEngine;
using UnityEngine.AI;

namespace Escape.Enemy
{
    /// <summary>
    /// 추적자 애니메이션. 추적 중이면 달리기, 그 외에는 걷기. 멈춰 있으면 동작을 멈춘다.
    /// Varco 애니메이션은 클립마다 몸이 향하는 방향이 달라서(달리기는 옆을 봄),
    /// 에디터에서 측정한 보정 각도(walkYaw, runYaw)로 모델을 돌려 항상 이동 방향을 보게 한다.
    /// Animator 컨트롤러에는 bool 파라미터 "Run"이 있고, Walk/Run 두 상태가 있다.
    /// </summary>
    public class ChaserAnimator : MonoBehaviour
    {
        static readonly int RunParam = Animator.StringToHash("Run");

        [SerializeField] Animator animator;
        [SerializeField] ChaserAI ai;
        [SerializeField] NavMeshAgent agent;
        [SerializeField] float movingThreshold = 0.15f;

        [Header("클립별 몸 방향 보정 (도). 씬 빌더가 측정해서 채운다")]
        [SerializeField] float walkYaw;
        [SerializeField] float runYaw;
        [SerializeField] float yawTurnSpeed = 720f;

        bool facePoseShown;

        public void SetYawOffsets(float walk, float run)
        {
            walkYaw = walk;
            runYaw = run;
        }

        void Awake()
        {
            if (ai == null) ai = GetComponent<ChaserAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (animator == null || ai == null || agent == null || facePoseShown) return;

            bool moving = agent.velocity.magnitude > movingThreshold;
            bool running = ai.Current == ChaserAI.State.Chase;
            animator.SetBool(RunParam, running);
            animator.speed = moving ? 1f : 0f;

            // 클립이 바라보는 방향을 이동 방향(+Z)에 맞춘다
            float target = running ? runYaw : walkYaw;
            var visual = animator.transform;
            var want = Quaternion.Euler(0f, target, 0f);
            visual.localRotation = Quaternion.RotateTowards(visual.localRotation, want, yawTurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 잡히는 연출용. 애니메이션을 끄고 기본 자세로 되돌려, 얼굴이 정면(+Z)을 똑바로 보게 한다.
        /// </summary>
        public void ShowFacePose()
        {
            if (animator == null) return;
            facePoseShown = true;
            animator.runtimeAnimatorController = null;
            animator.Rebind();
            animator.transform.localRotation = Quaternion.identity;
        }
    }
}
