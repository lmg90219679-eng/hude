using Escape.Core;
using Escape.Interaction;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// 비밀 엘리베이터 공간을 만든다. 각 층 북쪽 중앙 방(1,2)의 북쪽 벽 너머에 숨겨진 승강장 홀이 있고,
    /// 그 뒤에 5개 층을 관통하는 승강로와 오르내리는 승강기(cabin)가 있다.
    ///
    ///   z=36  북쪽 가운데 방(시작 방)의 북쪽 벽 (숨겨진 문, 발전기가 복구돼야 열린다)
    ///   z=36~40  승강장 홀 (x 21~27)
    ///   z=40  승강장 벽 + 엘리베이터 바깥 문 (폭 2.4m)
    ///   z=40~44  승강로 (x 22~26). 승강기는 가운데(24, 42)에 있고 안쪽 폭은 3.6m
    /// 텍스처는 Varco 이미지를 쓴다 (Assets/Textures/Elevator).
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        const float HallMinX = 21f, HallMaxX = 27f, HallMinZ = 36f, HallMaxZ = 40f;
        const float ShaftMinX = 22f, ShaftMaxX = 26f, ShaftMinZ = 40f, ShaftMaxZ = 44f;
        const float ShaftCenterX = 24f, CabinCenterZ = 42f;
        const float LiftDoorWidth = 2.4f, LiftDoorHeight = 2.4f;
        const string ElevTexDir = "Assets/Textures/Elevator";

        static Material elevDoorMat, elevDoorMatFlipped, cabinWallMat, cabinFloorMat, floorButtonsMat, elevMetalMat;

        static void EnsureElevatorMaterials()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            elevMetalMat = metalMat;
            elevDoorMat = TextureMaterial("ElevDoor", ElevTexDir + "/DoorLeaf.png", Vector2.one, Vector2.zero, 0.3f);
            elevDoorMatFlipped = TextureMaterial("ElevDoorFlipped", ElevTexDir + "/DoorLeaf.png", new Vector2(-1f, 1f), new Vector2(1f, 0f), 0.3f);
            cabinWallMat = TextureMaterial("CabinWall", ElevTexDir + "/CabinWall.png", Vector2.one, Vector2.zero, 0.2f);
            cabinFloorMat = TextureMaterial("CabinFloor", ElevTexDir + "/CabinFloor.png", new Vector2(1.5f, 1.5f), Vector2.zero, 0.15f);
            // 버튼 패널 그림은 양옆이 검은 여백이라 가운데 절반만 쓴다
            floorButtonsMat = TextureMaterial("CabinButtons", ElevTexDir + "/FloorButtons.png", new Vector2(0.52f, 1f), new Vector2(0.24f, 0f), 0.4f);
        }

        static Material TextureMaterial(string name, string texPath, Vector2 tiling, Vector2 offset, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.82f));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ---------- 1) 고정 기하 구조 (NavMesh를 굽기 전) ----------

        static void BuildElevatorGeometry(Transform parent)
        {
            EnsureElevatorMaterials();
            var root = new GameObject("ElevatorShaft").transform;
            root.SetParent(parent);

            float bottomY = TowerLayout.FloorY(0);
            float topY = TowerLayout.FloorY(TowerLayout.Floors - 1) + WallHeight;

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                float y = TowerLayout.FloorY(f);
                var hall = new GameObject($"Hall_{TowerLayout.ShortLabel(f)}").transform;
                hall.SetParent(root);

                // 바닥판 (아래층 홀의 천장 겸용) + 문턱: 문이 열렸을 때 승강기로 건너가는 자리
                Box("HallSlab", hall, new Vector3(ShaftCenterX, y - 0.5f, (HallMinZ + HallMaxZ) * 0.5f), new Vector3(HallMaxX - HallMinX, 1f, HallMaxZ - HallMinZ), floorMat);
                Box("Threshold", hall, new Vector3(ShaftCenterX, y - 0.5f, HallMaxZ + 0.15f), new Vector3(LiftDoorWidth + 0.2f, 1f, 0.3f), floorMat);

                // 홀의 서쪽/동쪽 벽
                MakeWall(hall, new Vector3(HallMinX, y, (HallMinZ + HallMaxZ) * 0.5f), Vector3.forward, HallMaxZ - HallMinZ);
                MakeWall(hall, new Vector3(HallMaxX, y, (HallMinZ + HallMaxZ) * 0.5f), Vector3.forward, HallMaxZ - HallMinZ);

                // 승강장 벽 (엘리베이터 문 자리를 비운다)
                float openMin = ShaftCenterX - LiftDoorWidth * 0.5f, openMax = ShaftCenterX + LiftDoorWidth * 0.5f;
                MakeWall(hall, new Vector3((HallMinX + openMin) * 0.5f, y, HallMaxZ), Vector3.right, openMin - HallMinX);
                MakeWall(hall, new Vector3((openMax + HallMaxX) * 0.5f, y, HallMaxZ), Vector3.right, HallMaxX - openMax);
                float headerH = WallHeight - LiftDoorHeight;
                Box("Header", hall, new Vector3(ShaftCenterX, y + LiftDoorHeight + headerH * 0.5f, HallMaxZ), new Vector3(LiftDoorWidth + WallThick, headerH, WallThick), wallMat);

                // 문틀 (금속)
                Box("FrameL", hall, new Vector3(openMin - 0.06f, y + LiftDoorHeight * 0.5f, HallMaxZ - 0.12f), new Vector3(0.12f, LiftDoorHeight, 0.1f), elevMetalMat, false);
                Box("FrameR", hall, new Vector3(openMax + 0.06f, y + LiftDoorHeight * 0.5f, HallMaxZ - 0.12f), new Vector3(0.12f, LiftDoorHeight, 0.1f), elevMetalMat, false);
                Box("FrameTop", hall, new Vector3(ShaftCenterX, y + LiftDoorHeight + 0.06f, HallMaxZ - 0.12f), new Vector3(LiftDoorWidth + 0.24f, 0.12f, 0.1f), elevMetalMat, false);
            }

            // 승강로 벽 (맨 아래부터 맨 위까지 이어진다)
            float h = topY - bottomY + 1f;
            float cy = bottomY - 0.5f + h * 0.5f;
            Box("ShaftWallW", root, new Vector3(ShaftMinX, cy, (ShaftMinZ + ShaftMaxZ) * 0.5f), new Vector3(0.3f, h, ShaftMaxZ - ShaftMinZ), wallMat);
            Box("ShaftWallE", root, new Vector3(ShaftMaxX, cy, (ShaftMinZ + ShaftMaxZ) * 0.5f), new Vector3(0.3f, h, ShaftMaxZ - ShaftMinZ), wallMat);
            Box("ShaftWallN", root, new Vector3(ShaftCenterX, cy, ShaftMaxZ), new Vector3(ShaftMaxX - ShaftMinX + 0.3f, h, 0.3f), wallMat);

            // 승강로 바닥(맨 아래)과 지붕(맨 위, 홀과 함께)
            Box("ShaftPit", root, new Vector3(ShaftCenterX, bottomY - 0.5f, (ShaftMinZ + ShaftMaxZ) * 0.5f), new Vector3(ShaftMaxX - ShaftMinX + 0.3f, 1f, ShaftMaxZ - ShaftMinZ + 0.3f), floorMat);
            Box("ElevatorRoof", root, new Vector3(ShaftCenterX, topY + 0.5f, (HallMinZ + ShaftMaxZ) * 0.5f), new Vector3(HallMaxX - HallMinX, 1f, ShaftMaxZ - HallMinZ), wallMat);
        }

        // ---------- 2) 문, 호출 패널, 승강기 (NavMesh를 굽고 난 뒤) ----------

        static void BuildElevatorProps(Transform parent)
        {
            var root = new GameObject("ElevatorProps").transform;
            root.SetParent(parent);

            var landings = new ElevatorLanding[TowerLayout.Floors];
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                float y = TowerLayout.FloorY(f);
                var landingGo = new GameObject($"Landing_{TowerLayout.ShortLabel(f)}");
                landingGo.transform.SetParent(root);
                landingGo.transform.position = new Vector3(ShaftCenterX, y, HallMaxZ);

                // 바깥 문 한 쌍: 승강장 벽 평면(z=28)에서 좌우로 열린다
                var pair = MakeDoorPair(landingGo.transform, new Vector3(ShaftCenterX, y, HallMaxZ - 0.02f));

                var hallPoint = new GameObject("HallPoint").transform;
                hallPoint.SetParent(landingGo.transform);
                hallPoint.position = new Vector3(ShaftCenterX, y, (HallMinZ + HallMaxZ) * 0.5f);

                var landing = landingGo.AddComponent<ElevatorLanding>();
                landing.floor = f;
                landing.doors = pair;
                landing.hallPoint = hallPoint;
                EditorUtility.SetDirty(landing);
                landings[f] = landing;

                BuildCallPanel(landingGo.transform, f, y);

                // 홀 안의 희미한 조명: 승강장을 알아볼 수 있게 (비상등 톤)
                var lg = new GameObject("HallLamp").AddComponent<Light>();
                lg.transform.SetParent(landingGo.transform);
                lg.transform.position = new Vector3(ShaftCenterX, y + 2.6f, (HallMinZ + HallMaxZ) * 0.5f);
                lg.type = LightType.Point;
                lg.range = 6f;
                lg.intensity = 0.55f;
                lg.color = new Color(1f, 0.55f, 0.3f);
            }

            var cabin = BuildCabin(root, out var cabinDoors);

            var controllerGo = new GameObject("ElevatorController");
            var ec = controllerGo.AddComponent<ElevatorController>();
            var so = new SerializedObject(ec);
            so.FindProperty("cabin").objectReferenceValue = cabin;
            so.FindProperty("cabinDoors").objectReferenceValue = cabinDoors;
            var arr = so.FindProperty("landings");
            arr.arraySize = landings.Length;
            for (int i = 0; i < landings.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = landings[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ec);
        }

        /// <summary>왼쪽/오른쪽 문짝 두 개로 된 문 한 쌍. center는 문의 가운데 바닥.</summary>
        static ElevatorDoorPair MakeDoorPair(Transform parent, Vector3 center)
        {
            var go = new GameObject("DoorPair");
            go.transform.SetParent(parent);
            go.transform.position = center;

            float leafW = LiftDoorWidth * 0.5f, leafH = LiftDoorHeight - 0.03f;
            var left = Box("LeafL", go.transform, center + new Vector3(-leafW * 0.5f, 0.015f + leafH * 0.5f, 0f), new Vector3(leafW, leafH, 0.06f), elevDoorMat);
            var right = Box("LeafR", go.transform, center + new Vector3(leafW * 0.5f, 0.015f + leafH * 0.5f, 0f), new Vector3(leafW, leafH, 0.06f), elevDoorMatFlipped);

            var pair = go.AddComponent<ElevatorDoorPair>();
            var so = new SerializedObject(pair);
            so.FindProperty("leftLeaf").objectReferenceValue = left.transform;
            so.FindProperty("rightLeaf").objectReferenceValue = right.transform;
            so.FindProperty("openDistance").floatValue = leafW - 0.05f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pair);
            return pair;
        }

        static void BuildCallPanel(Transform parent, int floor, float y)
        {
            // 문 오른쪽(동쪽) 벽에, 홀 쪽(-Z)을 향해 단다
            var holder = new GameObject("CallPanel").transform;
            holder.SetParent(parent);
            holder.position = new Vector3(ShaftCenterX + LiftDoorWidth * 0.5f + 0.55f, y + 1.1f, HallMaxZ - WallThick * 0.5f - 0.02f);

            var model = PropTools.Instantiate("ElevatorCallPanel", holder, 0.4f, 0f, yaw: 180f);
            var col = holder.gameObject.AddComponent<BoxCollider>();
            if (model != null)
            {
                var b = PropTools.Bounds(model);
                col.center = holder.InverseTransformPoint(b.center);
                col.size = new Vector3(Mathf.Max(b.size.x, 0.2f), Mathf.Max(b.size.y, 0.3f), 0.25f);
            }
            else
            {
                col.size = new Vector3(0.25f, 0.4f, 0.1f);
            }
            col.isTrigger = true;

            var panel = holder.gameObject.AddComponent<ElevatorCallPanel>();
            panel.floor = floor;
            EditorUtility.SetDirty(panel);
        }

        /// <summary>승강기: 바닥, 벽 3면(Varco 이미지), 천장 조명, 층 버튼 패널, 안쪽 문 한 쌍.</summary>
        static Transform BuildCabin(Transform parent, out ElevatorDoorPair doors)
        {
            float y = TowerLayout.FloorY(TowerLayout.Floors - 1);   // 처음에는 맨 위층에 서 있다
            var cabin = new GameObject("ElevatorCabin").transform;
            cabin.SetParent(parent);
            cabin.position = new Vector3(ShaftCenterX, y, CabinCenterZ);

            const float half = 1.8f, height = 2.5f;
            Vector3 c = cabin.position;

            Box("Floor", cabin, c + new Vector3(0f, -0.1f, 0f), new Vector3(half * 2f, 0.2f, half * 2f), cabinFloorMat);
            Box("Ceiling", cabin, c + new Vector3(0f, height + 0.05f, 0f), new Vector3(half * 2f, 0.1f, half * 2f), wallMat);
            Box("WallW", cabin, c + new Vector3(-half + 0.05f, height * 0.5f, 0f), new Vector3(0.1f, height, half * 2f), cabinWallMat);
            Box("WallE", cabin, c + new Vector3(half - 0.05f, height * 0.5f, 0f), new Vector3(0.1f, height, half * 2f), cabinWallMat);
            Box("WallN", cabin, c + new Vector3(0f, height * 0.5f, half - 0.05f), new Vector3(half * 2f, height, 0.1f), cabinWallMat);

            // 앞쪽(남쪽, 문 쪽) 벽: 문 자리를 비운다
            float frontZ = c.z - half + 0.05f;
            float sideW = half - LiftDoorWidth * 0.5f;
            Box("FrontL", cabin, new Vector3(c.x - LiftDoorWidth * 0.5f - sideW * 0.5f, y + height * 0.5f, frontZ), new Vector3(sideW, height, 0.1f), cabinWallMat);
            Box("FrontR", cabin, new Vector3(c.x + LiftDoorWidth * 0.5f + sideW * 0.5f, y + height * 0.5f, frontZ), new Vector3(sideW, height, 0.1f), cabinWallMat);
            float fh = height - LiftDoorHeight;
            Box("FrontTop", cabin, new Vector3(c.x, y + LiftDoorHeight + fh * 0.5f, frontZ), new Vector3(LiftDoorWidth, fh, 0.1f), cabinWallMat);

            doors = MakeDoorPair(cabin, new Vector3(c.x, y, frontZ + 0.07f));

            // 천장 조명: 낡은 따뜻한 불빛
            var lg = new GameObject("CabinLight").AddComponent<Light>();
            lg.transform.SetParent(cabin);
            lg.transform.position = c + new Vector3(0f, height - 0.25f, 0f);
            lg.type = LightType.Point;
            lg.range = 5.5f;
            lg.intensity = 1.1f;
            lg.color = new Color(1f, 0.82f, 0.55f);

            // 층 버튼 패널: 북쪽 벽(들어서면 마주 보는 벽), 오른쪽 가까이
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FloorButtons";
            quad.transform.SetParent(cabin, false);
            quad.transform.position = c + new Vector3(0.9f, 1.25f, half - 0.11f);
            quad.transform.rotation = Quaternion.identity;          // Quad는 -Z 쪽(들어선 사람 쪽)을 본다
            quad.transform.localScale = new Vector3(0.34f, 0.46f, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = floorButtonsMat;
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            return cabin;
        }
    }
}