using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>옷장, 침대 밑. F키로 들어가 숨고, 숨은 동안 추적자는 플레이어를 볼 수 없다.</summary>
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform hidePoint;
        [SerializeField] Transform exitPoint;
        [SerializeField] string prompt;               // 비어 있지 않으면 이 문구를 보여 준다
        [SerializeField] float eyeHeight = -1f;      // 침대 뒤처럼 몸을 낮춰 숨는 곳은 눈높이를 낮춘다 (-1이면 그대로)

        public string GetPrompt(Interactor who) =>
            !string.IsNullOrEmpty(prompt) ? "F: " + prompt : (eyeHeight > 0f ? "F: 침대 뒤에 숨는다" : "F: 숨는다");

        public void Interact(Interactor who)
        {
            Transform hp = hidePoint != null ? hidePoint : transform;
            Vector3 exit = exitPoint != null ? exitPoint.position : transform.position + transform.forward * 1.5f;
            who.Player.EnterHiding(hp.position, exit, hp.rotation, eyeHeight);
        }
    }
}
