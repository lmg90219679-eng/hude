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
        // 맵은 24m x 24m(방 8m). 걸으면 옆방 가까이, 달리면 한 칸 반, 웅크리면 거의 안 들린다.
        public const float WalkRadius = 5.5f;
        public const float SprintRadius = 13f;
        public const float CrouchRadius = 1.5f;
        public const float DoorRadius = 9f;

        public static event Action<Vector3, float> OnNoise;

        public static void Emit(Vector3 position, float loudness)
        {
            OnNoise?.Invoke(position, loudness);
        }
    }
}
