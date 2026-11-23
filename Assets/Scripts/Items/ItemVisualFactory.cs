using UnityEngine;

namespace Escape.Items
{
    /// <summary>
    /// 서랍을 열었을 때 안에 보이는 물건 모형을 만든다.
    /// Varco로 만든 모델 prefab(Resources/Items/이름)이 있으면 그것을 쓰고, 없으면 기본 도형으로 대신한다.
    /// 열쇠와 열쇠 조각은 어둠 속에서 눈에 띄게 은은히 빛난다.
    /// </summary>
    public static class ItemVisualFactory
    {
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        static readonly Color Steel = new Color(0.55f, 0.58f, 0.62f);

        /// <summary>물건 종류 → Resources/Items 안의 prefab 이름.</summary>
        public static string ModelName(ItemDrop drop)
        {
            if (drop.IsJunk) return drop.JunkName;
            if (!drop.Type.HasValue) return "RoomKey";
            switch (drop.Type.Value)
            {
                case ItemType.Battery: return "Battery";
                case ItemType.Cloth: return "ClothRag";
                case ItemType.Lighter: return "Lighter";
                case ItemType.Crowbar: return "Crowbar";
                case ItemType.Wire: return "WireCoil";
                case ItemType.SpareFlashlight: return "SpareFlashlight";
                case ItemType.SecurityCard: return "SecurityCard";
                case ItemType.MasterKey: return "MasterKey";
                default: return ItemInfo.IsPiece(drop.Type.Value) ? "KeyPiece" : null;
            }
        }

        public static GameObject Create(ItemDrop drop, Transform parent)
        {
            var root = new GameObject("ItemVisual");
            root.transform.SetParent(parent, false);

            string modelName = ModelName(drop);
            var prefab = modelName != null ? Resources.Load<GameObject>("Items/" + modelName) : null;
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);   // 서랍마다 놓인 방향이 조금씩 다르다
                foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

                if (!drop.IsJunk && (!drop.Type.HasValue || drop.Type.Value == ItemType.MasterKey || ItemInfo.IsPiece(drop.Type.Value)))
                    AddGlow(model, GlowColor(drop));
                return root;
            }

            BuildPrimitive(drop, root);
            return root;
        }

        static Color GlowColor(ItemDrop drop)
        {
            if (!drop.Type.HasValue) return Gold;                                   // 방 열쇠: 금색
            if (drop.Type.Value == ItemType.MasterKey) return new Color(0.7f, 0.9f, 1f);
            int index = (int)drop.Type.Value - (int)ItemType.Piece0;               // 탈출 열쇠 조각: 층마다 다른 색
            return Color.HSVToRGB(Mathf.Clamp01(index / 5f), 0.6f, 1f);
        }

        /// <summary>아주 작은 점광원으로 열쇠를 은은하게 비춘다.</summary>
        static void AddGlow(GameObject model, Color c)
        {
            // 발광(emission)은 쓰지 않는다: 후처리에서 색이 청록으로 틀어진다. 점광원만으로 은은하게 눈에 띄게 한다.
            var go = new GameObject("Glow");
            go.transform.SetParent(model.transform.parent, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = 0.3f;
            l.range = 1.6f;
            l.shadows = LightShadows.None;
        }

        // ---------- 모델이 없을 때 쓰는 기본 도형 ----------

        static void BuildPrimitive(ItemDrop drop, GameObject root)
        {
            if (drop.IsJunk)
            {
                Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.08f), new Color(0.4f, 0.35f, 0.3f), Quaternion.identity);
                return;
            }
            if (!drop.Type.HasValue)
            {
                BuildRoomKey(root.transform);
                return;
            }

            switch (drop.Type.Value)
            {
                case ItemType.Battery:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0), new Vector3(0.07f, 0.07f, 0.07f), new Color(0.1f, 0.45f, 0.15f), Quaternion.Euler(0, 0, 90));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0.075f, 0.04f, 0), new Vector3(0.03f, 0.012f, 0.03f), Steel, Quaternion.Euler(0, 0, 90));
                    break;
                case ItemType.Cloth:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.015f, 0), new Vector3(0.28f, 0.03f, 0.22f), new Color(0.55f, 0.55f, 0.5f), Quaternion.Euler(0, 20, 0));
                    break;
                case ItemType.Lighter:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(0.06f, 0.1f, 0.025f), new Color(0.7f, 0.1f, 0.1f), Quaternion.Euler(0, 0, 80));
                    Part(root, PrimitiveType.Cube, new Vector3(0.05f, 0.03f, 0), new Vector3(0.025f, 0.035f, 0.02f), Steel, Quaternion.Euler(0, 0, 80));
                    break;
                case ItemType.Crowbar:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(0.025f, 0.2f, 0.025f), new Color(0.2f, 0.2f, 0.22f), Quaternion.Euler(0, 0, 90));
                    Part(root, PrimitiveType.Cube, new Vector3(0.2f, 0.035f, 0), new Vector3(0.07f, 0.02f, 0.03f), new Color(0.2f, 0.2f, 0.22f), Quaternion.Euler(0, 0, 35));
                    break;
                case ItemType.Wire:
                    for (int i = 0; i < 3; i++)
                        Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.012f + i * 0.02f, 0), new Vector3(0.14f - i * 0.02f, 0.008f, 0.14f - i * 0.02f), new Color(0.75f, 0.35f, 0.1f), Quaternion.identity);
                    break;
                case ItemType.SpareFlashlight:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.035f, 0), new Vector3(0.055f, 0.1f, 0.055f), new Color(0.15f, 0.15f, 0.18f), Quaternion.Euler(0, 0, 90));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0.12f, 0.035f, 0), new Vector3(0.08f, 0.03f, 0.08f), Steel, Quaternion.Euler(0, 0, 90));
                    break;
                case ItemType.SecurityCard:
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.006f, 0), new Vector3(0.17f, 0.008f, 0.11f), new Color(0.15f, 0.35f, 0.9f), Quaternion.Euler(0, 15, 0), glow: new Color(0.1f, 0.25f, 0.9f));
                    Part(root, PrimitiveType.Cube, new Vector3(0, 0.011f, 0.03f), new Vector3(0.17f, 0.004f, 0.02f), Color.white, Quaternion.Euler(0, 15, 0));
                    break;
                default:
                    if (ItemInfo.IsPiece(drop.Type.Value) || drop.Type.Value == ItemType.MasterKey)
                    {
                        int index = (int)drop.Type.Value - (int)ItemType.Piece0;
                        Color c = Color.HSVToRGB(Mathf.Clamp01(index / 5f), 0.7f, 1f);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.05f, 0), new Vector3(0.1f, 0.1f, 0.03f), c, Quaternion.Euler(30, 45, 20), glow: c);
                    }
                    else
                    {
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.04f, 0), new Vector3(0.08f, 0.08f, 0.08f), Color.magenta, Quaternion.identity);
                    }
                    break;
            }
        }

        static void BuildRoomKey(Transform t)
        {
            var root = t.gameObject;
            // 고리 + 몸통 + 이빨 (납작하게 놓인 열쇠). 어둠 속에서 눈에 띄게 은은히 빛난다.
            Part(root, PrimitiveType.Cylinder, new Vector3(-0.07f, 0.008f, 0), new Vector3(0.06f, 0.008f, 0.06f), Gold, Quaternion.identity, glow: Gold * 0.6f);
            Part(root, PrimitiveType.Cube, new Vector3(0.03f, 0.008f, 0), new Vector3(0.14f, 0.012f, 0.02f), Gold, Quaternion.identity, glow: Gold * 0.6f);
            Part(root, PrimitiveType.Cube, new Vector3(0.085f, 0.008f, 0.02f), new Vector3(0.015f, 0.012f, 0.035f), Gold, Quaternion.identity);
            Part(root, PrimitiveType.Cube, new Vector3(0.06f, 0.008f, 0.02f), new Vector3(0.015f, 0.012f, 0.025f), Gold, Quaternion.identity);
        }

        static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Quaternion rot, Color? glow = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;

            var r = go.GetComponent<Renderer>();
            var settings = ItemVisualSettings.Instance;
            Material mat = settings != null && settings.baseMaterial != null
                ? new Material(settings.baseMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.35f);
            if (glow.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                // 세기가 1을 넘으면 후처리(대비)가 색을 틀어서 청록으로 번진다. 약하게만 빛나게 한다.
                mat.SetColor("_EmissionColor", glow.Value * 0.45f);
            }
            r.sharedMaterial = mat;
        }
    }
}
