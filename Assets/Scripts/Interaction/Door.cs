using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 여닫는 문. 열고 닫을 때 소음이 난다.
    /// lockedByCrowbar가 켜져 있으면 쇠막대가 있어야 강제로 열 수 있다 (쇠막대 + 잠긴 문).
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform hinge;
        [SerializeField] float openAngle = 95f;
        [SerializeField] float speed = 4f;
        [SerializeField] bool lockedByCrowbar;
        [SerializeField] float noiseRadius = NoiseSystem.DoorRadius;

        bool open;
        bool unlocked;
        float angle;

        void Awake()
        {
            if (hinge == null) hinge = transform;
        }

        public string GetPrompt(Interactor who)
        {
            if (lockedByCrowbar && !unlocked)
                return who.Inventory.Has(ItemType.Crowbar) ? "E: 쇠막대로 문을 강제로 연다" : "잠겨 있다";
            return open ? "E: 문을 닫는다" : "E: 문을 연다";
        }

        public void Interact(Interactor who)
        {
            if (lockedByCrowbar && !unlocked)
            {
                if (!who.Inventory.Has(ItemType.Crowbar))
                {
                    who.Inventory.Say("문이 잠겨 있다");
                    return;
                }
                who.Inventory.Consume(ItemType.Crowbar);
                unlocked = true;
                who.Inventory.Say("문을 강제로 열었다");
                NoiseSystem.Emit(transform.position, noiseRadius * 1.5f);
            }
            else
            {
                NoiseSystem.Emit(transform.position, noiseRadius);
            }
            open = !open;
        }

        void Update()
        {
            float target = open ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, speed * 60f * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
