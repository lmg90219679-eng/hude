using System.Collections.Generic;
using Escape.Core;
using Escape.Enemy;
using Escape.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Escape.EditorTools
{
    /// <summary>
    /// 1주차 블록아웃 씬을 자동으로 만든다. 메뉴: Escape > Build Week1 Scene
    /// 3x3 방(각 8m) 박스 맵, 플레이어, 추적자(캡슐), 순찰 지점, NavMesh를 생성한다.
    /// </summary>
    public static class Week1SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Week1_Blockout.unity";
        const string MatDir = "Assets/Materials";
        const float Cell = 8f;
        const int Grid = 3;
        const float WallHeight = 3f;
        const float WallThick = 0.3f;
        const float DoorWidth = 3f;

        static Material floorMat, wallMat, chaserMat;

        [MenuItem("Escape/Build Week1 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureMaterials();
            SetupAmbient();

            var map = new GameObject("Map");
            BuildFloor(map.transform);
            BuildWalls(map.transform);

            var waypoints = BuildWaypoints();
            BuildNavMesh(map);

            var player = BuildPlayer();
            BuildChaser(player.transform, waypoints);

            new GameObject("GameFlow").AddComponent<GameFlow>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[Week1SceneBuilder] 씬 생성 완료: " + ScenePath);
        }

        // 배치 모드(unity run)에서 호출하는 진입점
        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        // ---------- 재질 ----------

        static void EnsureMaterials()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            floorMat = GetOrCreateMat("Floor", new Color(0.18f, 0.18f, 0.18f));
            wallMat = GetOrCreateMat("Wall", new Color(0.35f, 0.33f, 0.30f));
            chaserMat = GetOrCreateMat("ChaserPlaceholder", new Color(0.5f, 0.05f, 0.05f));
        }

        static Material GetOrCreateMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void SetupAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.04f;
        }

        // ---------- 맵 ----------

        static void BuildFloor(Transform parent)
        {
            float size = Cell * Grid;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent);
            floor.transform.position = new Vector3(size * 0.5f, -0.1f, size * 0.5f);
            floor.transform.localScale = new Vector3(size, 0.2f, size);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(parent);
            ceiling.transform.position = new Vector3(size * 0.5f, WallHeight + 0.1f, size * 0.5f);
            ceiling.transform.localScale = new Vector3(size, 0.2f, size);
            ceiling.GetComponent<Renderer>().sharedMaterial = wallMat;
        }

        // 일부 문은 막아서 방 구조를 만든다. (x방향 경계 k, 행 j) / (z방향 경계 k, 열 i)
        static readonly HashSet<(char axis, int line, int index)> ClosedDoors = new HashSet<(char, int, int)>
        {
            ('x', 2, 1), // 오른쪽 가운데 방 벽
            ('z', 2, 0), // 아래쪽 왼쪽 방 위 벽
        };

        static void BuildWalls(Transform parent)
        {
            // x = line * Cell 에 있는 세로 벽 (z 방향으로 뻗음), index = 행(j)
            for (int line = 0; line <= Grid; line++)
            {
                for (int j = 0; j < Grid; j++)
                {
                    bool outer = line == 0 || line == Grid;
                    bool door = !outer && !ClosedDoors.Contains(('x', line, j));
                    Vector3 center = new Vector3(line * Cell, 0f, j * Cell + Cell * 0.5f);
                    WallSegment(parent, center, along: Vector3.forward, door);
                }
            }
            // z = line * Cell 에 있는 가로 벽 (x 방향으로 뻗음), index = 열(i)
            for (int line = 0; line <= Grid; line++)
            {
                for (int i = 0; i < Grid; i++)
                {
                    bool outer = line == 0 || line == Grid;
                    bool door = !outer && !ClosedDoors.Contains(('z', line, i));
                    Vector3 center = new Vector3(i * Cell + Cell * 0.5f, 0f, line * Cell);
                    WallSegment(parent, center, along: Vector3.right, door);
                }
            }
        }

        // 길이 Cell짜리 벽 한 칸. door이면 가운데에 문틈을 뚫는다.
        static void WallSegment(Transform parent, Vector3 center, Vector3 along, bool door)
        {
            if (!door)
            {
                MakeWall(parent, center, along, Cell);
                return;
            }
            float side = (Cell - DoorWidth) * 0.5f;
            float offset = DoorWidth * 0.5f + side * 0.5f;
            MakeWall(parent, center - along * offset, along, side);
            MakeWall(parent, center + along * offset, along, side);
        }

        static void MakeWall(Transform parent, Vector3 center, Vector3 along, float length)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent);
            wall.transform.position = center + Vector3.up * (WallHeight * 0.5f);
            bool alongX = Mathf.Abs(along.x) > 0.5f;
            wall.transform.localScale = alongX
                ? new Vector3(length + WallThick, WallHeight, WallThick)
                : new Vector3(WallThick, WallHeight, length + WallThick);
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;
        }

        // ---------- 순찰 지점 / NavMesh ----------

        static Transform[] BuildWaypoints()
        {
            var root = new GameObject("PatrolWaypoints").transform;
            var order = new (int i, int j)[]
            {
                (2, 2), (1, 2), (1, 1), (2, 1), (2, 0), (1, 0), (0, 1), (0, 2), (1, 1),
            };
            var list = new List<Transform>();
            for (int n = 0; n < order.Length; n++)
            {
                var p = new GameObject($"WP_{n}").transform;
                p.SetParent(root);
                p.position = CellCenter(order[n].i, order[n].j);
                list.Add(p);
            }
            return list.ToArray();
        }

        static Vector3 CellCenter(int i, int j) =>
            new Vector3(i * Cell + Cell * 0.5f, 0f, j * Cell + Cell * 0.5f);

        static void BuildNavMesh(GameObject map)
        {
            var surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                string dir = "Assets/Scenes/Week1_Blockout";
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(surface.navMeshData, dir + "/NavMesh.asset");
            }
        }

        // ---------- 플레이어 / 추적자 ----------

        static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = CellCenter(0, 0) + Vector3.up * 0.1f;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var camRoot = new GameObject("CameraRoot").transform;
            camRoot.SetParent(player.transform, false);
            camRoot.localPosition = new Vector3(0f, 1.65f, 0f);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.SetParent(camRoot, false);
            var camera = cam.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cam.AddComponent<AudioListener>();

            var lightObj = new GameObject("Flashlight");
            lightObj.transform.SetParent(camRoot, false);
            var spot = lightObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 18f;
            spot.spotAngle = 55f;
            spot.intensity = 3f;
            spot.shadows = LightShadows.Soft;
            lightObj.AddComponent<Flashlight>();

            var pc = player.AddComponent<PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("cameraRoot").objectReferenceValue = camRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        static void BuildChaser(Transform player, Transform[] waypoints)
        {
            var chaser = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            chaser.name = "Chaser";
            chaser.transform.position = CellCenter(2, 2) + Vector3.up * 1.0f;
            chaser.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);
            chaser.GetComponent<Renderer>().sharedMaterial = chaserMat;
            Object.DestroyImmediate(chaser.GetComponent<Collider>());

            var agent = chaser.AddComponent<NavMeshAgent>();
            agent.height = 2f;
            agent.radius = 0.4f;
            agent.acceleration = 12f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.3f;

            var eye = new GameObject("Eye").transform;
            eye.SetParent(chaser.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0.3f);

            var ai = chaser.AddComponent<ChaserAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("eye").objectReferenceValue = eye;
            so.ApplyModifiedPropertiesWithoutUndo();
            ai.Configure(player, waypoints);
            EditorUtility.SetDirty(ai);
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
