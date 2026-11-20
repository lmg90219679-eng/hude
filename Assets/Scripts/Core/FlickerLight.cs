using UnityEngine;

namespace Escape.Core
{
    /// <summary>고장 난 형광등처럼 깜박이는 조명. 방마다 켜지는 간격이 다르다.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 0.3f;
        Light lamp;
        float seed;

        void Awake()
        {
            lamp = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * 3f + seed;
            float n = Mathf.PerlinNoise(t, seed);
            // 대부분 켜져 있다가 가끔 확 꺼지는 모양
            float on = n < 0.28f ? 0.05f : (n < 0.36f ? 0.5f : 1f);
            lamp.intensity = baseIntensity * on;
        }

        public void SetBase(float v) { baseIntensity = v; }
    }
}
