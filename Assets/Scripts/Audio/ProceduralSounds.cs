using UnityEngine;

namespace Escape.Audio
{
    /// <summary>
    /// 코드로 만드는 임시 효과음. 나중에 실제 오디오 파일로 교체한다.
    /// </summary>
    public static class ProceduralSounds
    {
        const int Rate = 44100;

        /// <summary>두근두근 하는 심장 소리 한 박자(쿵-쿵).</summary>
        public static AudioClip Heartbeat()
        {
            float len = 1.0f;
            var data = new float[(int)(Rate * len)];
            AddThump(data, 0.00f, 0.18f, 60f);
            AddThump(data, 0.28f, 0.16f, 50f);
            return Make("Heartbeat", data);
        }

        /// <summary>발소리. 짧은 노이즈 + 저음.</summary>
        public static AudioClip Footstep()
        {
            float len = 0.18f;
            var data = new float[(int)(Rate * len)];
            var rng = new System.Random(7);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 28f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float low = Mathf.Sin(2f * Mathf.PI * 90f * t);
                data[i] = (noise * 0.35f + low * 0.65f) * env * 0.8f;
            }
            return Make("Footstep", data);
        }

        /// <summary>어두운 배경 소리(저음 웅웅거림). 루프용.</summary>
        public static AudioClip AmbientDrone()
        {
            float len = 8f;
            var data = new float[(int)(Rate * len)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float a = Mathf.Sin(2f * Mathf.PI * 43f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 57.5f * t) * 0.6f;
                float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                data[i] = (a + b) * 0.12f * wobble;
            }
            return Make("AmbientDrone", data, loopSafe: true);
        }

        /// <summary>잡혔을 때의 비명. 높아지는 톤 + 노이즈.</summary>
        public static AudioClip Scream()
        {
            float len = 2.0f;
            var data = new float[(int)(Rate * len)];
            var rng = new System.Random(13);
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = Mathf.Lerp(500f, 1500f, Mathf.Clamp01(t / 0.8f)) + 40f * Mathf.Sin(2f * Mathf.PI * 18f * t);
                phase += 2f * Mathf.PI * freq / Rate;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-Mathf.Max(0f, t - 1.0f) * 2.5f);
                float tone = Mathf.Sign(Mathf.Sin(phase)) * 0.25f + Mathf.Sin(phase) * 0.35f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
                data[i] = (tone + noise) * env * 0.8f;
            }
            return Make("Scream", data);
        }

        static void AddThump(float[] data, float startSec, float lenSec, float freq)
        {
            int start = (int)(startSec * Rate);
            int count = (int)(lenSec * Rate);
            for (int i = 0; i < count && start + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 22f);
                data[start + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.9f;
            }
        }

        static AudioClip Make(string name, float[] data, bool loopSafe = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
