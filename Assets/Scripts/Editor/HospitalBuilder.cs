using System.Collections.Generic;
using System.Linq;
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
            if (name[1] == 'M')   // 북쪽/남쪽 벽 가운데: 벽에 붙어 방 안쪽을 본다
                return new AnchorSpot { pos = new Vector2(0f, sz * (hz - 1.9f)), faceYaw = north ? 180f : 0f };

            if (hz < 3f)
                return new AnchorSpot { pos = new Vector2(sx * hx * 0.5f, sz * (hz - 0.75f)), faceYaw = north ? 180f : 0f };

            float x = sx * (hx - (north ? 1.6f : 2.1f));
            float z = sz * (hz - (hz >= 4.5f ? 2.5f : 2.2f));
            return new AnchorSpot { pos = new Vector2(x, z), faceYaw = east ? 270f : 90f };
        }

        /// <summary>계단 홀에는 소품을 놓지 않는다.</summary>
        static bool AnchorUsable(int floor, int room, string anchorName)
        {
            if ((room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom) && HallHasStairs(floor, room)) return false;   // 계단통이 차지한다
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

        // 층마다 방 테마에 맞는 소품 후보 (방마다 이 중에서 무작위로 몇 개를 네 모서리 자리에 놓는다)
        static readonly string[][] ThemePool =
        {
            new[] { "MetalShelf", "MetalShelf", "MetalShelf", "MedicineCart", "Wheelchair" },                       // 지하2: 창고, 기계실
            new[] { "ExamTable", "ExamTable", "FoldingScreen", "MetalShelf", "MedicineCart", "IVStand" },           // 지하1: 영안실, 검사실
            new[] { "WaitingBench", "WaitingBench", "MetalShelf", "MedicineCart", "IVStand", "Wheelchair", "FoldingScreen", "NurseCounter" },   // 1층: 접수, 진료
            new[] { "HospitalBed", "HospitalBed", "HospitalBed", "IVStand", "FoldingScreen", "MedicineCart" },     // 2층: 병실
            new[] { "HospitalBed", "HospitalBed", "IVStand", "FoldingScreen", "ExamTable", "Wheelchair" },          // 3층: 병실, 수술 준비
        };
        static readonly string[] AnchorNames = { "NW", "NE", "SW", "SE" };

        /// <summary>방 하나의 소품 목록 ("모델:자리"). 복도는 따로(CorridorPropSpots) 놓는다.</summary>
        static List<string> PropsFor(int floor, int room)
        {
            var list = new List<string>();
            if (TowerLayout.IsCorridorRoom(room)) return list;
            var rng = new System.Random(floor * 977 + room * 31 + 5);
            var pool = ThemePool[floor];
            int count = 3 + rng.Next(3);   // 3~5개
            Rect rr = TowerLayout.RoomRect(room);
            var names = new List<string>(AnchorNames);
            if (rr.width * rr.height >= 80f) { names.Add("NM"); names.Add("SM"); }   // 큰 방은 벽 가운데에도 놓는다
            // 문 앞은 피한다
            var doorPts = doorRequests.Where(d => d.floor == floor).Select(d => new Vector2(d.center.x, d.center.z)).Concat(Edges.Select(e => new Vector2(e.x, e.z))).ToList();
            names = names.Where(n =>
            {
                var a = ResolveAnchor(n, room);
                var wp = new Vector2(rr.center.x + a.pos.x, rr.center.y + a.pos.y);
                return !doorPts.Any(d => Vector2.Distance(d, wp) < 2.8f);
            }).ToList();
            var anchors = names.OrderBy(_ => rng.Next()).ToList();

            // 특별한 방: 정문 로비는 접수대, 간호사실은 간호사 카운터
            if (floor == TowerLayout.GroundFloor && room == TowerLayout.ExitRoom) { for (int i = 0; i < anchors.Count && i < 4; i++) list.Add(new[] { "NurseCounter", "WaitingBench", "WaitingBench", "Wheelchair" }[i] + ":" + anchors[i]); return list; }
            if (floor == TowerLayout.NurseStationFloor && room == TowerLayout.NurseStationRoom) { for (int i = 0; i < anchors.Count && i < 3; i++) list.Add(new[] { "NurseCounter", "MetalShelf", "MedicineCart" }[i] + ":" + anchors[i]); return list; }
            if (HallHasStairsInRoom(floor, room)) return list;

            for (int i = 0; i < count && i < anchors.Count; i++) list.Add(pool[rng.Next(pool.Length)] + ":" + anchors[i]);
            return list;
        }

        static bool HallHasStairsInRoom(int floor, int room) =>
            (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom) && HallHasStairs(floor, room);

        struct CorridorProp { public string model; public Vector3 pos; public float yaw; }

        /// <summary>복도 한 구간의 소품 자리: 긴 벽을 따라 5m쯤마다 번갈아 놓는다. 문과 이음매 앞은 비운다.</summary>
        static List<CorridorProp> CorridorPropSpots(int floor, int room)
        {
            var spots = new List<CorridorProp>();
            if (!TowerLayout.IsCorridorRoom(room) || HallHasStairsInRoom(floor, room)) return spots;
            Rect r = TowerLayout.RoomRect(room);
            if (r.width < 6f) return spots;   // 세로 연결 복도

            var avoid = new List<Vector2>();
            foreach (var d in doorRequests.Where(d => d.floor == floor)) avoid.Add(new Vector2(d.center.x, d.center.z));
            foreach (var e in Edges) avoid.Add(new Vector2(e.x, e.z));

            var rng = new System.Random(floor * 131 + room * 17 + 3);
            string[] models = { "WaitingBench", "Wheelchair", "IVStand", "MedicineCart", "WaitingBench", "FoldingScreen" };
            int k = 0;
            for (float x = r.xMin + 2.2f; x < r.xMax - 1.2f; x += 4.6f + (float)rng.NextDouble() * 1.8f)
            {
                bool south = (k % 2 == 0);
                float z = south ? r.yMin + 0.8f : r.yMax - 0.8f;
                var p = new Vector2(x, z);
                if (avoid.Any(a => Vector2.Distance(a, p) < 2.5f)) { k++; continue; }
                spots.Add(new CorridorProp { model = models[rng.Next(models.Length)], pos = new Vector3(x, TowerLayout.FloorY(floor), z), yaw = south ? 0f : 180f });
                k++;
            }
            return spots;
        }

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

            // 방문: 두 짝이 좌우로 갈라지는 미닫이 양문이라 짝마다 이미지 한 장
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HospitalTexDir + "/DoorRoom.png") != null)
                doorMat = TextureMaterial("HospitalDoor", HospitalTexDir + "/DoorRoom.png", new Vector2(1f, 1f), Vector2.zero, 0.2f);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HospitalTexDir + "/DoorLocked.png") != null)
                lockedDoorMat = TextureMaterial("HospitalDoorLocked", HospitalTexDir + "/DoorLocked.png", new Vector2(1f, 1f), Vector2.zero, 0.25f);
        }

        static Material WallMaterialFor(float length)
        {
            var decay = DecayWallMaterial(length);
            if (decay != null) return decay;
            if (wallShortMat == null || wallLongMat == null) return wallMat;
            return length <= 3.5f ? wallShortMat : (length <= 6.5f ? wallMat : wallLongMat);
        }

        /// <summary>모든 층의 방과 복도에 소품을 놓는다. 없는 모델은 건너뛴다 (모델이 준비되는 대로 늘어난다).</summary>
        static void BuildHospitalRooms(Transform parent)
        {
            var root = new GameObject("HospitalProps").transform;
            root.SetParent(parent);
            int placed = 0, skipped = 0;

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                {
                    foreach (var entry in PropsFor(f, room))
                    {
                        var parts = entry.Split(':');
                        if (PlaceProp(root, f, room, parts[0], parts[1])) placed++; else skipped++;
                    }
                    foreach (var cp in CorridorPropSpots(f, room))
                    {
                        if (PlacePropAt(root, f, cp.model, cp.pos, cp.yaw, null)) placed++; else skipped++;
                    }
                }
            }
            Debug.Log($"[HospitalBuilder] 소품 {placed}개 배치, 건너뜀 {skipped}개 (모델 없음)");
        }

        static bool PlaceProp(Transform root, int floor, int room, string model, string anchorName)
        {
            if (!AnchorUsable(floor, room, anchorName)) return false;
            var anchor = ResolveAnchor(anchorName, room);
            Vector3 center = TowerLayout.RoomCenter(floor, room);
            Vector3 pos = center + new Vector3(anchor.pos.x, 0f, anchor.pos.y);
            return PlacePropAt(root, floor, model, pos, anchor.faceYaw, anchor);
        }

        /// <summary>소품 하나를 pos에 놓는다 (faceYaw: 소품이 바라볼 방향). anchor가 있으면 침대에 숨는 자리를 붙인다.</summary>
        static bool PlacePropAt(Transform root, int floor, string model, Vector3 pos, float faceYaw, AnchorSpot? anchor)
        {
            if (!System.IO.File.Exists($"{PropTools.PropsDir}/{model}/{model}.fbx")) return false;
            if (!PropSpecs.TryGetValue(model, out var spec)) return false;

            var holder = new GameObject($"{model}_{TowerLayout.ShortLabel(floor)}").transform;
            holder.SetParent(root);
            holder.position = pos;

            var go = PropTools.Instantiate(model, holder, spec.height, spec.maxSide, faceYaw + spec.frontYaw);
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

            if (model == "HospitalBed" && anchor.HasValue) AddBedHidingSpot(holder, anchor.Value, TowerLayout.FloorY(floor));
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