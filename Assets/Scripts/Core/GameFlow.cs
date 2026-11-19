using System.Collections;
using Escape.Audio;
using Escape.Enemy;
using Escape.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Escape.Core
{
    public enum GameState { Playing, Cleared, Caught }

    /// <summary>
    /// 게임 흐름. 플레이 시간을 재고, 클리어(열쇠 3개로 탈출)와 게임오버(잡힘)를 처리한다.
    /// 잡히면: 추적자 얼굴이 화면 앞에 확 다가오고 + 비명 + 화면 흔들림 → 암전 → 재시작.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public const string MenuSceneName = "MainMenu";

        public static GameFlow Instance { get; private set; }

        [Header("Catch sequence")]
        [SerializeField] float faceStartDistance = 1.0f;   // 컷 직후 얼굴과의 거리
        [SerializeField] float faceEndDistance = 0.4f;     // 펀치 후 얼굴과의 거리 (화면을 꽉 채운다)
        [SerializeField] float faceFov = 26f;              // 좁은 화각으로 얼굴을 크게 확대
        [SerializeField] float faceDuration = 2.4f;
        [SerializeField] float shakeAmount = 0.1f;
        [SerializeField] float punchTime = 0.07f;          // 들이미는 데 걸리는 시간 (짧을수록 갑작스럽다)
        [SerializeField] float fadeDuration = 0.6f;
        [SerializeField] float blackHold = 1.2f;

        PlayerController player;
        ChaserAI chaser;

        public GameState State { get; private set; } = GameState.Playing;
        public float Elapsed { get; private set; }
        public float ClearTime { get; private set; }

        /// <summary>0이면 투명, 1이면 완전 암전. HUD가 그린다.</summary>
        public float FadeAlpha { get; private set; }

        /// <summary>점프 스케어 순간의 섬광. 1에서 0으로 빠르게 사라진다.</summary>
        public float FlashAlpha { get; private set; }

        void Awake()
        {
            Instance = this;
            player = FindFirstObjectByType<PlayerController>();
            chaser = FindFirstObjectByType<ChaserAI>();
        }

        void OnEnable() => ChaserAI.PlayerCaught += OnCaught;
        void OnDisable() => ChaserAI.PlayerCaught -= OnCaught;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (State == GameState.Playing) Elapsed += Time.deltaTime;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (State == GameState.Cleared)
            {
                if (kb.rKey.wasPressedThisFrame) Reload();
                if (kb.mKey.wasPressedThisFrame) GoToMenu();
            }
        }

        public void Clear()
        {
            if (State != GameState.Playing) return;
            State = GameState.Cleared;
            ClearTime = Elapsed;
            LockPlayer();
        }

        void OnCaught()
        {
            if (State != GameState.Playing) return;
            State = GameState.Caught;
            LockPlayer();
            StartCoroutine(CatchSequence());
        }

        void LockPlayer()
        {
            if (player != null) player.InputLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator CatchSequence()
        {
            var cam = Camera.main;
            Transform face = chaser != null ? chaser.FaceCamPoint : null;

            if (GameAudio.Instance != null) GameAudio.Instance.PlayScream();

            if (cam != null && face != null)
            {
                // 추적자가 플레이어를 바라보게 한다 (얼굴이 정면으로 오도록)
                Vector3 look = player.transform.position - chaser.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) chaser.transform.rotation = Quaternion.LookRotation(look);

                cam.transform.SetParent(null, true);
                float baseFov = cam.fieldOfView;

                // 점프 스케어: 아무 예고 없이 한 프레임에 얼굴 코앞으로 컷. 번쩍이는 섬광 + 화면 꽉 찬 얼굴.
                FlashAlpha = 1f;

                float t = 0f;
                while (t < faceDuration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / faceDuration);

                    // 처음 0.12초에 확 들이미는 펀치, 그 뒤로는 천천히 더 다가온다
                    float punch = Mathf.Clamp01(t / punchTime);
                    float dist = Mathf.Lerp(faceStartDistance, faceEndDistance, punch * punch) - 0.12f * k;
                    cam.fieldOfView = Mathf.Lerp(baseFov, faceFov, punch) - 4f * k;

                    // 섬광은 빠르게 사라지고, 중간에 두 번 더 번쩍인다 (스트로브)
                    FlashAlpha = Mathf.MoveTowards(FlashAlpha, 0f, Time.deltaTime * 4f);
                    if ((t > 0.38f && t < 0.43f) || (t > 0.72f && t < 0.76f) || (t > 1.2f && t < 1.23f))
                        FlashAlpha = 0.75f;

                    Vector3 center = face.position;
                    Vector3 dir = chaser.transform.forward;
                    float shake = shakeAmount * (1.6f - k) * (punch < 1f ? 0.3f : 1f);
                    Vector3 pos = center + dir * dist + Random.insideUnitSphere * shake;
                    cam.transform.position = pos;
                    cam.transform.rotation = Quaternion.LookRotation((center - pos).normalized);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(faceDuration);
            }

            // 암전
            float f = 0f;
            while (f < fadeDuration)
            {
                f += Time.deltaTime;
                FadeAlpha = Mathf.Clamp01(f / fadeDuration);
                yield return null;
            }
            FadeAlpha = 1f;

            yield return new WaitForSeconds(blackHold);
            Reload();
        }

        static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        static void GoToMenu()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(MenuSceneName);
        }
    }
}
