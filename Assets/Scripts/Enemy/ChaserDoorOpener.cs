using Escape.Interaction;
using UnityEngine;

namespace Escape.Enemy
{
    /// <summary>추적자가 문 가까이 가면 문을 연다 (잠기거나 숨겨진 문은 못 연다). 벽은 길찾기 영역이 아니라 통과하지 못한다.</summary>
    public class ChaserDoorOpener : MonoBehaviour
    {
        [SerializeField] float openRange = 2.8f;
        float next;

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.15f;
            Vector3 p = transform.position;
            float r2 = openRange * openRange;
            foreach (var d in Door.All)
            {
                if (d == null || d.IsOpen) continue;
                Vector3 dp = d.transform.position - p;
                if (Mathf.Abs(dp.y) > 1.5f) continue;
                dp.y = 0f;
                if (dp.sqrMagnitude < r2) d.TryOpenByChaser();
            }
        }
    }
}
