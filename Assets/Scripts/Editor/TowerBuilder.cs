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
        enum DoorKind { Normal, Locked, Exit }

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

                WallSegment(root, new Vector3(0f, y, c), Vector3.forward, false);                 // 서쪽
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
                    req.kind == DoorKind.Exit ? exitMat : (req.kind == DoorKind.Locked ? lockedDoorMat : doorMat);

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

                foreach (var o in slots)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Container";
                    box.transform.SetParent(root);
                    box.transform.position = c + new Vector3(o.x, 0.45f, o.y);
                    box.transform.localScale = new Vector3(1.0f, 0.9f, 0.6f);
                    box.GetComponent<Renderer>().sharedMaterial = woodMat;
                    var cont = box.AddComponent<Container>();
                    cont.floor = floor;
                    cont.room = room;
                    EditorUtility.SetDirty(cont);
                }

                // 옷장 (숨는 곳): 계단방이 아닌 방 중 절반 정도
                bool wardrobe = !stairRoom && ((room + floor) % 2 == 0);
                if (wardrobe) BuildWardrobe(root, c);
            }
        }

        static void BuildWardrobe(Transform parent, Vector3 roomCenter)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Wardrobe";
            w.transform.SetParent(parent);
            w.transform.position = roomCenter + new Vector3(WardrobeSlot.x, 1.1f, WardrobeSlot.y);
            w.transform.localScale = new Vector3(0.7f, 2.2f, 1.0f);
            w.GetComponent<Renderer>().sharedMaterial = wardrobeMat;

            var hide = new GameObject("HidePoint").transform;
            hide.SetParent(w.transform);
            hide.position = new Vector3(w.transform.position.x, roomCenter.y + 0.1f, w.transform.position.z);
            hide.rotation = Quaternion.Euler(0f, -90f, 0f);   // 방 안쪽(서쪽)을 본다

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(w.transform);
            exit.position = new Vector3(w.transform.position.x - 1.4f, roomCenter.y + 0.1f, w.transform.position.z);

            var spot = w.AddComponent<HidingSpot>();
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
