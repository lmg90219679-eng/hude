using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.UI
{
    /// <summary>메인 메뉴. 시작과 종료 버튼 (임시 OnGUI).</summary>
    public class MainMenu : MonoBehaviour
    {
        public const string GameSceneName = "Week1_Blockout";

        GUIStyle title, button;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.1f, 0.1f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 30 };
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, h * 0.15f, w, 120), "ESCAPE", title);

            if (GUI.Button(new Rect(w * 0.5f - 160, h * 0.50f, 320, 60), "시작", button))
                SceneManager.LoadScene(GameSceneName);

            if (GUI.Button(new Rect(w * 0.5f - 160, h * 0.50f + 80, 320, 60), "종료", button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            var hint = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
            GUI.Label(new Rect(0, h * 0.82f, w, 60),
                "W/A/S/D 이동   Shift 달리기   Ctrl 웅크리기   F 손전등   E 상호작용   Tab 인벤토리   G 연막", hint);
        }
    }
}
