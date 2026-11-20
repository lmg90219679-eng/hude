using Escape.Core;
using Escape.Enemy;
using Escape.Items;
using UnityEngine;

namespace Escape.Interaction
{
    /// <summary>
    /// 서랍장. F키로 열면 서랍이 스르륵 밀려 나오고(소음), 안에 물건이 있으면 눈에 보인다.
    /// 한 번 더 E를 누르면 물건을 집는다. 물건이 없으면 빈 서랍이 그대로 보인다.
    /// TowerSpawner가 판마다 무작위로 아이템과 방 열쇠를 채운다.
    ///
    /// metalDrawer(간호사실의 카드 서랍): 금속 소음이 커서, 같은 층 추적자가 20초 안에 다가온다.
    /// </summary>
    public class Container : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform drawer;        // 밀려 나오는 서랍 (로컬 +Z 방향으로 나온다)
        [SerializeField] Transform itemAnchor;    // 서랍 위, 물건이 놓이는 자리 (서랍과 함께 움직인다)
        [SerializeField] float openNoiseRadius = 8f;
        [SerializeField] float slideDistance = 0.42f;
        [SerializeField] float slideTime = 0.45f;

        [Header("금속 서랍")]
        public bool metalDrawer;
        [SerializeField] float metalNoiseRadius = 40f;
        [SerializeField] float metalArriveSeconds = 20f;

        /// <summary>어느 층, 어느 방에 있는지 (씬 빌더가 채운다).</summary>
        public int floor;
        public int room;

        Vector3 drawerClosed, anchorClosed;
        float open01;            // 0 = 닫힘, 1 = 활짝
        bool isOpen;
        GameObject itemVisual;

        public ItemDrop Content { get; private set; }
        public bool IsOpen => isOpen;

        void Awake()
        {
            if (drawer != null) drawerClosed = drawer.localPosition;
            if (itemAnchor != null) anchorClosed = itemAnchor.localPosition;
        }

        public void Fill(ItemDrop drop)
        {
            Content = drop;
            if (itemVisual != null) Destroy(itemVisual);
            itemVisual = null;

            if (drop != null && itemAnchor != null)
            {
                itemVisual = ItemVisualFactory.Create(drop, itemAnchor);
                itemVisual.SetActive(isOpen);
            }
        }

        public string GetPrompt(Interactor who)
        {
            if (!isOpen) return "F: 서랍을 연다";
            if (Content != null)
                return "F: " + (Content.Type.HasValue ? ItemInfo.Label(Content.Type.Value) : "방 열쇠") + " 줍는다";
            return null;
        }

        public void Interact(Interactor who)
        {
            if (!isOpen)
            {
                isOpen = true;
                if (itemVisual != null) itemVisual.SetActive(true);

                NoiseSystem.Emit(transform.position, openNoiseRadius);
                if (metalDrawer) RaiseMetalAlarm();

                if (Content == null) who.Inventory.Say("아무것도 없다");
                return;
            }

            if (Content == null) return;

            if (Content.Type.HasValue) who.Inventory.Add(Content.Type.Value);
            else who.Inventory.AddRoomKey(Content.KeyId);

            Content = null;
            if (itemVisual != null) Destroy(itemVisual);
            itemVisual = null;
        }

        void RaiseMetalAlarm()
        {
            // 쨍그랑: 같은 층의 모든 추적자가 소리를 듣고 20초 안에 도착한다
            NoiseSystem.Emit(transform.position, metalNoiseRadius);
            foreach (var chaser in ChaserAI.Active)
                if (chaser != null) chaser.Alert(transform.position, metalArriveSeconds);

            GameAudio_PlayMetal();
        }

        static void GameAudio_PlayMetal()
        {
            if (Escape.Audio.GameAudio.Instance != null) Escape.Audio.GameAudio.Instance.PlayMetal();
        }

        void Update()
        {
            if (drawer == null) return;

            float target = isOpen ? 1f : 0f;
            open01 = Mathf.MoveTowards(open01, target, Time.deltaTime / Mathf.Max(0.05f, slideTime));
            float eased = open01 * open01 * (3f - 2f * open01);   // 부드럽게 시작하고 멈춘다

            Vector3 offset = Vector3.forward * (slideDistance * eased);
            drawer.localPosition = drawerClosed + offset;
            if (itemAnchor != null) itemAnchor.localPosition = anchorClosed + offset;
        }
    }
}
