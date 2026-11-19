using Escape.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Player
{
    /// <summary>
    /// 1인칭 이동, 달리기, 웅크리기. 움직임에 따라 소음을 낸다.
    /// W/A/S/D 이동, Shift 달리기, Ctrl 웅크리기, 마우스 시점.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform cameraRoot;

        [Header("Speed")]
        [SerializeField] float walkSpeed = 3f;
        [SerializeField] float sprintSpeed = 5.5f;
        [SerializeField] float crouchSpeed = 1.5f;

        [Header("Look")]
        [SerializeField] float lookSensitivity = 0.1f;
        [SerializeField] float maxPitch = 85f;

        [Header("Crouch")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.0f;
        [SerializeField] float eyeOffset = 0.15f;
        [SerializeField] float crouchLerp = 12f;

        [Header("Noise")]
        [SerializeField] float footstepInterval = 0.5f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;
        float stepTimer;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving { get; private set; }

        /// <summary>인벤토리, 클리어, 게임오버 등으로 조작을 막는다.</summary>
        public bool InputLocked { get; set; }
        public bool IsHidden { get; private set; }

        Vector3 hideExitPosition;

        public void EnterHiding(Vector3 hidePosition, Vector3 exitPosition, Quaternion facing)
        {
            hideExitPosition = exitPosition;
            IsHidden = true;
            IsMoving = false;
            IsSprinting = false;
            controller.enabled = false;
            transform.SetPositionAndRotation(hidePosition, facing);
        }

        public void ExitHiding()
        {
            transform.position = hideExitPosition;
            controller.enabled = true;
            IsHidden = false;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            if (InputLocked) return;

            Look(mouse);
            if (IsHidden) return;
            UpdateCrouch(kb);
            Move(kb);
        }

        void Look(Mouse mouse)
        {
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            if (cameraRoot != null)
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateCrouch(Keyboard kb)
        {
            bool wantCrouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            if (wantCrouch) IsCrouching = true;
            else if (IsCrouching && CanStandUp()) IsCrouching = false;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchLerp * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            if (cameraRoot != null)
            {
                var pos = cameraRoot.localPosition;
                pos.y = controller.height - eyeOffset;
                cameraRoot.localPosition = pos;
            }
        }

        bool CanStandUp()
        {
            float r = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (crouchHeight - r);
            return !Physics.SphereCast(origin, r, Vector3.up, out _, standHeight - crouchHeight,
                ~0, QueryTriggerInteraction.Ignore);
        }

        void Move(Keyboard kb)
        {
            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            IsMoving = input.sqrMagnitude > 0.01f;
            IsSprinting = IsMoving && !IsCrouching && kb.leftShiftKey.isPressed;

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);

            EmitFootsteps();
        }

        void EmitFootsteps()
        {
            if (!IsMoving) { stepTimer = 0f; return; }

            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;

            float radius = IsCrouching ? NoiseSystem.CrouchRadius
                         : IsSprinting ? NoiseSystem.SprintRadius
                         : NoiseSystem.WalkRadius;
            NoiseSystem.Emit(transform.position, radius);
            stepTimer = IsSprinting ? footstepInterval * 0.7f : footstepInterval;
        }
    }
}
