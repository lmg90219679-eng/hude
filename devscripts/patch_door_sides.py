p = "Assets/Scripts/Editor/TowerBuilder.cs"
s = open(p, encoding="utf-8").read()


def rep(old, new):
    global s
    assert old in s, old
    s = s.replace(old, new, 1)


# 1) leaf: two materials (one per face), each matching the room on that side
rep('''                    leaf.GetComponent<MeshFilter>().sharedMesh = DoorLeafMesh(alongX, -dir * AlongSign(req, alongX));''',
    '''                    leaf.GetComponent<MeshFilter>().sharedMesh = DoorLeafMesh(alongX, -dir * AlongSign(req, alongX));
                    // 문 앞뒤 면은 각각 그 면이 보이는 방의 벽 질감에 어울리는 문 이미지를 쓴다
                    Vector3 faceN = alongX ? Vector3.forward : Vector3.right;
                    Material matPlus = mat, matMinus = mat;
                    if (req.kind == DoorKind.Normal)
                    {
                        matPlus = DoorMaterialForSide(req.floor, req.center + faceN * 0.7f) ?? mat;
                        matMinus = DoorMaterialForSide(req.floor, req.center - faceN * 0.7f) ?? mat;
                    }
                    leaf.GetComponent<Renderer>().sharedMaterials = new[] { matPlus, matMinus };''')

# 2) mesh: split into two submeshes (front face + edges / back face)
rep('''            mesh.uv = uv;
            doorLeafMeshes[(alongX, hs)] = mesh;''',
    '''            mesh.uv = uv;

            // 서브메시 0: 앞면(+) + 얇은 옆면, 서브메시 1: 뒷면(-). 면마다 다른 재질을 쓸 수 있다.
            var all = mesh.triangles; var front = new List<int>(); var back = new List<int>();
            for (int t = 0; t < all.Length; t += 3)
            {
                int vi = all[t];
                bool bigFace = alongX ? Mathf.Abs(n[vi].z) > 0.5f : Mathf.Abs(n[vi].x) > 0.5f;
                float nSign = alongX ? n[vi].z : n[vi].x;
                var dst = (bigFace && nSign < 0f) ? back : front;
                dst.Add(all[t]); dst.Add(all[t + 1]); dst.Add(all[t + 2]);
            }
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(back, 1);
            doorLeafMeshes[(alongX, hs)] = mesh;''')

# 3) material chooser
rep('''        static float AlongSign(DoorRequest req, bool alongX)''', '''        static readonly Dictionary<string, Material> doorSideMats = new Dictionary<string, Material>();

        /// <summary>문 면이 보이는 방(pos가 있는 방)의 벽 질감과 색조에 맞는 문 재질. 방이 없으면 null.</summary>
        static Material DoorMaterialForSide(int floor, Vector3 pos)
        {
            int room = TowerLayout.RoomAt(pos);
            if (room < 0 || TowerLayout.IsVoidRoom(room)) return null;
            var env = RoomThemes.Get(floor, room);
            string file;
            switch (env.wallTex)
            {
                case 3: case 10: file = "DoorWood.png"; break;                       // 나무 벽 → 나무 문
                case 4: case 6: case 9: file = "DoorSteel.png"; break;               // 콘크리트·벙커·벽돌 → 강철 문
                case 0: case 1: case 5: case 7: case 8: case 11: file = "DoorWhite.png"; break;   // 페인트·타일·벽지 → 흰 칠한 문
                default: file = "DoorRoom.png"; break;                               // 푸른 페인트 → 청회색 문
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{HospitalTexDir}/{file}") == null) file = "DoorRoom.png";
            string name = $"Door_{System.IO.Path.GetFileNameWithoutExtension(file)}_t{env.tint}";
            if (doorSideMats.TryGetValue(name, out var cached) && cached != null) return cached;
            var mat = TextureMaterial(name, $"{HospitalTexDir}/{file}", Vector2.one, Vector2.zero, 0.2f);
            var c = RoomThemes.TintColors[env.tint % RoomThemes.Tints] * 0.85f;
            mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
            EditorUtility.SetDirty(mat);
            doorSideMats[name] = mat;
            return mat;
        }

        static float AlongSign(DoorRequest req, bool alongX)''')
rep("            doorLeafMeshes.Clear();", "            doorLeafMeshes.Clear();\n            doorSideMats.Clear();")
open(p, "w", encoding="utf-8", newline="").write(s)
print("ok")
