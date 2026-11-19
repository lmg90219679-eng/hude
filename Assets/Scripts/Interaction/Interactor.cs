using Escape.Core;
using Escape.Items;
using Escape.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Interaction
{
    public interface IInteractable
    {
        /// <summary>화면에 띄울 안내 문구. null이면 상호작용 불가.</summary>
        string GetPrompt(Interactor who);
        void Interact(Interactor who);
    }

    /// <summary>
    /// 플레이어 시선 앞의 물체와 E키로 상호작용한다. G키로 연막을 던진다.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [SerializeField] float reach = 2.5f;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerController player;

        [Header("Smoke")]
        [SerializeField] float smokeThrowDistance = 9f;
        [SerializeField] float smokeNoiseRadius = 22f;
        [SerializeField] float smokeDuration = 4f;

        public Inventory Inventory => inventory;
        public PlayerController Player => player;
        public string CurrentPrompt { get; private set; }

        IInteractable current;

        void Awake()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (player == null) player = GetComponent<PlayerController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || cameraTransform == null) return;

            if (player.InputLocked) { CurrentPrompt = null; return; }

            if (player.IsHidden)
            {
                CurrentPrompt = "E: 나오기";
                if (kb.eKey.wasPressedThisFrame) player.ExitHiding();
                return;
            }

            current = Find();
            CurrentPrompt = current?.GetPrompt(this);

            if (current != null && CurrentPrompt != null && kb.eKey.wasPressedThisFrame)
                current.Interact(this);

            if (kb.gKey.wasPressedThisFrame) ThrowSmoke();
        }

        IInteractable Find()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Collide)) return null;
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        void ThrowSmoke()
        {
            if (!inventory.Consume(ItemType.Smoke))
            {
                inventory.Say("연막이 없다");
                return;
            }

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            Vector3 target = cameraTransform.position + forward.normalized * smokeThrowDistance;
            target.y = 0f;
            inventory.Say("연막을 던졌다");
            StartCoroutine(SmokeRoutine(target));
        }

        System.Collections.IEnumerator SmokeRoutine(Vector3 point)
        {
            float t = 0f;
            while (t < smokeDuration)
            {
                NoiseSystem.Emit(point, smokeNoiseRadius);
                t += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
