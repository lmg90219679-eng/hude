using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 서랍, 책상 위, 옷장 안 같은 "뒤질 수 있는 곳". 열 때 소음이 난다.
    /// ItemSpawner가 판마다 무작위로 아이템을 채운다.
    /// </summary>
    public class Container : MonoBehaviour, IInteractable
    {
        [SerializeField] float openNoiseRadius = 8f;

        public bool Searched { get; private set; }
        public ItemType? Content { get; private set; }

        public void Fill(ItemType? item)
        {
            Content = item;
            Searched = false;
        }

        public string GetPrompt(Interactor who) => Searched ? null : "E: 뒤져본다";

        public void Interact(Interactor who)
        {
            if (Searched) return;
            Searched = true;

            NoiseSystem.Emit(transform.position, openNoiseRadius);

            if (Content.HasValue)
            {
                who.Inventory.Add(Content.Value);
                Content = null;
            }
            else
            {
                who.Inventory.Say("아무것도 없다");
            }

            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = r.material.color * 0.6f;
        }
    }
}
