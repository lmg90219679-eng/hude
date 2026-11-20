using System;
using System.Collections;
using Escape.Audio;
using Escape.Enemy;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 건물 전력(발전기) 상태. 3층 전기실의 차단기 3개를 순서대로(1 → 2 → 3) 올려야 한다.
    /// - 순서가 틀리면 차단기가 모두 내려가고 큰 소음이 난다.
    /// - 첫 번째 차단기: 지하1·2층의 조명이 미세하게 들어온다.
    /// - 마지막 차단기: 추적자 5마리가 모두 추격 상태로 바뀌어 건물 전체를 돌아다닌다.
    /// </summary>
    public class PowerGrid : MonoBehaviour
    {
        public const int LeverCount = 3;
        public static PowerGrid Instance { get; private set; }

        public int Raised { get; private set; }
        public bool Complete => Raised >= LeverCount;

        public event Action<int> LeverRaised;     // 올라간 차단기 번호 (0부터)
        public event Action WrongOrder;           // 순서가 틀려 모두 내려감
        public event Action Completed;            // 3개를 모두 올림

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>차단기를 올려 본다. 올바른 차례면 true, 순서가 틀리면 전부 내리고 false.</summary>
        public bool TryRaise(int index)
        {
            if (Complete) return false;

            if (index == Raised)
            {
                Raised++;
                LeverRaised?.Invoke(index);
                if (Complete) Completed?.Invoke();
                return true;
            }

            Raised = 0;
            WrongOrder?.Invoke();
            return false;
        }
    }
}
