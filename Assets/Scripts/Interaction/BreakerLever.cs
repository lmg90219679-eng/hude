using Escape.Core;
using Escape.Audio;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 전기실 차단기 레버. 1 → 2 → 3 순서로 올려야 한다. 순서가 틀리면 전부 내려가고 큰 소음이 난다.
    /// 레버 모델은 바닥판의 힌지 축(X)을 기준으로 젖혀서 올림/내림을 보여 준다.
    /// </summary>
    public class BreakerLever : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform pivot;                 // 젖혀지는 레버 모델의 부모
        [SerializeField] float downAngle = 32f;           // 내려간 각도 (도)
        [SerializeField] float upAngle = -32f;            // 올라간 각도 (도)
        [SerializeField] float travelTime = 0.5f;

        public int index;                                 // 0, 1, 2

        float angle;
        bool raised;

        void Start()
        {
            angle = downAngle;
            Apply();
            if (PowerGrid.Instance != null) PowerGrid.Instance.WrongOrder += OnWrongOrder;
        }

        void OnDestroy()
        {
            if (PowerGrid.Instance != null) PowerGrid.Instance.WrongOrder -= OnWrongOrder;
        }

        public string GetPrompt(Interactor who)
        {
            var grid = PowerGrid.Instance;
            if (grid == null) return null;
            if (grid.Complete || raised) return null;
            return $"F: 차단기 {index + 1}을 올린다";
        }

        public void Interact(Interactor who)
        {
            var grid = PowerGrid.Instance;
            if (grid == null || grid.Complete || raised) return;

            bool ok = grid.TryRaise(index);
            if (ok)
            {
                raised = true;
                NoiseSystem.Emit(transform.position, 28f);
                if (GameAudio.Instance != null) GameAudio.Instance.PlayMetal();

                if (grid.Complete) who.Inventory.Say("전원이 모두 복구됐다... 건물 어딘가에서 무언가 깨어났다");
                else if (index == 0) who.Inventory.Say("차단기를 올렸다. 아래층 어딘가에 희미한 불이 들어온다");
                else who.Inventory.Say("차단기를 올렸다");
            }
            else
            {
                NoiseSystem.Emit(transform.position, 45f);
                if (GameAudio.Instance != null) GameAudio.Instance.PlayMetal();
                who.Inventory.Say("순서가 틀렸다! 차단기가 전부 내려갔다");
            }
        }

        void OnWrongOrder() => raised = false;

        void Update()
        {
            float target = raised ? upAngle : downAngle;
            angle = Mathf.MoveTowards(angle, target, Mathf.Abs(downAngle - upAngle) / travelTime * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
