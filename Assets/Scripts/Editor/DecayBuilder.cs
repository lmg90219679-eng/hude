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

            // 방 바닥: 지하는 깨진 타일, 지상은 이끼 낀 방과 잔해가 흩어진 방을 섞는다
            var moss = DecayMat("FloorMoss", "FloorMoss.png", Vector2.one, 0.35f, 0.9f);
            var debris = DecayMat("FloorDebris", "FloorDebris.png", Vector2.one, 0.2f);
            var tileOld = DecayMat("FloorTileOld", "FloorTileOld.png", Vector2.one, 0.3f);
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom) continue;
                Material m;
                float tile = 4f;
                if (floor <= 1) { m = tileOld; tile = 3f; }
                else if (room == TowerLayout.CenterRoom) m = debris;
                else m = ((room + floor) % 2 == 0) ? moss : debris;
                if (m == null) continue;

                Rect r = TowerLayout.RoomRect(room);
                var inner = new Rect(r.xMin + 0.15f, r.yMin + 0.15f, r.width - 0.3f, r.height - 0.3f);
                FlatQuad(root, $"Floor_{room}", inner, y + 0.012f, true, tile, m);
            }
            // 계단 홀 승강장
            if (debris != null)
            {
                FlatQuad(root, "Landing_W", new Rect(4.35f, HallZ0, 1.5f, HallZ1 - HallZ0), y + 0.012f, true, 4f, debris);
                FlatQuad(root, "Landing_E", new Rect(18.15f, HallZ0, 1.5f, HallZ1 - HallZ0), y + 0.012f, true, 4f, debris);
            }

            // 천장: 위층 바닥판의 계단 구멍은 비워 둔다
            var ceil = DecayMat("CeilingDecay", "CeilingDecay.png", Vector2.one, 0.1f, 0.8f);
            if (ceil != null)
            {
                var holes = floor < TowerLayout.Floors - 1 ? SlabHoles(floor + 1) : new List<Rect>();
                var xs = new SortedSet<float> { 0f, TowerLayout.Span };
                var zs = new SortedSet<float> { 0f, TowerLayout.Span };
                foreach (var h in holes) { xs.Add(h.xMin); xs.Add(h.xMax); zs.Add(h.yMin); zs.Add(h.yMax); }
                var xl = xs.ToList(); var zl = zs.ToList();
                for (int i = 0; i < xl.Count - 1; i++)
                    for (int j = 0; j < zl.Count - 1; j++)
                    {
                        var c = new Vector2((xl[i] + xl[i + 1]) * 0.5f, (zl[j] + zl[j + 1]) * 0.5f);
                        if (holes.Any(h => h.Contains(c))) continue;
                        FlatQuad(root, "Ceiling", Rect.MinMaxRect(xl[i], zl[j], xl[i + 1], zl[j + 1]), y + 2.97f, false, 4f, ceil);
                    }
            }

            // 노출된 배관: 중앙 복도 천장을 따라 세 가닥, 방마다 한 가닥씩 (무작위)
            var rust = RustMaterial();
            Pipe(root, new Vector3(6.2f, y + 2.8f, 10.6f), new Vector3(17.8f, y + 2.8f, 10.6f), 0.13f, rust);
            Pipe(root, new Vector3(6.2f, y + 2.72f, 13.4f), new Vector3(17.8f, y + 2.72f, 13.4f), 0.1f, rust);
            Pipe(root, new Vector3(6.2f, y + 2.86f, 11.1f), new Vector3(17.8f, y + 2.86f, 11.1f), 0.07f, rust);
            var rng = new System.Random(9100 + floor);
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (room == 3 || room == 4 || room == 5) continue;
                if (rng.NextDouble() < 0.4) continue;
                Rect r = TowerLayout.RoomRect(room);
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

        static void AddNavObstacle(GameObject holder, Bounds b)
        {
            var col = holder.AddComponent<BoxCollider>();
            col.center = holder.transform.InverseTransformPoint(b.center);
            col.size = b.size;
            var ob = holder.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box;
            ob.center = col.center;
            ob.size = b.size;
            ob.carving = true;
            ob.carveOnlyStationary = true;
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
                    foreach (float x in new[] { 8.2f, 12f, 15.8f })
                    {
                        var holder = new GameObject($"CeilingLight_{TowerLayout.ShortLabel(f)}").transform;
                        holder.SetParent(root);
                        var go = PropTools.Instantiate("CeilingLight", holder, 0.55f, 1.3f, (x > 12f ? 0f : 180f) + (float)(rng.NextDouble() * 10 - 5));
                        if (go == null) { Object.DestroyImmediate(holder.gameObject); continue; }
                        holder.position = new Vector3(x, y + 2.97f - 0.55f, 12f);
                        Object.DestroyImmediate(go.GetComponent<Collider>());
                        lights++;
                    }
                }
                if (HasProp("CeilingPanel"))
                {
                    foreach (int room in new[] { 1, 4, 6, 8 })
                    {
                        if (rng.NextDouble() < 0.35) continue;
                        Rect r = TowerLayout.RoomRect(room);
                        var holder = new GameObject($"CeilingPanel_{TowerLayout.ShortLabel(f)}_{room}").transform;
                        holder.SetParent(root);
                        var go = PropTools.Instantiate("CeilingPanel", holder, 0.9f, 2.0f, (float)(rng.NextDouble() * 360));
                        if (go == null) { Object.DestroyImmediate(holder.gameObject); continue; }
                        float px = r.center.x + (float)(rng.NextDouble() * 2 - 1) * Mathf.Max(0.5f, r.width * 0.25f);
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

            // (방, 벽, 위치) : 벽 N/S/W/E 위의 좌표 (벽을 따라)
            var spots = new List<(int room, char wall, float at)>
            {
                (0, 'S', 3.5f), (1, 'S', 9.0f), (1, 'S', 15.0f), (2, 'S', 20.5f),
                (6, 'N', 4.5f), (8, 'N', 19.5f), (0, 'W', 8.0f), (6, 'W', 19.0f),
                (2, 'E', 4.0f), (8, 'E', 19.0f),
            };
            int count = 0;
            for (int f = TowerLayout.GroundFloor; f < TowerLayout.Floors; f++)
            {
                float y = TowerLayout.FloorY(f);
                foreach (var s in spots)
                {
                    Vector3 p; Quaternion rot;
                    const float inset = 0.17f;
                    switch (s.wall)
                    {
                        case 'S': p = new Vector3(s.at, y + 1.7f, inset); rot = Quaternion.Euler(0, 0, 0); break;
                        case 'N': p = new Vector3(s.at, y + 1.7f, TowerLayout.Span - inset); rot = Quaternion.Euler(0, 180, 0); break;
                        case 'W': p = new Vector3(inset, y + 1.7f, s.at); rot = Quaternion.Euler(0, 90, 0); break;
                        default: p = new Vector3(TowerLayout.Span - inset, y + 1.7f, s.at); rot = Quaternion.Euler(0, 270, 0); break;
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
