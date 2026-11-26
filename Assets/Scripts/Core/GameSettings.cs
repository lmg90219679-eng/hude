using UnityEngine;

namespace Escape.Core
{
    public enum Difficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>
    /// 메인 메뉴에서 고르는 설정(난이도, 화면)을 PlayerPrefs에 저장한다.
    /// 중간 난이도가 기본 값이고, 쉬움과 어려움은 중간에 곱하는 배율로만 다르다.
    /// </summary>
    public static class GameSettings
    {
        const string KeyDifficulty = "Escape.Difficulty";
        const string KeyFullscreen = "Escape.Fullscreen";
        const string KeyWindowWidth = "Escape.WindowWidth";

        public static readonly int[] WindowWidths = { 1280, 1600, 1920 };

        public static Difficulty Difficulty
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, (int)Difficulty.Normal), 0, 2);
            set { PlayerPrefs.SetInt(KeyDifficulty, (int)value); PlayerPrefs.Save(); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); PlayerPrefs.Save(); ApplyScreen(); }
        }

        public static int WindowWidth
        {
            get => PlayerPrefs.GetInt(KeyWindowWidth, 1600);
            set { PlayerPrefs.SetInt(KeyWindowWidth, value); PlayerPrefs.Save(); ApplyScreen(); }
        }

        public static string Label(Difficulty d) => d == Difficulty.Easy ? "쉬움" : d == Difficulty.Hard ? "어려움" : "중간";

        static float Pick(float easy, float normal, float hard)
        {
            var d = Difficulty;
            return d == Difficulty.Easy ? easy : d == Difficulty.Hard ? hard : normal;
        }

        // 추적자
        public static float ChaserSpeed => Pick(0.9f, 1f, 1.1f);         // 순찰, 조사, 추격 속도 배율
        public static float SightRange => Pick(0.85f, 1f, 1.15f);        // 시야 거리 배율
        public static float Hearing => Pick(0.8f, 1f, 1.2f);             // 소리가 들리는 거리 배율
        public static float SearchSeconds => Pick(0.7f, 1f, 1.3f);       // 놓친 뒤 수색 시간 배율
        public static float GraceSeconds => Pick(45f, 30f, 20f);         // 시작 후 추적자가 움직이지 않는 시간
        // 자원
        public static float BatteryDrain => Pick(0.8f, 1f, 1.25f);       // 손전등이 닳는 속도 배율
        public static int BatteriesPerFloor => (int)Pick(3f, 2f, 2f);    // 층마다 놓이는 배터리 수

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnLaunch() => ApplyScreen();

        public static void ApplyScreen()
        {
            if (Fullscreen)
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                int w = WindowWidth;
                Screen.SetResolution(w, Mathf.RoundToInt(w * 9f / 16f), FullScreenMode.Windowed);
            }
        }
    }
}
