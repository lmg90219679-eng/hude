using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>엘리베이터 문 한 쌍. 두 문짝이 좌우로 미끄러져 열리고 닫힌다.</summary>
    public class ElevatorDoorPair : MonoBehaviour
    {
        [SerializeField] Transform leftLeaf;
        [SerializeField] Transform rightLeaf;
        [SerializeField] float openDistance = 1.15f;

        Vector3 leftClosed, rightClosed;
        bool initialized;
        bool wantOpen;
        float t01;             // 0 = 닫힘, 1 = 활짝
        float duration = 1.5f;

        public bool IsOpen => t01 > 0.95f;
        public bool IsClosed => t01 < 0.02f;

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (leftLeaf != null) leftClosed = leftLeaf.localPosition;
            if (rightLeaf != null) rightClosed = rightLeaf.localPosition;
        }

        public void SetOpen(bool open, float seconds)
        {
            Init();
            wantOpen = open;
            duration = Mathf.Max(0.1f, seconds);
        }

        void Update()
        {
            t01 = Mathf.MoveTowards(t01, wantOpen ? 1f : 0f, Time.deltaTime / duration);
            float e = t01 * t01 * (3f - 2f * t01);
            if (leftLeaf != null) leftLeaf.localPosition = leftClosed + Vector3.left * (openDistance * e);
            if (rightLeaf != null) rightLeaf.localPosition = rightClosed + Vector3.right * (openDistance * e);
        }
    }
}