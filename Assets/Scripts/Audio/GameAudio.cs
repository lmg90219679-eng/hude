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
        [SerializeField] ChaserAI chaser;
        [SerializeField] float heartbeatMaxDistance = 18f;

        AudioSource ambient, heart, steps, sfx, sfxLow;
        AudioClip footstep, scream, sting;

        void Awake()
        {
            Instance = this;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (chaser == null) chaser = FindFirstObjectByType<ChaserAI>();

            ambient = NewSource("Ambient", ProceduralSounds.AmbientDrone(), loop: true, volume: 0.5f);
            heart = NewSource("Heartbeat", ProceduralSounds.Heartbeat(), loop: true, volume: 0f);
            steps = NewSource("Steps", null, loop: false, volume: 0.6f);
            sfx = NewSource("Sfx", null, loop: false, volume: 1f);
            sfxLow = NewSource("SfxLow", null, loop: false, volume: 1f);
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

        void Update()
        {
            if (player == null || chaser == null) return;

            float dist = Vector3.Distance(player.transform.position, chaser.transform.position);
            float closeness = 1f - Mathf.Clamp01(dist / heartbeatMaxDistance);

            // 추적 중이면 최소 일정 크기 이상으로 올린다
            if (chaser.Current == ChaserAI.State.Chase) closeness = Mathf.Max(closeness, 0.7f);
            else if (chaser.Current == ChaserAI.State.Search) closeness = Mathf.Max(closeness, 0.25f);

            heart.volume = Mathf.Lerp(heart.volume, closeness, 3f * Time.deltaTime);
            heart.pitch = Mathf.Lerp(0.85f, 1.6f, closeness);
        }

        void OnStep(bool crouching, bool sprinting)
        {
            float vol = crouching ? 0.15f : sprinting ? 0.9f : 0.5f;
            steps.pitch = Random.Range(0.9f, 1.1f);
            steps.PlayOneShot(footstep, vol);
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
        }
    }
}
