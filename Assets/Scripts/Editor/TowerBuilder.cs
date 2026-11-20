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
    /// 층마다 폐병원 평면 (TowerLayout 참고): 가운데 4m 폭 복도, 남쪽/북쪽 병동의 방들은 크기가 제각각.
    /// - 복도 양 끝은 방화문 너머의 계단 홀 (서쪽/동쪽). 계단은 거기까지 가야 보인다.
    /// - 시작 방(북쪽 가운데 방), 복도, 계단 홀은 열쇠 없이 이어진다 (어느 계단으로 와도 막히지 않게).
    /// - 나머지 5개 방은 "잠긴 문"으로 이어진다. 문마다 전용 방 열쇠가 있고, 열쇠는 문을 열기 전에 갈 수 있는 방에 숨는다.
    /// - 열리는 순서(트리)는 빌드 때 무작위로 정하고, 열쇠 위치는 매 판 무작위다 (TowerSpawner).
    /// - 계단 홀마다 올라가는 계단과 내려가는 계단이 하나씩 있다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        enum DoorKind { Normal, Locked, Exit, Secret, Stair }

        struct DoorRequest
        {
            public Vector3 center;
            public Vector3 along;
            public DoorKind kind;
            public int floor;
            public int roomA, roomB;
            public string keyId;
            public int treeOrder;
            public bool requiresPower;
            public float width;        // 0이면 기본 문 너비
            public float openAngle;    // 여닫이 문(계단 비상구)의 열리는 각도 (부호로 방향)
        }

        static readonly List<DoorRequest> doorRequests = new List<DoorRequest>();

        // 방 사이 통로는 TowerLayout이 생성한다. (문의 중심 좌표, 벽 방향, 뚫린 이음매 여부)
        static IReadOnlyList<TowerLayout.DoorEdge> Edges => TowerLayout.Edges;

        const float StairDoorWidth = 1.8f;
        static int EdgeStairWest => FindStairEdge(TowerLayout.WestStairRoom);
        static int EdgeStairEast => FindStairEdge(TowerLayout.EastStairRoom);
        static int EdgeStartRoom
        {
            get
            {
                // 시작 방의 문 하나는 복도와 열쇠 없이 이어진다
                for (int i = 0; i < Edges.Count; i++)
                    if (!Edges[i].open && (Edges[i].a == TowerLayout.StartRoom || Edges[i].b == TowerLayout.StartRoom)
                        && TowerLayout.IsCorridorRoom(Edges[i].a == TowerLayout.StartRoom ? Edges[i].b : Edges[i].a)) return i;
                return -1;
            }
        }

        static int FindStairEdge(int hall)
        {
            for (int i = 0; i < Edges.Count; i++) if (Edges[i].stair && (Edges[i].a == hall || Edges[i].b == hall)) return i;
            return -1;
        }

        const int LockedPerFloor = 10;
        const int LoopsPerFloor = 70;

        class FloorPlan
        {
            public readonly Dictionary<int, DoorKind?> Type = new Dictionary<int, DoorKind?>();   // null = 막힌 벽
            public readonly List<(int edge, int newRoom)> Locked = new List<(int, int)>();        // 잠긴 문 (열리는 순서)
        }

        public static void Build()
        {
            cachedSkin = null;
            cachedController = null;
            doorRequests.Clear();
            debrisSpots.Clear();
            decayCache.Clear();
            stairStepMat = stairWallMat = stairRailMat = null;

            SetupChaserImport();
            BuildMenuScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureMaterials();
            EnsureHospitalMaterials();
            SetupAmbient();

            var rng = new System.Random(20260101);   // 건물 구조는 고정 (열쇠와 아이템 위치만 매 판 무작위)
            var map = new GameObject("Tower");

            var plans = new FloorPlan[TowerLayout.Floors];
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                plans[f] = MakePlan(rng);
                wallFloor = f;
                BuildFloorGeometry(map.transform, f, plans[f]);
                BuildDecayShell(map.transform, f);
            }

            BuildElectricalRoomGeometry(map.transform);
            BuildElevatorGeometry(map.transform);
            BuildStairwells(map.transform);

            // 가구와 소품은 NavMesh를 굽기 전에 놓는다 (소품 자리는 걸을 수 없는 구역으로 표시돼 길이 비켜 간다)
            for (int f = 0; f < TowerLayout.Floors; f++) BuildFurniture(map.transform, f);
            BuildHospitalRooms(map.transform);
            BuildDecayProps(map.transform);
            BuildNavMeshForTower(map);

            // 플레이어: 1층 북쪽 가운데 방(상담실)에서 시작. 계단은 복도 양 끝 방화문 너머에 있어서 직접 찾아야 한다
            var player = BuildPlayer(TowerLayout.RoomCenter(TowerLayout.GroundFloor, TowerLayout.StartRoom) + new Vector3(0f, 0f, 1.5f));

            // 층마다 추적자 2명 (건물이 크다)
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                for (int k = 0; k < 2; k++)
                {
                    var wps = BuildFloorWaypoints(f, rng);
                    int startRoom = k == 0 ? TowerLayout.ChaserStartRooms[f] : TowerLayout.ChaserStartRooms[(f + 2) % TowerLayout.Floors];
                    BuildChaser(player.transform, wps, TowerLayout.RoomCenter(f, startRoom), $"Chaser_{TowerLayout.ShortLabel(f)}_{k}");
                }
            }

            // 문과 계단 입구는 NavMesh를 굽고 나서 만든다 (닫힌 문이 길을 막지 않게)
            BuildDoors(map.transform);
            BuildStairs(map.transform);
            BuildPowerBoxOnGround(map.transform);
            BuildElectricalRoomProps(map.transform);
            BuildElevatorProps(map.transform);
            BuildRoomLights(map.transform);
            new GameObject("PowerGrid").AddComponent<Escape.Core.PowerGrid>();
            new GameObject("PowerEffects").AddComponent<Escape.Core.PowerEffects>();

            var itemSettings = new GameObject("ItemVisualSettings").AddComponent<ItemVisualSettings>();
            itemSettings.baseMaterial = floorMat;
            EditorUtility.SetDirty(itemSettings);
            new GameObject("FloorVisibility").AddComponent<Escape.Core.FloorVisibility>();
            MarkStaticBatching(map.transform);
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

        /// <summary>움직이지 않는 건물 부품(벽, 바닥, 천장, 소품, 계단통)을 정적 배칭 대상으로 표시한다. 문, 서랍장, 승강기는 움직이므로 뺀다.</summary>
        static void MarkStaticBatching(Transform map)
        {
            string[] prefixes = { "Floor_", "Decay_", "WallSkins_", "DecayProps", "HospitalProps", "Stairwells", "ElectricalRoom" };
            int count = 0;
            foreach (Transform child in map)
            {
                if (!prefixes.Any(p => child.name.StartsWith(p))) continue;
                foreach (var t in child.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    count++;
                }
            }
            Debug.Log($"[TowerBuilder] 정적 배칭 표시 {count}개");
        }

        // ---------- 층 설계 (방 연결) ----------

        static FloorPlan MakePlan(System.Random rng)
        {
            var plan = new FloorPlan();

            // 열쇠 없이 갈 수 있는 곳: 모든 복도(계단 홀 포함)와 시작 방
            var reach = new HashSet<int> { TowerLayout.StartRoom };
            for (int r = 0; r < TowerLayout.RoomsPerFloor; r++) if (TowerLayout.IsCorridorRoom(r)) reach.Add(r);

            var used = new HashSet<int>();
            for (int e = 0; e < Edges.Count; e++) if (Edges[e].open || Edges[e].stair) used.Add(e);
            foreach (int e in new[] { EdgeStairWest, EdgeStairEast, EdgeStartRoom })
            {
                plan.Type[e] = DoorKind.Normal;
                used.Add(e);
            }

            // 나머지 방을 문으로 하나씩 연결 (트리). 어떤 방이든 복도에서 이어진 길이 하나는 있다.
            var tree = new List<(int edge, int newRoom)>();
            while (reach.Count < TowerLayout.RoomsPerFloor)
            {
                var candidates = Enumerable.Range(0, Edges.Count)
                    .Where(e => !used.Contains(e) && (reach.Contains(Edges[e].a) ^ reach.Contains(Edges[e].b))).ToList();
                int pick = candidates[rng.Next(candidates.Count)];
                int newRoom = reach.Contains(Edges[pick].a) ? Edges[pick].b : Edges[pick].a;
                tree.Add((pick, newRoom));
                used.Add(pick);
                reach.Add(newRoom);
            }

            // 이 중 일부만 잠긴 문 (나머지는 열쇠 없이 지나는 문). 열쇠는 문을 열기 전에 갈 수 있는 방에 숨는다.
            var lockedIdx = new HashSet<int>(Enumerable.Range(0, tree.Count).OrderBy(_ => rng.Next()).Take(LockedPerFloor));
            for (int i = 0; i < tree.Count; i++)
            {
                plan.Type[tree[i].edge] = lockedIdx.Contains(i) ? DoorKind.Locked : DoorKind.Normal;
                if (lockedIdx.Contains(i)) plan.Locked.Add(tree[i]);
            }

            // 순환 통로: 열쇠 없이 지나는 일반 문 몇 개 (막다른 길만 있으면 쫓길 때 갇힌다). 나머지는 막힌 벽.
            var rest = Enumerable.Range(0, Edges.Count).Where(e => !used.Contains(e)).OrderBy(_ => rng.Next()).ToList();
            for (int k = 0; k < rest.Count; k++)
                plan.Type[rest[k]] = k < LoopsPerFloor ? DoorKind.Normal : (DoorKind?)null;

            return plan;
        }

        // ---------- 층 기하 구조 ----------

        static List<(float c, float w)> Gaps(params float[] centers)
        {
            var l = new List<(float, float)>();
            foreach (float c in centers) l.Add((c, DoorWidth));
            return l;
        }

        /// <summary>벽 한 줄을 만든다. gaps에 든 좌표마다 문틈을 내고, 문(좁은 틈) 위에는 상인방(문 위 벽)을 둔다.</summary>
        static void WallRun(Transform root, float y, bool vertical, float line, float from, float to, List<(float c, float w)> gaps)
        {
            gaps.Sort((p, q) => p.c.CompareTo(q.c));
            float cursor = from;
            foreach (var g in gaps)
            {
                float gapStart = g.c - g.w * 0.5f, gapEnd = g.c + g.w * 0.5f;
                if (gapStart > cursor + 0.01f) WallPiece(root, y, vertical, line, cursor, gapStart);
                cursor = gapEnd;

                if (g.w > 2.5f) continue;   // 복도 이음매는 넓게 뚫려 있다
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Wall";
                lintel.transform.SetParent(root);
                lintel.transform.position = vertical ? new Vector3(line, y + 2.8f, g.c) : new Vector3(g.c, y + 2.8f, line);
                lintel.transform.localScale = vertical ? new Vector3(WallThick, 0.4f, g.w) : new Vector3(g.w, 0.4f, WallThick);
                lintel.GetComponent<Renderer>().sharedMaterial = WallMaterialFor(g.w);
            }
            if (to > cursor + 0.01f) WallPiece(root, y, vertical, line, cursor, to);
        }

        static void WallPiece(Transform root, float y, bool vertical, float line, float a, float b)
        {
            float mid = (a + b) * 0.5f;
            Vector3 center = vertical ? new Vector3(line, y, mid) : new Vector3(mid, y, line);
            MakeWall(root, center, vertical ? Vector3.forward : Vector3.right, b - a);
            AddWallSkins(y, vertical, line, a, b);
        }

        static int LineKey(float coord) => Mathf.RoundToInt(coord * 100f);

        static void BuildFloorGeometry(Transform parent, int floor, FloorPlan plan)
        {
            float y = TowerLayout.FloorY(floor);
            string label = TowerLayout.ShortLabel(floor);
            var root = new GameObject($"Floor_{label}").transform;
            root.SetParent(parent);

            // 바닥판 (두께 1m: 아래층의 천장도 겸한다). 계단통 자리는 구멍이 뚫린다.
            BuildSlab(root, floor, y, floorMat);

            // 꼭대기 층은 지붕이 필요하다
            if (floor == TowerLayout.Floors - 1)
                Box(root, "Roof", new Vector3(TowerLayout.SpanX * 0.5f, y + WallHeight + 0.5f, TowerLayout.SpanZ * 0.5f),
                    new Vector3(TowerLayout.SpanX, 1f, TowerLayout.SpanZ), wallMat);

            // 벽 줄: 모든 방의 네 변을 모아 같은 줄끼리 합친다 (줄 = 방향 + 좌표)
            var sides = new Dictionary<(bool v, int k), List<Vector2>>();
            void AddSide(bool v, float coord, float lo, float hi)
            {
                var key = (v, LineKey(coord));
                if (!sides.TryGetValue(key, out var l)) sides[key] = l = new List<Vector2>();
                l.Add(new Vector2(lo, hi));
            }
            for (int r = 0; r < TowerLayout.RoomsPerFloor; r++)
            {
                Rect rc = TowerLayout.RoomRect(r);
                AddSide(true, rc.xMin, rc.yMin, rc.yMax);
                AddSide(true, rc.xMax, rc.yMin, rc.yMax);
                AddSide(false, rc.yMin, rc.xMin, rc.xMax);
                AddSide(false, rc.yMax, rc.xMin, rc.xMax);
            }

            var gaps = new Dictionary<(bool v, int k), List<(float c, float w)>>();
            void AddGap(bool v, float coord, float c, float w)
            {
                var key = (v, LineKey(coord));
                if (!gaps.TryGetValue(key, out var l)) gaps[key] = l = new List<(float, float)>();
                l.Add((c, w));
            }

            bool top = floor == TowerLayout.Floors - 1;
            bool ground = floor == TowerLayout.GroundFloor;
            float lobbyX = TowerLayout.RoomCenter(floor, TowerLayout.ExitRoom).x;
            float liftX = TowerLayout.RoomCenter(floor, TowerLayout.StartRoom).x;   // 북쪽 가운데 방 = 승강기 승강장 쪽

            // 서쪽 바깥벽: 3층 남서쪽 방(0)에는 전기실로 통하는 숨겨진 문이 있다 (벽처럼 보인다)
            const float elecDoorZ = 4f;
            if (top)
            {
                AddGap(true, 0f, elecDoorZ, DoorWidth);
                doorRequests.Add(new DoorRequest
                {
                    center = new Vector3(0f, y, elecDoorZ), along = Vector3.forward, kind = DoorKind.Secret,
                    floor = floor, roomA = 0, roomB = 0, treeOrder = -1,
                });
            }
            // 북쪽 바깥벽: 북쪽 가운데 방(시작 방)에는 엘리베이터 승강장으로 통하는 숨겨진 문이 있다 (발전기가 복구돼야 열린다)
            AddGap(false, TowerLayout.SpanZ, liftX, DoorWidth);
            doorRequests.Add(new DoorRequest
            {
                center = new Vector3(liftX, y, TowerLayout.SpanZ), along = Vector3.right, kind = DoorKind.Secret,
                floor = floor, roomA = TowerLayout.StartRoom, roomB = TowerLayout.StartRoom, treeOrder = -1, requiresPower = true,
            });
            // 남쪽 바깥벽: 1층 정문 로비의 정문
            if (ground)
            {
                AddGap(false, 0f, lobbyX, DoorWidth);
                doorRequests.Add(new DoorRequest
                {
                    center = new Vector3(lobbyX, y, 0f), along = Vector3.right, kind = DoorKind.Exit,
                    floor = floor, roomA = TowerLayout.ExitRoom, roomB = TowerLayout.ExitRoom, treeOrder = -1,
                });
            }

            // 방 사이 통로: 문 또는 뚫린 이음매
            for (int e = 0; e < Edges.Count; e++)
            {
                var ed = Edges[e];
                float line = ed.vertical ? ed.x : ed.z;
                float along = ed.vertical ? ed.z : ed.x;

                if (ed.open)
                {
                    AddGap(ed.vertical, line, along, ed.w);
                    continue;
                }

                plan.Type.TryGetValue(e, out DoorKind? kind);
                if (kind == null) continue;   // 막힌 벽

                bool isStairEdge = e == EdgeStairWest || e == EdgeStairEast;
                int hallRoom = e == EdgeStairWest ? TowerLayout.WestStairRoom : TowerLayout.EastStairRoom;
                bool stairDoor = isStairEdge && HallHasStairs(floor, hallRoom);   // 계단이 없는 층의 홀은 일반 문
                float width = stairDoor ? StairDoorWidth : DoorWidth;
                AddGap(ed.vertical, line, along, width);

                int lockedIndex = plan.Locked.FindIndex(t => t.edge == e);
                int roomB = lockedIndex >= 0 ? plan.Locked[lockedIndex].newRoom : ed.b;
                int roomA = roomB == ed.b ? ed.a : ed.b;

                doorRequests.Add(new DoorRequest
                {
                    center = new Vector3(ed.x, y, ed.z),
                    along = ed.vertical ? Vector3.forward : Vector3.right,
                    kind = stairDoor ? DoorKind.Stair : kind.Value,
                    width = stairDoor ? StairDoorWidth : 0f,
                    openAngle = e == EdgeStairWest ? 95f : -95f,   // 계단 비상구는 복도 쪽으로 열린다
                    floor = floor, roomA = roomA, roomB = roomB,
                    keyId = kind == DoorKind.Locked ? $"{label}-{lockedIndex + 1}" : null,
                    treeOrder = lockedIndex,
                });
            }

            // 벽 줄마다 겹치는 구간을 합쳐 벽을 세운다
            foreach (var kv in sides)
            {
                bool vertical = kv.Key.v;
                float line = kv.Key.k / 100f;
                var list = kv.Value.OrderBy(v => v.x).ToList();
                var merged = new List<Vector2>();
                foreach (var seg in list)
                {
                    if (merged.Count > 0 && seg.x <= merged[merged.Count - 1].y + 0.001f)
                        merged[merged.Count - 1] = new Vector2(merged[merged.Count - 1].x, Mathf.Max(merged[merged.Count - 1].y, seg.y));
                    else merged.Add(seg);
                }
                gaps.TryGetValue(kv.Key, out var lineGaps);
                foreach (var m in merged)
                {
                    var inside = new List<(float c, float w)>();
                    if (lineGaps != null)
                        foreach (var g in lineGaps) if (g.c > m.x && g.c < m.y) inside.Add(g);
                    WallRun(root, y, vertical, line, m.x, m.y, inside);
                }
            }

            FlushWallSkins(parent, floor);
        }

        static Transform[] BuildFloorWaypoints(int floor, System.Random rng)
        {
            var root = new GameObject($"Patrol_{TowerLayout.ShortLabel(floor)}").transform;
            // 건물이 커서 모든 방을 돌지 않고, 복도와 방 몇 곳을 무작위로 순찰한다
            var rooms = Enumerable.Range(0, TowerLayout.RoomsPerFloor)
                .Where(r => r != TowerLayout.WestStairRoom && r != TowerLayout.EastStairRoom)
                .OrderBy(_ => rng.Next()).Take(36).ToList();
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
                float doorW = req.width > 0f ? req.width : DoorWidth;
                Material mat = req.kind == DoorKind.Exit ? exitMat : req.kind == DoorKind.Stair ? StairDoorMaterial(true)
                    : req.kind == DoorKind.Locked ? lockedDoorMat : req.kind == DoorKind.Secret ? wallMat : doorMat;
                Vector3 panelSize = alongX ? new Vector3(doorW, 2.6f, 0.1f) : new Vector3(0.1f, 2.6f, doorW);

                if (req.kind == DoorKind.Exit)
                {
                    // 정문: 한 장짜리 문 (열리지 않는다. 열쇠가 있으면 탈출)
                    var single = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    single.name = "Panel";
                    single.transform.SetParent(root.transform);
                    single.transform.position = req.center + Vector3.up * 1.3f;
                    single.transform.localScale = panelSize;
                    single.GetComponent<Renderer>().sharedMaterial = mat;
                    root.AddComponent<ExitDoor>();
                    continue;
                }

                var door = root.AddComponent<Door>();
                var so = new SerializedObject(door);

                if (req.kind == DoorKind.Stair)
                {
                    // 계단 비상구: 한 짝, 경첩을 축으로 돌아서 열린다
                    var hinge = new GameObject("Hinge").transform;
                    hinge.SetParent(root.transform);
                    hinge.position = req.center - req.along * (doorW * 0.5f);
                    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.name = "Panel";
                    panel.transform.SetParent(hinge);
                    panel.transform.position = req.center + Vector3.up * 1.3f;
                    panel.transform.localScale = panelSize;
                    panel.GetComponent<Renderer>().sharedMaterial = mat;
                    so.FindProperty("hinge").objectReferenceValue = hinge;
                    so.FindProperty("openAngle").floatValue = req.openAngle != 0f ? req.openAngle : 95f;
                }
                else
                {
                    // 일반 문: 한 짝이 벽 속(문틈과 같은 두께 안)으로 미끄러져 들어가며 열린다
                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaf.name = "Leaf";
                    leaf.transform.SetParent(root.transform);
                    leaf.transform.position = req.center + Vector3.up * 1.3f;
                    leaf.transform.localScale = alongX ? new Vector3(doorW, 2.6f, 0.08f) : new Vector3(0.08f, 2.6f, doorW);
                    leaf.GetComponent<Renderer>().sharedMaterial = mat;
                    float dir = SlideDirection(req);
                    so.FindProperty("leaf").objectReferenceValue = leaf.transform;
                    so.FindProperty("slideDir").vector3Value = req.along * dir;
                    so.FindProperty("slideDistance").floatValue = doorW * 0.95f;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                door.floor = req.floor;
                door.roomA = req.roomA;
                door.roomB = req.roomB;
                door.lockKeyId = req.keyId;
                door.treeOrder = req.treeOrder;
                if (req.kind == DoorKind.Secret) AttachLightLeak(root.transform, door, req, alongX);
                door.secret = req.kind == DoorKind.Secret;
                door.requiresPower = req.requiresPower;
                EditorUtility.SetDirty(door);
            }
        }

        /// <summary>문이 열릴 때 벽 속으로 들어갈 방향(+1/-1). 두 방이 공유하는 벽 구간 중 더 긴 쪽으로 간다.</summary>
        static float SlideDirection(DoorRequest req)
        {
            bool alongX = Mathf.Abs(req.along.x) > 0.5f;
            float c = alongX ? req.center.x : req.center.z;
            float lo = 0f, hi = alongX ? TowerLayout.SpanX : TowerLayout.SpanZ;
            foreach (int room in new[] { req.roomA, req.roomB })
            {
                Rect r = TowerLayout.RoomRect(room);
                float a = alongX ? r.xMin : r.yMin, b = alongX ? r.xMax : r.yMax;
                lo = Mathf.Max(lo, a);
                hi = Mathf.Min(hi, b);
            }
            if (hi <= lo) { lo = 0f; hi = alongX ? TowerLayout.SpanX : TowerLayout.SpanZ; }
            return (hi - c) >= (c - lo) ? 1f : -1f;
        }

        // ---------- 숨겨진 문의 빛 단서 ----------

        static Material leakRedMat, leakWarmMat;

        static Material LeakMaterial(bool warm)
        {
            if (warm ? leakWarmMat != null : leakRedMat != null) return warm ? leakWarmMat : leakRedMat;
            string name = warm ? "LightLeakWarm" : "LightLeakRed";
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Color c = warm ? new Color(1f, 0.72f, 0.38f) : new Color(0.95f, 0.18f, 0.1f);
            mat.SetColor("_BaseColor", c * 0.4f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 0.5f);   // 세기가 1을 넘으면 후처리에서 색이 틀어진다
            EditorUtility.SetDirty(mat);
            if (warm) leakWarmMat = mat; else leakRedMat = mat;
            return mat;
        }

        /// <summary>문 바닥 틈으로 빛이 새어 나오는 가는 띠. 전기실 문은 붉게, 엘리베이터 승강장 문은 따뜻하게.</summary>
        static void AttachLightLeak(Transform doorRoot, Door door, DoorRequest req, bool alongX)
        {
            bool warm = req.requiresPower;
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "LightLeak";
            strip.transform.SetParent(doorRoot, true);
            strip.transform.position = req.center + Vector3.up * 0.02f;
            strip.transform.localScale = alongX
                ? new Vector3(DoorWidth - 0.15f, 0.03f, 0.2f)
                : new Vector3(0.2f, 0.03f, DoorWidth - 0.15f);
            strip.GetComponent<Renderer>().sharedMaterial = LeakMaterial(warm);
            Object.DestroyImmediate(strip.GetComponent<Collider>());

            var so = new SerializedObject(door);
            so.FindProperty("leak").objectReferenceValue = strip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 방 안 가구 ----------

        static bool IsStairRoom(int room) =>
            room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom;

        struct CabinetSlot { public Vector3 pos; public Quaternion rot; }

        /// <summary>벽을 따라 서랍장을 둘 수 있는 자리. 문, 계단, 소품 자리 근처(avoid)는 뺀다. 서랍은 방 안쪽으로 열린다.</summary>
        static List<CabinetSlot> CabinetCandidates(int floor, int room, List<(Vector2 p, float r)> avoid)
        {
            Rect r = TowerLayout.RoomRect(room);
            float y = TowerLayout.FloorY(floor);
            var list = new List<CabinetSlot>();
            const float step = 2.2f, inset = 0.75f, edge = 1.6f;

            for (float x = r.xMin + edge; x <= r.xMax - edge + 0.01f; x += step)
            {
                list.Add(new CabinetSlot { pos = new Vector3(x, y, r.yMax - inset), rot = Quaternion.Euler(0f, 180f, 0f) });   // 북쪽 벽, 남쪽을 본다
                list.Add(new CabinetSlot { pos = new Vector3(x, y, r.yMin + inset), rot = Quaternion.identity });               // 남쪽 벽, 북쪽을 본다
            }
            if (r.height > 5f)   // 복도는 좁아서 긴 벽에만 둔다
            {
                for (float z = r.yMin + edge; z <= r.yMax - edge + 0.01f; z += step)
                {
                    list.Add(new CabinetSlot { pos = new Vector3(r.xMin + inset, y, z), rot = Quaternion.Euler(0f, 90f, 0f) });    // 서쪽 벽, 동쪽을 본다
                    list.Add(new CabinetSlot { pos = new Vector3(r.xMax - inset, y, z), rot = Quaternion.Euler(0f, 270f, 0f) });   // 동쪽 벽, 서쪽을 본다
                }
            }

            list.RemoveAll(s =>
            {
                var p = new Vector2(s.pos.x, s.pos.z);
                foreach (var a in avoid) if (Vector2.Distance(p, a.p) < a.r) return true;
                return false;
            });
            return list;
        }

        static void BuildFurniture(Transform parent, int floor)
        {
            var root = new GameObject($"Furniture_{TowerLayout.ShortLabel(floor)}").transform;
            root.SetParent(parent);
            var rng = new System.Random(5000 + floor);

            // 이 층에서 서랍장을 피해야 할 자리: 모든 문 앞, 계단 앞
            var floorAvoid = new List<(Vector2 p, float r)>();
            foreach (var d in doorRequests.Where(d => d.floor == floor))
                floorAvoid.Add((new Vector2(d.center.x, d.center.z), 2.2f));
            foreach (var ed in Edges.Where(e => e.open))   // 복도 이음매 앞도 비운다
                floorAvoid.Add((new Vector2(ed.x, ed.z), 2.7f));

            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                Rect r = TowerLayout.RoomRect(room);
                bool stairRoom = IsStairRoom(room) && HallHasStairs(floor, room);   // 계단이 닿는 홀만 가구를 비운다
                var avoid = new List<(Vector2 p, float r)>(floorAvoid);

                // 소품이 놓일 자리 (HospitalBuilder)
                foreach (var entry in PropsFor(floor, room))
                {
                    string anchorName = entry.Split(':')[1];
                    if (!AnchorUsable(floor, room, anchorName)) continue;
                    var a = ResolveAnchor(anchorName, room);
                    avoid.Add((new Vector2(r.center.x + a.pos.x, r.center.y + a.pos.y), 1.7f));
                }
                foreach (var cp in CorridorPropSpots(floor, room))
                    avoid.Add((new Vector2(cp.pos.x, cp.pos.z), 1.7f));

                // 옷장 (숨는 곳): 복도·계단 홀이 아닌 방 중 절반 정도. 동쪽 벽 남쪽 끝에 붙인다.
                bool wardrobe = r.height > 5f && ((room + floor) % 2 == 0);
                if (wardrobe)
                {
                    var lockerPos = new Vector3(r.xMax - 0.6f, TowerLayout.FloorY(floor), r.yMin + 1.5f);
                    BuildWardrobe(root, lockerPos);
                    avoid.Add((new Vector2(lockerPos.x, lockerPos.z), 1.9f));
                }

                // 서랍장: 계단 홀 1개, 복도 2개, 나머지 3개
                bool corr = TowerLayout.IsCorridorRoom(room);
                float area = r.width * r.height;
                int want = stairRoom ? 0 : (corr ? Mathf.Clamp(Mathf.RoundToInt(r.width / 9f), 1, 3) : (area >= 90f ? 2 : (rng.NextDouble() < 0.55 ? 2 : 1)));   // 계단 홀에는 서랍장을 두지 않는다
                var cands = CabinetCandidates(floor, room, avoid).OrderBy(_ => rng.Next()).ToList();
                var chosen = new List<CabinetSlot>();
                foreach (var c in cands)
                {
                    if (chosen.Count >= want) break;
                    // 서로 겹치지 않게
                    if (chosen.Any(o => Vector3.Distance(o.pos, c.pos) < 1.4f)) continue;
                    chosen.Add(c);
                }

                // 잔해 더미 자리: 서랍장과 겹치지 않는 벽 자리 (복도 1개, 방 1~2개)
                int pileWant = (stairRoom || corr) ? 0 : 1 + rng.Next(2);   // 복도는 좁아서 잔해를 두지 않는다 (길이 막힌다)
                foreach (var c in cands)
                {
                    if (pileWant <= 0) break;
                    if (chosen.Any(o => Vector3.Distance(o.pos, c.pos) < 2.0f)) continue;
                    if (debrisSpots.Any(d => d.floor == floor && Vector3.Distance(d.pos, c.pos) < 2.2f)) continue;
                    debrisSpots.Add(new DebrisSpot { floor = floor, pos = c.pos, yaw = rng.Next(360) });
                    pileWant--;
                }

                for (int s = 0; s < chosen.Count; s++)
                {
                    // 간호사실의 첫 서랍장은 직원 출입카드가 든 금속 서랍장
                    bool metal = floor == TowerLayout.NurseStationFloor && room == TowerLayout.NurseStationRoom && s == 0;
                    BuildContainer(root, floor, room, chosen[s].pos, chosen[s].rot, metal);
                }
            }
        }

        /// <summary>
        /// 서랍장 하나: 몸통 + 밀려 나오는 서랍 + 물건이 놓이는 자리. 방 중심을 향해 서랍이 열린다.
        /// 지금은 기본 도형이고, 나중에 Varco 모델로 바꿀 때도 이 구조(Body, Drawer, ItemAnchor)를 유지한다.
        /// </summary>
        const string CabinetPrefabPath = "Assets/Prefabs/Cabinet.prefab";
        static Material metalCabinetTint;

        static Container BuildContainer(Transform parent, int floor, int room, Vector3 position, Quaternion rotation, bool metal)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CabinetPrefabPath);
            if (prefab != null) return BuildModelContainer(prefab, parent, floor, room, position, rotation, metal);
            return BuildPrimitiveContainer(parent, floor, room, position, rotation, metal);
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
        static Container BuildModelContainer(GameObject prefab, Transform parent, int floor, int room, Vector3 position, Quaternion rotation, bool metal)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = metal ? "MetalCabinet" : "Cabinet";
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.rotation = rotation;

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

            MarkNotWalkable(go);
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
        static Container BuildPrimitiveContainer(Transform parent, int floor, int room, Vector3 position, Quaternion rotation, bool metal)
        {
            var root = new GameObject(metal ? "MetalCabinet" : "Cabinet").transform;
            root.SetParent(parent);
            root.position = position;
            root.rotation = rotation;

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

            MarkNotWalkable(root.gameObject);
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
        static void BuildWardrobe(Transform parent, Vector3 position)
        {
            // 숨는 곳: 철제 로커 (Varco 모델). 문이 방 안쪽(서쪽)을 향한다.
            var holder = new GameObject("Locker").transform;
            holder.SetParent(parent);
            holder.position = position;

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
            hide.position = new Vector3(holder.position.x, position.y + 0.1f, holder.position.z);
            hide.rotation = Quaternion.Euler(0f, -90f, 0f);   // 방 안쪽(서쪽)을 본다

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(holder);
            exit.position = new Vector3(holder.position.x - 1.2f, position.y + 0.1f, holder.position.z);

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
            // 계단통마다 자기가 잇는 층 사이(pair)에만 입구가 있다. 위로 가는 입구와 위층의 내려가는 입구가 짝이다.
            foreach (bool west in new[] { true, false })
            {
                int room = west ? TowerLayout.WestStairRoom : TowerLayout.EastStairRoom;
                for (int n = 0; n < TowerLayout.Floors - 1; n++)
                {
                    if (!HallHasPair(west, n)) continue;
                    var up = MakePortal(parent, n, room, true);
                    var down = MakePortal(parent, n + 1, room, false);
                    up.partner = down;
                    down.partner = up;
                    EditorUtility.SetDirty(up);
                    EditorUtility.SetDirty(down);
                }
            }
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
                    // 긴 방과 복도는 7m쯤마다 하나씩 (불빛이 끝까지 닿도록)
                    Rect rc = TowerLayout.RoomRect(room);
                    bool alongX = rc.width >= rc.height;
                    int n = Mathf.Max(1, Mathf.RoundToInt((alongX ? rc.width : rc.height) / 7f));
                    for (int i = 0; i < n; i++)
                    {
                        float t = (i + 0.5f) / n;
                        Vector3 pos = alongX
                            ? new Vector3(rc.xMin + rc.width * t, TowerLayout.FloorY(f) + 2.6f, rc.center.y)
                            : new Vector3(rc.center.x, TowerLayout.FloorY(f) + 2.6f, rc.yMin + rc.height * t);
                        var go = new GameObject($"PowerLight_{TowerLayout.ShortLabel(f)}_{room}_{i}");
                        go.transform.SetParent(root);
                        go.transform.position = pos;
                        var lg = go.AddComponent<Light>();
                        lg.type = LightType.Point;
                        lg.range = 7f;
                        lg.color = new Color(0.75f, 0.82f, 1f);
                        lg.shadows = LightShadows.None;
                        go.AddComponent<Escape.Core.PowerLight>();
                    }
                }
            }
        }

        // ---------- 1층 전원함 ----------

        static void BuildPowerBoxOnGround(Transform parent)
        {
            int f = TowerLayout.GroundFloor;
            Vector3 c = TowerLayout.RoomCenter(f, TowerLayout.ExitRoom);
            Rect exitRect = TowerLayout.RoomRect(TowerLayout.ExitRoom);

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
            box.transform.position = new Vector3(exitRect.xMax - 0.3f, c.y + 1.5f, c.z + 2.5f);
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
