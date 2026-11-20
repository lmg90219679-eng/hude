using System;
using Escape.Audio;
using Escape.Enemy;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 차단기 효과를 묶어서 처리한다: 마지막 차단기가 올라가면 경보가 울리고 추적자들이 추격 상태로 바뀐다.
    /// </summary>
    public class PowerEffects : MonoBehaviour
    {
        void Start()
        {
            var grid = PowerGrid.Instance;
            if (grid == null) return;
            grid.Completed += OnCompleted;
        }

        void OnDestroy()
        {
            if (PowerGrid.Instance != null) PowerGrid.Instance.Completed -= OnCompleted;
        }

        void OnCompleted()
        {
            if (GameAudio.Instance != null) GameAudio.Instance.PlayAlarm();
            if (ChaserDirector.Instance != null) ChaserDirector.Instance.EnterHuntMode();
        }
    }
}
