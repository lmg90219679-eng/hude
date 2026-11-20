using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Varco에서 만든 소품(FBX)을 Unity에서 쓰기 좋게 준비한다.
    /// - FBX 임포트 설정(텍스처 추출, 카메라/조명 제외)
    /// - 소품마다 URP Lit 재질 생성 (기본색 + 노말, 무광). Varco의 orm 텍스처는 URP가 그대로 못 읽어서 쓰지 않는다.
    /// - 크기를 목표 치수에 맞춰 바닥 중앙을 원점으로 둔 소품 인스턴스를 만들어 준다.
    /// 메뉴: Escape > Setup Props
    /// </summary>
    public static class PropTools
    {
        public const string PropsDir = "Assets/Models/Props";

        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        /// <summary>임포트한 모델을 세우는 X축 회전(도).</summary>
        public static float PropUpAxisFix = -90f;

        [MenuItem("Escape/Setup Props")]
        public static void SetupAll()
        {
            materialCache.Clear();
            if (!Directory.Exists(PropsDir)) return;

            foreach (var dir in Directory.GetDirectories(PropsDir))
            {
                string name = Path.GetFileName(dir);
                SetupOne(name);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static string FbxPath(string name) => $"{PropsDir}/{name}/{name}.fbx";

        public static void SetupOne(string name)
        {
            string path = FbxPath(name);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[PropTools] FBX 없음: {path}"); return; }

            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.importCameras = false;
            imp.importLights = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.SaveAndReimport();

            string texDir = $"{PropsDir}/{name}/Textures";
            Directory.CreateDirectory(texDir);
            imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            imp.SaveAndReimport();

            // 노말맵은 노말맵으로 읽도록
            string normalPath = texDir + "/normal.png";
            var ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }

            BuildMaterial(name);
            Debug.Log($"[PropTools] 준비 완료: {name}");
        }

        public static Material BuildMaterial(string name)
        {
            if (materialCache.TryGetValue(name, out var cached) && cached != null) return cached;

            string texDir = $"{PropsDir}/{name}/Textures";
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/base_color.png");
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/normal.png");

            string matPath = $"{PropsDir}/{name}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            materialCache[name] = mat;
            return mat;
        }

        /// <summary>
        /// 소품 인스턴스를 만든다. 가장 긴 변이 아니라 "목표 치수(가로 x 높이 x 깊이)"에 맞춰 각 축을 따로 맞추지 않고,
        /// 비율을 유지한 채 targetHeight(미터)에 높이를 맞춘다. 바닥 중앙이 원점.
        /// widthLimit이 0보다 크면 가로(X)나 깊이(Z)가 그 값을 넘지 않게 더 줄인다.
        /// </summary>
        public static GameObject Instantiate(string name, Transform parent, float targetHeight, float widthLimit = 0f, float yaw = 0f, float? uniformScale = null, Quaternion? preRotation = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(name));
            if (prefab == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);

            var mat = BuildMaterial(name);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            Normalize(go, targetHeight, widthLimit, yaw, uniformScale, preRotation);
            return go;
        }

        /// <summary>크기를 맞추고 바닥 중앙을 부모의 원점에 놓는다.</summary>
        public static void Normalize(GameObject go, float targetHeight, float widthLimit, float yaw = 0f, float? uniformScale = null, Quaternion? preRotation = null)
        {
            var parent = go.transform.parent;
            go.transform.localPosition = Vector3.zero;
            // Varco FBX는 Z가 위쪽인 좌표로 나와서(누워 있다) 세워 준다
            go.transform.localRotation = (preRotation ?? Quaternion.identity) * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(PropUpAxisFix, 0f, 0f);
            go.transform.localScale = Vector3.one;

            var b = Bounds(go);
            float scale = b.size.y > 0.001f ? targetHeight / b.size.y : 1f;
            if (uniformScale.HasValue) scale = uniformScale.Value;
            else if (widthLimit > 0f)
            {
                float w = Mathf.Max(b.size.x, b.size.z) * scale;
                if (w > widthLimit) scale *= widthLimit / w;
            }
            go.transform.localScale = Vector3.one * scale;

            b = Bounds(go);
            Vector3 origin = parent != null ? parent.position : Vector3.zero;
            go.transform.position += new Vector3(origin.x - b.center.x, origin.y - b.min.y, origin.z - b.center.z);
        }

        // 서랍장 모델의 앞면이 향하는 방향을 +Z로 돌리는 각도 (모델마다 다르다. 서랍장은 -X를 향해 나온다)
        public static float CabinetBodyYaw = 90f;
        public static float CabinetDrawerYaw = 90f;
        public static float CabinetHeight = 0.9f;
        public static float DrawerWidth = 0.46f;
        public static float DrawerCenterY = 0.74f;      // 서랍이 들어가는 칸의 높이
        public static float DrawerInset = 0.0f;          // 닫혔을 때 앞면에서 안쪽으로 들어간 정도

        /// <summary>
        /// 서랍장 프리팹을 만든다: Body(몸통), Drawer(서랍), ItemAnchor(물건 자리).
        /// 구조는 Container 스크립트가 기대하는 것과 같다 (서랍은 로컬 +Z로 밀려 나온다).
        /// </summary>
        public static GameObject BuildCabinetPrefab(string prefabPath)
        {
            var root = new GameObject("Cabinet");

            var body = Instantiate("CabinetBody", root.transform, CabinetHeight, 0f, CabinetBodyYaw);
            body.name = "Body";
            var bb = Bounds(body);

            // 서랍: 가로가 DrawerWidth가 되도록 배율을 정한다
            var drawer = Instantiate("CabinetDrawer", root.transform, 0.2f, 0f, CabinetDrawerYaw);
            var d0 = Bounds(drawer);
            float s = DrawerWidth / Mathf.Max(d0.size.x, d0.size.z);
            Normalize(drawer, 0.2f, 0f, CabinetDrawerYaw, drawer.transform.localScale.x * s);
            drawer.name = "Drawer";
            var db = Bounds(drawer);

            // 서랍 위치: 몸통 앞면에 맞춰 바닥 중앙이 (0, DrawerCenterY - 높이/2, 앞면 - 깊이/2)
            float frontZ = bb.max.z - root.transform.position.z;
            drawer.transform.position += new Vector3(0f, DrawerCenterY - db.size.y * 0.5f, frontZ - db.size.z * 0.5f - DrawerInset);

            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root.transform, false);
            var db2 = Bounds(drawer);
            anchor.position = new Vector3(db2.center.x, db2.max.y, db2.center.z);

            // 충돌체: 몸통 전체를 덮는 박스 하나
            var col = body.AddComponent<BoxCollider>();
            var lb = Bounds(body);
            col.center = body.transform.InverseTransformPoint(lb.center);
            var inv = body.transform.lossyScale;
            col.size = new Vector3(lb.size.x / inv.x, lb.size.y / inv.y, lb.size.z / inv.z);

            string dir = System.IO.Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // 차단기 패널: 앞면이 +Z. 세로 칸 3개의 위치(패널 중앙 기준, 미터)는 렌더링으로 맞춘 값.
        public static float PanelHeight = 1.1f;
        public static float LeverHeight = 0.24f;
        public static float[] LeverSlotX = { 0.145f, 0.0f, -0.145f };   // 앞에서 봤을 때 왼쪽부터 (+X가 왼쪽)
        public static float LeverCenterX = -0.097f;                      // 가운데 칸의 x (패널 범위 중심 기준)
        public static float LeverY = 0.60f;                              // 칸 중심 높이 (패널 바닥 기준)
        public static float LeverDepthFromFront = 0.10f;                 // 앞면에서 안쪽으로 들어간 정도

        /// <summary>차단기 패널 프리팹: Body(열린 패널) + LeverPivot0~2(젖혀지는 레버). BreakerLever 컴포넌트는 씬 빌더가 붙인다.</summary>
        public static GameObject BuildBreakerPanelPrefab(string prefabPath)
        {
            var root = new GameObject("BreakerPanelAssembly");
            var body = Instantiate("BreakerPanel", root.transform, PanelHeight, 0f, 0f);
            body.name = "Body";
            var bb = Bounds(body);

            for (int i = 0; i < 3; i++)
            {
                var pivot = new GameObject("LeverPivot" + i).transform;
                pivot.SetParent(root.transform, false);
                float x = bb.center.x + LeverCenterX + LeverSlotX[i];
                float y = bb.min.y + LeverY;
                float z = bb.max.z - LeverDepthFromFront;
                pivot.position = new Vector3(x, y, z);

                // 크기는 레버를 "세운 상태"의 높이로 정한다 (눕히고 나서 재면 엉뚱하게 커진다)
                var probe = Instantiate("BreakerLever", pivot, LeverHeight, 0f, 0f);
                float leverScale = probe.transform.localScale.x;
                Object.DestroyImmediate(probe);

                // 레버 모델: 바닥판이 패널 안쪽을 향하고, 손잡이가 앞(+Z)으로 나오게 눕힌다 (X로 +90도)
                var lever = Instantiate("BreakerLever", pivot, LeverHeight, 0f, 0f, leverScale, Quaternion.Euler(90f, 0f, 0f));
                lever.name = "Lever";
                // 레버를 pivot 원점 근처(바닥판 중심)에 맞춘다
                var lb = Bounds(lever);
                lever.transform.position += new Vector3(pivot.position.x - lb.center.x, pivot.position.y - lb.center.y, pivot.position.z - lb.min.z);
            }

            // 몸통은 플레이어를 막기만 하고 시선은 통과시킨다 (안쪽 레버를 가리면 안 된다)
            var col = body.AddComponent<BoxCollider>();
            var b2 = Bounds(body);
            col.center = body.transform.InverseTransformPoint(b2.center);
            var s = body.transform.lossyScale;
            col.size = new Vector3(b2.size.x / s.x, b2.size.y / s.y, b2.size.z / s.z);
            body.AddComponent<Escape.Interaction.InteractPassThrough>();

            string dir = System.IO.Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
