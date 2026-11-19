using System;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// 소음 시스템. 플레이어 행동이 Emit으로 소리를 내면, 추적자가 구독해서 듣는다.
    /// loudness는 "들리는 반경(미터)"이다.
    /// </summary>
    public static class NoiseSystem
    {
        public const float WalkRadius = 6f;
        public const float SprintRadius = 14f;
        public const float CrouchRadius = 2f;
        public const float DoorRadius = 10f;

        public static event Action<Vector3, float> OnNoise;

        public static void Emit(Vector3 position, float loudness)
        {
            OnNoise?.Invoke(position, loudness);
        }
    }
}
