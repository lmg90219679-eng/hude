using System.Collections.Generic;
using System.Linq;
using Escape.Audio;
using Escape.Core;
using Escape.Enemy;
using Escape.Interaction;
using Escape.Items;
using Escape.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// 5개 층 건물(지하2, 지하1, 1층, 2층, 3층)을 만든다. 메뉴: Escape > Build Tower Scene
    ///
    /// 층마다: 3x3 방(각 8m), 방 사이 12개 통로.
    /// - 서쪽 계단방(0,1) - 가운데 방(1,1) - 동쪽 계단방(2,1)은 열쇠 없이 이어진다 (어느 계단으로 와도 막히지 않게).
    /// - 나머지 6개 방은 "잠긴 문"으로 이어진다. 문마다 전용 방 열쇠가 있고, 열쇠는 문을 열기 전에 갈 수 있는 방에 숨는다.
    /// - 열리는 순서(트리)는 빌드 때 무작위로 정하고, 열쇠 위치는 매 판 무작위다 (TowerSpawner).
    /// - 계단은 서쪽/동쪽 두 곳. 각 계단통에 올라가는 계단과 내려가는 계단이 하나씩 있다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        enum DoorKind { Normal, Locked, Exit, Secret }

        struct DoorRequest
        {
            public Vector3 center;
            public Vector3 along;
            public DoorKind kind;
            public int floor;
            public int roomA, roomB;
            public string keyId;
            public int treeOrder;
        }

        static readonly List<DoorRequest> doorRequests = new List<DoorRequest>();

        // 방 안 배치 슬롯 (방 중심 기준 x, z)
        static readonly Vector2[] ContainerSlots = { new Vector2(-3f, 3.3f), new Vector2(3f, 3.3f), new Vector2(-3.3f, -3f) };
        static readonly Vector2[] StairRoomContainerSlots = { new Vector2(-3.3f, -3f), new Vector2(3.3f, -3f) };
        static readonly Vector2 WardrobeSlot = new Vector2(3.4f, -2.9f);
        static readonly Vector2 UpStairSlot = new Vector2(-2.4f, 3.45f);
        static readonly Vector2 DownStairSlot = new Vector2(2.4f, 3.45f);

        class FloorPlan
        {
            public readonly List<(int a, int b, bool isVertical, int line, int index)> Edges =
                new List<(int, int, bool, int, int)>();
            public readonly Dictionary<(bool v, int line, int index), DoorKind?> Type =
                new Dictionary<(bool, int, int), DoorKind?>();   // null = 막힌 벽
            public readonly List<(bool v, int line, int index, int newRoom)> Tree =
                new List<(bool, int, int, int)>();
        }

        public static void Build()
        {
            cachedSkin = null;
            cachedController = null;
            doorRequests.Clear();

            SetupChaserImport();
            BuildMenuScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureMaterials();
            SetupAmbient();

            var rng = new System.Random(20260101);   // 건물 구조는 고정 (열쇠와 아이템 위치만 매 판 무작위)
            var map = new GameObject("Tower");

            var plans = new FloorPlan[TowerLayout.Floors];
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                plans[f] = MakePlan(rng);
                BuildFloorGeometry(map.transform, f, plans[f]);
            }

            BuildElectricalRoomGeometry(map.transform);
            BuildNavMeshForTower(map);

            // 플레이어: 1층 서쪽 계단방에서 시작
            var player = BuildPlayer(TowerLayout.RoomCenter(TowerLayout.GroundFloor, TowerLayout.StartRoom));

            // 층마다 추적자 1명
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                var wps = BuildFloorWaypoints(f, rng);
                int startRoom = f == TowerLayout.GroundFloor ? 8 : (f % 2 == 0 ? 8 : 6);
                BuildChaser(player.transform, wps, TowerLayout.RoomCenter(f, startRoom), $"Chaser_{TowerLayout.ShortLabel(f)}");
            }

            // 문, 가구, 계단은 NavMesh를 굽고 나서 만든다 (닫힌 문이 길을 막지 않게)
            BuildDoors(map.transform);
            for (int f = 0; f < TowerLayout.Floors; f++) BuildFurniture(map.transform, f);
            BuildStairs(map.transform);
            BuildPowerBoxOnGround(map.transform);
            BuildElectricalRoomProps(map.transform);
            BuildBasementLights(map.transform);
            new GameObject("PowerGrid").AddComponent<Escape.Core.PowerGrid>();
            new GameObject("PowerEffects").AddComponent<Escape.Core.PowerEffects>();

            var itemSettings = new GameObject("ItemVisualSettings").AddComponent<ItemVisualSettings>();
            itemSettings.baseMaterial = floorMat;
            EditorUtility.SetDirty(itemSettings);
            new GameObject("TowerSpawner").AddComponent<TowerSpawner>();
            new GameObject("MapTracker").AddComponent<MapTracker>();
            new GameObject("ChaserDirector").AddComponent<ChaserDirector>();
            new GameObject("GameFlow").AddComponent<GameFlow>();
            new GameObject("HUD").AddComponent<HUD>();
            new GameObject("GameAudio").AddComponent<GameAudio>();
            BuildPostProcessing();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes(MenuScenePath, ScenePath);
            Debug.Log($"[TowerBuilder] 씬 생성 완료: {ScenePath} (잠긴 문 {doorRequests.Count(d => d.kind == DoorKind.Locked)}개)");
        }

        // ---------- 층 설계 (방 연결) ----------

        static IEnumerable<(int a, int b, bool v, int line, int index)> AllEdges()
        {
            // 세로 벽 x = line*Cell (line 1..2), 행 j: (line-1, j) - (line, j)
            for (int line = 1; line < TowerLayout.Grid; line++)
                for (int j = 0; j < TowerLayout.Grid; j++)
                    yield return (TowerLayout.RoomIndex(line - 1, j), TowerLayout.RoomIndex(line, j), true, line, j);
            // 가로 벽 z = line*Cell (line 1..2), 열 i: (i, line-1) - (i, line)
            for (int line = 1; line < TowerLayout.Grid; line++)
                for (int i = 0; i < TowerLayout.Grid; i++)
                    yield return (TowerLayout.RoomIndex(i, line - 1), TowerLayout.RoomIndex(i, line), false, line, i);
        }

        static FloorPlan MakePlan(System.Random rng)
        {
            var plan = new FloorPlan();
            var all = AllEdges().ToList();

            var reach = new HashSet<int> { TowerLayout.WestStairRoom, TowerLayout.CenterRoom, TowerLayout.EastStairRoom };
            var used = new HashSet<(bool, int, int)>();

            // 서쪽 - 가운데 - 동쪽은 열쇠 없이 이어지는 일반 문
            foreach (var e in all)
            {
                bool wc = (e.a == TowerLayout.WestStairRoom && e.b == TowerLayout.CenterRoom);
                bool ce = (e.a == TowerLayout.CenterRoom && e.b == TowerLayout.EastStairRoom);
                if (wc || ce)
                {
                    plan.Type[(e.v, e.line, e.index)] = DoorKind.Normal;
                    used.Add((e.v, e.line, e.index));
                }
            }

            // 잠긴 문으로 나머지 6개 방을 하나씩 연결 (트리). 어떤 방이든 그 전 방들에서 열쇠를 구해 열 수 있다.
            while (reach.Count < TowerLayout.RoomsPerFloor)
            {
                var candidates = all.Where(e => !used.Contains((e.v, e.line, e.index)) &&
                                                (reach.Contains(e.a) ^ reach.Contains(e.b))).ToList();
                var pick = candidates[rng.Next(candidates.Count)];
                int newRoom = reach.Contains(pick.a) ? pick.b : pick.a;
                plan.Type[(pick.v, pick.line, pick.index)] = DoorKind.Locked;
                plan.Tree.Add((pick.v, pick.line, pick.index, newRoom));
                used.Add((pick.v, pick.line, pick.index));
                reach.Add(newRoom);
            }

            // 순환 통로 3개: 열쇠 없이 지나는 일반 문 (막다른 길만 있으면 쫓길 때 갇힌다)
            var rest = all.Where(e => !used.Contains((e.v, e.line, e.index))).OrderBy(_ => rng.Next()).ToList();
            for (int k = 0; k < rest.Count; k++)
                plan.Type[(rest[k].v, rest[k].line, rest[k].index)] = k < 3 ? DoorKind.Normal : (DoorKind?)null;

            return plan;
        }

        // ---------- 층 기하 구조 ----------

        static void BuildFloorGeometry(Transform parent, int floor, FloorPlan plan)
        {
            float y = TowerLayout.FloorY(floor);
            string label = TowerLayout.ShortLabel(floor);
            var root = new GameObject($"Floor_{label}").transform;
            root.SetParent(parent);

            float size = Cell * Grid;

            // 바닥판 (두께 1m: 아래층의 천장도 겸한다)
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(root);
            slab.transform.position = new Vector3(size * 0.5f, y - 0.5f, size * 0.5f);
            slab.transform.localScale = new Vector3(size, 1f, size);
            slab.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 꼭대기 층은 지붕이 필요하다
            if (floor == TowerLayout.Floors - 1)
            {
                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "Roof";
                roof.transform.SetParent(root);
                roof.transform.position = new Vector3(size * 0.5f, y + WallHeight + 0.5f, size * 0.5f);
                roof.transform.localScale = new Vector3(size, 1f, size);
                roof.GetComponent<Renderer>().sharedMaterial = wallMat;
            }

            // 바깥벽
            for (int k = 0; k < Grid; k++)
            {
                float c = k * Cell + Cell * 0.5f;
                bool exitHere = floor == TowerLayout.GroundFloor && k == 1;   // 남쪽 바깥벽의 가운데 방 = 1층 정문

                // 3층 서쪽 벽의 남쪽 방(0,0)에는 전기실로 통하는 숨겨진 문이 있다 (벽처럼 보인다)
                bool secretHere = floor == TowerLayout.Floors - 1 && k == 0;
                WallSegment(root, new Vector3(0f, y, c), Vector3.forward, secretHere);            // 서쪽
                if (secretHere)
                {
                    doorRequests.Add(new DoorRequest
                    {
                        center = new Vector3(0f, y, c), along = Vector3.forward, kind = DoorKind.Secret,
                        floor = floor, roomA = 0, roomB = 0, treeOrder = -1,
                    });
                }
                WallSegment(root, new Vector3(size, y, c), Vector3.forward, false);               // 동쪽
                WallSegment(root, new Vector3(c, y, size), Vector3.right, false);                 // 북쪽
                WallSegment(root, new Vector3(c, y, 0f), Vector3.right, exitHere);                // 남쪽
                if (exitHere)
                {
                    doorRequests.Add(new DoorRequest
                    {
                        center = new Vector3(c, y, 0f), along = Vector3.right, kind = DoorKind.Exit,
                        floor = floor, roomA = TowerLayout.ExitRoom, roomB = TowerLayout.ExitRoom, treeOrder = -1,
                    });
                }
            }

            // 안쪽 벽 12곳
            foreach (var e in AllEdges())
            {
                plan.Type.TryGetValue((e.v, e.line, e.index), out DoorKind? kind);

                Vector3 center, along;
                if (e.v)
                {
                    center = new Vector3(e.line * Cell, y, e.index * Cell + Cell * 0.5f);
                    along = Vector3.forward;
                }
                else
                {
                    center = new Vector3(e.index * Cell + Cell * 0.5f, y, e.line * Cell);
                    along = Vector3.right;
                }

                if (kind == null)
                {
                    WallSegment(root, center, along, false);   // 막힌 벽
                    continue;
                }

                WallSegment(root, center, along, true);        // 문틈이 있는 벽

                int treeIndex = plan.Tree.FindIndex(t => t.v == e.v && t.line == e.line && t.index == e.index);
                int roomB = e.b;
                if (treeIndex >= 0) roomB = plan.Tree[treeIndex].newRoom;
                int roomA = roomB == e.b ? e.a : e.b;

                doorRequests.Add(new DoorRequest
                {
                    center = center, along = along, kind = kind.Value,
                    floor = floor, roomA = roomA, roomB = roomB,
                    keyId = kind == DoorKind.Locked ? $"{label}-{treeIndex + 1}" : null,
                    treeOrder = treeIndex,
                });
            }
        }

        static Transform[] BuildFloorWaypoints(int floor, System.Random rng)
        {
            var root = new GameObject($"Patrol_{TowerLayout.ShortLabel(floor)}").transform;
            var rooms = Enumerable.Range(0, TowerLayout.RoomsPerFloor).OrderBy(_ => rng.Next()).ToList();
            var list = new List<Transform>();
            for (int n = 0; n < rooms.Count; n++)
            {
                var p = new GameObject($"WP_{n}").transform;
                p.SetParent(root);
                p.position = TowerLayout.RoomCenter(floor, rooms[n]);
                list.Add(p);
            }
            return list.ToArray();
        }

        static void BuildNavMeshForTower(GameObject map)
        {
            BuildNavMesh(map);
        }

        // ---------- 문 ----------

        static void BuildDoors(Transform parent)
        {
            foreach (var req in doorRequests)
            {
                string label = TowerLayout.ShortLabel(req.floor);
                var root = new GameObject($"{req.kind}Door_{label}_{req.roomA}-{req.roomB}");
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
                panel.GetComponent<Renderer>().sharedMaterial =
                    req.kind == DoorKind.Exit ? exitMat : req.kind == DoorKind.Locked ? lockedDoorMat : req.kind == DoorKind.Secret ? wallMat : doorMat;

                if (req.kind == DoorKind.Exit)
                {
                    root.AddComponent<ExitDoor>();
                    continue;
                }

                var door = root.AddComponent<Door>();
                var so = new SerializedObject(door);
                so.FindProperty("hinge").objectReferenceValue = hinge;
                so.ApplyModifiedPropertiesWithoutUndo();

                door.floor = req.floor;
                door.roomA = req.roomA;
                door.roomB = req.roomB;
                door.lockKeyId = req.keyId;
                door.treeOrder = req.treeOrder;
                door.secret = req.kind == DoorKind.Secret;
                EditorUtility.SetDirty(door);
            }
        }

        // ---------- 방 안 가구 ----------

        static bool IsStairRoom(int room) =>
            room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom;

        static void BuildFurniture(Transform parent, int floor)
        {
            var root = new GameObject($"Furniture_{TowerLayout.ShortLabel(floor)}").transform;
            root.SetParent(parent);

            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                Vector3 c = TowerLayout.RoomCenter(floor, room);
                bool stairRoom = IsStairRoom(room);
                var slots = stairRoom ? StairRoomContainerSlots : ContainerSlots;

                for (int s = 0; s < slots.Length; s++)
                {
                    // 간호사실의 첫 서랍장은 직원 출입카드가 든 금속 서랍장
                    bool metal = floor == TowerLayout.NurseStationFloor && room == TowerLayout.NurseStationRoom && s == 0;
                    BuildContainer(root, floor, room, c, slots[s], metal);
                }

                // 옷장 (숨는 곳): 계단방이 아닌 방 중 절반 정도
                bool wardrobe = !stairRoom && ((room + floor) % 2 == 0);
                if (wardrobe) BuildWardrobe(root, c);
            }
        }

        /// <summary>
        /// 서랍장 하나: 몸통 + 밀려 나오는 서랍 + 물건이 놓이는 자리. 방 중심을 향해 서랍이 열린다.
        /// 지금은 기본 도형이고, 나중에 Varco 모델로 바꿀 때도 이 구조(Body, Drawer, ItemAnchor)를 유지한다.
        /// </summary>
        const string CabinetPrefabPath = "Assets/Prefabs/Cabinet.prefab";
        static Material metalCabinetTint;

        static Container BuildContainer(Transform parent, int floor, int room, Vector3 roomCenter, Vector2 slot, bool metal)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CabinetPrefabPath);
            if (prefab != null) return BuildModelContainer(prefab, parent, floor, room, roomCenter, slot, metal);
            return BuildPrimitiveContainer(parent, floor, room, roomCenter, slot, metal);
        }

        static Quaternion FaceRoomCenter(Vector3 position, Vector3 roomCenter)
        {
            Vector3 toCenter = roomCenter - position;
            toCenter.y = 0f;
            Vector3 face = Mathf.Abs(toCenter.x) > Mathf.Abs(toCenter.z)
                ? new Vector3(Mathf.Sign(toCenter.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(toCenter.z));
            return Quaternion.LookRotation(face);
        }

        /// <summary>Varco 서랍장 모델(Body, Drawer, ItemAnchor)로 서랍장을 만든다. 방 중심을 향해 서랍이 열린다.</summary>
        static Container BuildModelContainer(GameObject prefab, Transform parent, int floor, int room, Vector3 roomCenter, Vector2 slot, bool metal)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = metal ? "MetalCabinet" : "Cabinet";
            go.transform.SetParent(parent);
            go.transform.position = roomCenter + new Vector3(slot.x, 0f, slot.y);
            go.transform.rotation = FaceRoomCenter(go.transform.position, roomCenter);

            if (metal)
            {
                // 간호사실 금속 서랍장: 차갑고 푸르스름하게 눌러서 구분한다
                if (metalCabinetTint == null)
                {
                    var src = go.GetComponentInChildren<Renderer>().sharedMaterial;
                    metalCabinetTint = new Material(src) { name = "CabinetMetalTint" };
                    metalCabinetTint.SetColor("_BaseColor", new Color(0.55f, 0.65f, 0.8f));
                    AssetDatabase.CreateAsset(metalCabinetTint, "Assets/Materials/CabinetMetalTint.mat");
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = metalCabinetTint;
            }

            var cont = go.AddComponent<Container>();
            var so = new SerializedObject(cont);
            so.FindProperty("drawer").objectReferenceValue = go.transform.Find("Drawer");
            so.FindProperty("itemAnchor").objectReferenceValue = go.transform.Find("ItemAnchor");
            so.ApplyModifiedPropertiesWithoutUndo();
            cont.floor = floor;
            cont.room = room;
            cont.metalDrawer = metal;
            EditorUtility.SetDirty(cont);
            return cont;
        }

        /// <summary>
        /// 서랍장 하나: 몸통 + 밀려 나오는 서랍 + 물건이 놓이는 자리. (모델이 없을 때 쓰는 기본 도형 버전)
        /// </summary>
        static Container BuildPrimitiveContainer(Transform parent, int floor, int room, Vector3 roomCenter, Vector2 slot, bool metal)
        {
            var root = new GameObject(metal ? "MetalCabinet" : "Cabinet").transform;
            root.SetParent(parent);
            root.position = roomCenter + new Vector3(slot.x, 0f, slot.y);
            root.rotation = FaceRoomCenter(root.position, roomCenter);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(1.0f, 0.9f, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = metal ? metalMat : woodMat;

            var drawer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drawer.name = "Drawer";
            drawer.transform.SetParent(root, false);
            drawer.transform.localPosition = new Vector3(0f, 0.62f, 0.06f);
            drawer.transform.localScale = new Vector3(0.86f, 0.25f, 0.5f);
            drawer.GetComponent<Renderer>().sharedMaterial = metal ? metalDrawerMat : drawerMat;
            Object.DestroyImmediate(drawer.GetComponent<Collider>());

            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, 0.76f, 0.06f);

            var cont = root.gameObject.AddComponent<Container>();
            var so = new SerializedObject(cont);
            so.FindProperty("drawer").objectReferenceValue = drawer.transform;
            so.FindProperty("itemAnchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            cont.floor = floor;
            cont.room = room;
            cont.metalDrawer = metal;
            EditorUtility.SetDirty(cont);
            return cont;
        }
        static void BuildWardrobe(Transform parent, Vector3 roomCenter)
        {
            // 숨는 곳: 철제 로커 (Varco 모델). 문이 방 안쪽(서쪽)을 향한다.
            var holder = new GameObject("Locker").transform;
            holder.SetParent(parent);
            holder.position = roomCenter + new Vector3(WardrobeSlot.x, 0f, WardrobeSlot.y);

            var model = Escape.EditorTools.PropTools.Instantiate("SteelLocker", holder, 1.9f, 0f, yaw: -90f);
            if (model != null)
            {
                var lb = Escape.EditorTools.PropTools.Bounds(model);
                var col = holder.gameObject.AddComponent<BoxCollider>();
                col.center = holder.InverseTransformPoint(lb.center);
                col.size = lb.size;
            }
            else
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = "Wardrobe";
                w.transform.SetParent(holder, false);
                w.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                w.transform.localScale = new Vector3(0.7f, 2.2f, 1.0f);
                w.GetComponent<Renderer>().sharedMaterial = wardrobeMat;
            }

            var hide = new GameObject("HidePoint").transform;
            hide.SetParent(holder);
            hide.position = new Vector3(holder.position.x, roomCenter.y + 0.1f, holder.position.z);
            hide.rotation = Quaternion.Euler(0f, -90f, 0f);   // 방 안쪽(서쪽)을 본다

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(holder);
            exit.position = new Vector3(holder.position.x - 1.2f, roomCenter.y + 0.1f, holder.position.z);

            var spot = holder.gameObject.AddComponent<HidingSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("hidePoint").objectReferenceValue = hide;
            so.FindProperty("exitPoint").objectReferenceValue = exit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // ---------- 계단 ----------

        /// <summary>
        /// 각 층의 서쪽/동쪽 계단방에 올라가는 계단(위쪽 층이 있을 때)과 내려가는 계단(아래층이 있을 때)을 둔다.
        /// 같은 계단통(서쪽끼리, 동쪽끼리)의 위/아래 계단이 서로 짝이다.
        /// </summary>
        static void BuildStairs(Transform parent)
        {
            var up = new Dictionary<(int floor, int room), StairPortal>();
            var down = new Dictionary<(int floor, int room), StairPortal>();

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                foreach (int room in new[] { TowerLayout.WestStairRoom, TowerLayout.EastStairRoom })
                {
                    if (f < TowerLayout.Floors - 1) up[(f, room)] = MakePortal(parent, f, room, true);
                    if (f > 0) down[(f, room)] = MakePortal(parent, f, room, false);
                }
            }

            foreach (var kv in up)
            {
                var (f, room) = kv.Key;
                var upper = down[(f + 1, room)];
                kv.Value.partner = upper;
                upper.partner = kv.Value;
                EditorUtility.SetDirty(kv.Value);
                EditorUtility.SetDirty(upper);
            }
        }

        static StairPortal MakePortal(Transform parent, int floor, int room, bool up)
        {
            Vector3 c = TowerLayout.RoomCenter(floor, room);
            Vector2 slot = up ? UpStairSlot : DownStairSlot;
            string label = up ? "Up" : "Down";

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Stair{label}_{TowerLayout.ShortLabel(floor)}_{room}";
            go.transform.SetParent(parent);
            go.transform.position = c + new Vector3(slot.x, 1.2f, slot.y);
            go.transform.localScale = new Vector3(1.6f, 2.4f, 0.3f);
            go.GetComponent<Renderer>().sharedMaterial = up ? stairUpMat : stairDownMat;

            // 도착했을 때 서는 자리: 계단 앞 (방 안쪽, 남쪽을 향해)
            var exit = new GameObject("ArrivalPoint").transform;
            exit.SetParent(go.transform);
            exit.position = new Vector3(go.transform.position.x, c.y + 0.1f, go.transform.position.z - 1.6f);
            exit.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 어둠 속에서 찾을 수 있게 은은한 빛 (올라가는 계단은 초록, 내려가는 계단은 붉은 기운)
            var lg = new GameObject("Glow").AddComponent<Light>();
            lg.transform.SetParent(go.transform);
            lg.transform.position = go.transform.position + new Vector3(0f, 0.4f, -0.8f);
            lg.type = LightType.Point;
            lg.range = 3.2f;
            lg.intensity = 0.9f;
            lg.color = up ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.35f, 0.3f);

            var portal = go.AddComponent<StairPortal>();
            portal.floor = floor;
            portal.room = room;
            portal.up = up;
            portal.exitPoint = exit;
            EditorUtility.SetDirty(portal);
            return portal;
        }

        // ---------- 3층 숨겨진 전기실 ----------

        const string BreakerPanelPrefabPath = "Assets/Prefabs/BreakerPanel.prefab";
        // 전기실: 3층 서쪽 건물 밖, x 0 ~ -6, z 0 ~ 8. 입구는 방 (0,0)의 서쪽 벽(숨겨진 문).
        const float ElecWidth = 6f;

        static void BuildElectricalRoomGeometry(Transform parent)
        {
            int floor = TowerLayout.Floors - 1;
            float y = TowerLayout.FloorY(floor);
            var root = new GameObject("ElectricalRoom").transform;
            root.SetParent(parent);

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(root);
            slab.transform.position = new Vector3(-ElecWidth * 0.5f, y - 0.5f, Cell * 0.5f);
            slab.transform.localScale = new Vector3(ElecWidth, 1f, Cell);
            slab.GetComponent<Renderer>().sharedMaterial = floorMat;

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(root);
            roof.transform.position = new Vector3(-ElecWidth * 0.5f, y + WallHeight + 0.5f, Cell * 0.5f);
            roof.transform.localScale = new Vector3(ElecWidth, 1f, Cell);
            roof.GetComponent<Renderer>().sharedMaterial = wallMat;

            // 서쪽 벽, 북쪽 벽, 남쪽 벽 (동쪽은 건물의 바깥벽이 문틈과 함께 이미 있다)
            MakeWall(root, new Vector3(-ElecWidth, y, Cell * 0.5f), Vector3.forward, Cell);
            MakeWall(root, new Vector3(-ElecWidth * 0.5f, y, Cell), Vector3.right, ElecWidth);
            MakeWall(root, new Vector3(-ElecWidth * 0.5f, y, 0f), Vector3.right, ElecWidth);
        }

        static void BuildElectricalRoomProps(Transform parent)
        {
            int floor = TowerLayout.Floors - 1;
            float y = TowerLayout.FloorY(floor);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakerPanelPrefabPath)
                         ?? Escape.EditorTools.PropTools.BuildBreakerPanelPrefab(BreakerPanelPrefabPath);

            var root = new GameObject("BreakerPanelInstance").transform;
            root.SetParent(parent);
            // 서쪽 벽에 붙이고(앞면이 동쪽을 향한다), 바닥에서 1m 위에 건다
            root.position = new Vector3(-ElecWidth + 0.16f + 0.26f, y + 1.0f, Cell * 0.5f);

            GameObject inst = null;
            if (prefab != null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(root, false);
                inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            if (inst != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var pivot = inst.transform.Find("LeverPivot" + i);
                    if (pivot == null) continue;

                    // 레버가 젖혀져도 움직이지 않는 판정용 충돌체 (BreakerLever 컴포넌트가 여기에 붙는다)
                    var hit = new GameObject("LeverHit" + i);
                    hit.transform.SetParent(inst.transform, false);
                    hit.transform.position = pivot.position + inst.transform.forward * 0.12f;
                    hit.transform.rotation = inst.transform.rotation;
                    var box = hit.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.16f, 0.42f, 0.34f);
                    box.isTrigger = true;

                    var lever = hit.AddComponent<BreakerLever>();
                    lever.index = i;
                    var so = new SerializedObject(lever);
                    so.FindProperty("pivot").objectReferenceValue = pivot;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(lever);
                }
            }
            else
            {
                Debug.LogWarning("[TowerBuilder] 차단기 패널 모델이 없어 전기실을 비워 둔다.");
            }

            // 비상등: 어둠 속에서 전기실을 찾을 수 있게 붉은 불빛 하나
            var lg = new GameObject("EmergencyLamp").AddComponent<Light>();
            lg.transform.SetParent(parent);
            lg.transform.position = new Vector3(-ElecWidth * 0.5f, y + 2.7f, Cell * 0.5f);
            lg.type = LightType.Point;
            lg.range = 7f;
            lg.intensity = 0.8f;
            lg.color = new Color(1f, 0.25f, 0.2f);
        }

        // ---------- 지하 조명 (첫 번째 차단기가 올라가면 미세하게 켜진다) ----------

        static void BuildBasementLights(Transform parent)
        {
            var root = new GameObject("BasementLights").transform;
            root.SetParent(parent);
            for (int f = 0; f <= 1; f++)   // 지하2, 지하1
            {
                for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                {
                    var go = new GameObject($"PowerLight_{TowerLayout.ShortLabel(f)}_{room}");
                    go.transform.SetParent(root);
                    go.transform.position = TowerLayout.RoomCenter(f, room) + Vector3.up * 2.6f;
                    var lg = go.AddComponent<Light>();
                    lg.type = LightType.Point;
                    lg.range = 7f;
                    lg.color = new Color(0.75f, 0.82f, 1f);
                    lg.shadows = LightShadows.None;
                    go.AddComponent<Escape.Core.PowerLight>();
                }
            }
        }

        // ---------- 1층 전원함 ----------

        static void BuildPowerBoxOnGround(Transform parent)
        {
            int f = TowerLayout.GroundFloor;
            Vector3 c = TowerLayout.RoomCenter(f, TowerLayout.ExitRoom);

            var lightObj = new GameObject("ExitRoomLight");
            lightObj.transform.SetParent(parent);
            lightObj.transform.position = c + Vector3.up * 2.6f;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.enabled = false;

            // 정문이 있는 방의 서쪽 벽에 붙인다 (문과 가구를 피해 방 중심에서 동쪽)
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "PowerBox";
            box.transform.SetParent(parent);
            box.transform.position = c + new Vector3(3.7f, 1.5f, 1.8f);
            box.transform.localScale = new Vector3(0.3f, 0.8f, 0.6f);
            box.GetComponent<Renderer>().sharedMaterial = boxMat;

            var pb = box.AddComponent<PowerBox>();
            var so = new SerializedObject(pb);
            var arr = so.FindProperty("lightsToEnable");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
