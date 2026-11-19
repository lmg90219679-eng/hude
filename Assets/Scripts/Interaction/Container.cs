using Escape.Core;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 서랍, 책상 위 같은 "뒤질 수 있는 곳". 열 때 소음이 난다.
    /// TowerSpawner가 판마다 무작위로 아이템과 방 열쇠를 채운다.
    /// </summary>
    public class Container : MonoBehaviour, IInteractable
    {
        [SerializeField] float openNoiseRadius = 8f;

        /// <summary>어느 층, 어느 방에 있는지 (씬 빌더가 채운다).</summary>
        public int floor;
        public int room;

        public bool Searched { get; private set; }
        public ItemDrop Content { get; private set; }

        public void Fill(ItemDrop drop)
        {
            Content = drop;
            Searched = false;
        }

        public string GetPrompt(Interactor who) => Searched ? null : "E: 뒤져본다";

        public void Interact(Interactor who)
        {
            if (Searched) return;
            Searched = true;

            NoiseSystem.Emit(transform.position, openNoiseRadius);

            if (Content != null)
            {
                if (Content.Type.HasValue) who.Inventory.Add(Content.Type.Value);
                else who.Inventory.AddRoomKey(Content.KeyId);
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
