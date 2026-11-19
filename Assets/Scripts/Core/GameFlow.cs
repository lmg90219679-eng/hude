using System.Collections;
using Escape.Enemy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.Core
{
    /// <summary>
    /// 1주차용 임시 게임 흐름. 잡히면 잠시 후 현재 씬을 다시 불러온다.
    /// 나중에 암전, 비명, 추적자 얼굴 클로즈업 연출로 교체한다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] float restartDelay = 2f;

        void OnEnable() => ChaserAI.PlayerCaught += OnCaught;
        void OnDisable() => ChaserAI.PlayerCaught -= OnCaught;

        void OnCaught()
        {
            Debug.Log("[GameFlow] 플레이어가 잡혔다. 재시작한다.");
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return new WaitForSeconds(restartDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
