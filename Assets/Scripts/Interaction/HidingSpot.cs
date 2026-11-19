using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>옷장, 침대 밑. E키로 들어가 숨고, 숨은 동안 추적자는 플레이어를 볼 수 없다.</summary>
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform hidePoint;
        [SerializeField] Transform exitPoint;

        public string GetPrompt(Interactor who) => "E: 숨는다";

        public void Interact(Interactor who)
        {
            Transform hp = hidePoint != null ? hidePoint : transform;
            Vector3 exit = exitPoint != null ? exitPoint.position : transform.position + transform.forward * 1.5f;
            who.Player.EnterHiding(hp.position, exit, hp.rotation);
        }
    }
}
