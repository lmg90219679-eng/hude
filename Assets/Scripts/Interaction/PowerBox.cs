using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>전원함. 전선을 가지고 있으면 전원을 복구해서 연결된 조명을 켠다 (전선 + 전원함).</summary>
    public class PowerBox : MonoBehaviour, IInteractable
    {
        [SerializeField] Light[] lightsToEnable;
        bool restored;

        public string GetPrompt(Interactor who)
        {
            if (restored) return null;
            return who.Inventory.Has(ItemType.Wire) ? "E: 전선을 연결한다" : "전원이 나갔다";
        }

        public void Interact(Interactor who)
        {
            if (restored) return;
            if (!who.Inventory.Consume(ItemType.Wire))
            {
                who.Inventory.Say("전선이 필요하다");
                return;
            }
            restored = true;
            foreach (var l in lightsToEnable)
                if (l != null) l.enabled = true;
            who.Inventory.Say("전원이 복구되었다");
        }
    }
}
