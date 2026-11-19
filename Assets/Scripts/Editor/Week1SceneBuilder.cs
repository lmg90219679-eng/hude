using System.Collections.Generic;
using Escape.Core;
using Escape.Audio;
using Escape.Enemy;
using Escape.Interaction;
using Escape.Items;
using Escape.Player;
using Escape.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        static Material floorMat, wallMat, chaserMat, woodMat, wardrobeMat, doorMat, exitMat, boxMat;

        struct DoorRequest
        {
            public Vector3 center;
            public Vector3 along;
            public DoorKind kind;
        }
        enum DoorKind { Normal, Locked, Exit }
        static readonly List<DoorRequest> doorRequests = new List<DoorRequest>();

        [MenuItem("Escape/Build Week1 Scene")]
        public static void Build()
        {
            SetupChaserImport();
            BuildMenuScene();

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

            // 문, 컨테이너 등은 NavMesh를 굽고 나서 만든다 (닫힌 문이 길을 막지 않게)
            BuildDoors(map.transform);
            var containers = BuildContainers(map.transform);
            BuildHidingSpots(map.transform);
            BuildPowerBox(map.transform);

            var spawner = new GameObject("ItemSpawner").AddComponent<ItemSpawner>();
            spawner.SetContainers(containers);
            EditorUtility.SetDirty(spawner);

            new GameObject("GameFlow").AddComponent<GameFlow>();
            new GameObject("HUD").AddComponent<HUD>();
            new GameObject("GameAudio").AddComponent<GameAudio>();
            BuildPostProcessing();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes(MenuScenePath, ScenePath);
            Debug.Log("[Week1SceneBuilder] 씬 생성 완료: " + ScenePath);
        }

        const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            new GameObject("MainMenu").AddComponent<MainMenu>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static void BuildPostProcessing()
        {
            const string path = "Assets/Settings/EscapePostFX.asset";
            System.IO.Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.55f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-35f);
            color.contrast.Override(15f);
            color.postExposure.Override(-0.2f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.35f);

            foreach (var comp in profile.components)
                AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
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
            woodMat = GetOrCreateMat("Container", new Color(0.35f, 0.22f, 0.12f));
            wardrobeMat = GetOrCreateMat("Wardrobe", new Color(0.22f, 0.15f, 0.1f));
            doorMat = GetOrCreateMat("Door", new Color(0.3f, 0.2f, 0.14f));
            exitMat = GetOrCreateMat("ExitDoor", new Color(0.1f, 0.35f, 0.15f));
            boxMat = GetOrCreateMat("PowerBox", new Color(0.3f, 0.3f, 0.35f));
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

        static void RequestDoor(Vector3 center, Vector3 along, bool locked, int parity)
        {
            // 잠긴 문(쇠막대 필요)은 항상, 일반 문은 절반만 (나머지는 뚫린 통로)
            if (locked) doorRequests.Add(new DoorRequest { center = center, along = along, kind = DoorKind.Locked });
            else if (parity % 2 == 0) doorRequests.Add(new DoorRequest { center = center, along = along, kind = DoorKind.Normal });
        }

        static void BuildWalls(Transform parent)
        {
            doorRequests.Clear();
            // x = line * Cell 에 있는 세로 벽 (z 방향으로 뻗음), index = 행(j)
            for (int line = 0; line <= Grid; line++)
            {
                for (int j = 0; j < Grid; j++)
                {
                    bool outer = line == 0 || line == Grid;
                    bool door = !outer;
                    Vector3 center = new Vector3(line * Cell, 0f, j * Cell + Cell * 0.5f);
                    WallSegment(parent, center, along: Vector3.forward, door);
                    if (door) RequestDoor(center, Vector3.forward, ClosedDoors.Contains(('x', line, j)), line + j);
                }
            }
            // z = line * Cell 에 있는 가로 벽 (x 방향으로 뻗음), index = 열(i)
            for (int line = 0; line <= Grid; line++)
            {
                for (int i = 0; i < Grid; i++)
                {
                    bool outer = line == 0 || line == Grid;
                    bool exitDoor = line == 0 && i == 2;   // 탈출 문: 남쪽 바깥벽, 오른쪽 아래 방
                    bool door = !outer || exitDoor;
                    Vector3 center = new Vector3(i * Cell + Cell * 0.5f, 0f, line * Cell);
                    WallSegment(parent, center, along: Vector3.right, door);
                    if (exitDoor) doorRequests.Add(new DoorRequest { center = center, along = Vector3.right, kind = DoorKind.Exit });
                    else if (door) RequestDoor(center, Vector3.right, ClosedDoors.Contains(('z', line, i)), line + i);
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

        // ---------- 문 / 컨테이너 / 숨는 곳 / 전원함 ----------

        static void BuildDoors(Transform parent)
        {
            foreach (var req in doorRequests)
            {
                var root = new GameObject(req.kind + "Door");
                root.transform.SetParent(parent);
                root.transform.position = req.center;

                bool alongX = Mathf.Abs(req.along.x) > 0.5f;
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(root.transform);
                hinge.position = req.center - req.along * (DoorWidth * 0.5f);

                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Panel";
                panel.transform.SetParent(hinge);
                panel.transform.position = req.center + Vector3.up * 1.3f;
                panel.transform.localScale = alongX
                    ? new Vector3(DoorWidth, 2.6f, 0.1f)
                    : new Vector3(0.1f, 2.6f, DoorWidth);
                panel.GetComponent<Renderer>().sharedMaterial = req.kind == DoorKind.Exit ? exitMat : doorMat;

                if (req.kind == DoorKind.Exit)
                {
                    root.AddComponent<ExitDoor>();
                    continue;
                }

                var door = root.AddComponent<Door>();
                var so = new SerializedObject(door);
                so.FindProperty("hinge").objectReferenceValue = hinge;
                so.FindProperty("lockedByCrowbar").boolValue = req.kind == DoorKind.Locked;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Container[] BuildContainers(Transform parent)
        {
            var offsets = new[] { new Vector2(-3f, 3.3f), new Vector2(3f, 3.3f), new Vector2(-3.3f, -3f) };
            var list = new List<Container>();
            for (int i = 0; i < Grid; i++)
            {
                for (int j = 0; j < Grid; j++)
                {
                    foreach (var o in offsets)
                    {
                        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        c.name = "Container";
                        c.transform.SetParent(parent);
                        c.transform.position = CellCenter(i, j) + new Vector3(o.x, 0.45f, o.y);
                        c.transform.localScale = new Vector3(1.0f, 0.9f, 0.6f);
                        c.GetComponent<Renderer>().sharedMaterial = woodMat;
                        list.Add(c.AddComponent<Container>());
                    }
                }
            }
            return list.ToArray();
        }

        static void BuildHidingSpots(Transform parent)
        {
            var cells = new[] { (0, 1), (1, 2), (2, 1), (1, 0), (0, 2), (2, 2) };
            foreach (var (i, j) in cells)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = "Wardrobe";
                w.transform.SetParent(parent);
                w.transform.position = CellCenter(i, j) + new Vector3(3.4f, 1.1f, -2.9f);
                w.transform.localScale = new Vector3(0.7f, 2.2f, 1.0f);
                w.GetComponent<Renderer>().sharedMaterial = wardrobeMat;

                var hide = new GameObject("HidePoint").transform;
                hide.SetParent(w.transform);
                hide.position = new Vector3(w.transform.position.x, 0.1f, w.transform.position.z);
                hide.rotation = Quaternion.Euler(0f, -90f, 0f);   // 방 안쪽(서쪽)을 본다

                var exit = new GameObject("ExitPoint").transform;
                exit.SetParent(w.transform);
                exit.position = new Vector3(w.transform.position.x - 1.4f, 0.1f, w.transform.position.z);

                var spot = w.AddComponent<HidingSpot>();
                var so = new SerializedObject(spot);
                so.FindProperty("hidePoint").objectReferenceValue = hide;
                so.FindProperty("exitPoint").objectReferenceValue = exit;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildPowerBox(Transform parent)
        {
            // 탈출 문이 있는 방(오른쪽 아래)을 밝히는 조명. 전원함에 전선을 연결하면 켜진다.
            var lightObj = new GameObject("ExitRoomLight");
            lightObj.transform.SetParent(parent);
            lightObj.transform.position = CellCenter(2, 0) + Vector3.up * 2.6f;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.enabled = false;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "PowerBox";
            box.transform.SetParent(parent);
            box.transform.position = new Vector3(Cell + 0.3f, 1.5f, Cell * 1.5f + 2.5f);
            box.transform.localScale = new Vector3(0.3f, 0.8f, 0.6f);
            box.GetComponent<Renderer>().sharedMaterial = boxMat;

            var pb = box.AddComponent<PowerBox>();
            var so = new SerializedObject(pb);
            var arr = so.FindProperty("lightsToEnable");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            cam.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

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

            var flash = lightObj.GetComponent<Flashlight>();
            var inv = player.AddComponent<Inventory>();
            var invSo = new SerializedObject(inv);
            invSo.FindProperty("flashlight").objectReferenceValue = flash;
            invSo.ApplyModifiedPropertiesWithoutUndo();

            var inter = player.AddComponent<Interactor>();
            var iSo = new SerializedObject(inter);
            iSo.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            iSo.FindProperty("inventory").objectReferenceValue = inv;
            iSo.FindProperty("player").objectReferenceValue = pc;
            iSo.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        static void BuildChaser(Transform player, Transform[] waypoints)
        {
            var chaser = new GameObject("Chaser");
            chaser.transform.position = CellCenter(2, 2) + Vector3.up * 0.05f;

            float height = AttachChaserVisual(chaser.transform);

            var agent = chaser.AddComponent<NavMeshAgent>();
            agent.height = height;
            agent.radius = 0.4f;
            agent.acceleration = 12f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.3f;

            float headY = height * 0.9f;

            var eye = new GameObject("Eye").transform;
            eye.SetParent(chaser.transform, false);
            eye.localPosition = new Vector3(0f, headY, 0.2f);

            // 잡히는 연출용: 머리 위치(카메라는 GameFlow가 이 앞에 놓는다) + 붉은 조명
            // 모델에 머리 뼈가 있으면 거기에 붙여서, 애니메이션 중 머리를 따라가게 한다
            var face = new GameObject("FaceCamPoint").transform;
            face.SetParent(chaser.transform, false);
            face.localPosition = new Vector3(0f, headY, 0.1f);
            var headBone = FindHeadBone(chaserVisualRoot);
            if (headBone != null)
            {
                face.position = headBone.position + Vector3.up * (ChaserHeight * 0.05f);
                face.SetParent(headBone, true);
                Debug.Log($"[Week1SceneBuilder] 머리 뼈 사용: {headBone.name}");
            }
            else
            {
                Debug.LogWarning("[Week1SceneBuilder] 머리 뼈를 찾지 못해 높이로 대체했다.");
            }
            // 얼굴 조명. 머리 뼈는 축 방향이 제멋대로라서, 조명은 몸통(루트) 기준으로 둔다.
            // 키: 차갑고 약한 흰빛을 위 앞쪽에서 비춰 얼굴 굴곡과 이빨을 드러낸다.
            var key = new GameObject("FaceKeyLight").AddComponent<Light>();
            key.transform.SetParent(chaser.transform, false);
            key.transform.localPosition = new Vector3(0.35f, headY + 0.45f, 1.2f);
            key.type = LightType.Point;
            key.range = 4f;
            key.intensity = 2.2f;
            key.color = new Color(0.85f, 0.92f, 1f);

            // 림: 뒤쪽 붉은 빛으로 윤곽을 살려 섬뜩하게 만든다.
            var rim = new GameObject("FaceRimLight").AddComponent<Light>();
            rim.transform.SetParent(chaser.transform, false);
            rim.transform.localPosition = new Vector3(-0.2f, headY + 0.25f, -0.8f);
            rim.type = LightType.Point;
            rim.range = 3.5f;
            rim.intensity = 3.5f;
            rim.color = new Color(1f, 0.15f, 0.1f);

            // 아래쪽 약한 채움빛: 턱과 입 안쪽을 보이게 한다.
            var fill = new GameObject("FaceFillLight").AddComponent<Light>();
            fill.transform.SetParent(chaser.transform, false);
            fill.transform.localPosition = new Vector3(0f, headY - 0.6f, 0.9f);
            fill.type = LightType.Point;
            fill.range = 3f;
            fill.intensity = 0.8f;
            fill.color = new Color(0.7f, 0.55f, 0.5f);

            var ai = chaser.AddComponent<ChaserAI>();
            var chaserAnim = chaser.AddComponent<ChaserAnimator>();
            chaserAnim.SetYawOffsets(walkYawFix, runYawFix);
            EditorUtility.SetDirty(chaserAnim);
            var so = new SerializedObject(ai);
            so.FindProperty("eye").objectReferenceValue = eye;
            so.FindProperty("faceCamPoint").objectReferenceValue = face;
            so.ApplyModifiedPropertiesWithoutUndo();
            ai.Configure(player, waypoints);
            EditorUtility.SetDirty(ai);
        }

        /// <summary>Varco에서 가져온 FBX의 임포트 설정: URP 재질로 변환, 텍스처 추출.</summary>
        static void SetupChaserImport()
        {
            // 텍스처는 걷기 FBX에서만 꺼낸다 (세 FBX가 같은 텍스처라 중복 저장을 막는다)
            ImportChaserFbx(ChaserModelPath, null, loopClips: false);
            ImportChaserFbx(ChaserWalkPath, "Textures_Walk", loopClips: true);
            ImportChaserFbx(ChaserRunPath, null, loopClips: true);
        }

        /// <summary>
        /// 추적자 전용 재질. Varco가 주는 orm 텍스처(R=차폐, G=거칠기, B=금속)는 URP Lit이 그대로 못 읽어서
        /// 반짝이거나 색이 틀어지므로 쓰지 않고, 기본색 + 노말맵만 쓰고 거칠게(무광) 만든다.
        /// </summary>
        static Material BuildChaserSkin()
        {
            const string dir = "Assets/Models/Chaser/Textures_Walk";
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/base_color.png");
            const string normalPath = dir + "/normal.png";

            var ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            const string matPath = "Assets/Materials/ChaserSkin.mat";
            System.IO.Directory.CreateDirectory(MatDir);
            AssetDatabase.DeleteAsset(matPath);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.62f));   // 과하게 밝고 붉은 색을 눌러준다
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.16f);                           // 거의 무광 (젖은 느낌만 약간)
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[Week1SceneBuilder] 추적자 재질 생성: 기본색 {(baseTex != null)}, 노말 {(normalTex != null)}");
            return mat;
        }

        static void ImportChaserFbx(string path, string texFolder, bool loopClips)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;

            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.importCameras = false;
            imp.importLights = false;

            if (loopClips)
            {
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips) { c.loopTime = true; c.loop = true; }
                imp.clipAnimations = clips;
            }
            imp.SaveAndReimport();

            if (texFolder == null) return;

            string texDir = "Assets/Models/Chaser/" + texFolder;
            System.IO.Directory.CreateDirectory(texDir);
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            imp.SaveAndReimport();
        }

        /// <summary>걷기/달리기 클립으로 Animator 컨트롤러를 만든다. 파라미터 Run(bool)로 전환.</summary>
        static RuntimeAnimatorController BuildChaserController()
        {
            var walk = FindClip(ChaserWalkPath);
            var run = FindClip(ChaserRunPath);
            if (walk == null || run == null)
            {
                Debug.LogWarning("[Week1SceneBuilder] 걷기/달리기 클립을 찾지 못했다.");
                return null;
            }

            const string path = "Assets/Models/Chaser/ChaserController.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Run", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            var runState = sm.AddState("Run");
            runState.motion = run;
            sm.defaultState = walkState;

            var toRun = walkState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;
            toRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Run");

            var toWalk = runState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.25f;
            toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Run");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Week1SceneBuilder] 추적자 애니메이터 생성: 걷기 {walk.name} ({walk.length:F2}s), 달리기 {run.name} ({run.length:F2}s)");
            return ctrl;
        }

        static AnimationClip FindClip(string fbxPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        const string ChaserWalkPath = "Assets/Models/Chaser/Chaser_Walk.fbx";
        const string ChaserRunPath = "Assets/Models/Chaser/Chaser_Run.fbx";

        const string ChaserModelPath = "Assets/Models/Chaser/Chaser.fbx";
        const float ChaserHeight = 2.4f;

        /// <summary>
        /// 추적자 외형을 붙이고 키(높이)를 돌려준다. Varco 모델이 있으면 그걸, 없으면 캡슐을 쓴다.
        /// 모델은 발이 바닥에 닿고 가로세로가 가운데에 오도록 크기와 위치를 맞춘다.
        /// </summary>
        static float AttachChaserVisual(Transform root)
        {
            // 걷기 FBX(모델 + 애니메이션)를 우선 사용하고, 없으면 T포즈 모델로 대체한다
            bool animated = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserWalkPath) != null
                         && AssetDatabase.LoadAssetAtPath<GameObject>(ChaserRunPath) != null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(animated ? ChaserWalkPath : ChaserModelPath);
            if (prefab == null)
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "Visual";
                cap.transform.SetParent(root, false);
                cap.transform.localPosition = new Vector3(0f, 1f, 0f);
                cap.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);
                cap.GetComponent<Renderer>().sharedMaterial = chaserMat;
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                return 2f;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Visual";
            chaserVisualRoot = model.transform;
            model.transform.SetParent(root, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // 전용 재질로 교체 (반짝임과 색 쏠림을 잡는다)
            var skin = BuildChaserSkin();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                r.sharedMaterials = mats;
            }

            if (animated)
            {
                walkYawFix = MeasureYawFix(prefab, FindClip(ChaserWalkPath));
                runYawFix = MeasureYawFix(prefab, FindClip(ChaserRunPath));
                Debug.Log($"[Week1SceneBuilder] 방향 보정: 걷기 {walkYawFix:F1}도, 달리기 {runYawFix:F1}도");

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildChaserController();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var b = GetBounds(model);
            float scale = b.size.y > 0.001f ? ChaserHeight / b.size.y : 1f;
            model.transform.localScale = Vector3.one * scale;

            b = GetBounds(model);
            Vector3 offset = new Vector3(root.position.x - b.center.x, root.position.y - b.min.y, root.position.z - b.center.z);
            model.transform.position += offset;

            Debug.Log($"[Week1SceneBuilder] 추적자 모델 적용: 높이 {b.size.y:F2}m, 폭 {b.size.x:F2}x{b.size.z:F2}, 배율 {scale:F3}");
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    Debug.Log($"[Week1SceneBuilder]  재질 {(m != null ? m.name : "null")} / 셰이더 {(m != null ? m.shader.name : "-")}");

            return ChaserHeight;
        }

        static Transform chaserVisualRoot;
        static float walkYawFix, runYawFix;

        /// <summary>
        /// 클립을 샘플링해서 몸이 향하는 방향을 잰다 (양쪽 다리 위치로 계산).
        /// 반환값은 이 클립을 +Z 방향으로 맞추기 위해 모델에 더할 Y 회전 각도(도).
        /// 임시 인스턴스에서 재고 지우므로 씬의 모델 자세는 바뀌지 않는다.
        /// </summary>
        static float MeasureYawFix(GameObject prefab, AnimationClip clip)
        {
            if (prefab == null || clip == null) return 0f;

            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            tmp.transform.localScale = Vector3.one;

            Transform left = null, right = null;
            foreach (var t in tmp.GetComponentsInChildren<Transform>())
            {
                if (t.name == "LeftUpLeg") left = t;
                else if (t.name == "RightUpLeg") right = t;
            }

            float fixDeg = 0f;
            if (left != null && right != null)
            {
                Vector3 sum = Vector3.zero;
                const int samples = 8;
                for (int i = 0; i < samples; i++)
                {
                    clip.SampleAnimation(tmp, clip.length * i / samples);
                    Vector3 r = right.position - left.position;
                    r.y = 0f;
                    if (r.sqrMagnitude < 1e-6f) continue;
                    sum += Vector3.Cross(r.normalized, Vector3.up);
                }
                if (sum.sqrMagnitude > 1e-6f)
                    fixDeg = -Mathf.Atan2(sum.x, sum.z) * Mathf.Rad2Deg;
            }
            else
            {
                Debug.LogWarning("[Week1SceneBuilder] 다리 뼈를 찾지 못해 방향 보정을 건너뛴다.");
            }

            Object.DestroyImmediate(tmp);
            return fixDeg;
        }

        /// <summary>이름에 head가 들어간 뼈 중 머리 본체를 찾는다 (headend, top 같은 끝 뼈는 제외).</summary>
        static Transform FindHeadBone(Transform root)
        {
            if (root == null) return null;
            Transform best = null;
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("head")) continue;
                Debug.Log($"[Week1SceneBuilder]  뼈 후보: {t.name}");
                if (n.Contains("end") || n.Contains("top") || n.Contains("neck")) continue;
                if (best == null) best = t;
            }
            return best;
        }

        static Bounds GetBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // 빌드 순서: 0 = 메인 메뉴, 1 = 게임
        static void SetBuildScenes(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var p in paths) scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
