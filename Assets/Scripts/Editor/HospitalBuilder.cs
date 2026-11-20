using System.Collections.Generic;
using Escape.Core;
using Escape.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Escape.EditorTools
{
    /// <summary>
    /// 폐병원 방 구성. 방마다 다른 소품(Varco 모델)을 네 모서리 자리(NW, NE, SW, SE)에 놓는다.
    /// 소품은 방 한가운데 십자 통로(문에서 문으로 이어지는 길)를 비워 두고, 서랍장과 계단, 로커 자리를 피해서 놓는다.
    /// 방 이름은 RoomNames에 있다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        // 자리: 방 중심 기준 (x, z)와, 소품이 바라볼 방향(방 안쪽)의 yaw
        struct AnchorSpot { public Vector2 pos; public float faceYaw; }

        /// <summary>
        /// 소품 자리 이름(NW, NE, SW, SE)을 방 크기에 맞는 좌표로 바꾼다. 좌표는 방 중심 기준 (x, z).
        /// - 일반 방: 서/동쪽 벽 가까이, 방 중심선 위아래. 소품은 방 안쪽을 본다.
        /// - 복도·계단 홀(깊이 4m 이하): 긴 벽을 따라 놓고 반대쪽 벽을 본다.
        /// </summary>
        static AnchorSpot ResolveAnchor(string name, int room)
        {
            Rect r = TowerLayout.RoomRect(room);
            float hx = r.width * 0.5f, hz = r.height * 0.5f;
            bool north = name[0] == 'N', east = name[1] == 'E';
            float sx = east ? 1f : -1f, sz = north ? 1f : -1f;

            if (hz < 3f)
                return new AnchorSpot { pos = new Vector2(sx * hx * 0.5f, sz * (hz - 0.75f)), faceYaw = north ? 180f : 0f };

            float x = sx * (hx - (north ? 1.6f : 2.1f));
            float z = sz * (hz - (hz >= 4.5f ? 2.5f : 2.2f));
            return new AnchorSpot { pos = new Vector2(x, z), faceYaw = east ? 270f : 90f };
        }

        /// <summary>계단 홀에서는 계단이 붙은 쪽 벽(서쪽 홀은 서쪽, 동쪽 홀은 동쪽)에 소품을 놓지 않는다.</summary>
        static bool AnchorUsable(int room, string anchorName)
        {
            if (room == TowerLayout.WestStairRoom && anchorName.EndsWith("W")) return false;
            if (room == TowerLayout.EastStairRoom && anchorName.EndsWith("E")) return false;
            return true;
        }

        struct PropSpec
        {
            public float height;       // 목표 높이 (m)
            public float maxSide;      // 가로/깊이 중 긴 쪽의 상한 (m)
            public float frontYaw;     // 모델의 앞면을 +Z로 돌리는 각도 (모델마다 다르다)
            public PropSpec(float h, float side, float front = 0f) { height = h; maxSide = side; frontYaw = front; }
        }

        static readonly Dictionary<string, PropSpec> PropSpecs = new Dictionary<string, PropSpec>
        {
            { "HospitalBed",   new PropSpec(0.85f, 1.9f) },
            { "NurseCounter",  new PropSpec(1.1f,  2.0f) },
            { "WaitingBench",  new PropSpec(0.9f,  1.7f) },
            { "Wheelchair",    new PropSpec(0.95f, 0.8f) },
            { "IVStand",       new PropSpec(1.8f,  0.8f) },
            { "MedicineCart",  new PropSpec(0.9f,  0.8f) },
            { "ExamTable",     new PropSpec(0.85f, 1.9f) },
            { "FoldingScreen", new PropSpec(1.6f,  1.5f) },
            { "MetalShelf",    new PropSpec(2.0f,  1.2f) },
        };

        // [층][방] -> "모델:자리"
        // 계단방(3, 5)은 계단 도착 지점이 NW/NE 자리라서 SW/SE만 쓴다.
        static readonly string[][][] RoomProps =
        {
            // ----- 지하2 -----
            new[]
            {
                new[] { "MetalShelf:NW", "MetalShelf:SE", "MedicineCart:NE" },                      // 0 세탁실
                new[] { "MetalShelf:SW", "MetalShelf:SE", "MedicineCart:NE" },                      // 1 기계실
                new[] { "MetalShelf:NW", "MetalShelf:NE", "MetalShelf:SW", "MetalShelf:SE" },       // 2 창고
                new[] { "WaitingBench:SW", "Wheelchair:SE" },                                       // 3 복도(서)
                new[] { "WaitingBench:NW", "Wheelchair:SW" },                                       // 4 중앙 통로
                new[] { "IVStand:SW", "Wheelchair:SE" },                                            // 5 복도(동)
                new[] { "MetalShelf:NE", "MedicineCart:SW" },                                       // 6 폐기물실
                new[] { "MetalShelf:SW" },                                                          // 7 승강기 앞
                new[] { "MetalShelf:SE", "MetalShelf:SW" },                                         // 8 비품실
            },
            // ----- 지하1 -----
            new[]
            {
                new[] { "ExamTable:NW", "ExamTable:NE", "FoldingScreen:SW" },                       // 0 영안실
                new[] { "ExamTable:NE", "MedicineCart:SW", "FoldingScreen:SE" },                    // 1 방사선실
                new[] { "MetalShelf:NW", "MetalShelf:NE", "MedicineCart:SE" },                      // 2 약품 창고
                new[] { "WaitingBench:SW", "Wheelchair:SE" },                                       // 3
                new[] { "IVStand:NW", "Wheelchair:NE" },                                            // 4
                new[] { "WaitingBench:SE", "IVStand:SW" },                                          // 5
                new[] { "HospitalBed:NW", "FoldingScreen:NE", "IVStand:SW" },                       // 6 격리실
                new[] { "MedicineCart:NW", "WaitingBench:NE" },                                     // 7
                new[] { "MetalShelf:NW", "MedicineCart:NE" },                                       // 8 소독실
            },
            // ----- 1층 -----
            new[]
            {
                new[] { "MetalShelf:NW", "MetalShelf:NE", "NurseCounter:SW" },                      // 0 약국
                new[] { "NurseCounter:NW", "WaitingBench:NE", "WaitingBench:SW", "Wheelchair:SE" }, // 1 정문 로비
                new[] { "HospitalBed:NW", "HospitalBed:NE", "MedicineCart:SW", "IVStand:SE" },      // 2 응급실
                new[] { "WaitingBench:SW", "Wheelchair:SE" },                                       // 3 로비(서)
                new[] { "WaitingBench:NW", "WaitingBench:NE", "WaitingBench:SW", "Wheelchair:SE" }, // 4 중앙 대기실
                new[] { "WaitingBench:SE", "IVStand:SW" },                                          // 5 로비(동)
                new[] { "NurseCounter:NW", "MetalShelf:NE", "WaitingBench:SE" },                    // 6 원무과
                new[] { "FoldingScreen:NE", "MedicineCart:SW" },                                    // 7 상담실
                new[] { "WaitingBench:NW", "WaitingBench:NE", "IVStand:SE" },                       // 8 접수 대기실
            },
            // ----- 2층 -----
            new[]
            {
                new[] { "ExamTable:NW", "MedicineCart:NE", "FoldingScreen:SW", "IVStand:SE" },      // 0 처치실
                new[] { "ExamTable:NW", "ExamTable:NE", "MedicineCart:SW", "IVStand:SE" },          // 1 주사실
                new[] { "HospitalBed:NW", "HospitalBed:NE", "IVStand:SE", "FoldingScreen:SW" },     // 2 병실 201
                new[] { "WaitingBench:SW", "Wheelchair:SE" },                                       // 3 병실 복도(서)
                new[] { "IVStand:NW", "Wheelchair:NE", "WaitingBench:SW" },                         // 4 병실 복도(중앙)
                new[] { "WaitingBench:SE", "MedicineCart:SW" },                                     // 5 병실 복도(동)
                new[] { "NurseCounter:NW", "MetalShelf:NE", "MedicineCart:SE" },                    // 6 간호사실
                new[] { "HospitalBed:NW", "HospitalBed:NE", "FoldingScreen:SW" },                   // 7 병실 202
                new[] { "HospitalBed:NE", "IVStand:NW", "FoldingScreen:SW" },                       // 8 병실 203
            },
            // ----- 3층 -----
            new[]
            {
                new[] { "HospitalBed:NW", "HospitalBed:NE", "IVStand:SE" },                         // 0 병실 301
                new[] { "HospitalBed:NW", "HospitalBed:NE", "FoldingScreen:SW" },                   // 1 병실 302
                new[] { "ExamTable:NW", "MedicineCart:NE", "MetalShelf:SW", "IVStand:SE" },         // 2 수술 준비실
                new[] { "WaitingBench:SW", "Wheelchair:SE" },                                       // 3 병실 복도(서)
                new[] { "IVStand:NW", "Wheelchair:NE", "WaitingBench:SW" },                         // 4 병실 복도(중앙)
                new[] { "WaitingBench:SE", "Wheelchair:SW" },                                       // 5 병실 복도(동)
                new[] { "HospitalBed:NW", "HospitalBed:NE", "IVStand:SE" },                         // 6 병실 303
                new[] { "HospitalBed:NW", "FoldingScreen:NE", "MedicineCart:SW" },                  // 7 VIP 병실
                new[] { "HospitalBed:NW", "HospitalBed:NE", "FoldingScreen:SW" },                   // 8 병실 304
            },
        };

        const string HospitalTexDir = "Assets/Textures/Hospital";
        static Material wallShortMat, wallLongMat;

        /// <summary>벽과 바닥에 Varco 병원 텍스처를 입힌다. 벽은 길이에 따라 반복 횟수를 달리해서 무늬 크기를 비슷하게 맞춘다.</summary>
        static void EnsureHospitalMaterials()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HospitalTexDir + "/FloorTile.png") == null) return;

            floorMat = TextureMaterial("HospitalFloor", HospitalTexDir + "/FloorTile.png", new Vector2(12f, 12f), Vector2.zero, 0.25f);
            wallShortMat = TextureMaterial("HospitalWallShort", HospitalTexDir + "/WallPaint.png", new Vector2(1f, 1f), Vector2.zero, 0.1f);
            wallMat = TextureMaterial("HospitalWallMid", HospitalTexDir + "/WallPaint.png", new Vector2(2f, 1f), Vector2.zero, 0.1f);
            wallLongMat = TextureMaterial("HospitalWallLong", HospitalTexDir + "/WallPaint.png", new Vector2(3f, 1f), Vector2.zero, 0.1f);

            // 방문: 문틈이 3m라서 두 짝이 나란히 있는 양문처럼 보이게 가로로 두 번 반복한다
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HospitalTexDir + "/DoorRoom.png") != null)
                doorMat = TextureMaterial("HospitalDoor", HospitalTexDir + "/DoorRoom.png", new Vector2(2f, 1f), Vector2.zero, 0.2f);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HospitalTexDir + "/DoorLocked.png") != null)
                lockedDoorMat = TextureMaterial("HospitalDoorLocked", HospitalTexDir + "/DoorLocked.png", new Vector2(2f, 1f), Vector2.zero, 0.25f);
        }

        static Material WallMaterialFor(float length)
        {
            if (wallShortMat == null || wallLongMat == null) return wallMat;
            return length <= 3.5f ? wallShortMat : (length <= 6.5f ? wallMat : wallLongMat);
        }

        /// <summary>모든 층의 방에 소품을 놓는다. 없는 모델은 건너뛴다 (모델이 준비되는 대로 늘어난다).</summary>
        static void BuildHospitalRooms(Transform parent)
        {
            var root = new GameObject("HospitalProps").transform;
            root.SetParent(parent);
            int placed = 0, skipped = 0;

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                {
                    var list = RoomProps[f][room];
                    foreach (var entry in list)
                    {
                        var parts = entry.Split(':');
                        if (PlaceProp(root, f, room, parts[0], parts[1])) placed++; else skipped++;
                    }
                }
            }
            Debug.Log($"[HospitalBuilder] 소품 {placed}개 배치, 건너뜀 {skipped}개 (모델 없음)");
        }

        static bool PlaceProp(Transform root, int floor, int room, string model, string anchorName)
        {
            if (!System.IO.File.Exists($"{PropTools.PropsDir}/{model}/{model}.fbx")) return false;
            if (!PropSpecs.TryGetValue(model, out var spec)) return false;
            if (!AnchorUsable(room, anchorName)) return false;
            var anchor = ResolveAnchor(anchorName, room);

            Vector3 center = TowerLayout.RoomCenter(floor, room);
            var holder = new GameObject($"{model}_{TowerLayout.ShortLabel(floor)}_{room}_{anchorName}").transform;
            holder.SetParent(root);
            holder.position = center + new Vector3(anchor.pos.x, 0f, anchor.pos.y);

            var go = PropTools.Instantiate(model, holder, spec.height, spec.maxSide, anchor.faceYaw + spec.frontYaw);
            if (go == null) { Object.DestroyImmediate(holder.gameObject); return false; }

            // 충돌체 (플레이어가 통과하지 못한다) + 추적자 길찾기에서 비켜 가도록 NavMesh 장애물
            var b = PropTools.Bounds(go);
            var col = holder.gameObject.AddComponent<BoxCollider>();
            col.center = holder.InverseTransformPoint(b.center);
            col.size = b.size;

            var obstacle = holder.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = new Vector3(b.size.x, b.size.y, b.size.z);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            if (model == "HospitalBed") AddBedHidingSpot(holder, anchor, center.y);
            return true;
        }

        /// <summary>침대 옆 바닥(벽 쪽)에 몸을 낮춰 숨는다. 눈높이를 낮추고, 방 안쪽으로 빠져나온다.</summary>
        static void AddBedHidingSpot(Transform holder, AnchorSpot anchor, float floorY)
        {
            float towardWallZ = anchor.pos.y > 0f ? 1f : -1f;
            float towardCenterX = anchor.pos.x < 0f ? 1f : -1f;

            var hide = new GameObject("HidePoint").transform;
            hide.SetParent(holder);
            hide.position = new Vector3(holder.position.x, floorY + 0.1f, holder.position.z + towardWallZ * 0.82f);
            hide.rotation = Quaternion.Euler(0f, anchor.faceYaw, 0f);

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(holder);
            exit.position = new Vector3(holder.position.x + towardCenterX * 1.9f, floorY + 0.1f, hide.position.z);

            var spot = holder.gameObject.AddComponent<HidingSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("hidePoint").objectReferenceValue = hide;
            so.FindProperty("exitPoint").objectReferenceValue = exit;
            so.FindProperty("eyeHeight").floatValue = 0.55f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}