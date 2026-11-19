using Escape.Core;
using Escape.Enemy;
using Escape.Player;
using UnityEngine;

namespace Escape.Audio
{
    /// <summary>
    /// 게임 소리 전체. 배경 소리, 추적자가 가까울수록 빨라지고 커지는 심장 소리, 발소리, 비명.
    /// 소리가 곧 긴장감이라서, 추적자가 상태(추적/수색)일 때 심장 소리가 더 커진다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [SerializeField] PlayerController player;
        [SerializeField] float heartbeatMaxDistance = 18f;

        AudioSource ambient, heart, steps, sfx, sfxLow, sfxHigh;
        AudioClip footstep, scream, sting, click;

        void Awake()
        {
            Instance = this;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            click = ProceduralSounds.Click();

            ambient = NewSource("Ambient", ProceduralSounds.AmbientDrone(), loop: true, volume: 0.5f);
            heart = NewSource("Heartbeat", ProceduralSounds.Heartbeat(), loop: true, volume: 0f);
            steps = NewSource("Steps", null, loop: false, volume: 0.6f);
            sfx = NewSource("Sfx", null, loop: false, volume: 1f);
            sfxLow = NewSource("SfxLow", null, loop: false, volume: 1f);
            sfxHigh = NewSource("SfxHigh", null, loop: false, volume: 1f);
            footstep = ProceduralSounds.Footstep();
            scream = ProceduralSounds.Scream();
            sting = ProceduralSounds.Sting();

            ambient.Play();
            heart.Play();
        }

        void OnEnable() => PlayerController.Stepped += OnStep;
        void OnDisable() => PlayerController.Stepped -= OnStep;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource NewSource(string name, AudioClip clip, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.volume = volume;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        ChaserAI[] chasers = new ChaserAI[0];
        float nextScan;

        void Update()
        {
            if (player == null) return;

            // 추적자가 여러 명(층마다 1명)이라 가끔 목록을 갱신한다
            if (Time.time >= nextScan)
            {
                chasers = FindObjectsByType<ChaserAI>(FindObjectsSortMode.None);
                nextScan = Time.time + 1f;
            }

            // 가장 위협적인 추적자를 기준으로 심장 소리를 정한다
            float closeness = 0f;
            foreach (var c in chasers)
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                float dist = Vector3.Distance(player.transform.position, c.transform.position);
                // 다른 층이면 거리를 크게 보정 (벽과 바닥이 가로막는다)
                if (c.Floor != Escape.Core.TowerLayout.FloorOf(player.transform.position.y)) dist += 14f;
                float k = 1f - Mathf.Clamp01(dist / heartbeatMaxDistance);
                if (c.Current == ChaserAI.State.Chase) k = Mathf.Max(k, 0.7f);
                else if (c.Current == ChaserAI.State.Search) k = Mathf.Max(k, 0.25f);
                closeness = Mathf.Max(closeness, k);
            }

            heart.volume = Mathf.Lerp(heart.volume, closeness, 3f * Time.deltaTime);
            heart.pitch = Mathf.Lerp(0.85f, 1.6f, closeness);
        }

        public void PlayClick()
        {
            if (click != null) sfx.PlayOneShot(click, 0.7f);
        }

        void OnStep(bool crouching, bool sprinting)
        {
            float vol = crouching ? 0.15f : sprinting ? 0.9f : 0.5f;
            steps.pitch = Random.Range(0.9f, 1.1f);
            steps.PlayOneShot(footstep, vol);
        }

        System.Collections.IEnumerator DelayedSting(float delay)
        {
            yield return new WaitForSeconds(delay);
            sfx.PlayOneShot(sting, 1f);
        }

        public void PlayScream()
        {
            ambient.Stop();
            heart.volume = 0f;
            AudioListener.volume = 1f;
            sfx.PlayOneShot(sting, 1f);
            sfx.PlayOneShot(scream, 1f);
            // 낮은 피치의 비명을 겹쳐서 더 섬뜩하고 묵직하게
            sfxLow.pitch = 0.6f;
            sfxLow.PlayOneShot(scream, 1f);
            // 높은 피치의 비명도 겹쳐서 귀를 찌르는 소리로
            sfxHigh.pitch = 1.35f;
            sfxHigh.PlayOneShot(scream, 0.8f);
            // 충격음을 한 번 더 늦게 때려서 연달아 놀라게 한다
            StartCoroutine(DelayedSting(0.45f));
        }
    }
}
