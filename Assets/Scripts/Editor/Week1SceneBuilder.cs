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
    public static partial class Week1SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Tower.unity";
        const string MatDir = "Assets/Materials";
        const float Cell = 8f;
        const int Grid = 3;
        const float WallHeight = 3f;
        const float WallThick = 0.3f;
        const float DoorWidth = 3f;

        static Material floorMat, wallMat, chaserMat, woodMat, wardrobeMat, doorMat, lockedDoorMat, exitMat, boxMat, stairUpMat, stairDownMat, drawerMat, metalMat, metalDrawerMat;

        // Build()는 TowerBuilder.cs에 있다 (5개 층 건물을 만든다).
        [MenuItem("Escape/Build Tower Scene")]
        public static void BuildMenu() => Build();
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
            color.postExposure.Override(0f);

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
            lockedDoorMat = GetOrCreateMat("LockedDoor", new Color(0.35f, 0.08f, 0.08f));
            stairUpMat = GetOrCreateMat("StairUp", new Color(0.25f, 0.45f, 0.3f));
            stairDownMat = GetOrCreateMat("StairDown", new Color(0.4f, 0.15f, 0.15f));
            drawerMat = GetOrCreateMat("CabinetDrawer", new Color(0.46f, 0.3f, 0.18f));
            metalMat = GetOrCreateMat("Metal", new Color(0.5f, 0.52f, 0.56f));
            metalDrawerMat = GetOrCreateMat("MetalDrawer", new Color(0.62f, 0.65f, 0.7f));
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
            RenderSettings.ambientLight = new Color(0.014f, 0.015f, 0.02f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.07f;
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
            wall.GetComponent<Renderer>().sharedMaterial = WallMaterialFor(length);
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
                string dir = "Assets/Scenes/Tower";
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(surface.navMeshData, dir + "/NavMesh.asset");
            }
        }

        // ---------- 플레이어 / 추적자 ----------

        static GameObject BuildPlayer(Vector3 startPosition)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = startPosition + Vector3.up * 0.1f;
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 시작할 때 복도 쪽(남쪽)을 본다

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
            spot.range = 24f;
            spot.spotAngle = 50f;
            spot.innerSpotAngle = 30f;
            spot.intensity = 9f;
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

        static ChaserAI BuildChaser(Transform player, Transform[] waypoints, Vector3 startPosition, string name)
        {
            var chaser = new GameObject(name);
            chaser.transform.position = startPosition + Vector3.up * 0.05f;

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
            key.enabled = false;

            // 림: 뒤쪽 붉은 빛으로 윤곽을 살려 섬뜩하게 만든다.
            var rim = new GameObject("FaceRimLight").AddComponent<Light>();
            rim.transform.SetParent(chaser.transform, false);
            rim.transform.localPosition = new Vector3(-0.2f, headY + 0.25f, -0.8f);
            rim.type = LightType.Point;
            rim.range = 3.5f;
            rim.intensity = 3.5f;
            rim.color = new Color(1f, 0.15f, 0.1f);
            rim.enabled = false;

            // 아래쪽 약한 채움빛: 턱과 입 안쪽을 보이게 한다.
            var fill = new GameObject("FaceFillLight").AddComponent<Light>();
            fill.transform.SetParent(chaser.transform, false);
            fill.transform.localPosition = new Vector3(0f, headY - 0.6f, 0.9f);
            fill.type = LightType.Point;
            fill.range = 3f;
            fill.intensity = 0.8f;
            fill.color = new Color(0.7f, 0.55f, 0.5f);
            fill.enabled = false;

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
            return ai;
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
        static Material cachedSkin;
        static RuntimeAnimatorController cachedController;

        static Material BuildChaserSkin()
        {
            if (cachedSkin != null) return cachedSkin;
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
            cachedSkin = mat;
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
            if (cachedController != null) return cachedController;
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
            cachedController = ctrl;
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
