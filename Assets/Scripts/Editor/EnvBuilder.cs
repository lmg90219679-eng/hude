using System.Collections.Generic;
using System.Linq;
using Escape.Core;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// 방마다 다른 환경을 그린다 (RoomThemes가 정한 바닥·벽 질감, 색조, 조명).
    /// - 방 바닥 덧칠 (이끼, 파편, 타일, 마룻바닥, 카펫, 핏자국 등)
    /// - 벽: 방마다 자기 쪽 면에 자기 벽 질감을 입힌다 (한 방의 벽 면 전부를 메시 하나로 합친다)
    /// - 천장, 방 조명(색, 깜박임)
    /// 질감 파일이 없으면 있는 질감으로 대신한다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        static readonly string[] FloorFiles =
        {
            "FloorMoss.png", "FloorDebris.png", "FloorTileOld.png", "FloorConcrete.png",
            "FloorWood.png", "FloorLino.png", "FloorCarpet.png", "FloorBlood.png",
        };
        static readonly string[] WallFiles =
        {
            "WallDecay.png", "WallTile.png", "WallBlue.png", "WallWood.png", "WallGraffiti.png", "WallPaper.png",
        };

        /// <summary>질감 + 색조 재질. 파일이 없으면 대체 질감을 쓴다.</summary>
        static Material EnvMat(string[] files, int idx, int tint, float smooth, string fallback)
        {
            string file = files[idx % files.Length];
            if (!HasTex(file)) file = fallback;
            if (!HasTex(file)) return null;
            string name = $"Env_{System.IO.Path.GetFileNameWithoutExtension(file)}_t{tint}";
            if (decayCache.TryGetValue(name, out var m) && m != null) return m;
            var mat = TextureMaterial(name, $"{DecayTexDir}/{file}", Vector2.one, Vector2.zero, smooth);
            var c = RoomThemes.TintColors[tint % RoomThemes.Tints] * 0.82f;
            mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
            EditorUtility.SetDirty(mat);
            decayCache[name] = mat;
            return mat;
        }

        static Material EnvFloorMat(RoomEnv e) => EnvMat(FloorFiles, e.floorTex, e.tint, 0.3f, "FloorDebris.png");
        static Material EnvWallMat(RoomEnv e) => EnvMat(WallFiles, e.wallTex, e.tint, 0.1f, "WallDecay.png");

        // ---------- 벽 면 (방마다 하나로 합친 메시) ----------

        class SkinMesh
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public Material mat;
        }

        static readonly Dictionary<int, SkinMesh> skins = new Dictionary<int, SkinMesh>();

        static void AddSkinQuad(int room, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, float len)
        {
            if (!skins.TryGetValue(room, out var sm))
            {
                sm = new SkinMesh { mat = EnvWallMat(RoomThemes.Get(wallFloor, room)) };
                skins[room] = sm;
            }
            if (sm.mat == null) return;
            int i = sm.v.Count;
            sm.v.Add(bl); sm.v.Add(tl); sm.v.Add(tr); sm.v.Add(br);
            float u = len / 3f;
            sm.uv.Add(new Vector2(0, 0)); sm.uv.Add(new Vector2(0, 1)); sm.uv.Add(new Vector2(u, 1)); sm.uv.Add(new Vector2(u, 0));
            sm.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        /// <summary>벽 한 조각(코어 위)의 양쪽 면에, 그 면을 보는 방의 벽 질감을 입힌다.</summary>
        static void AddWallSkins(float y, bool vertical, float line, float a, float b)
        {
            float len = b - a, mid = (a + b) * 0.5f;
            float y0 = y, y1 = y + WallHeight;
            const float off = WallThick * 0.5f + 0.004f;
            if (vertical)
            {
                int west = TowerLayout.RoomAt(new Vector3(line - 0.3f, 0, mid));
                int east = TowerLayout.RoomAt(new Vector3(line + 0.3f, 0, mid));
                if (west >= 0 && !TowerLayout.IsVoidRoom(west))   // 서쪽 방에서 동쪽을 본다: 오른쪽이 -z
                    AddSkinQuad(west, new Vector3(line - off, y0, b), new Vector3(line - off, y1, b), new Vector3(line - off, y1, a), new Vector3(line - off, y0, a), len);
                if (east >= 0 && !TowerLayout.IsVoidRoom(east))   // 동쪽 방에서 서쪽을 본다: 오른쪽이 +z
                    AddSkinQuad(east, new Vector3(line + off, y0, a), new Vector3(line + off, y1, a), new Vector3(line + off, y1, b), new Vector3(line + off, y0, b), len);
            }
            else
            {
                int south = TowerLayout.RoomAt(new Vector3(mid, 0, line - 0.3f));
                int north = TowerLayout.RoomAt(new Vector3(mid, 0, line + 0.3f));
                if (south >= 0 && !TowerLayout.IsVoidRoom(south))  // 남쪽 방에서 북쪽을 본다: 오른쪽이 +x
                    AddSkinQuad(south, new Vector3(a, y0, line - off), new Vector3(a, y1, line - off), new Vector3(b, y1, line - off), new Vector3(b, y0, line - off), len);
                if (north >= 0 && !TowerLayout.IsVoidRoom(north))  // 북쪽 방에서 남쪽을 본다: 오른쪽이 -x
                    AddSkinQuad(north, new Vector3(b, y0, line + off), new Vector3(b, y1, line + off), new Vector3(a, y1, line + off), new Vector3(a, y0, line + off), len);
            }
        }

        static void FlushWallSkins(Transform parent, int floor)
        {
            var root = new GameObject($"WallSkins_{TowerLayout.ShortLabel(floor)}").transform;
            root.SetParent(parent);
            foreach (var kv in skins)
            {
                var sm = kv.Value;
                if (sm.v.Count == 0 || sm.mat == null) continue;
                var mesh = new Mesh { name = $"WallSkin_{floor}_{kv.Key}" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(sm.v);
                mesh.SetUVs(0, sm.uv);
                mesh.SetTriangles(sm.t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject($"WallSkin_{kv.Key}");
                go.transform.SetParent(root);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = sm.mat;
            }
            skins.Clear();
        }

        // ---------- 바닥, 천장, 조명 ----------

        static void BuildRoomSurfaces(Transform root, int floor)
        {
            float y = TowerLayout.FloorY(floor);
            var ceilBase = DecayMat("CeilingDecay", "CeilingDecay.png", Vector2.one, 0.1f, 0.8f);
            for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
            {
                if (TowerLayout.IsVoidRoom(room)) continue;
                var env = RoomThemes.Get(floor, room);
                Rect r = TowerLayout.RoomRect(room);
                bool hall = (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom) && HallHasStairs(floor, room);

                if (!hall)
                {
                    var fm = EnvFloorMat(env);
                    if (fm != null)
                    {
                        var inner = new Rect(r.xMin + 0.15f, r.yMin + 0.15f, r.width - 0.3f, r.height - 0.3f);
                        float tile = (env.floorTex == 2 || env.floorTex == 7) ? 3f : 4f;
                        FlatQuad(root, $"Floor_{room}", inner, y + 0.012f, true, tile, fm);
                    }
                }

                // 천장: 위층 바닥판의 계단 구멍은 비워 둔다
                var cm = EnvMat(new[] { "CeilingDecay.png" }, 0, env.tint, 0.1f, "CeilingDecay.png") ?? ceilBase;
                if (cm == null) continue;
                var holes = floor < TowerLayout.Floors - 1 ? SlabHoles(floor + 1).Where(h => h.Overlaps(r)).ToList() : new List<Rect>();
                if (holes.Count == 0)
                {
                    FlatQuad(root, $"Ceiling_{room}", r, y + 2.97f, false, 4f, cm);
                    continue;
                }
                var xs = new SortedSet<float> { r.xMin, r.xMax };
                var zs = new SortedSet<float> { r.yMin, r.yMax };
                foreach (var h in holes) { xs.Add(Mathf.Clamp(h.xMin, r.xMin, r.xMax)); xs.Add(Mathf.Clamp(h.xMax, r.xMin, r.xMax)); zs.Add(Mathf.Clamp(h.yMin, r.yMin, r.yMax)); zs.Add(Mathf.Clamp(h.yMax, r.yMin, r.yMax)); }
                var xl = xs.ToList(); var zl = zs.ToList();
                for (int i = 0; i < xl.Count - 1; i++)
                    for (int j = 0; j < zl.Count - 1; j++)
                    {
                        var c = new Vector2((xl[i] + xl[i + 1]) * 0.5f, (zl[j] + zl[j + 1]) * 0.5f);
                        if (holes.Any(h => h.Contains(c))) continue;
                        FlatQuad(root, "Ceiling", Rect.MinMaxRect(xl[i], zl[j], xl[i + 1], zl[j + 1]), y + 2.97f, false, 4f, cm);
                    }
            }
        }

        /// <summary>방마다 조명 색이 다른 희미한 조명. 일부 방은 불이 나가 있고, 일부는 깜박인다. 지하는 발전기가 복구돼야 켜진다(PowerLight).</summary>
        static void BuildRoomLights(Transform parent)
        {
            var root = new GameObject("RoomLights").transform;
            root.SetParent(parent);
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                {
                    if (room == TowerLayout.WestStairRoom || room == TowerLayout.EastStairRoom || TowerLayout.IsVoidRoom(room)) continue;
                    var env = RoomThemes.Get(f, room);
                    Rect rc = TowerLayout.RoomRect(room);
                    int h = (room * 7919 + f * 104729) & 0x7fffffff;
                    bool corridor = TowerLayout.IsCorridorRoom(room);
                    if (!corridor && h % 100 < 22) continue;   // 불이 나간 방

                    bool alongX = rc.width >= rc.height;
                    float length = alongX ? rc.width : rc.height;
                    int n = corridor ? Mathf.Max(1, Mathf.RoundToInt(length / 9f)) : 1;
                    for (int i = 0; i < n; i++)
                    {
                        float t = (i + 0.5f) / n;
                        Vector3 pos = alongX
                            ? new Vector3(rc.xMin + rc.width * t, TowerLayout.FloorY(f) + 2.6f, rc.center.y)
                            : new Vector3(rc.center.x, TowerLayout.FloorY(f) + 2.6f, rc.yMin + rc.height * t);
                        var go = new GameObject($"RoomLight_{TowerLayout.ShortLabel(f)}_{room}_{i}");
                        go.transform.SetParent(root);
                        go.transform.position = pos;
                        var lg = go.AddComponent<Light>();
                        lg.type = LightType.Point;
                        lg.range = corridor ? 8f : Mathf.Clamp(Mathf.Max(rc.width, rc.height) * 0.75f, 5f, 9f);
                        lg.color = RoomThemes.LightColorTable[env.light % RoomThemes.LightColors];
                        lg.intensity = 0.3f;
                        lg.shadows = LightShadows.None;
                        if (f <= 1) go.AddComponent<PowerLight>();                 // 지하: 발전기 복구 전에는 꺼져 있다
                        else if (env.flicker == 1)
                        {
                            var fl = go.AddComponent<FlickerLight>();
                            fl.SetBase(0.34f);
                        }
                    }
                }
            }
        }
    }
}
