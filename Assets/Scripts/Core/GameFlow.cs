using System.Collections;
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
    /// 잡히는 연출(암전, 비명, 추적자 얼굴 클로즈업)은 3주차에 이 클래스에 붙인다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [SerializeField] float restartDelay = 3f;

        PlayerController player;

        public GameState State { get; private set; } = GameState.Playing;
        public float Elapsed { get; private set; }
        public float ClearTime { get; private set; }

        void Awake()
        {
            Instance = this;
            player = FindFirstObjectByType<PlayerController>();
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
            if (State == GameState.Cleared && kb != null && kb.rKey.wasPressedThisFrame)
                Reload();
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
            StartCoroutine(RestartAfterDelay());
        }

        void LockPlayer()
        {
            if (player != null) player.InputLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            Reload();
        }

        static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
