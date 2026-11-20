using System.Collections.Generic;
using Escape.Core;
using Escape.Interaction;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// 실제로 걸어서 오르내리는 계단통. 서쪽/동쪽 계단 홀마다 모든 층을 꿰뚫는 'ㄷ자 꺾임 계단'이 하나씩 있다.
    ///
    /// 홀 평면 (서쪽 홀 기준, 동쪽은 좌우 반전):
    ///   x 4.35~5.85 : 층 바닥과 같은 높이의 승강장. 복도로 나가는 계단 문(x=6)이 여기에 있다.
    ///   x 0.15~4.35 : 북쪽 줄(올라가는 계단)과 남쪽 줄(내려가는 계단), 가운데 벽으로 나뉜다.
    ///   x 0.15~1.49 : 반층 참(층 높이의 절반, 2m). 북쪽 줄 끝에서 남쪽 줄 시작으로 이어진다.
    /// 층마다 승강장에서 북쪽 줄을 따라 올라가면 반층 참, 거기서 남쪽 줄을 따라 올라가면 위층 승강장이다.
    /// 바닥판(슬래브)에는 계단 자리만큼 구멍이 뚫린다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        const float HallZ0 = 10.15f, HallZ1 = 13.85f, HallZc = 12f;   // 계단 홀 안쪽 z 범위와 중심
        const float LaneGap = 0.15f;                                  // 가운데 벽 반 두께
        const int StepsPerFlight = 11;
        const float FlightRise = 2f;                                  // 계단 한 줄(반층)의 높이
        const float Tread = 0.26f;                                    // 디딤판 깊이
        const float StepRise = FlightRise / StepsPerFlight;           // 계단 한 칸 높이 (0.18m)
        const float TopCutWest = 3.6f;                                // 맨 위층에서 올라가는 줄을 막는 벽 위치(서쪽 홀)

        struct Hall
        {
            public bool west;
            public float xl;    // 승강장 가장자리 x (여기서 계단이 시작된다)
            public float xw;    // 계단 쪽 끝 벽의 안쪽 x
            public float d;     // 승강장에서 계단 쪽으로 가는 x 방향 (서쪽 홀은 -1, 동쪽 홀은 +1)
        }

        static Hall HallOf(bool west) =>
            west ? new Hall { west = true, xl = 4.35f, xw = 0.15f, d = -1f }
                 : new Hall { west = false, xl = 19.65f, xw = 23.85f, d = 1f };

        static float LaneUpZ => (HallZc + LaneGap + HallZ1) * 0.5f;     // 올라가는 줄 중심 (북쪽)
        static float LaneDownZ => (HallZ0 + HallZc - LaneGap) * 0.5f;   // 내려가는 줄 중심 (남쪽)
        static float LaneWidth => HallZ1 - (HallZc + LaneGap);

        // ---------- 바닥판 구멍 ----------

        /// <summary>해당 층 바닥판에 뚫는 구멍들 (x, y=z, width, height). 맨 아래층은 구멍이 없다.</summary>
        static List<Rect> SlabHoles(int floor)
        {
            var holes = new List<Rect>();
            if (floor <= 0) return holes;
            foreach (bool west in new[] { true, false })
            {
                var h = HallOf(west);
                float xa = Mathf.Min(h.xl, h.xw), xb = Mathf.Max(h.xl, h.xw);
                if (floor < TowerLayout.Floors - 1)
                {
                    holes.Add(new Rect(xa, HallZ0, xb - xa, HallZ1 - HallZ0));
                }
                else
                {
                    // 맨 위층: 위로 올라가는 계단이 없다. 내려가는 줄과 가운데는 뚫고, 올라가는 줄은 안쪽 절반만 뚫는다 (나머지는 막고 벽을 세운다).
                    holes.Add(new Rect(xa, HallZ0, xb - xa, HallZc + LaneGap - HallZ0));
                    float cut = west ? TopCutWest : 24f - TopCutWest;
                    float ua = Mathf.Min(h.xw, cut), ub = Mathf.Max(h.xw, cut);
                    holes.Add(new Rect(ua, HallZc + LaneGap, ub - ua, HallZ1 - (HallZc + LaneGap)));
                }
            }
            return holes;
        }

        /// <summary>구멍이 뚫린 바닥판. 구멍 경계로 평면을 격자로 나누고, 구멍이 아닌 칸마다 상자를 둔다.</summary>
        static void BuildSlab(Transform root, int floor, float y, float size, Material mat)
        {
            var holes = SlabHoles(floor);
            var xs = new SortedSet<float> { 0f, size };
            var zs = new SortedSet<float> { 0f, size };
            foreach (var r in holes) { xs.Add(r.xMin); xs.Add(r.xMax); zs.Add(r.yMin); zs.Add(r.yMax); }
            var xl = new List<float>(xs); var zl = new List<float>(zs);

            for (int i = 0; i < xl.Count - 1; i++)
            {
                for (int j = 0; j < zl.Count - 1; j++)
                {
                    var c = new Vector2((xl[i] + xl[i + 1]) * 0.5f, (zl[j] + zl[j + 1]) * 0.5f);
                    bool hole = false;
                    foreach (var r in holes) if (r.Contains(c)) { hole = true; break; }
                    if (hole) continue;
                    Box(root, "Slab", new Vector3(c.x, y - 0.5f, c.y), new Vector3(xl[i + 1] - xl[i], 1f, zl[j + 1] - zl[j]), mat);
                }
            }
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ---------- 재질 ----------

        const string StairTexDir = "Assets/Textures/Hospital";
        static Material stairStepMat, stairWallMat, stairRailMat;

        static Material StairStepMaterial()
        {
            if (stairStepMat != null) return stairStepMat;
            string tex = StairTexDir + "/StairConcrete.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null) return stairStepMat = floorMat;
            stairStepMat = TextureMaterial("StairConcrete", tex, new Vector2(1f, 1f), Vector2.zero, 0.15f);
            return stairStepMat;
        }

        static Material StairWallMaterial()
        {
            if (stairWallMat != null) return stairWallMat;
            string tex = StairTexDir + "/StairWall.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null) return stairWallMat = wallMat;
            stairWallMat = TextureMaterial("StairWall", tex, new Vector2(1f, 1f), Vector2.zero, 0.1f);
            return stairWallMat;
        }

        static Material StairRailMaterial()
        {
            if (stairRailMat != null) return stairRailMat;
            stairRailMat = GetOrCreateMat("StairRail", new Color(0.32f, 0.33f, 0.35f));
            stairRailMat.SetFloat("_Metallic", 0.6f);
            stairRailMat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(stairRailMat);
            return stairRailMat;
        }

        /// <summary>계단 문 한 짝: Varco 이미지(방화문 + STAIRS 표지판)의 왼쪽/오른쪽 절반. 이미지의 어두운 가장자리는 잘라 쓴다.</summary>
        static Material StairDoorLeafMaterial(bool left)
        {
            const string tex = "Assets/Textures/Hospital/StairDoor.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null) return doorMat;
            float half = 0.621f * 0.5f;
            var mat = TextureMaterial(left ? "StairDoorL" : "StairDoorR", tex, new Vector2(half, 0.915f),
                new Vector2(0.193f + (left ? 0f : half), 0.0625f), 0.2f);
            mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- 계단통 ----------

        static void BuildStairwells(Transform parent)
        {
            var root = new GameObject("Stairwells").transform;
            root.SetParent(parent);
            var stepMat = StairStepMaterial();
            var wallM = StairWallMaterial();
            var railM = StairRailMaterial();

            float yBottom = TowerLayout.FloorY(0);
            float yTop = TowerLayout.FloorY(TowerLayout.Floors - 1) + WallHeight;
            int pairs = TowerLayout.Floors - 1;

            foreach (bool west in new[] { true, false })
            {
                var h = HallOf(west);
                var wr = new GameObject(west ? "Stairwell_West" : "Stairwell_East").transform;
                wr.SetParent(root);
                float xmid = (h.xl + h.xw) * 0.5f, xlen = Mathf.Abs(h.xl - h.xw);

                // 가운데 벽: 두 줄 사이를 막아 계단 옆으로 떨어지지 않게 한다 (맨 아래부터 맨 위까지). 반층 참에서는 두 줄이 이어지므로 계단 구간까지만 세운다.
                float runLen = StepsPerFlight * Tread;
                Box(wr, "CenterWall", new Vector3(h.xl + h.d * runLen * 0.5f, (yBottom + yTop) * 0.5f, HallZc), new Vector3(runLen, yTop - yBottom, LaneGap * 2f), wallM);

                for (int n = 0; n < pairs; n++)
                {
                    float y0 = TowerLayout.FloorY(n);
                    float mid = y0 + FlightRise;
                    float xm = h.xl + h.d * StepsPerFlight * Tread;   // 반층 참이 시작되는 x

                    // 1) 올라가는 줄 (북쪽): 승강장 -> 반층 참, 올라갈수록 승강장에서 멀어진다
                    for (int i = 0; i < StepsPerFlight; i++)
                    {
                        float xa = h.xl + h.d * i * Tread, xb = h.xl + h.d * (i + 1) * Tread;
                        float top = y0 + (i + 1) * StepRise;
                        Box(wr, "StepUp", new Vector3((xa + xb) * 0.5f, (y0 + top) * 0.5f, LaneUpZ),
                            new Vector3(Tread, top - y0, LaneWidth), stepMat);
                    }

                    // 2) 반층 참: 두 줄을 잇는 평평한 바닥
                    float platX0 = Mathf.Min(xm, h.xw), platX1 = Mathf.Max(xm, h.xw);
                    Box(wr, "MidLanding", new Vector3((platX0 + platX1) * 0.5f, mid - 0.15f, (HallZ0 + HallZ1) * 0.5f),
                        new Vector3(platX1 - platX0, 0.3f, HallZ1 - HallZ0), stepMat);

                    // 3) 위층으로 이어지는 줄 (남쪽): 반층 참 -> 위층 승강장
                    for (int j = 0; j < StepsPerFlight; j++)
                    {
                        float xa = xm - h.d * j * Tread, xb = xm - h.d * (j + 1) * Tread;
                        float top = mid + (j + 1) * StepRise;
                        Box(wr, "StepDown", new Vector3((xa + xb) * 0.5f, (mid + top) * 0.5f, LaneDownZ),
                            new Vector3(Tread, top - mid, LaneWidth), stepMat);
                    }

                    // 난간: 가운데 벽 쪽에 줄마다 하나씩
                    Rail(wr, new Vector3(h.xl, y0 + 0.9f, HallZc + LaneGap + 0.08f), new Vector3(xm, mid + 0.9f, HallZc + LaneGap + 0.08f), railM);
                    Rail(wr, new Vector3(xm, mid + 0.9f, HallZc - LaneGap - 0.08f), new Vector3(h.xl, y0 + 2f * FlightRise + 0.9f, HallZc - LaneGap - 0.08f), railM);
                }

                // 바닥판 구멍 둘레를 막는 띠: 층 사이 1m 틈으로 바깥이 보이지 않게 한다
                for (int f = 1; f < TowerLayout.Floors; f++)
                {
                    float yb = TowerLayout.FloorY(f) - 0.5f;
                    float farX = west ? 0f : 24f;
                    Box(wr, "ShaftBand", new Vector3(farX, yb, HallZc), new Vector3(WallThick, 1f, HallZ1 - HallZ0 + WallThick * 2f), wallM);
                    Box(wr, "ShaftBand", new Vector3(xmid, yb, 10f), new Vector3(xlen, 1f, WallThick), wallM);
                    Box(wr, "ShaftBand", new Vector3(xmid, yb, 14f), new Vector3(xlen, 1f, WallThick), wallM);
                }

                // 맨 위층: 올라가는 줄을 막는 벽 (아래 계단의 윗부분 위로 떨어지지 않게)
                float cutX = west ? TopCutWest : 24f - TopCutWest;
                float yTopFloor = TowerLayout.FloorY(TowerLayout.Floors - 1);
                Box(wr, "TopBlock", new Vector3(cutX, yTopFloor + WallHeight * 0.5f, LaneUpZ), new Vector3(0.3f, WallHeight, LaneWidth), wallM);

                // 층마다 승강장에 희미한 비상등 (어둠 속에서 계단 홀을 알아볼 수 있게)
                for (int f = 0; f < TowerLayout.Floors; f++)
                {
                    var lg = new GameObject($"StairLamp_{TowerLayout.ShortLabel(f)}").AddComponent<Light>();
                    lg.transform.SetParent(wr);
                    lg.transform.position = new Vector3(h.xl - h.d * 0.2f, TowerLayout.FloorY(f) + 2.5f, HallZc);
                    lg.type = LightType.Point;
                    lg.range = 5.5f;
                    lg.intensity = 0.55f;
                    lg.color = new Color(0.78f, 1f, 0.85f);
                    lg.shadows = LightShadows.None;
                }
            }
        }

        static void Rail(Transform parent, Vector3 a, Vector3 b, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Handrail";
            go.transform.SetParent(parent);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation((b - a).normalized);
            go.transform.localScale = new Vector3(0.05f, 0.05f, (b - a).magnitude);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 난간은 보이기만 한다
        }

        // ---------- 추적자가 층을 옮길 때 쓰는 보이지 않는 계단 입구 ----------

        /// <summary>
        /// 승강장에서 계단 줄로 들어가는 자리. 플레이어는 직접 걸어서 오르내리고, 추적자는 이 자리에서 층을 옮긴다.
        /// 올라가는 입구(북쪽 줄)의 짝은 위층의 내려가는 입구(남쪽 줄)다.
        /// </summary>
        static StairPortal MakePortal(Transform parent, int floor, int room, bool up)
        {
            bool west = room == TowerLayout.WestStairRoom;
            var h = HallOf(west);
            float floorY = TowerLayout.FloorY(floor);
            float z = up ? LaneUpZ : LaneDownZ;

            var go = new GameObject($"Stair{(up ? "Up" : "Down")}_{TowerLayout.ShortLabel(floor)}_{room}");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(h.xl, floorY + 0.1f, z);

            var exit = new GameObject("ArrivalPoint").transform;
            exit.SetParent(go.transform);
            exit.position = new Vector3(h.xl - h.d * 0.6f, floorY + 0.1f, z);   // 승강장 쪽
            exit.rotation = Quaternion.Euler(0f, west ? 90f : 270f, 0f);

            var portal = go.AddComponent<StairPortal>();
            portal.floor = floor;
            portal.room = room;
            portal.up = up;
            portal.exitPoint = exit;
            EditorUtility.SetDirty(portal);
            return portal;
        }
    }
}
