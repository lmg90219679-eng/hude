using System.Collections.Generic;
using System.IO;
using System.Linq;
using Escape.Core;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// - 새로 가져온 소품 모델의 텍스처와 재질을 자동으로 준비한다 (Setup Props를 따로 누르지 않아도 된다)
    /// - 서랍 안에 보이는 물건(열쇠, 배터리, 약병 등) 모델을 Resources/Items prefab으로 만든다 (실행 중에 불러 쓴다)
    /// - 방마다 잡동사니(상자 더미, 걸레 양동이, 부서진 의자 등)를 벽을 따라 놓아 방을 채운다
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        // 서랍 안 물건: 이름, 가장 긴 변(m), 눕혀서 놓을지
        static readonly (string name, float size, bool lie)[] ItemModelSpecs =
        {
            ("RoomKey", 0.17f, true), ("KeyPiece", 0.15f, true), ("MasterKey", 0.22f, true),
            ("Battery", 0.12f, true), ("ClothRag", 0.22f, false), ("Lighter", 0.075f, true),
            ("Crowbar", 0.45f, true), ("WireCoil", 0.16f, false), ("SpareFlashlight", 0.22f, true),
            ("SecurityCard", 0.14f, true),
            ("PillBottle", 0.085f, false), ("Syringe", 0.15f, true), ("BandageRoll", 0.1f, false),
            ("Notebook", 0.15f, true), ("Scalpel", 0.14f, true),
        };

        const string ItemPrefabDir = "Assets/Resources/Items";

        /// <summary>텍스처 폴더가 없는 소품(새로 가져온 모델)만 준비한다.</summary>
        static void EnsureNewPropsReady()
        {
            if (!Directory.Exists(PropTools.PropsDir)) return;
            foreach (var dir in Directory.GetDirectories(PropTools.PropsDir))
            {
                string name = Path.GetFileName(dir);
                if (!File.Exists($"{dir}/{name}.fbx")) continue;
                if (Directory.Exists(dir + "/Textures")) continue;
                PropTools.SetupOne(name);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void BuildItemPrefabs()
        {
            Directory.CreateDirectory(ItemPrefabDir);
            int made = 0;
            foreach (var spec in ItemModelSpecs)
            {
                if (!File.Exists($"{PropTools.PropsDir}/{spec.name}/{spec.name}.fbx")) continue;

                var holder = new GameObject(spec.name);
                var go = PropTools.Instantiate(spec.name, holder.transform, 1f, 0f, 0f, 1f);
                if (go == null) { Object.DestroyImmediate(holder); continue; }

                var b = PropTools.Bounds(go);
                float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                float scale = longest > 0.0001f ? spec.size / longest : 1f;
                PropTools.Normalize(go, 1f, 0f, 0f, scale, spec.lie ? Quaternion.Euler(90f, 0f, 0f) : (Quaternion?)null);

                PrefabUtility.SaveAsPrefabAsset(holder, $"{ItemPrefabDir}/{spec.name}.prefab");
                Object.DestroyImmediate(holder);
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemBuilder] 서랍 안 물건 모델 {made}/{ItemModelSpecs.Length}개 준비");
        }

        // ---------- 방 잡동사니 ----------

        static readonly string[] ClutterModels = { "CardboardBoxes", "MopBucket", "BrokenChair", "WoodenCrate", "MedicalWasteBin" };

        /// <summary>
        /// 일반 방의 벽 가까이에 잡동사니를 2~5개 놓는다. 문 앞, 이미 놓인 가구·소품·옷장과 겹치는 자리는 피한다.
        /// NavMesh를 굽기 전에 놓아서 추적자가 비켜 다니게 한다.
        /// </summary>
        static void BuildClutter(Transform parent)
        {
            var root = new GameObject("RoomClutter").transform;
            root.SetParent(parent);
            Physics.SyncTransforms();

            int placed = 0;
            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                var doorPts = doorRequests.Where(d => d.floor == f).Select(d => new Vector2(d.center.x, d.center.z))
                    .Concat(Edges.Select(e => new Vector2(e.x, e.z))).ToList();

                for (int room = 0; room < TowerLayout.RoomsPerFloor; room++)
                {
                    if (TowerLayout.IsVoidRoom(room) || TowerLayout.IsCorridorRoom(room)) continue;
                    if (HallHasStairsInRoom(f, room)) continue;
                    Rect r = TowerLayout.RoomRect(room);
                    float area = r.width * r.height;
                    if (area < 30f) continue;

                    var rng = new System.Random(f * 7919 + room * 131 + 17);
                    int want = Mathf.Clamp(2 + (int)(area / 60f) + rng.Next(2), 2, 5);
                    float y = TowerLayout.FloorY(f);
                    int tries = 0, got = 0;
                    while (got < want && tries++ < 40)
                    {
                        // 벽 가까이의 무작위 자리 (0.8m 안쪽)
                        int side = rng.Next(4);
                        float inset = 0.8f;
                        float x, z, yaw;
                        if (side < 2)
                        {
                            x = Mathf.Lerp(r.xMin + 1.2f, r.xMax - 1.2f, (float)rng.NextDouble());
                            z = side == 0 ? r.yMin + inset : r.yMax - inset;
                            yaw = side == 0 ? 0f : 180f;
                        }
                        else
                        {
                            z = Mathf.Lerp(r.yMin + 1.2f, r.yMax - 1.2f, (float)rng.NextDouble());
                            x = side == 2 ? r.xMin + inset : r.xMax - inset;
                            yaw = side == 2 ? 90f : 270f;
                        }
                        yaw += (float)(rng.NextDouble() * 50.0 - 25.0);

                        var p2 = new Vector2(x, z);
                        if (doorPts.Any(d => Vector2.Distance(d, p2) < 2.6f)) continue;
                        if (debrisSpots.Any(d => d.floor == f && Vector2.Distance(new Vector2(d.pos.x, d.pos.z), p2) < 2.2f)) continue;

                        string model = ClutterModels[rng.Next(ClutterModels.Length)];
                        string resolved = ResolveModel(model);
                        if (resolved == null || !PropSpecs.TryGetValue(resolved, out var spec)) continue;

                        // 이미 놓인 것(벽, 가구, 소품, 옷장)과 겹치면 건너뛴다
                        var center = new Vector3(x, y + 0.9f, z);
                        float half = Mathf.Max(spec.maxSide, 0.7f) * 0.5f + 0.15f;
                        if (Physics.CheckBox(center, new Vector3(half, 0.55f, half), Quaternion.Euler(0f, yaw, 0f))) continue;

                        if (PlacePropAt(root, f, resolved, new Vector3(x, y, z), yaw, null)) { got++; placed++; }
                    }
                }
            }
            Debug.Log($"[ItemBuilder] 방 잡동사니 {placed}개 배치");
        }
        // ---------- 배치 검증과 재배치 ----------

        class LayoutEntry { public Transform t; public Bounds b; public int priority; public int floor; }

        /// <summary>
        /// 소품·서랍장·옷장이 서로 겹치거나, 문 앞과 숨는 곳 출구를 막으면 우선순위가 낮은 쪽을 치운다.
        /// 우선순위: 서랍장·옷장·침대(숨는 곳) 3 &gt; 일반 소품 2 &gt; 잡동사니 1. NavMesh를 굽기 전에 부른다.
        /// </summary>
        static void ResolveLayout(Transform parent)
        {
            Physics.SyncTransforms();
            var entries = new List<LayoutEntry>();
            void Collect(Transform t, int priority)
            {
                var col = t.GetComponent<BoxCollider>() ?? t.GetComponentInChildren<BoxCollider>();
                if (col == null) return;
                entries.Add(new LayoutEntry { t = t, b = col.bounds, priority = priority, floor = TowerLayout.FloorOf(col.bounds.center.y) });
            }
            var hp = GameObject.Find("Tower/HospitalProps");
            if (hp != null) foreach (Transform t in hp.transform) Collect(t, t.GetComponent<Escape.Interaction.HidingSpot>() != null ? 3 : 2);
            var cl = GameObject.Find("Tower/RoomClutter");
            if (cl != null) foreach (Transform t in cl.transform) Collect(t, 1);
            foreach (var c in Object.FindObjectsByType<Escape.Interaction.Container>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Collect(c.transform, 3);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.name == "Locker")) Collect(t, 3);

            int doorRemoved = 0, exitRemoved = 0, overlapRemoved = 0;
            var gone = new HashSet<LayoutEntry>();
            void Kill(LayoutEntry e) { if (gone.Add(e)) Object.DestroyImmediate(e.t.gameObject); }

            // 1) 문 앞: 문 중심에서 양쪽으로 0.25~1.55m, 문 폭 90%
            foreach (var req in doorRequests)
            {
                bool alongX = Mathf.Abs(req.along.x) > 0.5f;
                float w = req.width > 0f ? req.width : DoorWidth;
                Vector3 n = alongX ? Vector3.forward : Vector3.right;
                foreach (float side in new[] { 1f, -1f })
                {
                    var zone = new Bounds(req.center + n * side * 0.9f + Vector3.up * 1f, alongX ? new Vector3(w * 0.9f, 2f, 1.3f) : new Vector3(1.3f, 2f, w * 0.9f));
                    foreach (var e in entries) if (!gone.Contains(e) && e.floor == req.floor && zone.Intersects(e.b)) { Kill(e); doorRemoved++; }
                }
            }

            // 2) 숨는 곳 출구 앞
            foreach (var hs in Object.FindObjectsByType<Escape.Interaction.HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hs == null) continue;   // 앞에서 치운 소품의 숨는 곳
                var ex = hs.transform.Find("ExitPoint"); if (ex == null) continue;
                var zone = new Bounds(ex.position + Vector3.up * 1f, new Vector3(0.9f, 2f, 0.9f));
                foreach (var e in entries)
                {
                    if (gone.Contains(e) || e.t == null || e.t == hs.transform || e.t.IsChildOf(hs.transform)) continue;
                    if (zone.Intersects(e.b)) { Kill(e); exitRemoved++; }
                }
            }

            // 3) 서로 겹침: 우선순위가 낮은 쪽을 치운다 (같으면 나중 것)
            var alive = entries.Where(e => !gone.Contains(e)).ToList();
            for (int i = 0; i < alive.Count; i++)
            {
                if (gone.Contains(alive[i])) continue;
                for (int j = i + 1; j < alive.Count; j++)
                {
                    if (gone.Contains(alive[j]) || alive[i].floor != alive[j].floor) continue;
                    var a = alive[i].b; var b = alive[j].b;
                    a.Expand(-0.1f); b.Expand(-0.1f);
                    if (!a.Intersects(b)) continue;
                    var loser = alive[i].priority < alive[j].priority ? alive[i] : alive[j];
                    Kill(loser); overlapRemoved++;
                    if (loser == alive[i]) break;
                }
            }
            Debug.Log($"[ItemBuilder] 배치 정리: 문 앞 {doorRemoved}개, 숨는 곳 출구 {exitRemoved}개, 겹침 {overlapRemoved}개를 치움 (남은 소품 {entries.Count - gone.Count}개)");
        }
    }
}
