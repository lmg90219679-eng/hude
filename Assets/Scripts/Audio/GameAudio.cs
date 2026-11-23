using Escape.Core;
using Escape.Enemy;
using Escape.Player;
using System.Linq;
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
        AudioClip footstep, scream, sting, click, metal;
        AudioClip[] footsteps;

        /// <summary>Resources/Audio에 있는 소리 파일을 불러온다. 없으면 null (그때는 코드로 만든 소리로 대신한다).</summary>
        public static AudioClip Load(string name) => Resources.Load<AudioClip>("Audio/" + name);

        void Awake()
        {
            Instance = this;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            click = Load("Click") ?? ProceduralSounds.Click();

            ambient = NewSource("Ambient", Load("Amb_Drone") ?? ProceduralSounds.AmbientDrone(), loop: true, volume: 0.5f);
            heart = NewSource("Heartbeat", Load("Heartbeat") ?? ProceduralSounds.Heartbeat(), loop: true, volume: 0f);
            steps = NewSource("Steps", null, loop: false, volume: 0.6f);
            sfx = NewSource("Sfx", null, loop: false, volume: 1f);
            sfxLow = NewSource("SfxLow", null, loop: false, volume: 1f);
            sfxHigh = NewSource("SfxHigh", null, loop: false, volume: 1f);
            footsteps = new[] { Load("Footstep1"), Load("Footstep2"), Load("Footstep3"), Load("Footstep4") };
            if (footsteps.Any(c => c == null)) footsteps = new[] { ProceduralSounds.Footstep() };
            footstep = footsteps[0];
            scream = Load("Scream") ?? ProceduralSounds.Scream();
            sting = Load("Sting") ?? ProceduralSounds.Sting();
            growl = Load("Growl");

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

        /// <summary>소리 파일을 한 지점에서 입체음으로 한 번 재생한다 (문, 서랍, 레버 등). 파일이 없으면 아무것도 안 한다.</summary>
        public void PlayAt(string clipName, Vector3 position, float volume = 1f, float maxDistance = 28f)
        {
            var clip = Load(clipName);
            if (clip == null) return;
            var go = new GameObject("Sfx_" + clipName);
            go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            s.pitch = Random.Range(0.94f, 1.06f);
            s.Play();
            Destroy(go, clip.length / Mathf.Max(0.5f, s.pitch) + 0.2f);
        }

        /// <summary>플레이어 자신이 내는 소리(줍기, 인벤토리)를 한 번 재생한다.</summary>
        public void PlayUi(string clipName, float volume = 1f)
        {
            var clip = Load(clipName);
            if (clip != null) sfx.PlayOneShot(clip, volume);
        }

        AudioClip growl;
        float nextGrowl;
        readonly System.Collections.Generic.Dictionary<ChaserAI, ChaserAI.State> lastStates = new System.Collections.Generic.Dictionary<ChaserAI, ChaserAI.State>();

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
                // 추적자가 추격을 시작하면 으르렁거린다 (가까울 때만, 너무 자주는 아니게)
                if (lastStates.TryGetValue(c, out var prev) && prev != ChaserAI.State.Chase && c.Current == ChaserAI.State.Chase
                    && growl != null && dist < 32f && Time.time >= nextGrowl)
                {
                    PlayAt("Growl", c.transform.position, 1f, 36f);
                    nextGrowl = Time.time + 6f;
                }
                lastStates[c] = c.Current;
                if (c.Current == ChaserAI.State.Chase) k = Mathf.Max(k, 0.7f);
                else if (c.Current == ChaserAI.State.Search) k = Mathf.Max(k, 0.25f);
                closeness = Mathf.Max(closeness, k);
            }

            heart.volume = Mathf.Lerp(heart.volume, closeness, 3f * Time.deltaTime);
            heart.pitch = Mathf.Lerp(0.85f, 1.6f, closeness);
        }

        // ---------- 엘리베이터 ----------

        AudioClip ding, hum, whisper;
        AudioSource humSource, whisperSource;
        Coroutine dreadRoutine;

        public void PlayDing()
        {
            if (ding == null) ding = Load("Ding") ?? ProceduralSounds.Ding();
            sfx.PlayOneShot(ding, 0.9f);
        }

        /// <summary>엘리베이터가 움직이는 동안 낮은 윙윙 소리를 켜고 끈다.</summary>
        public void SetElevatorHum(bool on)
        {
            if (humSource == null)
            {
                hum = Load("ElevatorHum") ?? ProceduralSounds.ElevatorHum();
                humSource = NewSource("ElevatorHum", hum, loop: true, volume: 0f);
            }
            if (on && !humSource.isPlaying) humSource.Play();
            humSource.volume = on ? 0.5f : 0f;
            if (!on) humSource.Stop();
        }

        /// <summary>
        /// 엘리베이터 안에서 뒤쪽 소리가 점점 가까워진다: 속삭임이 커지고 발소리가 빨라지며 커진다.
        /// seconds 동안 0에서 1까지 다가온다.
        /// </summary>
        public void StartDread(float seconds)
        {
            StopDread();
            dreadRoutine = StartCoroutine(DreadRoutine(seconds));
        }

        public void StopDread()
        {
            if (dreadRoutine != null) StopCoroutine(dreadRoutine);
            dreadRoutine = null;
            if (whisperSource != null) { whisperSource.Stop(); whisperSource.volume = 0f; }
        }

        System.Collections.IEnumerator DreadRoutine(float seconds)
        {
            if (whisperSource == null)
            {
                whisper = Load("Whisper") ?? ProceduralSounds.Whisper();
                whisperSource = NewSource("Whisper", whisper, loop: true, volume: 0f);
            }
            whisperSource.Play();

            float t = 0f, nextStep = 0f;
            while (t < seconds)
            {
                float k = Mathf.Clamp01(t / seconds);
                whisperSource.volume = Mathf.Lerp(0.05f, 1f, k * k);
                whisperSource.pitch = Random.Range(0.95f, 1.05f);

                if (t >= nextStep)
                {
                    steps.pitch = Random.Range(0.75f, 0.95f);
                    steps.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)], Mathf.Lerp(0.1f, 1f, k));
                    nextStep = t + Mathf.Lerp(0.9f, 0.4f, k);   // 다가올수록 빨라진다
                }
                t += Time.deltaTime;
                yield return null;
            }
        }

        public void PlayClick()
        {
            if (click != null) sfx.PlayOneShot(click, 0.7f);
        }

        AudioClip alarm;

        public void PlayAlarm()
        {
            if (alarm == null) alarm = Load("Alarm") ?? ProceduralSounds.Alarm();
            sfx.PlayOneShot(alarm, 0.9f);
        }

        public void PlayMetal()
        {
            if (metal == null) metal = Load("MetalClang") ?? ProceduralSounds.Metal();
            sfx.PlayOneShot(metal, 1f);
        }

        void OnStep(bool crouching, bool sprinting)
        {
            float vol = crouching ? 0.15f : sprinting ? 0.9f : 0.5f;
            steps.pitch = Random.Range(0.92f, 1.08f);
            steps.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)], vol);
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
