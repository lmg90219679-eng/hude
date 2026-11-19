using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Player
{
    /// <summary>
    /// 손전등. F키로 켜고 끈다. 배터리가 닳고, 배터리 아이템으로 충전한다 (조합 시스템에서 Recharge 호출).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] float maxBattery = 120f;      // 켜 둔 상태로 버틸 수 있는 시간(초)
        [SerializeField] float flickerBelow = 0.2f;    // 배터리 비율이 이 아래면 깜빡임
        [SerializeField] float intensity = 3f;

        Light spot;
        float battery;

        public bool IsOn { get; private set; }
        public float BatteryRatio => battery / maxBattery;

        void Awake()
        {
            spot = GetComponent<Light>();
            battery = maxBattery;
            SetOn(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame && battery > 0f)
                SetOn(!IsOn);

            if (!IsOn) return;

            battery -= Time.deltaTime;
            if (battery <= 0f)
            {
                battery = 0f;
                SetOn(false);
                return;
            }

            spot.intensity = BatteryRatio < flickerBelow
                ? intensity * Random.Range(0.2f, 1f)
                : intensity;
        }

        public void Recharge(float seconds)
        {
            battery = Mathf.Min(maxBattery, battery + seconds);
        }

        void SetOn(bool on)
        {
            IsOn = on;
            spot.enabled = on;
            spot.intensity = intensity;
        }
    }
}
