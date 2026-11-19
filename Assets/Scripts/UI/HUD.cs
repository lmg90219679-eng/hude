using Escape.Core;
using Escape.Interaction;
using Escape.Items;
using Escape.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.UI
{
    /// <summary>
    /// 임시 HUD (OnGUI). 상호작용 안내, 열쇠 수, 시간, 손전등 배터리, 인벤토리/조합 창.
    /// Tab: 인벤토리 열기/닫기. 아이템 두 개를 차례로 클릭하면 조합한다.
    /// 3주차에 uGUI 또는 UI Toolkit으로 교체한다.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] Interactor interactor;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerController player;
        [SerializeField] Flashlight flashlight;

        bool inventoryOpen;
        ItemType? selected;
        GUIStyle label, big, center;

        void Awake()
        {
            if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || GameFlow.Instance == null) return;
            if (GameFlow.Instance.State != GameState.Playing) return;

            if (kb.tabKey.wasPressedThisFrame) SetInventory(!inventoryOpen);
        }

        void SetInventory(bool open)
        {
            inventoryOpen = open;
            selected = null;
            if (player != null) player.InputLocked = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.white } };
            big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            center = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            EnsureStyles();
            var flow = GameFlow.Instance;
            if (flow == null) return;

            float w = Screen.width, h = Screen.height;

            // 상단 정보
            GUI.Label(new Rect(20, 16, 400, 30), $"열쇠 {inventory.KeyCount}/{Inventory.KeysRequired}", label);
            GUI.Label(new Rect(20, 44, 400, 30), $"시간 {FormatTime(flow.Elapsed)}", label);
            if (flashlight != null)
                GUI.Label(new Rect(20, 72, 400, 30), $"손전등 {Mathf.RoundToInt(flashlight.BatteryRatio * 100)}%", label);

            // 하단 안내
            if (flow.State == GameState.Playing && !inventoryOpen && interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt))
                GUI.Label(new Rect(0, h * 0.62f, w, 40), interactor.CurrentPrompt, center);

            if (Time.time - inventory.LastMessageTime < 2.5f)
                GUI.Label(new Rect(0, h * 0.70f, w, 40), inventory.LastMessage, center);

            GUI.Label(new Rect(w - 360, h - 36, 340, 30), "Tab: 인벤토리   G: 연막   F: 손전등", new GUIStyle(label) { fontSize = 16, alignment = TextAnchor.MiddleRight });

            if (inventoryOpen && flow.State == GameState.Playing) DrawInventory(w, h);

            if (flow.TintAlpha > 0.01f)
            {
                GUI.color = new Color(0.8f, 0f, 0f, flow.TintAlpha);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (flow.FlashAlpha > 0.01f)
            {
                GUI.color = new Color(0.9f, 0.9f, 0.9f, flow.FlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (flow.State == GameState.Caught)
            {
                GUI.color = new Color(0, 0, 0, flow.FadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (flow.FadeAlpha > 0.95f)
                    GUI.Label(new Rect(0, h * 0.4f, w, 80), "잡혔다...", big);
            }
            else if (flow.State == GameState.Cleared)
            {
                GUI.color = new Color(0, 0, 0, 0.85f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, h * 0.35f, w, 80), "탈출 성공!", big);
                GUI.Label(new Rect(0, h * 0.5f, w, 40), $"클리어 시간 {FormatTime(flow.ClearTime)}   (R: 다시 시작   M: 메인 메뉴)", center);
            }
        }

        void DrawInventory(float w, float h)
        {
            var panel = new Rect(w * 0.5f - 240, h * 0.5f - 200, 480, 400);
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 20, panel.y + 12, 440, 30), "인벤토리 (두 아이템을 차례로 눌러 조합)", label);

            float y = panel.y + 56;
            foreach (var item in inventory.Owned())
            {
                string text = $"{ItemInfo.Label(item)} x{inventory.Count(item)}" + (selected == item ? "  ◀" : "");
                if (GUI.Button(new Rect(panel.x + 20, y, 440, 36), text))
                    OnItemClicked(item);
                y += 42;
            }

            if (Time.time - inventory.LastMessageTime < 2.5f)
                GUI.Label(new Rect(panel.x + 20, panel.yMax - 44, 440, 30), inventory.LastMessage, label);
        }

        void OnItemClicked(ItemType item)
        {
            if (selected == null) { selected = item; return; }
            inventory.TryCombine(selected.Value, item);
            selected = null;
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:00}:{s:00.0}";
        }
    }
}
