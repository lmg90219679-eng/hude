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
            public bool requiresPower;
        }

        static readonly List<DoorRequest> doorRequests = new List<DoorRequest>();

        // 방 사이 통로 12곳 (TowerLayout 평면도 참고). 문의 중심 좌표(x, z)와 벽 방향.
        struct Edge { public int a, b; public bool vertical; public float x, z; }

        static readonly Edge[] Edges =
        {
            new Edge { a = 0, b = 1, vertical = true,  x = 7f,  z = 5f  },    // 0  남쪽 병동 안
            new Edge { a = 1, b = 2, vertical = true,  x = 17f, z = 5f  },    // 1
            new Edge { a = 6, b = 7, vertical = true,  x = 9f,  z = 19f },    // 2  북쪽 병동 안
            new Edge { a = 7, b = 8, vertical = true,  x = 15f, z = 19f },    // 3
            new Edge { a = 0, b = 3, vertical = false, x = 3f,  z = 10f },    // 4  남쪽 병동 <-> 복도 줄
            new Edge { a = 1, b = 4, vertical = false, x = 12f, z = 10f },    // 5
            new Edge { a = 2, b = 5, vertical = false, x = 21f, z = 10f },    // 6
            new Edge { a = 3, b = 6, vertical = false, x = 3f,  z = 14f },    // 7  복도 줄 <-> 북쪽 병동
            new Edge { a = 4, b = 7, vertical = false, x = 12f, z = 14f },    // 8
            new Edge { a = 5, b = 8, vertical = false, x = 21f, z = 14f },    // 9
            new Edge { a = 3, b = 4, vertical = true,  x = 6f,  z = 12f },    // 10 서쪽 계단 홀의 방화문
            new Edge { a = 4, b = 5, vertical = true,  x = 18f, z = 12f },    // 11 동쪽 계단 홀의 방화문
        };
        const int EdgeStairWest = 10, EdgeStairEast = 11, EdgeStartRoom = 8;   // 열쇠 없이 지나는 문

        // 벽 한 줄: 같은 좌표에 놓인 벽 (문이 있는 통로는 틈을 낸다)
        struct WallLine { public bool vertical; public float line, from, to; public int[] edges; }

        static readonly WallLine[] InnerWalls =
        {
            new WallLine { vertical = false, line = 10f, from = 0f,  to = 24f, edges = new[] { 4, 5, 6 } },
            new WallLine { vertical = false, line = 14f, from = 0f,  to = 24f, edges = new[] { 7, 8, 9 } },
            new WallLine { vertical = true,  line = 7f,  from = 0f,  to = 10f, edges = new[] { 0 } },
            new WallLine { vertical = true,  line = 17f, from = 0f,  to = 10f, edges = new[] { 1 } },
            new WallLine { vertical = true,  line = 9f,  from = 14f, to = 24f, edges = new[] { 2 } },
            new WallLine { vertical = true,  line = 15f, from = 14f, to = 24f, edges = new[] { 3 } },
            new WallLine { vertical = true,  line = 6f,  from = 10f, to = 14f, edges = new[] { 10 } },
            new WallLine { vertical = true,  line = 18f, from = 10f, to = 14f, edges = new[] { 11 } },
        };

        class FloorPlan
        {
            public readonly Dictionary<int, DoorKind?> Type = new Dictionary<int, DoorKind?>();   // null = 막힌 벽
            public readonly List<(int edge, int newRoom)> Tree = new List<(int, int)>();
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
            EnsureHospitalMaterials();
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
            BuildElevatorGeometry(map.transform);
            BuildNavMeshForTower(map);

            // 플레이어: 1층 북쪽 가운데 방(상담실)에서 시작. 계단은 복도 양 끝 방화문 너머에 있어서 직접 찾아야 한다
            var player = BuildPlayer(TowerLayout.RoomCenter(TowerLayout.GroundFloor, TowerLayout.StartRoom) + new Vector3(0f, 0f, 1.5f));

            // 층마다 추적자 1명
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                var wps = BuildFloorWaypoints(f, rng);
                int startRoom = f == TowerLayout.GroundFloor ? 0 : (f % 2 == 0 ? 8 : 6);
                BuildChaser(player.transform, wps, TowerLayout.RoomCenter(f, startRoom), $"Chaser_{TowerLayout.ShortLabel(f)}");
            }

            // 문, 가구, 계단은 NavMesh를 굽고 나서 만든다 (닫힌 문이 길을 막지 않게)
            BuildDoors(map.transform);
            for (int f = 0; f < TowerLayout.Floors; f++) BuildFurniture(map.transform, f);
            BuildHospitalRooms(map.transform);
            BuildStairs(map.transform);
            BuildPowerBoxOnGround(map.transform);
            BuildElectricalRoomProps(map.transform);
            BuildElevatorProps(map.transform);
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

        static FloorPlan MakePlan(System.Random rng)
        {
            var plan = new FloorPlan();

            // 열쇠 없이 갈 수 있는 곳: 계단 홀 둘, 중앙 복도, 시작 방. (어느 계단으로 올라와도, 어디서 시작해도 막히지 않게)
            var reach = new HashSet<int> { TowerLayout.WestStairRoom, TowerLayout.CenterRoom, TowerLayout.EastStairRoom, TowerLayout.StartRoom };
            var used = new HashSet<int> { EdgeStairWest, EdgeStairEast, EdgeStartRoom };
            foreach (int e in used) plan.Type[e] = DoorKind.Normal;

            // 잠긴 문으로 나머지 방을 하나씩 연결 (트리). 어떤 방이든 그 전 방들에서 열쇠를 구해 열 수 있다.
            while (reach.Count < TowerLayout.RoomsPerFloor)
            {
                var candidates = Enumerable.Range(0, Edges.Length)
                    .Where(e => !used.Contains(e) && (reach.Contains(Edges[e].a) ^ reach.Contains(Edges[e].b))).ToList();
                int pick = candidates[rng.Next(candidates.Count)];
                int newRoom = reach.Contains(Edges[pick].a) ? Edges[pick].b : Edges[pick].a;
                plan.Type[pick] = DoorKind.Locked;
                plan.Tree.Add((pick, newRoom));
                used.Add(pick);
                reach.Add(newRoom);
            }

            // 순환 통로 3개: 열쇠 없이 지나는 일반 문 (막다른 길만 있으면 쫓길 때 갇힌다)
            var rest = Enumerable.Range(0, Edges.Length).Where(e => !used.Contains(e)).OrderBy(_ => rng.Next()).ToList();
            for (int k = 0; k < rest.Count; k++)
                plan.Type[rest[k]] = k < 3 ? DoorKind.Normal : (DoorKind?)null;

            return plan;
        }

        // ---------- 층 기하 구조 ----------

        /// <summary>벽 한 줄을 만든다. doors에 든 좌표마다 문틈(DoorWidth)을 낸다.</summary>
        static void WallRun(Transform root, float y, bool vertical, float line, float from, float to, List<float> doors)
        {
            doors.Sort();
            float cursor = from;
            foreach (float d in doors)
            {
                float gapStart = d - DoorWidth * 0.5f, gapEnd = d + DoorWidth * 0.5f;
                if (gapStart > cursor + 0.01f) WallPiece(root, y, vertical, line, cursor, gapStart);
                cursor = gapEnd;
            }
            if (to > cursor + 0.01f) WallPiece(root, y, vertical, line, cursor, to);
        }

        static void WallPiece(Transform root, float y, bool vertical, float line, float a, float b)
        {
            float mid = (a + b) * 0.5f;
            Vector3 center = vertical ? new Vector3(line, y, mid) : new Vector3(mid, y, line);
            MakeWall(root, center, vertical ? Vector3.forward : Vector3.right, b - a);
        }

        static void BuildFloorGeometry(Transform parent, int floor, FloorPlan plan)
        {
            float y = TowerLayout.FloorY(floor);
            string label = TowerLayout.ShortLabel(floor);
            var root = new GameObject($"Floor_{label}").transform;
            root.SetParent(parent);

            float size = TowerLayout.Span;

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

            // 바깥벽 네 면
            bool top = floor == TowerLayout.Floors - 1;
            bool ground = floor == TowerLayout.GroundFloor;
            float lobbyX = TowerLayout.RoomCenter(floor, TowerLayout.ExitRoom).x;
            float liftX = TowerLayout.RoomCenter(floor, TowerLayout.StartRoom).x;   // 북쪽 가운데 방 = 승강기 승강장 쪽

            // 서쪽: 3층 남서쪽 방(0)에는 전기실로 통하는 숨겨진 문이 있다 (벽처럼 보인다)
            const float elecDoorZ = 4f;
            WallRun(root, y, true, 0f, 0f, size, top ? new List<float> { elecDoorZ } : new List<float>());
            if (top)
            {
                doorRequests.Add(new DoorRequest
                {
                    center = new Vector3(0f, y, elecDoorZ), along = Vector3.forward, kind = DoorKind.Secret,
                    floor = floor, roomA = 0, roomB = 0, treeOrder = -1,
                });
            }
            // 동쪽
            WallRun(root, y, true, size, 0f, size, new List<float>());
            // 북쪽: 북쪽 가운데 방(7)에는 엘리베이터 승강장으로 통하는 숨겨진 문이 있다 (발전기가 복구돼야 열린다)
            WallRun(root, y, false, size, 0f, size, new List<float> { liftX });
            doorRequests.Add(new DoorRequest
            {
                center = new Vector3(liftX, y, size), along = Vector3.right, kind = DoorKind.Secret,
                floor = floor, roomA = TowerLayout.StartRoom, roomB = TowerLayout.StartRoom, treeOrder = -1, requiresPower = true,
            });
            // 남쪽: 1층 정문 로비의 정문
            WallRun(root, y, false, 0f, 0f, size, ground ? new List<float> { lobbyX } : new List<float>());
            if (ground)
            {
                doorRequests.Add(new DoorRequest
                {
                    center = new Vector3(lobbyX, y, 0f), along = Vector3.right, kind = DoorKind.Exit,
                    floor = floor, roomA = TowerLayout.ExitRoom, roomB = TowerLayout.ExitRoom, treeOrder = -1,
                });
            }

            // 안쪽 벽
            foreach (var wl in InnerWalls)
            {
                var gaps = new List<float>();
                foreach (int e in wl.edges)
                {
                    plan.Type.TryGetValue(e, out DoorKind? kind);
                    if (kind == null) continue;

                    var ed = Edges[e];
                    gaps.Add(ed.vertical ? ed.z : ed.x);

                    int treeIndex = plan.Tree.FindIndex(t => t.edge == e);
                    int roomB = treeIndex >= 0 ? plan.Tree[treeIndex].newRoom : ed.b;
                    int roomA = roomB == ed.b ? ed.a : ed.b;

                    doorRequests.Add(new DoorRequest
                    {
                        center = new Vector3(ed.x, y, ed.z),
                        along = ed.vertical ? Vector3.forward : Vector3.right,
                        kind = kind.Value,
                        floor = floor, roomA = roomA, roomB = roomB,
                        keyId = kind == DoorKind.Locked ? $"{label}-{treeIndex + 1}" : null,
                        treeOrder = treeIndex,
                    });
                }
                WallRun(root, y, wl.vertical, wl.line, wl.from, wl.to, gaps);
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
                if (req.kind == DoorKind.Secret) AttachLightLeak(root.transform, door, req, alongX);
                door.secret = req.kind == DoorKind.Secret;
                door.requiresPower = req.requiresPower;
                EditorUtility.SetDirty(door);
            }
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
            foreach (int room in new[] { TowerLayout.WestStairRoom, TowerLayout.EastStairRoom })
                foreach (bool up in new[] { true, false })
                    floorAvoid.Add((StairSlotXZ(room, up), 2.4f));

            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                Rect r = TowerLayout.RoomRect(room);
                bool stairRoom = IsStairRoom(room);
                var avoid = new List<(Vector2 p, float r)>(floorAvoid);

                // 소품이 놓일 자리 (HospitalBuilder)
                foreach (var entry in RoomProps[floor][room])
                {
                    string anchorName = entry.Split(':')[1];
                    if (!AnchorUsable(room, anchorName)) continue;
                    var a = ResolveAnchor(anchorName, room);
                    avoid.Add((new Vector2(r.center.x + a.pos.x, r.center.y + a.pos.y), 1.7f));
                }

                // 옷장 (숨는 곳): 복도·계단 홀이 아닌 방 중 절반 정도. 동쪽 벽 남쪽 끝에 붙인다.
                bool wardrobe = r.height > 5f && ((room + floor) % 2 == 0);
                if (wardrobe)
                {
                    var lockerPos = new Vector3(r.xMax - 0.6f, TowerLayout.FloorY(floor), r.yMin + 1.5f);
                    BuildWardrobe(root, lockerPos);
                    avoid.Add((new Vector2(lockerPos.x, lockerPos.z), 1.9f));
                }

                // 서랍장: 계단 홀 1개, 복도 2개, 나머지 3개
                int want = stairRoom ? 1 : (room == TowerLayout.CenterRoom ? 2 : 3);
                var cands = CabinetCandidates(floor, room, avoid).OrderBy(_ => rng.Next()).ToList();
                var chosen = new List<CabinetSlot>();
                foreach (var c in cands)
                {
                    if (chosen.Count >= want) break;
                    // 서로 겹치지 않게
                    if (chosen.Any(o => Vector3.Distance(o.pos, c.pos) < 1.4f)) continue;
                    chosen.Add(c);
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

        /// <summary>계단이 붙는 자리(월드 x, z). 서쪽 홀은 서쪽 벽, 동쪽 홀은 동쪽 벽에 위/아래 계단이 나란히 있다.</summary>
        static Vector2 StairSlotXZ(int room, bool up)
        {
            Rect r = TowerLayout.RoomRect(room);
            float x = room == TowerLayout.WestStairRoom ? r.xMin + 0.45f : r.xMax - 0.45f;
            float z = r.center.y + (up ? 1.05f : -1.05f);
            return new Vector2(x, z);
        }

        static Material stairDoorUpMat, stairDoorDownMat;

        /// <summary>Varco 이미지로 만든 계단 방화문(STAIRS 표지판). 이미지의 어두운 가장자리는 잘라 쓰고, 올라가는 문은 초록빛, 내려가는 문은 붉은빛을 살짝 얹는다.</summary>
        static Material StairDoorMaterial(bool up)
        {
            const string tex = "Assets/Textures/Hospital/StairDoor.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null) return up ? stairUpMat : stairDownMat;
            ref Material cached = ref (up ? ref stairDoorUpMat : ref stairDoorDownMat);
            if (cached != null) return cached;
            var mat = TextureMaterial(up ? "StairDoorUp" : "StairDoorDown", tex, new Vector2(0.621f, 0.915f), new Vector2(0.193f, 0.0625f), 0.2f);
            mat.SetColor("_BaseColor", up ? new Color(0.78f, 0.95f, 0.8f) : new Color(0.95f, 0.76f, 0.74f));
            EditorUtility.SetDirty(mat);
            cached = mat;
            return mat;
        }

        static StairPortal MakePortal(Transform parent, int floor, int room, bool up)
        {
            float floorY = TowerLayout.FloorY(floor);
            Vector2 slot = StairSlotXZ(room, up);
            bool west = room == TowerLayout.WestStairRoom;
            float inward = west ? 1f : -1f;   // 홀 안쪽 (x 방향)
            string label = up ? "Up" : "Down";

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Stair{label}_{TowerLayout.ShortLabel(floor)}_{room}";
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(slot.x, floorY + 1.2f, slot.y);
            go.transform.localScale = new Vector3(0.3f, 2.4f, 1.4f);
            go.GetComponent<Renderer>().sharedMaterial = StairDoorMaterial(up);

            // 도착했을 때 서는 자리: 계단 앞 (홀 안쪽을 본다)
            var exit = new GameObject("ArrivalPoint").transform;
            exit.SetParent(go.transform);
            exit.position = new Vector3(slot.x + inward * 1.7f, floorY + 0.1f, slot.y);
            exit.rotation = Quaternion.Euler(0f, west ? 90f : 270f, 0f);

            // 가까이 가야 보이는 은은한 빛 (올라가는 계단은 초록, 내려가는 계단은 붉은 기운)
            var lg = new GameObject("Glow").AddComponent<Light>();
            lg.transform.SetParent(go.transform);
            lg.transform.position = go.transform.position + new Vector3(inward * 0.8f, -0.8f, 0f);
            lg.type = LightType.Point;
            lg.range = 2.6f;
            lg.intensity = 0.6f;
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
