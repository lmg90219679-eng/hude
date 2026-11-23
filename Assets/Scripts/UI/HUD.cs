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
    /// 게임 화면 UI (OnGUI, 1920x1080 기준으로 화면 크기에 맞춰 늘린다).
    /// - 왼쪽 위: 플레이어 주변을 따라가는 미니맵 (가 본 방만, 문 상태, 계단, 바라보는 방향)
    /// - 오른쪽 위: 탈출 열쇠 조각 5칸, 손전등 배터리, 시간, 발전기, 엘리베이터
    /// - 아래: 상호작용 안내(키 모양), 획득 알림(아이콘), 메시지
    /// - Tab: 인벤토리 (아이콘 칸, 설명, 조합법, 방 열쇠). 두 아이템을 차례로 눌러 조합한다.
    /// - M: 큰 지도 (층 탭, 방 이름, 문 상태, 범례). 좌우 방향키로 가 본 층을 넘겨 본다.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] Interactor interactor;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerController player;
        [SerializeField] Flashlight flashlight;

        const float RefHeight = 1080f;
        const float MiniSize = 230f;
        const float MiniSpan = 46f;       // 미니맵이 한 변에 보여 주는 거리 (m)

        bool inventoryOpen;
        bool bigMap;
        int mapFloor;
        ItemType? selected;
        float toastUntil;
        int lastFloor = -1;
        string toast = "";

        struct DoorInfo { public Door door; public bool alongX; public float width; }
        readonly List<DoorInfo> doors = new List<DoorInfo>();
        StairPortal[] stairs = new StairPortal[0];

        GUIStyle label, big, center, small, right, tiny, key, title;
        Texture2D arrowTex, circleTex;
        readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        static readonly Color Panel = new Color(0.04f, 0.045f, 0.055f, 0.9f);
        static readonly Color Border = new Color(0.55f, 0.58f, 0.65f, 0.9f);

        void Awake()
        {
            if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        }

        void Start()
        {
            foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                var panel = d.transform.Find("Leaf") ?? d.transform.Find("Hinge/Panel");
                if (panel == null) continue;
                bool alongX = panel.localScale.x > panel.localScale.z;
                doors.Add(new DoorInfo { door = d, alongX = alongX, width = alongX ? panel.localScale.x : panel.localScale.z });
            }
            stairs = FindObjectsByType<StairPortal>(FindObjectsSortMode.None);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || GameFlow.Instance == null) return;
            if (GameFlow.Instance.State != GameState.Playing) return;

            if (kb.tabKey.wasPressedThisFrame) SetInventory(!inventoryOpen);
            if (kb.mKey.wasPressedThisFrame && !inventoryOpen)
            {
                bigMap = !bigMap;
                if (bigMap && MapTracker.Instance != null) mapFloor = MapTracker.Instance.CurrentFloor;
            }
            if (bigMap)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) StepMapFloor(1);
                if (kb.leftArrowKey.wasPressedThisFrame) StepMapFloor(-1);
            }

            // 층이 바뀌면 층 이름을 잠깐 크게 보여 준다
            var map = MapTracker.Instance;
            if (map != null && map.CurrentFloor != lastFloor)
            {
                if (lastFloor >= 0) { toast = TowerLayout.Label(map.CurrentFloor); toastUntil = Time.time + 2.2f; }
                lastFloor = map.CurrentFloor;
            }
        }

        void StepMapFloor(int dir)
        {
            var map = MapTracker.Instance;
            if (map == null) return;
            for (int f = mapFloor + dir; f >= 0 && f < TowerLayout.Floors; f += dir)
                if (map.VisitedFloor(f)) { mapFloor = f; return; }
        }

        void SetInventory(bool open)
        {
            inventoryOpen = open;
            selected = null;
            if (open) bigMap = false;
            if (player != null) player.InputLocked = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        // ---------- 그리기 도구 ----------

        void EnsureResources()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            right = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            big = new GUIStyle(GUI.skin.label) { fontSize = 56, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            center = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tiny = new GUIStyle(small) { fontSize = 13 };
            key = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            title = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold };

            // 화살표(위쪽을 가리키는 삼각형)와 원
            arrowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float fy = y / 31f, fx = (x - 15.5f) / 15.5f;
                    bool inside = fy > 0.05f && Mathf.Abs(fx) < (1f - fy) * 0.95f + 0.0f && !(fy < 0.4f && Mathf.Abs(fx) < (0.4f - fy) * 1.2f);
                    px[y * 32 + x] = inside ? Color.white : new Color(1, 1, 1, 0);
                }
            arrowTex.SetPixels(px); arrowTex.Apply();
            circleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    circleTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 12f)));
                }
            circleTex.Apply();
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Frame(Rect r, Color bg, Color border, float t = 2f)
        {
            Fill(r, border);
            Fill(new Rect(r.x + t, r.y + t, r.width - t * 2f, r.height - t * 2f), bg);
        }

        static void Outline(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static void Bar(Rect r, float k, Color c)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(r.x + 2, r.y + 2, (r.width - 4f) * Mathf.Clamp01(k), r.height - 4f), c);
            Outline(r, new Color(1f, 1f, 1f, 0.35f));
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        static string IconName(ItemType t)
        {
            switch (t)
            {
                case ItemType.Wire: return "WireCoil";
                case ItemType.Battery: return "Battery";
                case ItemType.Cloth: return "ClothRag";
                case ItemType.Lighter: return "Lighter";
                case ItemType.Crowbar: return "Crowbar";
                case ItemType.Smoke: return "ClothRag";
                case ItemType.Flashlight: return "SpareFlashlight";
                case ItemType.SpareFlashlight: return "SpareFlashlight";
                case ItemType.MasterKey: return "MasterKey";
                case ItemType.SecurityCard: return "SecurityCard";
                default: return ItemInfo.IsPiece(t) ? "KeyPiece" : "RoomKey";
            }
        }

        Texture2D Icon(string name)
        {
            if (iconCache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Icons/" + name);
            iconCache[name] = t;
            return t;
        }

        static Color IconTint(ItemType t)
        {
            if (ItemInfo.IsPiece(t)) return Color.Lerp(Color.white, Color.HSVToRGB(Mathf.Clamp01(((int)t - (int)ItemType.Piece0) / 5f), 0.6f, 1f), 0.7f);
            if (t == ItemType.Smoke) return new Color(0.75f, 0.7f, 0.65f);
            return Color.white;
        }

        void DrawIcon(Rect r, string name, Color tint)
        {
            var tex = Icon(name);
            if (tex == null)
            {
                Fill(new Rect(r.center.x - r.width * 0.25f, r.center.y - r.height * 0.25f, r.width * 0.5f, r.height * 0.5f), new Color(1f, 1f, 1f, 0.3f));
                return;
            }
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        static string Describe(ItemType t)
        {
            switch (t)
            {
                case ItemType.Flashlight: return "항상 가지고 있는 손전등. E로 켜고 끈다. 끄면 추적자의 시야가 짧아진다. 배터리나 예비 손전등과 조합해 충전한다.";
                case ItemType.SpareFlashlight: return "예비 손전등. 손전등과 조합하면 배터리가 가득 찬 것으로 바뀐다.";
                case ItemType.Battery: return "배터리. 손전등과 조합하면 충전된다.";
                case ItemType.Cloth: return "낡은 천. 라이터와 조합하면 연막이 된다.";
                case ItemType.Lighter: return "라이터. 천과 조합하면 연막이 된다.";
                case ItemType.Smoke: return "연막. G로 던지면 추적자가 그쪽으로 끌려간다.";
                case ItemType.Crowbar: return "쇠막대. 잠긴 문을 열쇠 없이 강제로 열 수 있다. 소리가 아주 크다.";
                case ItemType.Wire: return "전선. 전원함을 되살리는 데 쓴다.";
                case ItemType.SecurityCard: return "직원 출입카드. 비밀 엘리베이터의 잠금을 해제한다.";
                case ItemType.MasterKey: return "탈출 열쇠. 1층 정문에서 쓴다.";
                default:
                    return ItemInfo.IsPiece(t) ? "탈출 열쇠 조각. 다섯 층의 조각을 모두 모으면 합쳐서 탈출 열쇠를 만들 수 있다. 조각 두 개를 차례로 눌러 합친다." : "";
            }
        }

        // ---------- OnGUI ----------

        void OnGUI()
        {
            EnsureResources();
            var flow = GameFlow.Instance;
            if (flow == null) return;

            float s = Screen.height / RefHeight;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = RefHeight;

            if (bigMap) DrawBigMap(w, h);
            else DrawMiniMap(26f, 62f);

            DrawStatus(w, flow);

            // 하단 안내 (키 모양)
            if (flow.State == GameState.Playing && !inventoryOpen && interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt))
                DrawPrompt(w, h, interactor.CurrentPrompt);

            if (Time.time - inventory.LastMessageTime < 2.5f && !inventoryOpen)
            {
                float a = Mathf.Clamp01((2.5f - (Time.time - inventory.LastMessageTime)) * 2f);
                var c = center.normal.textColor; center.normal.textColor = new Color(1f, 1f, 1f, a);
                Shadowed(new Rect(0, h * 0.70f, w, 40), inventory.LastMessage, center);
                center.normal.textColor = c;
            }

            DrawPickupCard(h);

            if (Time.time < toastUntil)
                Shadowed(new Rect(0, h * 0.2f, w, 90), toast, big);

            // 조작 안내 (작게)
            var hint = new GUIStyle(right) { fontSize = 15, normal = { textColor = new Color(1f, 1f, 1f, 0.45f) } };
            GUI.Label(new Rect(w - 700, h - 34, 680, 26), "Tab 소지품   M 지도   F 상호작용   E 손전등   G 연막   Shift 달리기   Ctrl 웅크리기", hint);

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
                if (flow.FadeAlpha > 0.95f) GUI.Label(new Rect(0, h * 0.4f, w, 90), "잡혔다...", big);
            }
            else if (flow.State == GameState.Cleared)
            {
                GUI.color = new Color(0, 0, 0, 0.85f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, h * 0.35f, w, 90), "탈출 성공!", big);
                GUI.Label(new Rect(0, h * 0.5f, w, 40), $"클리어 시간 {FormatTime(flow.ClearTime)}   (R: 다시 시작   M: 메인 메뉴)", center);
            }

            GUI.matrix = oldMatrix;
        }

        // ---------- 상태 표시 ----------

        void DrawStatus(float w, GameFlow flow)
        {
            float x = w - 360f, y = 24f;
            var box = new Rect(x - 14, y - 10, 346, 214);
            Frame(box, new Color(0.03f, 0.035f, 0.045f, 0.72f), new Color(0.5f, 0.52f, 0.6f, 0.55f), 1.5f);

            // 탈출 열쇠 조각 5칸 (층마다 다른 색)
            for (int f = 0; f < Inventory.PiecesRequired; f++)
            {
                var slot = new Rect(x + f * 66, y, 58, 58);
                var piece = ItemInfo.PieceForFloor(f);
                bool has = inventory.HasMasterKey || inventory.Has(piece);
                Frame(slot, new Color(0.07f, 0.08f, 0.1f, 0.9f), has ? Gold : new Color(0.35f, 0.37f, 0.42f), 2f);
                if (has) DrawIcon(new Rect(slot.x + 4, slot.y + 4, 50, 50), inventory.HasMasterKey ? "MasterKey" : "KeyPiece", inventory.HasMasterKey ? Color.white : IconTint(piece));
                else Shadowed(slot, TowerLayout.ShortLabel(f), tiny);
            }
            string keyText = inventory.HasMasterKey ? "탈출 열쇠 완성! 1층 정문으로" : $"탈출 열쇠 조각 {inventory.PieceCount}/{Inventory.PiecesRequired}";
            var rs = new GUIStyle(right) { fontSize = 18 };
            Shadowed(new Rect(x, y + 62, 330, 24), keyText, rs);

            // 손전등 배터리
            if (flashlight != null)
            {
                float k = flashlight.BatteryRatio;
                Color c = !flashlight.IsOn ? new Color(0.5f, 0.5f, 0.55f) : (k > 0.5f ? new Color(0.95f, 0.85f, 0.4f) : k > 0.2f ? new Color(0.95f, 0.6f, 0.25f) : new Color(0.95f, 0.25f, 0.2f));
                DrawIcon(new Rect(x, y + 92, 34, 34), "SpareFlashlight", flashlight.IsOn ? Color.white : new Color(0.55f, 0.55f, 0.6f));
                Bar(new Rect(x + 44, y + 100, 190, 18), k, c);
                var ls = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };
                Shadowed(new Rect(x + 244, y + 96, 90, 26), $"{Mathf.RoundToInt(k * 100)}% {(flashlight.IsOn ? "켜짐" : "꺼짐")}", ls);
            }

            Shadowed(new Rect(x, y + 128, 330, 28), $"시간  {FormatTime(flow.Elapsed)}", right);
            if (PowerGrid.Instance != null && PowerGrid.Instance.Raised > 0)
                Shadowed(new Rect(x, y + 156, 330, 28), PowerGrid.Instance.Complete ? "발전기 복구 완료" : $"발전기 차단기 {PowerGrid.Instance.Raised}/{PowerGrid.LeverCount}", right);
        }

        void DrawPrompt(float w, float h, string prompt)
        {
            // "F: 문을 연다" 형태면 키 모양으로 그린다
            string keyLabel = null, text = prompt;
            int colon = prompt.IndexOf(':');
            if (colon > 0 && colon <= 2) { keyLabel = prompt.Substring(0, colon).Trim(); text = prompt.Substring(colon + 1).Trim(); }

            var tw = center.CalcSize(new GUIContent(text)).x;
            float total = tw + (keyLabel != null ? 64f : 0f);
            float x0 = (w - total) * 0.5f, y0 = h * 0.62f;
            if (keyLabel != null)
            {
                var kr = new Rect(x0, y0 - 2, 44, 44);
                Frame(kr, new Color(0.1f, 0.11f, 0.14f, 0.95f), Gold, 2f);
                GUI.Label(kr, keyLabel, key);
                x0 += 56f;
            }
            Shadowed(new Rect(x0, y0, tw + 10, 40), text, new GUIStyle(center) { alignment = TextAnchor.MiddleLeft });
        }

        void DrawPickupCard(float h)
        {
            if (inventory.LastAddedTime < 0f) return;
            float age = Time.time - inventory.LastAddedTime;
            if (age > 3.2f || inventoryOpen) return;
            float a = Mathf.Clamp01((3.2f - age) * 2f) * Mathf.Clamp01(age * 8f);
            float slide = (1f - Mathf.Clamp01(age * 6f)) * -40f;
            var card = new Rect(30 + slide, h - 190, 330, 84);
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
            Frame(card, new Color(0.04f, 0.045f, 0.055f, 0.88f), Gold, 2f);
            string icon = inventory.LastAddedRoomKey ? "RoomKey" : IconName(inventory.LastAdded);
            Color tint = inventory.LastAddedRoomKey ? Color.white : IconTint(inventory.LastAdded);
            DrawIcon(new Rect(card.x + 8, card.y + 8, 68, 68), icon, tint);
            GUI.Label(new Rect(card.x + 86, card.y + 8, 236, 28), "획득", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Gold } });
            GUI.Label(new Rect(card.x + 86, card.y + 34, 236, 40), inventory.LastAddedRoomKey ? $"방 열쇠 ({inventory.LastAddedText})" : ItemInfo.Label(inventory.LastAdded),
                new GUIStyle(label) { fontSize = 22, wordWrap = true });
            GUI.color = old;
        }

        // ---------- 지도 ----------

        Color RoomColor(int floor, int room, bool here)
        {
            Color c;
            if (TowerLayout.IsCorridorRoom(room)) c = new Color(0.17f, 0.2f, 0.26f);
            else
            {
                string th = RoomThemes.Get(floor, room).theme ?? "";
                if (th.Contains("병실") || th.Contains("격리") || th.Contains("회복") || th.Contains("중환자") || th.Contains("분만")) c = new Color(0.2f, 0.32f, 0.27f);
                else if (th.Contains("수술") || th.Contains("마취") || th.Contains("처치") || th.Contains("주사") || th.Contains("응급")) c = new Color(0.38f, 0.21f, 0.23f);
                else if (th.Contains("영안") || th.Contains("안치") || th.Contains("부검") || th.Contains("병리") || th.Contains("냉동")) c = new Color(0.18f, 0.32f, 0.35f);
                else if (th.Contains("원장") || th.Contains("회의") || th.Contains("도서") || th.Contains("진료") || th.Contains("상담") || th.Contains("의국") || th.Contains("원무") || th.Contains("접수") || th.Contains("약국")) c = new Color(0.37f, 0.33f, 0.21f);
                else if (th.Contains("보일러") || th.Contains("기계") || th.Contains("배전") || th.Contains("정비") || th.Contains("환기") || th.Contains("세탁") || th.Contains("린넨") || th.Contains("폐기")) c = new Color(0.33f, 0.28f, 0.22f);
                else c = new Color(0.26f, 0.28f, 0.34f);
            }
            if (here) c = Color.Lerp(c, Color.white, 0.28f);
            return new Color(c.r, c.g, c.b, 0.96f);
        }

        /// <summary>월드 좌표를 지도 화면 좌표로 바꾸는 값을 가지고 방, 문, 계단, 플레이어를 그린다.</summary>
        void DrawFloorMap(int floor, System.Func<float, float, Vector2> toScreen, float k, bool large, Rect clip)
        {
            var map = MapTracker.Instance;
            if (map == null) return;

            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (!map.Visited(floor, room)) continue;
                Rect wr = TowerLayout.RoomRect(room);
                Vector2 tl = toScreen(wr.xMin, wr.yMax), br = toScreen(wr.xMax, wr.yMin);
                var r = new Rect(tl.x, tl.y, br.x - tl.x, br.y - tl.y);
                if (!r.Overlaps(clip)) continue;
                bool here = floor == map.CurrentFloor && room == map.CurrentRoom;
                Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), RoomColor(floor, room, here));
                Outline(r, new Color(0.62f, 0.66f, 0.74f, 0.85f), large ? 2f : 1.5f);
                if (large && r.width > 110f && r.height > 36f)
                    GUI.Label(r, RoomNames.Get(floor, room), new GUIStyle(small) { fontSize = 17, wordWrap = true, normal = { textColor = new Color(1f, 1f, 1f, 0.88f) } });
            }

            // 문: 벽 위의 짧은 막대. 잠김 빨강, 열림 초록, 닫힘 회색
            foreach (var di in doors)
            {
                var d = di.door;
                if (d == null || d.floor != floor || d.secret) continue;
                if (!map.Visited(floor, d.roomA) && !map.Visited(floor, d.roomB)) continue;
                Vector3 dp = d.transform.position;
                Vector2 c = toScreen(dp.x, dp.z);
                float len = Mathf.Max(6f, di.width * k * 0.9f), th = large ? 6f : 4f;
                var dr = di.alongX ? new Rect(c.x - len * 0.5f, c.y - th * 0.5f, len, th) : new Rect(c.x - th * 0.5f, c.y - len * 0.5f, th, len);
                if (!dr.Overlaps(clip)) continue;
                Color col = d.IsLocked ? new Color(0.95f, 0.22f, 0.2f) : (d.IsOpen ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.88f, 0.88f, 0.92f));
                Fill(dr, col);
            }

            // 계단과 정문
            foreach (var st in stairs)
            {
                if (st == null || st.floor != floor || !map.Visited(floor, st.room)) continue;
                Rect wr = TowerLayout.RoomRect(st.room);
                Vector2 tl = toScreen(wr.xMin, wr.yMax);
                var gs = new GUIStyle(small) { fontSize = large ? 22 : 15, normal = { textColor = Gold } };
                GUI.Label(new Rect(tl.x + (st.up ? 2 : wr.width * k - (large ? 30 : 20)), tl.y + 2, large ? 28 : 20, large ? 26 : 18), st.up ? "▲" : "▼", gs);
            }
            if (floor == TowerLayout.GroundFloor && map.Visited(floor, TowerLayout.ExitRoom))
            {
                Rect wr = TowerLayout.RoomRect(TowerLayout.ExitRoom);
                Vector2 bl = toScreen(wr.xMin, wr.yMin);
                GUI.Label(new Rect(bl.x, bl.y - (large ? 26 : 18), wr.width * k, large ? 24 : 16), "탈출구", new GUIStyle(small) { fontSize = large ? 17 : 12, normal = { textColor = new Color(0.5f, 1f, 0.6f) } });
            }

            // 플레이어: 미니맵은 동그란 점, 큰 지도는 바라보는 방향으로 돌린 화살표
            if (floor == map.CurrentFloor)
            {
                Vector3 p = map.PlayerPosition;
                Vector2 c = toScreen(p.x, p.z);
                var old = GUI.color;
                {
                    float dotSize = large ? 18f : 13f;
                    GUI.color = new Color(0f, 0f, 0f, 0.85f);
                    GUI.DrawTexture(new Rect(c.x - dotSize * 0.5f - 2f, c.y - dotSize * 0.5f - 2f, dotSize + 4f, dotSize + 4f), circleTex);
                    GUI.color = new Color(1f, 0.9f, 0.25f);
                    GUI.DrawTexture(new Rect(c.x - dotSize * 0.5f, c.y - dotSize * 0.5f, dotSize, dotSize), circleTex);
                }
                GUI.color = old;
            }
        }

        /// <summary>왼쪽 위 미니맵. 플레이어 주변 46m를 따라가며 보여 준다 (북쪽이 위).</summary>
        void DrawMiniMap(float x0, float y0)
        {
            var map = MapTracker.Instance;
            if (map == null) return;
            int floor = map.CurrentFloor;
            var box = new Rect(x0, y0, MiniSize, MiniSize);

            Frame(new Rect(x0 - 3, y0 - 34, MiniSize + 6, MiniSize + 37), new Color(0.03f, 0.035f, 0.045f, 0.8f), new Color(0.5f, 0.52f, 0.6f, 0.65f), 1.5f);
            string roomName = map.CurrentRoom >= 0 ? RoomNames.Get(floor, map.CurrentRoom) : "";
            string head = TowerLayout.Label(floor) + (roomName.Length > 0 ? "  ·  " + roomName : "");
            Shadowed(new Rect(x0 + 4, y0 - 31, MiniSize - 8, 26), head, new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, fontSize = 17 });

            Fill(box, new Color(0.02f, 0.022f, 0.028f, 0.96f));
            float k = MiniSize / MiniSpan;
            Vector3 pp = map.PlayerPosition;
            GUI.BeginGroup(box);
            var local = new Rect(0, 0, MiniSize, MiniSize);
            Vector2 mid = new Vector2(MiniSize * 0.5f, MiniSize * 0.5f);
            DrawFloorMap(floor, (wx, wz) => new Vector2(mid.x + (wx - pp.x) * k, mid.y - (wz - pp.z) * k), k, false, local);
            GUI.EndGroup();
            Outline(box, new Color(0.62f, 0.66f, 0.74f, 0.9f), 2f);
        }

        void DrawBigMap(float w, float h)
        {
            var map = MapTracker.Instance;
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.86f));
            if (map == null) return;
            if (!map.VisitedFloor(mapFloor)) mapFloor = map.CurrentFloor;

            // 층 탭
            float tabW = 150f, tx = (w - tabW * TowerLayout.Floors) * 0.5f;
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                var tr = new Rect(tx + f * tabW, 30, tabW - 8, 42);
                bool known = map.VisitedFloor(f), cur = f == mapFloor;
                Frame(tr, cur ? new Color(0.25f, 0.27f, 0.33f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.9f), cur ? Gold : new Color(0.35f, 0.37f, 0.42f), 2f);
                var st = new GUIStyle(center) { fontSize = 22, normal = { textColor = known ? (cur ? Color.white : new Color(0.8f, 0.82f, 0.88f)) : new Color(0.4f, 0.4f, 0.44f) } };
                GUI.Label(tr, TowerLayout.Label(f) + (f == map.CurrentFloor ? " ●" : ""), st);
                if (known && !cur && GUI.Button(tr, GUIContent.none, GUIStyle.none)) mapFloor = f;
            }

            float areaW = w - 260f, areaH = h - 260f;
            float k = Mathf.Min(areaW / TowerLayout.SpanX, areaH / TowerLayout.SpanZ);
            float mw = TowerLayout.SpanX * k, mh = TowerLayout.SpanZ * k;
            float x0 = (w - mw) * 0.5f, y0 = 100f + (areaH - mh) * 0.5f;
            Fill(new Rect(x0 - 8, y0 - 8, mw + 16, mh + 16), new Color(0.02f, 0.022f, 0.028f, 0.95f));
            Outline(new Rect(x0 - 8, y0 - 8, mw + 16, mh + 16), new Color(0.5f, 0.52f, 0.6f, 0.8f), 2f);
            DrawFloorMap(mapFloor, (wx, wz) => new Vector2(x0 + wx * k, y0 + (TowerLayout.SpanZ - wz) * k), k, true, new Rect(x0 - 8, y0 - 8, mw + 16, mh + 16));

            // 범례
            float ly = h - 118f;
            string[] names = { "잠긴 문", "열린 문", "닫힌 문" };
            Color[] cols = { new Color(0.95f, 0.22f, 0.2f), new Color(0.45f, 0.9f, 0.5f), new Color(0.88f, 0.88f, 0.92f) };
            float lx = (w - 760f) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                Fill(new Rect(lx, ly + 10, 30, 6), cols[i]);
                GUI.Label(new Rect(lx + 38, ly, 110, 26), names[i], new GUIStyle(small) { alignment = TextAnchor.MiddleLeft });
                lx += 160f;
            }
            GUI.Label(new Rect(lx, ly, 120, 26), "▲▼ 계단", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Gold } });
            lx += 130f;
            var old = GUI.color; GUI.color = new Color(1f, 0.9f, 0.25f);
            GUI.DrawTexture(new Rect(lx + 3, ly + 5, 16, 16), circleTex);
            GUI.color = old;
            GUI.Label(new Rect(lx + 28, ly, 120, 26), "나", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft });

            GUI.Label(new Rect(0, h - 70, w, 36), "M 닫기      ← → 다른 층 보기      가 본 방만 표시된다", center);
        }

        // ---------- 엘리베이터 ----------

        void DrawElevator(float w, float h)
        {
            var ec = ElevatorController.Instance;
            if (ec == null) return;

            if (ec.State == ElevatorState.Called)
                Shadowed(new Rect(w - 374, 246, 346, 30), $"엘리베이터 도착까지 {Mathf.CeilToInt(ec.CallSecondsLeft)}초", right);
            else if (ec.State == ElevatorState.Riding)
                Shadowed(new Rect(w - 374, 246, 346, 30), $"이동 중... {Mathf.CeilToInt(ec.RideSecondsLeft)}초", right);

            if (Time.time - ec.MessageTime < 3.5f)
                Shadowed(new Rect(0, h * 0.78f, w, 40), ec.Message, center);

            // 뒤를 돌아보면 안 되는 7초: 붉게 맥동하는 경고
            if (ec.State == ElevatorState.Riding && ec.NoLookSecondsLeft > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                var style = new GUIStyle(center) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.2f, 0.2f, 0.5f + 0.5f * pulse) } };
                GUI.Label(new Rect(0, h * 0.18f, w, 56), "돌아보지 마", style);
            }

            // 층 선택: 가 본 층만 누를 수 있다
            if (ec.ShowFloorSelect)
            {
                var panel = new Rect(w * 0.5f - 340, h - 140, 680, 84);
                Frame(panel, new Color(0.03f, 0.035f, 0.045f, 0.88f), Border, 2f);
                GUI.Label(new Rect(panel.x, panel.y + 4, panel.width, 26), "행선지를 고르세요 (숫자 키)", small);
                float cell = panel.width / TowerLayout.Floors;
                for (int f = 0; f < TowerLayout.Floors; f++)
                {
                    bool here = f == ec.CabinFloor;
                    bool known = MapTracker.Instance != null && MapTracker.Instance.VisitedFloor(f);
                    var c = here ? Gold : known ? new Color(0.88f, 0.88f, 0.94f) : new Color(0.38f, 0.38f, 0.42f);
                    var st = new GUIStyle(center) { fontSize = 22, normal = { textColor = c } };
                    GUI.Label(new Rect(panel.x + f * cell, panel.y + 34, cell, 40), $"[{f + 1}] {TowerLayout.Label(f)}", st);
                }
            }
        }

        // ---------- 인벤토리 ----------

        void DrawInventory(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.55f));
            var panel = new Rect(w * 0.5f - 540, h * 0.5f - 300, 1080, 600);
            Frame(panel, Panel, Border, 2f);
            Shadowed(new Rect(panel.x + 28, panel.y + 18, 400, 40), "소지품", title);
            GUI.Label(new Rect(panel.x + 28, panel.y + 58, 560, 26), "아이템 두 개를 차례로 눌러 조합한다", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.55f) } });

            // 슬롯 격자 (4열)
            var owned = inventory.Owned().ToList();
            const int cols = 4;
            const float slotW = 148f, slotH = 156f, gap = 12f;
            float gx = panel.x + 28, gy = panel.y + 96;
            for (int i = 0; i < Mathf.Max(owned.Count, 12); i++)
            {
                var sr = new Rect(gx + (i % cols) * (slotW + gap), gy + (i / cols) * (slotH + gap), slotW, slotH);
                if (sr.yMax > panel.yMax - 110) break;
                if (i >= owned.Count) { Frame(sr, new Color(0.06f, 0.065f, 0.08f, 0.6f), new Color(0.22f, 0.24f, 0.28f, 0.7f), 1f); continue; }

                var item = owned[i];
                bool sel = selected == item;
                bool hover = sr.Contains(Event.current.mousePosition);
                Frame(sr, sel ? new Color(0.22f, 0.2f, 0.1f, 0.95f) : hover ? new Color(0.13f, 0.14f, 0.18f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.95f), sel ? Gold : new Color(0.4f, 0.43f, 0.5f), sel ? 3f : 1.5f);
                DrawIcon(new Rect(sr.x + 14, sr.y + 8, slotW - 28, 98), IconName(item), IconTint(item));
                GUI.Label(new Rect(sr.x + 4, sr.y + 108, slotW - 8, 44), ItemInfo.Label(item), new GUIStyle(small) { fontSize = 16, wordWrap = true });
                int count = inventory.Count(item);
                if (count > 1)
                {
                    var br = new Rect(sr.xMax - 38, sr.y + 6, 32, 26);
                    Frame(br, new Color(0.1f, 0.1f, 0.12f, 0.95f), Gold, 1.5f);
                    GUI.Label(br, "x" + count, new GUIStyle(small) { fontSize = 15 });
                }
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) { Escape.Audio.GameAudio.Instance?.PlayClick(); OnItemClicked(item); }
            }

            // 오른쪽: 설명과 조합법
            float ix = panel.x + 28 + cols * (slotW + gap) + 8, iw = panel.xMax - ix - 28;
            var infoBox = new Rect(ix, gy, iw, 360);
            Frame(infoBox, new Color(0.06f, 0.065f, 0.08f, 0.9f), new Color(0.3f, 0.32f, 0.38f), 1.5f);
            if (selected.HasValue)
            {
                GUI.Label(new Rect(infoBox.x + 14, infoBox.y + 10, iw - 28, 34), ItemInfo.Label(selected.Value), new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Gold } });
                GUI.Label(new Rect(infoBox.x + 14, infoBox.y + 50, iw - 28, 150), Describe(selected.Value), new GUIStyle(small) { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true });
                GUI.Label(new Rect(infoBox.x + 14, infoBox.y + 200, iw - 28, 28), "다른 아이템을 눌러 조합", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.7f, 0.9f, 1f) } });
            }
            else
            {
                GUI.Label(new Rect(infoBox.x + 14, infoBox.y + 10, iw - 28, 30), "조합법", new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Gold } });
                string recipes =
                    "배터리 + 손전등  →  충전\n" +
                    "예비 손전등 + 손전등  →  교체\n" +
                    "천 + 라이터  →  연막\n" +
                    "열쇠 조각 5개  →  탈출 열쇠\n\n" +
                    "쇠막대: 잠긴 문 앞에서 자동으로 쓴다\n(큰 소음)";
                GUI.Label(new Rect(infoBox.x + 14, infoBox.y + 46, iw - 28, 300), recipes, new GUIStyle(small) { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true });
            }

            // 아래: 방 열쇠
            var keyBox = new Rect(panel.x + 28, panel.yMax - 100, panel.width - 56, 66);
            Frame(keyBox, new Color(0.06f, 0.065f, 0.08f, 0.9f), new Color(0.3f, 0.32f, 0.38f), 1.5f);
            GUI.Label(new Rect(keyBox.x + 12, keyBox.y + 4, 120, 58), "방 열쇠", new GUIStyle(small) { fontSize = 18, alignment = TextAnchor.MiddleLeft, normal = { textColor = Gold } });
            var ids = inventory.RoomKeyIds.OrderBy(k => k).ToList();
            if (ids.Count == 0) GUI.Label(new Rect(keyBox.x + 110, keyBox.y + 4, 300, 58), "없음", new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.45f) } });
            float kx = keyBox.x + 110;
            foreach (var id in ids)
            {
                if (kx + 90 > keyBox.xMax) break;
                DrawIcon(new Rect(kx, keyBox.y + 6, 34, 34), "RoomKey", Color.white);
                GUI.Label(new Rect(kx - 4, keyBox.y + 40, 70, 22), id, tiny);
                kx += 74f;
            }

            if (Time.time - inventory.LastMessageTime < 2.5f)
                GUI.Label(new Rect(panel.x, panel.yMax - 30, panel.width, 28), inventory.LastMessage, new GUIStyle(small) { fontSize = 18, normal = { textColor = Gold } });
        }

        void OnItemClicked(ItemType item)
        {
            if (selected == null) { selected = item; return; }
            if (selected == item) { selected = null; return; }
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
