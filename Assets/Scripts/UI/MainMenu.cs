using Escape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.UI
{
    /// <summary>
    /// 메인 메뉴. 게임 시작, 게임 조작, 설명, 설정(창 모드, 난이도), 종료.
    /// 1080p 기준으로 그려서 화면 크기에 맞춰 늘린다 (OnGUI).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public const string GameSceneName = "Tower";

        enum Page { Main, Controls, Guide, Settings }

        static readonly Color Ink = new Color(0.04f, 0.04f, 0.05f);
        static readonly Color Panel = new Color(0.09f, 0.09f, 0.10f, 0.96f);
        static readonly Color Line = new Color(0.26f, 0.27f, 0.29f);
        static readonly Color Blood = new Color(0.78f, 0.12f, 0.10f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.32f);
        static readonly Color Text = new Color(0.88f, 0.87f, 0.84f);
        static readonly Color Dim = new Color(0.58f, 0.59f, 0.61f);

        Page page = Page.Main;
        GUIStyle title, sub, btn, head, body, small, keyCap;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 78, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, normal = { textColor = Blood } };
            sub = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.UpperLeft, normal = { textColor = Dim } };
            btn = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(26, 0, 0, 0) };
            head = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, normal = { textColor = Gold } };
            body = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.UpperLeft, wordWrap = true, normal = { textColor = Text } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.UpperLeft, wordWrap = true, normal = { textColor = Dim } };
            keyCap = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Text } };
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Frame(Rect r, Color c, float t = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>왼쪽 메뉴 버튼 한 줄. selected면 강조한다.</summary>
        bool MenuButton(Rect r, string label, bool selected)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, selected ? new Color(0.20f, 0.07f, 0.07f) : hover ? new Color(0.16f, 0.16f, 0.18f) : new Color(0.10f, 0.10f, 0.11f));
            Fill(new Rect(r.x, r.y, 5f, r.height), selected || hover ? Blood : Line);
            btn.normal.textColor = selected ? Gold : hover ? Color.white : Text;
            GUI.Label(r, label, btn);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>설정 화면의 선택 버튼 (가로로 나열).</summary>
        bool Choice(Rect r, string label, bool on)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, on ? new Color(0.24f, 0.09f, 0.08f) : hover ? new Color(0.17f, 0.17f, 0.19f) : new Color(0.11f, 0.11f, 0.12f));
            Frame(r, on ? Gold : hover ? Dim : Line);
            keyCap.normal.textColor = on ? Gold : Text;
            GUI.Label(r, label, keyCap);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void OnGUI()
        {
            EnsureStyles();
            float k = Screen.height / 1080f;
            float W = Screen.width / k, H = 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));

            Fill(new Rect(0, 0, W, H), Ink);
            Fill(new Rect(0, 0, W, 6f), Blood);

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && page != Page.Main) { page = Page.Main; e.Use(); }

            float lx = 110f;
            GUI.Label(new Rect(lx, 100, 420, 110), "ESCAPE", title);
            GUI.Label(new Rect(lx + 6, 212, 420, 40), "폐병원에서 살아서 나가라", sub);

            // 왼쪽 메뉴
            float by = 330f; const float bw = 380f, bh = 70f, gap = 14f;
            if (MenuButton(new Rect(lx, by, bw, bh), "게임 시작", false)) SceneManager.LoadScene(GameSceneName);
            if (MenuButton(new Rect(lx, by + (bh + gap), bw, bh), "게임 조작", page == Page.Controls)) page = Page.Controls;
            if (MenuButton(new Rect(lx, by + (bh + gap) * 2, bw, bh), "설명", page == Page.Guide)) page = Page.Guide;
            if (MenuButton(new Rect(lx, by + (bh + gap) * 3, bw, bh), "설정", page == Page.Settings)) page = Page.Settings;
            if (MenuButton(new Rect(lx, by + (bh + gap) * 4, bw, bh), "종료", false)) Quit();

            GUI.Label(new Rect(lx, H - 120, 600, 40), $"난이도  {GameSettings.Label(GameSettings.Difficulty)}", new GUIStyle(sub) { normal = { textColor = Gold } });
            GUI.Label(new Rect(lx, H - 80, 700, 40), "설정에서 난이도와 화면을 바꿀 수 있다", small);

            // 오른쪽 내용
            var pr = new Rect(lx + bw + 90, 90, W - (lx + bw + 90) - 110, H - 180);
            if (page == Page.Main) DrawWelcome(pr);
            else
            {
                Fill(pr, Panel); Frame(pr, Line);
                var inner = new Rect(pr.x + 48, pr.y + 36, pr.width - 96, pr.height - 72);
                if (page == Page.Controls) DrawControls(inner);
                else if (page == Page.Guide) DrawGuide(inner);
                else DrawSettings(inner);
                GUI.Label(new Rect(pr.x + 48, pr.yMax - 50, 600, 30), "Esc  처음 화면으로", small);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void DrawWelcome(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 40, r.width, 50), "어둠 속에서 열쇠 조각 5개를 모아 정문으로", head);
            GUI.Label(new Rect(r.x, r.y + 110, r.width * 0.8f, 400), "추적자는 소리와 불빛으로 당신을 찾는다.\n들키면 숨거나 달려야 한다.\n처음이라면 왼쪽의 게임 조작과 설명을 먼저 읽어 보자.", body);
        }

        void DrawControls(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 50), "게임 조작", head);
            var rows = new[]
            {
                ("W A S D", "이동"), ("마우스", "시선"), ("Shift", "달리기 (소리가 크다)"), ("Ctrl 또는 C", "웅크리기 (조용하고 눈에 덜 띈다)"),
                ("F", "문 열기, 서랍 열기, 줍기, 숨기, 계단 타기, 숨은 곳에서 나오기"), ("E", "손전등 켜고 끄기"), ("G", "연막 던지기 (천과 라이터를 합쳐 만든다)"),
                ("Tab", "소지품 (아이템 두 개를 차례로 눌러 합친다)"), ("M", "큰 지도 (← → 로 층 넘기기)"),
            };
            float y = r.y + 78f;
            foreach (var (key, what) in rows)
            {
                var kr = new Rect(r.x, y, 210, 46);
                Fill(kr, new Color(0.14f, 0.14f, 0.16f)); Frame(kr, Line);
                GUI.Label(kr, key, keyCap);
                GUI.Label(new Rect(r.x + 240, y + 6, r.width - 240, 44), what, body);
                y += 62f;
            }
            GUI.Label(new Rect(r.x, y + 8, r.width, 60), "클리어 또는 잡힌 뒤에는 R 다시 시작, M 메인 메뉴", small);
        }

        void DrawGuide(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 50), "설명", head);
            string[] lines =
            {
                "목표   층마다 숨겨진 탈출 열쇠 조각 1개, 모두 5개를 모아 소지품에서 합친 뒤 1층 정문에서 탈출한다.",
                "건물   지하2층부터 3층까지 5개 층. 계단은 복도 끝에 있고 시작 방에서는 보이지 않는다.",
                "열쇠   잠긴 문은 전용 방 열쇠가 필요하다. 열쇠는 다른 방의 서랍에 있다. 서랍은 열 때 소리가 난다.",
                "추적자   층마다 있고 소리와 불빛으로 찾아온다. 달리기와 문, 서랍 소리가 멀리 퍼진다. 시작 후 잠깐은 움직이지 않는다.",
                "손전등   끄면 멀리서는 안 보이지만 앞도 안 보인다. 배터리가 닳으면 배터리나 예비 손전등으로 채운다.",
                "숨기   옷장, 침대 뒤, 탁자 밑, 욕조, 관 등에 들어가 숨으면 추적자가 보지 못한다. F로 나온다.",
                "잡히면   암전과 함께 처음부터 다시 시작한다.",
            };
            float y = r.y + 78f;
            foreach (var l in lines)
            {
                float h = body.CalcHeight(new GUIContent(l), r.width);
                GUI.Label(new Rect(r.x, y, r.width, h + 4), l, body);
                y += h + 18f;
            }
        }

        void DrawSettings(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 50), "설정", head);

            // 화면
            float y = r.y + 80f;
            GUI.Label(new Rect(r.x, y, 220, 40), "화면", new GUIStyle(body) { fontStyle = FontStyle.Bold });
            bool full = GameSettings.Fullscreen;
            if (Choice(new Rect(r.x + 200, y - 6, 220, 52), "전체 화면", full)) GameSettings.Fullscreen = true;
            if (Choice(new Rect(r.x + 432, y - 6, 220, 52), "창 모드", !full)) GameSettings.Fullscreen = false;
            if (!full)
            {
                y += 70f;
                GUI.Label(new Rect(r.x, y, 220, 40), "창 크기", body);
                float x = r.x + 200;
                foreach (int w in GameSettings.WindowWidths)
                {
                    if (Choice(new Rect(x, y - 6, 190, 52), $"{w} x {Mathf.RoundToInt(w * 9f / 16f)}", GameSettings.WindowWidth == w)) GameSettings.WindowWidth = w;
                    x += 202;
                }
            }

            // 난이도
            y += 110f;
            GUI.Label(new Rect(r.x, y, 220, 40), "난이도", new GUIStyle(body) { fontStyle = FontStyle.Bold });
            var cur = GameSettings.Difficulty;
            float dx = r.x + 200;
            foreach (Difficulty d in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
            {
                if (Choice(new Rect(dx, y - 6, 190, 52), GameSettings.Label(d), cur == d)) GameSettings.Difficulty = d;
                dx += 202;
            }

            string detail =
                cur == Difficulty.Easy ? "추적자가 10% 느리고 시야와 청각이 짧다. 시작 후 45초 동안 움직이지 않고, 손전등이 천천히 닳으며 층마다 배터리가 3개 있다."
                : cur == Difficulty.Hard ? "추적자가 10% 빠르고 더 멀리서 보고 듣는다. 시작 후 20초만 기다려 주고, 손전등이 25% 빨리 닳는다."
                : "기본 설정. 추적자 속도와 시야는 기준값이고, 시작 후 30초 동안 움직이지 않는다.";
            GUI.Label(new Rect(r.x, y + 68f, r.width, 130), detail, body);
            GUI.Label(new Rect(r.x, y + 150f, r.width, 60), "난이도는 다음 게임을 시작할 때부터 적용된다.", small);
        }
    }
}
