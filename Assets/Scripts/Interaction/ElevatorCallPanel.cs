using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 승강장 벽의 호출 패널. 카드를 대서 잠금을 풀어야 호출 버튼이 작동한다.
    /// 호출하면 엘리베이터가 45초 뒤에 도착한다.
    /// </summary>
    public class ElevatorCallPanel : MonoBehaviour, IInteractable
    {
        public int floor;

        public string GetPrompt(Interactor who)
        {
            var ec = ElevatorController.Instance;
            if (ec == null) return null;

            if (!ec.Unlocked)
            {
                if (!ec.HasPower) return "엘리베이터 전원이 나갔다";
                return who.Inventory.Has(ItemType.SecurityCard)
                    ? "E: 출입카드를 대어 잠금을 해제한다"
                    : "호출 버튼이 작동하지 않는다 (잠금 상태)";
            }

            switch (ec.State)
            {
                case ElevatorState.Idle: return ec.CabinFloor == floor ? "E: 엘리베이터 문을 연다" : "E: 엘리베이터를 호출한다";
                case ElevatorState.Called: return $"엘리베이터가 오는 중... {Mathf.CeilToInt(ec.CallSecondsLeft)}초";
                default: return null;
            }
        }

        public void Interact(Interactor who)
        {
            var ec = ElevatorController.Instance;
            if (ec == null) return;

            if (!ec.Unlocked)
            {
                ec.TryUnlock(who.Inventory);
                return;
            }
            if (ec.State == ElevatorState.Idle) ec.Call(floor);
        }
    }
}