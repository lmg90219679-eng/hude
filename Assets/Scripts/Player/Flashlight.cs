using Escape.Audio;
using Escape.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Player
{
    /// <summary>
    /// 손전등. E키로 켜고 끈다. 배터리가 닳고(켜 둔 동안만), 배터리 아이템으로 충전한다.
    /// 배터리가 다하면 예비 손전등이 있을 때 자동으로 교체한다.
    /// 어두운 건물에서는 이 빛이 유일한 조명이라, 켜고 끄는 차이가 한눈에 보인다.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] float maxBattery = 600f;      // 켜 둔 상태로 버틸 수 있는 시간(초). 한 판이 1시간이라 넉넉히
        [SerializeField] float flickerBelow = 0.15f;   // 배터리 비율이 이 아래면 깜빡임
        [SerializeField] float intensity = 9f;

        Light spot;
        Inventory inventory;
        float battery;

        public bool IsOn { get; private set; }
        public float BatteryRatio => battery / maxBattery;

        void Awake()
        {
            spot = GetComponent<Light>();
            inventory = GetComponentInParent<Inventory>();
            battery = maxBattery;
            SetOn(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (battery > 0f) { SetOn(!IsOn); GameAudio.Instance?.PlayClick(); }
                else TrySwapSpare();
            }

            if (!IsOn) return;

            battery -= Time.deltaTime;
            if (battery <= 0f)
            {
                battery = 0f;
                if (!TrySwapSpare()) SetOn(false);
                return;
            }

            spot.intensity = BatteryRatio < flickerBelow
                ? intensity * Random.Range(0.15f, 1f)
                : intensity;
        }

        bool TrySwapSpare()
        {
            if (inventory == null || !inventory.Consume(ItemType.SpareFlashlight)) return false;
            battery = maxBattery;
            SetOn(true);
            inventory.Say("예비 손전등으로 교체했다");
            return true;
        }

        public void Recharge(float seconds)
        {
            battery = Mathf.Min(maxBattery, battery + seconds);
        }

        public void FullRecharge() => battery = maxBattery;

        void SetOn(bool on)
        {
            IsOn = on;
            spot.enabled = on;
            spot.intensity = intensity;
        }
    }
}
