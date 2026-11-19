using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 탈출 문. 위치는 고정이고, 열쇠 3개를 모으기 전에는 잠겨 있다.
    /// 열쇠 3개를 가지고 열면 클리어.
    /// </summary>
    public class ExitDoor : MonoBehaviour, IInteractable
    {
        public string GetPrompt(Interactor who)
        {
            if (who.Inventory.HasAllKeys) return "E: 탈출한다";
            return $"잠겨 있다 (열쇠 {who.Inventory.KeyCount}/{Inventory.KeysRequired})";
        }

        public void Interact(Interactor who)
        {
            if (!who.Inventory.HasAllKeys)
            {
                who.Inventory.Say("열쇠 3개가 필요하다");
                return;
            }
            GameFlow.Instance?.Clear();
        }
    }
}
