using System;
using Escape.Audio;
using Escape.Enemy;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 지하 조명. 평소에는 꺼져 있다가 첫 번째 차단기가 올라가면 미세하게(깜빡이며) 들어온다.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class PowerLight : MonoBehaviour
    {
        [SerializeField] float targetIntensity = 0.45f;
        [SerializeField] float fadeSeconds = 4f;

        Light lamp;
        bool on;
        float timer;

        void Awake()
        {
            lamp = GetComponent<Light>();
            lamp.intensity = 0f;
            lamp.enabled = false;
        }

        void Start()
        {
            if (PowerGrid.Instance != null) PowerGrid.Instance.LeverRaised += OnLever;
        }

        void OnDestroy()
        {
            if (PowerGrid.Instance != null) PowerGrid.Instance.LeverRaised -= OnLever;
        }

        void OnLever(int index)
        {
            if (index != 0 || on) return;
            on = true;
            lamp.enabled = true;
            timer = 0f;
        }

        void Update()
        {
            if (!on) return;
            timer += Time.deltaTime;
            float k = Mathf.Clamp01(timer / fadeSeconds);
            // 불안정하게 깜빡이며 서서히 밝아진다
            float flicker = k < 1f ? UnityEngine.Random.Range(0.4f, 1f) : (UnityEngine.Random.value < 0.015f ? 0.3f : 1f);
            lamp.intensity = targetIntensity * k * flicker;
        }
    }
}
