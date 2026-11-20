using System.Collections.Generic;
using System.Linq;
using Escape.Core;
using Escape.Interaction;
using Escape.Items;
using Escape.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.UI
{
    /// <summary>
    /// 임시 HUD (OnGUI). 왼쪽 위 지도, 상호작용 안내, 탈출 열쇠 조각, 시간, 손전등, 인벤토리/조합 창.
    /// Tab: 인벤토리 열기/닫기. 아이템 두 개를 차례로 클릭하면 조합한다.
    /// 지도는 지나가 본 방만 보여 준다.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] Interactor interactor;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerController player;
        [SerializeField] Flashlight flashlight;

        const float MapSize = 174f;
        const float MapMargin = 18f;

        bool inventoryOpen;
        ItemType? selected;
        GUIStyle label, big, center, small, right;

        Door[] doors = new Door[0];
        StairPortal[] stairs = new StairPortal[0];
        float toastUntil;
        int lastFloor = -1;
        string toast = "";

        void Awake()
        {
            if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        }

        void Start()
        {
            doors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            stairs = FindObjectsByType<StairPortal>(FindObjectsSortMode.None);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || GameFlow.Instance == null) return;
            if (GameFlow.Instance.State != GameState.Playing) return;

            if (kb.tabKey.wasPressedThisFrame) SetInventory(!inventoryOpen);

            // 층이 바뀌면 층 이름을 잠깐 크게 보여 준다
            var map = MapTracker.Instance;
            if (map != null && map.CurrentFloor != lastFloor)
            {
                if (lastFloor >= 0) { toast = TowerLayout.Label(map.CurrentFloor); toastUntil = Time.time + 2.2f; }
                lastFloor = map.CurrentFloor;
            }
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
            right = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            center = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            EnsureStyles();
            var flow = GameFlow.Instance;
            if (flow == null) return;

            float w = Screen.width, h = Screen.height;

            DrawMiniMap();

            // 오른쪽 위 정보
            string keyText = inventory.HasMasterKey
                ? "탈출 열쇠 완성! 1층 정문으로"
                : $"탈출 열쇠 조각 {inventory.PieceCount}/{Inventory.PiecesRequired}";
            GUI.Label(new Rect(w - 420, 16, 400, 30), keyText, right);
            GUI.Label(new Rect(w - 420, 44, 400, 30), $"시간 {FormatTime(flow.Elapsed)}", right);
            if (PowerGrid.Instance != null && PowerGrid.Instance.Raised > 0)
                GUI.Label(new Rect(w - 420, 100, 400, 30), PowerGrid.Instance.Complete ? "발전기 복구 완료" : $"발전기 차단기 {PowerGrid.Instance.Raised}/{PowerGrid.LeverCount}", right);
            if (flashlight != null)
            {
                string state = flashlight.IsOn ? "켜짐" : "꺼짐";
                GUI.Label(new Rect(w - 420, 72, 400, 30), $"손전등 {state}  {Mathf.RoundToInt(flashlight.BatteryRatio * 100)}%", right);
            }

            // 하단 안내
            if (flow.State == GameState.Playing && !inventoryOpen && interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt))
                GUI.Label(new Rect(0, h * 0.62f, w, 40), interactor.CurrentPrompt, center);

            if (Time.time - inventory.LastMessageTime < 2.5f)
                GUI.Label(new Rect(0, h * 0.70f, w, 40), inventory.LastMessage, center);

            if (Time.time < toastUntil)
                GUI.Label(new Rect(0, h * 0.2f, w, 80), toast, big);

            GUI.Label(new Rect(w - 420, h - 36, 400, 30), "Tab: 인벤토리   G: 연막   F: 손전등", new GUIStyle(right) { fontSize = 16 });

            DrawElevator(w, h);

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

        // ---------- 지도 ----------

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>왼쪽 위 지도. 현재 층의 3x3 방 중에서 지나가 본 방만 그린다.</summary>
        void DrawMiniMap()
        {
            var map = MapTracker.Instance;
            if (map == null) return;

            int floor = map.CurrentFloor;
            float x0 = MapMargin, y0 = MapMargin + 24f;
            
            // 바탕과 테두리
            Fill(new Rect(x0 - 4, y0 - 28, MapSize + 8, MapSize + 32), new Color(0f, 0f, 0f, 0.7f));
            Fill(new Rect(x0, y0, MapSize, MapSize), new Color(0.04f, 0.04f, 0.05f, 0.9f));
            string roomName = map.CurrentRoom >= 0 ? RoomNames.Get(floor, map.CurrentRoom) : "";
            string title = TowerLayout.Label(floor) + (roomName.Length > 0 ? "  ·  " + roomName : "");
            GUI.Label(new Rect(x0, y0 - 26, MapSize + 120, 24), title, new GUIStyle(small) { fontSize = 16, alignment = TextAnchor.MiddleLeft });

            // 방 (북쪽이 위: z가 클수록 위)
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (!map.Visited(floor, room)) continue;
                Rect r = RoomRect(x0, y0, room);
                bool here = room == map.CurrentRoom;
                Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4),
                     here ? new Color(0.45f, 0.5f, 0.6f, 0.95f) : new Color(0.28f, 0.3f, 0.36f, 0.95f));
            }

            // 문: 둘 중 한 방이라도 가 봤으면 표시 (가 보지 않은 방 자체는 안 보인다)
            foreach (var d in doors)
            {
                if (d == null || d.floor != floor || d.secret) continue;
                if (!map.Visited(floor, d.roomA) && !map.Visited(floor, d.roomB)) continue;
                Vector3 dp = d.transform.position;
                Vector2 mid = new Vector2(x0 + dp.x / TowerLayout.Span * MapSize, y0 + (1f - dp.z / TowerLayout.Span) * MapSize);
                Color c = d.IsLocked ? new Color(0.9f, 0.2f, 0.2f) : (d.IsOpen ? new Color(0.5f, 0.85f, 0.5f) : new Color(0.75f, 0.75f, 0.75f));
                Fill(new Rect(mid.x - 5, mid.y - 5, 10, 10), c);
            }

            // 계단과 탈출구
            foreach (var s in stairs)
            {
                if (s == null || s.floor != floor || !map.Visited(floor, s.room)) continue;
                Rect r = RoomRect(x0, y0, s.room);
                GUI.Label(new Rect(r.x + (s.up ? 3 : r.width - 24), r.y + 2, 22, 18), s.up ? "▲" : "▼", small);
            }
            if (floor == TowerLayout.GroundFloor && map.Visited(floor, TowerLayout.ExitRoom))
            {
                Rect r = RoomRect(x0, y0, TowerLayout.ExitRoom);
                GUI.Label(new Rect(r.x, r.yMax - 20, r.width, 18), "탈출구", small);
            }

            // 플레이어 (노란 점 + 바라보는 방향 표시)
            Vector3 p = map.PlayerPosition;
            float span = TowerLayout.Span;
            float px = x0 + Mathf.Clamp01(p.x / span) * MapSize;
            float py = y0 + (1f - Mathf.Clamp01(p.z / span)) * MapSize;
            Fill(new Rect(px - 4, py - 4, 8, 8), new Color(1f, 0.9f, 0.2f));
            float yaw = map.PlayerYaw * Mathf.Deg2Rad;
            Fill(new Rect(px + Mathf.Sin(yaw) * 9f - 2, py - Mathf.Cos(yaw) * 9f - 2, 4, 4), new Color(1f, 0.9f, 0.2f));
        }

        static Rect RoomRect(float x0, float y0, int room)
        {
            Rect w = TowerLayout.RoomRect(room);   // 월드 영역 (z가 클수록 지도 위쪽)
            float k = MapSize / TowerLayout.Span;
            return new Rect(x0 + w.xMin * k, y0 + (TowerLayout.Span - w.yMax) * k, w.width * k, w.height * k);
        }

        // ---------- 엘리베이터 ----------

        void DrawElevator(float w, float h)
        {
            var ec = ElevatorController.Instance;
            if (ec == null) return;

            if (ec.State == ElevatorState.Called)
                GUI.Label(new Rect(w - 420, 128, 400, 30), $"엘리베이터 도착까지 {Mathf.CeilToInt(ec.CallSecondsLeft)}초", right);
            else if (ec.State == ElevatorState.Riding)
                GUI.Label(new Rect(w - 420, 128, 400, 30), $"이동 중... {Mathf.CeilToInt(ec.RideSecondsLeft)}초", right);

            if (Time.time - ec.MessageTime < 3.5f)
                GUI.Label(new Rect(0, h * 0.78f, w, 40), ec.Message, center);

            // 뒤를 돌아보면 안 되는 7초: 붉게 맥동하는 경고
            if (ec.State == ElevatorState.Riding && ec.NoLookSecondsLeft > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                var style = new GUIStyle(center) { fontSize = 30, normal = { textColor = new Color(1f, 0.2f, 0.2f, 0.5f + 0.5f * pulse) } };
                GUI.Label(new Rect(0, h * 0.18f, w, 50), "돌아보지 마", style);
            }

            // 층 선택: 가 본 층만 누를 수 있다
            if (ec.ShowFloorSelect)
            {
                var panel = new Rect(w * 0.5f - 330, h - 120, 660, 70);
                Fill(panel, new Color(0f, 0f, 0f, 0.7f));
                GUI.Label(new Rect(panel.x, panel.y + 2, panel.width, 26), "행선지를 고르세요 (숫자 키)", small);

                float cell = panel.width / TowerLayout.Floors;
                for (int f = 0; f < TowerLayout.Floors; f++)
                {
                    bool here = f == ec.CabinFloor;
                    bool known = MapTracker.Instance != null && MapTracker.Instance.VisitedFloor(f);
                    var c = here ? new Color(0.9f, 0.8f, 0.3f) : known ? new Color(0.85f, 0.85f, 0.9f) : new Color(0.35f, 0.35f, 0.38f);
                    var st = new GUIStyle(center) { fontSize = 20, normal = { textColor = c } };
                    GUI.Label(new Rect(panel.x + f * cell, panel.y + 28, cell, 36), $"[{f + 1}] {TowerLayout.Label(f)}", st);
                }
            }
        }

        // ---------- 인벤토리 ----------

        void DrawInventory(float w, float h)
        {
            var panel = new Rect(w * 0.5f - 260, h * 0.5f - 250, 520, 500);
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 20, panel.y + 12, 480, 30), "인벤토리 (두 아이템을 차례로 눌러 조합)", label);

            float y = panel.y + 56;
            foreach (var item in inventory.Owned())
            {
                string text = $"{ItemInfo.Label(item)} x{inventory.Count(item)}" + (selected == item ? "  ◀" : "");
                if (GUI.Button(new Rect(panel.x + 20, y, 480, 34), text))
                    OnItemClicked(item);
                y += 38;
                if (y > panel.yMax - 90) break;
            }

            // 방 열쇠는 문마다 하나라서 목록으로 보여 준다
            string keys = inventory.RoomKeyCount == 0
                ? "방 열쇠: 없음"
                : "방 열쇠: " + string.Join(", ", inventory.RoomKeyIds.OrderBy(k => k));
            GUI.Label(new Rect(panel.x + 20, panel.yMax - 84, 480, 40), keys, new GUIStyle(small) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true });

            if (Time.time - inventory.LastMessageTime < 2.5f)
                GUI.Label(new Rect(panel.x + 20, panel.yMax - 40, 480, 30), inventory.LastMessage, label);
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
