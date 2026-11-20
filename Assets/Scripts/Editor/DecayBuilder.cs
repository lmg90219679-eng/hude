using System.Collections.Generic;
using System.Linq;
using Escape.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Escape.EditorTools
{
    /// <summary>
    /// 폐허 분위기: 이끼·곰팡이가 낀 바닥, 무너진 천장과 노출된 배관, 벗겨진 벽과 허리 몰딩,
    /// 잔해 더미, 매달린 형광등과 천장 패널, 푸른 달빛이 새는 깨진 창.
    /// 텍스처와 모델은 Varco에서 만든 것을 쓰고, 없으면 건너뛴다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        const string DecayTexDir = "Assets/Textures/Hospital";
        static int wallFloor;   // 지금 벽을 만드는 층 (지하는 타일 벽, 지상은 벗겨진 페인트 벽)

        static bool HasTex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{DecayTexDir}/{file}") != null;

        static readonly Dictionary<string, Material> decayCache = new Dictionary<string, Material>();

        static Material DecayMat(string name, string file, Vector2 tiling, float smooth, float shade = 1f)
        {
            string key = name;
            if (decayCache.TryGetValue(key, out var m) && m != null) return m;
            if (!HasTex(file)) return null;
            var mat = TextureMaterial(name, $"{DecayTexDir}/{file}", tiling, Vector2.zero, smooth);
            mat.SetColor("_BaseColor", new Color(0.82f * shade, 0.82f * shade, 0.82f * shade));
            EditorUtility.SetDirty(mat);
            decayCache[key] = mat;
            return mat;
        }

        /// <summary>벽 길이에 맞춰 무늬가 늘어나지 않게 반복 횟수를 정한 벽 재질. 텍스처가 없으면 null.</summary>
        static Material DecayWallMaterial(float length)
        {
            string file = wallFloor <= 1 ? "WallTile.png" : "WallDecay.png";
            string set = wallFloor <= 1 ? "WallTile" : "WallDecay";
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 3f));
            return DecayMat($"{set}_{n}", file, new Vector2(n, 1f), 0.1f);
        }

        // ---------- 바닥, 천장, 배관 ----------

        /// <summary>위/아래를 보는 평평한 면. UV는 tile(m)마다 한 번 반복된다.</summary>
        static GameObject FlatQuad(Transform parent, string name, Rect r, float y, bool facingUp, float tile, Material mat)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMin, y, r.yMax),
                new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMax, y, r.yMin),
            };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++) uv[i] = new Vector2(mesh.vertices[i].x / tile, mesh.vertices[i].z / tile);
            mesh.uv = uv;
            mesh.triangles = facingUp ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material TrimMaterial() => GetOrCreateMat("WallTrim", new Color(0.16f, 0.12f, 0.09f));

        static Material RustMaterial()
        {
            var m = GetOrCreateMat("RustPipe", new Color(0.3f, 0.17f, 0.1f));
            m.SetFloat("_Metallic", 0.5f);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        /// <summary>층마다 방별 바닥 덧칠, 무너진 천장, 노출된 배관.</summary>
        static void BuildDecayShell(Transform parent, int floor)
        {
            float y = TowerLayout.FloorY(floor);
            var root = new GameObject($"Decay_{TowerLayout.ShortLabel(floor)}").transform;
            root.SetParent(parent);

            // 방마다 다른 바닥(질감, 색조)과 천장, 계단 홀 승강장
            BuildRoomSurfaces(root, floor);
            var debris = DecayMat("FloorDebris", "FloorDebris.png", Vector2.one, 0.2f);
            if (debris != null)
            {
                foreach (bool west in new[] { true, false })
                {
                    int hr = west ? TowerLayout.WestStairRoom : TowerLayout.EastStairRoom;
                    if (!HallHasStairs(floor, hr)) continue;
                    FlatQuad(root, west ? "Landing_W" : "Landing_E", LandingRect(HallOf(west)), y + 0.012f, true, 4f, debris);
                }
            }

            // 노출된 배관: 복도 천장을 따라 길게, 방마다 한 가닥씩 (무작위)
            var rust = RustMaterial();
            var rng = new System.Random(9100 + floor);
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (TowerLayout.IsCorridorRoom(room) && (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom)) continue;
                Rect r = TowerLayout.RoomRect(room);
                if (TowerLayout.IsCorridorRoom(room))
                {
                    if (r.width < 6f) continue;   // 짧은 연결 복도(세로)는 건너뛴다
                    float x0 = r.xMin + 0.2f, x1 = r.xMax - 0.2f;
                    Pipe(root, new Vector3(x0, y + 2.8f, r.yMin + 0.6f), new Vector3(x1, y + 2.8f, r.yMin + 0.6f), 0.13f, rust);
                    Pipe(root, new Vector3(x0, y + 2.72f, r.yMax - 0.6f), new Vector3(x1, y + 2.72f, r.yMax - 0.6f), 0.1f, rust);
                    Pipe(root, new Vector3(x0, y + 2.86f, r.yMin + 1.1f), new Vector3(x1, y + 2.86f, r.yMin + 1.1f), 0.07f, rust);
                    continue;
                }
                if (TowerLayout.IsVoidRoom(room)) continue;
                if (rng.NextDouble() < 0.4) continue;
                float z = rng.NextDouble() < 0.5 ? r.yMin + 0.5f : r.yMax - 0.5f;
                Pipe(root, new Vector3(r.xMin + 0.3f, y + 2.78f, z), new Vector3(r.xMax - 0.3f, y + 2.78f, z), 0.1f, rust);
            }
        }

        static void Pipe(Transform parent, Vector3 a, Vector3 b, float diameter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pipe";
            go.transform.SetParent(parent);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            go.transform.localScale = new Vector3(diameter, (b - a).magnitude * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        // ---------- 벽 몰딩 (허리 높이 몰딩, 걸레받이) ----------

        static void AddWallTrim(Transform root, float y, bool vertical, float line, float a, float b)
        {
            float len = b - a;
            if (len < 0.4f) return;
            float mid = (a + b) * 0.5f;
            var trim = TrimMaterial();
            float thick = WallThick + 0.08f;
            Vector3 Pos(float h) => vertical ? new Vector3(line, y + h, mid) : new Vector3(mid, y + h, line);
            Vector3 Size(float h) => vertical ? new Vector3(thick, h, len) : new Vector3(len, h, thick);

            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            skirt.name = "Skirting";
            skirt.transform.SetParent(root);
            skirt.transform.position = Pos(0.09f);
            skirt.transform.localScale = Size(0.18f) - (vertical ? new Vector3(0.02f, 0, 0) : new Vector3(0, 0, 0.02f));
            skirt.GetComponent<Renderer>().sharedMaterial = trim;
            Object.DestroyImmediate(skirt.GetComponent<Collider>());

        }

        // ---------- 소품: 잔해 더미, 매달린 형광등과 천장 패널, 깨진 창 ----------

        struct DebrisSpot { public int floor; public Vector3 pos; public float yaw; }
        static readonly List<DebrisSpot> debrisSpots = new List<DebrisSpot>();

        static bool HasProp(string name) => System.IO.File.Exists($"{PropTools.PropsDir}/{name}/{name}.fbx");

        /// <summary>충돌체를 달고, NavMesh를 구울 때 이 물체 자리를 걸을 수 없는 구역으로 표시한다 (추적자가 비켜 간다).</summary>
        static void AddNavObstacle(GameObject holder, Bounds b)
        {
            var col = holder.AddComponent<BoxCollider>();
            col.center = holder.transform.InverseTransformPoint(b.center);
            col.size = b.size;
            MarkNotWalkable(holder);
        }

        static void MarkNotWalkable(GameObject go)
        {
            var mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 1;   // Not Walkable
        }

        static void BuildDecayProps(Transform parent)
        {
            var root = new GameObject("DecayProps").transform;
            root.SetParent(parent);
            var rng = new System.Random(7700);
            int piles = 0, lights = 0, panels = 0;

            // 잔해 더미: 서랍장과 겹치지 않는 벽 자리
            if (HasProp("DebrisPile"))
            {
                foreach (var s in debrisSpots)
                {
                    var holder = new GameObject($"DebrisPile_{TowerLayout.ShortLabel(s.floor)}").transform;
                    holder.SetParent(root);
                    holder.position = s.pos;
                    float h = 0.45f + (float)rng.NextDouble() * 0.35f;
                    var go = PropTools.Instantiate("DebrisPile", holder, h, 1.5f, s.yaw);
                    if (go == null) { Object.DestroyImmediate(holder.gameObject); continue; }
                    AddNavObstacle(holder.gameObject, PropTools.Bounds(go));
                    piles++;
                }
            }

            // 복도의 매달린 형광등 (꺼져 있다), 복도와 큰 방의 무너져 내린 천장 패널
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                float y = TowerLayout.FloorY(f);
                if (HasProp("CeilingLight"))
                {
                    for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                    {
                        if (!TowerLayout.IsCorridorRoom(room)) continue;
                        Rect r = TowerLayout.RoomRect(room);
                        if (r.width < 6f || room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom) continue;
                        for (float x = r.xMin + 2.5f; x < r.xMax - 1.5f; x += 4.2f)
                        {
                            if (rng.NextDouble() < 0.25) continue;   // 일부는 떨어져 나갔다
                            var holder = new GameObject($"CeilingLight_{TowerLayout.ShortLabel(f)}").transform;
                            holder.SetParent(root);
                            var go = PropTools.Instantiate("CeilingLight", holder, 0.55f, 1.3f, rng.Next(2) * 180f + (float)(rng.NextDouble() * 10 - 5));
                            if (go == null) { Object.DestroyImmediate(holder.gameObject); continue; }
                            holder.position = new Vector3(x, y + 2.97f - 0.55f, r.center.y);
                            Object.DestroyImmediate(go.GetComponent<Collider>());
                            lights++;
                        }
                    }
                }
                if (HasProp("CeilingPanel"))
                {
                    for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                    {
                        if (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom || TowerLayout.IsVoidRoom(room)) continue;
                        Rect r = TowerLayout.RoomRect(room);
                        double chance = TowerLayout.IsCorridorRoom(room) ? (r.width >= 10f ? 0.5 : 0.0) : 0.4;
                        if (rng.NextDouble() > chance) continue;
                        var holder = new GameObject($"CeilingPanel_{TowerLayout.ShortLabel(f)}_{room}").transform;
                        holder.SetParent(root);
                        var go = PropTools.Instantiate("CeilingPanel", holder, 0.9f, 2.0f, (float)(rng.NextDouble() * 360));
                        if (go == null) { Object.DestroyImmediate(holder.gameObject); continue; }
                        float px = r.center.x + (float)(rng.NextDouble() * 2 - 1) * Mathf.Max(0.5f, r.width * 0.3f);
                        float pz = r.center.y + (float)(rng.NextDouble() * 2 - 1) * Mathf.Max(0.3f, r.height * 0.2f);
                        holder.position = new Vector3(px, y + 2.97f - 0.9f, pz);
                        Object.DestroyImmediate(go.GetComponent<Collider>());
                        panels++;
                    }
                }
            }

            int windows = BuildWindows(root);
            Debug.Log($"[DecayBuilder] 잔해 {piles}, 형광등 {lights}, 천장 패널 {panels}, 창 {windows}");
        }

        /// <summary>바깥벽에 깨진 창. 차가운 푸른 빛이 방 안으로 조금 새어 든다 (지상층만).</summary>
        static int BuildWindows(Transform root)
        {
            if (!HasTex("WindowBroken.png")) return 0;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DecayTexDir}/WindowBroken.png");
            var mat = GetOrCreateMat("WindowBroken", Color.white);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.85f));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", new Color(0.22f, 0.26f, 0.34f));   // 1을 넘기면 후처리에서 색이 틀어진다
            mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);

            // 바깥벽에 닿은 방마다 창을 낸다. 문(정문, 승강기, 전기실) 자리는 피한다.
            var spots = new List<(int room, char wall, float at)>();
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (TowerLayout.IsCorridorRoom(room) || TowerLayout.IsVoidRoom(room)) continue;
                Rect r = TowerLayout.RoomRect(room);
                if (r.yMin <= 0.01f)
                    foreach (float t in new[] { 0.25f, 0.75f }) if (!(room == TowerLayout.ExitRoom && Mathf.Abs(r.xMin + r.width * t - r.center.x) < 2.6f)) spots.Add((room, 'S', r.xMin + r.width * t));
                if (r.yMax >= TowerLayout.SpanZ - 0.01f)
                    foreach (float t in new[] { 0.25f, 0.75f }) if (!(room == TowerLayout.StartRoom && Mathf.Abs(r.xMin + r.width * t - r.center.x) < 2.6f)) spots.Add((room, 'N', r.xMin + r.width * t));
                if (r.xMin <= 0.01f) spots.Add((room, 'W', r.center.y + (room == 0 ? 3.2f : 0f)));   // 3층 전기실 문(z=4) 자리를 피한다
                if (r.xMax >= TowerLayout.SpanX - 0.01f) spots.Add((room, 'E', r.center.y));
            }
            int count = 0;
            for (int f = TowerLayout.GroundFloor; f < TowerLayout.Floors; f++)
            {
                float y = TowerLayout.FloorY(f);
                foreach (var s in spots)
                {
                    if (!RoomThemes.Get(f, s.room).windowed) continue;
                    Vector3 p; Quaternion rot;
                    const float inset = 0.17f;
                    switch (s.wall)
                    {
                        case 'S': p = new Vector3(s.at, y + 1.7f, inset); rot = Quaternion.Euler(0, 0, 0); break;
                        case 'N': p = new Vector3(s.at, y + 1.7f, TowerLayout.SpanZ - inset); rot = Quaternion.Euler(0, 180, 0); break;
                        case 'W': p = new Vector3(inset, y + 1.7f, s.at); rot = Quaternion.Euler(0, 90, 0); break;
                        default: p = new Vector3(TowerLayout.SpanX - inset, y + 1.7f, s.at); rot = Quaternion.Euler(0, 270, 0); break;
                    }
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Window";
                    q.transform.SetParent(root);
                    q.transform.position = p;
                    q.transform.rotation = rot * Quaternion.Euler(0, 180, 0);   // 쿼드는 -z를 향해 보이므로, 방 안쪽을 보게 돌린다
                    q.transform.localScale = new Vector3(1.3f, 1.75f, 1f);
                    q.GetComponent<Renderer>().sharedMaterial = mat;
                    Object.DestroyImmediate(q.GetComponent<Collider>());

                    var lg = new GameObject("Moonlight").AddComponent<Light>();
                    lg.transform.SetParent(q.transform.parent);
                    lg.transform.position = p + rot * new Vector3(0, -0.2f, 1.0f);
                    lg.type = LightType.Point;
                    lg.range = 7f;
                    lg.intensity = 0.8f;
                    lg.color = new Color(0.55f, 0.7f, 1f);
                    lg.shadows = LightShadows.None;
                    count++;
                }
            }
            return count;
        }
    }
}
