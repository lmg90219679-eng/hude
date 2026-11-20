using UnityEngine;

namespace Escape.Items
{
    /// <summary>씬에 하나 두는 설정. 런타임에 만드는 아이템 모형이 쓸 기본 재질을 들고 있다 (셰이더가 빌드에서 빠지지 않게).</summary>
    public class ItemVisualSettings : MonoBehaviour
    {
        public static ItemVisualSettings Instance { get; private set; }
        public Material baseMaterial;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
    }

    /// <summary>
    /// 서랍을 열었을 때 안에 보이는 물건 모형을 만든다. 지금은 기본 도형으로 대충 알아볼 수 있게 만들고,
    /// 나중에 Varco에서 만든 모델이 생기면 prefab 조회로 바꾼다.
    /// </summary>
    public static class ItemVisualFactory
    {
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        static readonly Color Steel = new Color(0.55f, 0.58f, 0.62f);

        public static GameObject Create(ItemDrop drop, Transform parent)
        {
            var root = new GameObject("ItemVisual");
            root.transform.SetParent(parent, false);

            if (!drop.Type.HasValue)
            {
                BuildRoomKey(root.transform);
                return root;
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
                        // 탈출 열쇠 조각: 층마다 다른 색으로 어둠 속에서 은은하게 빛나는 파편
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
            return root;
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
                mat.SetColor("_EmissionColor", glow.Value * 1.5f);
            }
            r.sharedMaterial = mat;
        }
    }
}
