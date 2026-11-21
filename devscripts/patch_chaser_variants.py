import os
os.chdir(r"C:\Projects\Escape\Assets\Scripts\Editor")
p = "Week1SceneBuilder.cs"
s = open(p, encoding="utf-8").read()

# BuildChaser signature
s = s.replace("static ChaserAI BuildChaser(Transform player, Transform[] waypoints, Vector3 startPosition, string name)\n        {",
"static ChaserAI BuildChaser(Transform player, Transform[] waypoints, Vector3 startPosition, string name, int floor = 2)\n        {\n            SelectChaserVariant(floor);", 1)

# import setup for all variants
a = s.index("        static void SetupChaserImport()")
b = s.index("        /// <summary>\n        /// 추적자 전용 재질.")
s = s[:a] + '''        static void SetupChaserImport()
        {
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                SelectChaserVariant(f);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ChaserWalkPath) == null) continue;
                ImportChaserFbx(ChaserModelPath, null, loopClips: false);
                ImportChaserFbx(ChaserWalkPath, "Textures_Walk", loopClips: true);
                ImportChaserFbx(ChaserRunPath, null, loopClips: true);
            }
            SelectChaserVariant(TowerLayout.GroundFloor);
        }

        // 층마다 다른 추적자: 폴더, 키. 지하2 익사체, 지하1 집도의, 1층 살덩이(원래), 2층 환자, 3층 불탄 자.
        static readonly string[] ChaserDirs =
        {
            "Assets/Models/Chaser/Drowned", "Assets/Models/Chaser/Surgeon", "Assets/Models/Chaser",
            "Assets/Models/Chaser/Patient", "Assets/Models/Chaser/Burned",
        };
        static readonly float[] ChaserHeights = { 2.3f, 2.8f, 2.4f, 2.6f, 2.4f };
        static string chaserDir = "Assets/Models/Chaser";
        static string ChaserWalkPath => chaserDir + "/Chaser_Walk.fbx";
        static string ChaserRunPath => chaserDir + "/Chaser_Run.fbx";
        static string ChaserModelPath => chaserDir + "/Chaser.fbx";
        static float ChaserHeight = 2.4f;

        static void SelectChaserVariant(int floor)
        {
            floor = Mathf.Clamp(floor, 0, ChaserDirs.Length - 1);
            string dir = ChaserDirs[floor];
            if (!System.IO.File.Exists(dir + "/Chaser_Walk.fbx")) dir = "Assets/Models/Chaser";   // 모델이 아직 없으면 원래 모델
            if (dir != chaserDir) { cachedSkin = null; cachedController = null; }
            chaserDir = dir;
            ChaserHeight = ChaserHeights[floor];
        }

''' + s[b:]

s = s.replace('const string dir = "Assets/Models/Chaser/Textures_Walk";', 'string dir = chaserDir + "/Textures_Walk";')
s = s.replace('const string normalPath = dir + "/normal.png";', 'string normalPath = dir + "/normal.png";')
s = s.replace('const string matPath = "Assets/Materials/ChaserSkin.mat";', 'string matPath = "Assets/Materials/ChaserSkin_" + System.IO.Path.GetFileName(chaserDir) + ".mat";')
s = s.replace('string texDir = "Assets/Models/Chaser/" + texFolder;', 'string texDir = chaserDir + "/" + texFolder;')
s = s.replace('const string path = "Assets/Models/Chaser/ChaserController.controller";', 'string path = chaserDir + "/ChaserController.controller";')
for old in ['        const string ChaserWalkPath = "Assets/Models/Chaser/Chaser_Walk.fbx";\n',
            '        const string ChaserRunPath = "Assets/Models/Chaser/Chaser_Run.fbx";\n',
            '        const string ChaserModelPath = "Assets/Models/Chaser/Chaser.fbx";\n',
            '        const float ChaserHeight = 2.4f;\n']:
    assert old in s, old
    s = s.replace(old, "")
open(p, "w", encoding="utf-8", newline="").write(s)

p = "TowerBuilder.cs"
s = open(p, encoding="utf-8").read()
old = '$"Chaser_{TowerLayout.ShortLabel(f)}_{k}");'
assert old in s
s = s.replace(old, '$"Chaser_{TowerLayout.ShortLabel(f)}_{k}", f);')
open(p, "w", encoding="utf-8", newline="").write(s)
print("ok")
