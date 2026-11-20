using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 1층 정문. 층마다 하나씩 있는 탈출 열쇠 조각 5개를 합쳐 만든 탈출 열쇠가 있어야 열린다.
    /// </summary>
    public class ExitDoor : MonoBehaviour, IInteractable
    {
        public string GetPrompt(Interactor who)
        {
            if (who.Inventory.HasMasterKey) return "F: 탈출한다";
            return $"정문이 잠겨 있다 (탈출 열쇠 조각 {who.Inventory.PieceCount}/{Inventory.PiecesRequired})";
        }

        public void Interact(Interactor who)
        {
            if (!who.Inventory.HasMasterKey)
            {
                who.Inventory.Say("탈출 열쇠가 필요하다. 조각 5개를 합쳐야 한다");
                return;
            }
            GameFlow.Instance?.Clear();
        }
    }
}
