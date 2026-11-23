using System.IO;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// 인벤토리와 HUD에 쓸 아이템 아이콘을 모델에서 자동으로 만든다 (Resources/Icons/&lt;이름&gt;.png, 투명 배경).
    /// 서랍 안 모델(Resources/Items prefab)을 PreviewRenderUtility로 찍는다. 씬을 빌드할 때마다 새로 만든다.
    /// </summary>
    public static partial class Week1SceneBuilder
    {
        const string IconDir = "Assets/Resources/Icons";
        const int IconSize = 192;

        static readonly string[] IconModels =
        {
            "RoomKey", "KeyPiece", "MasterKey", "Battery", "ClothRag", "Lighter", "Crowbar", "WireCoil", "SpareFlashlight", "SecurityCard",
        };

        static Texture2D RenderIcon(PreviewRenderUtility pru, Color bg)
        {
            pru.camera.backgroundColor = bg;
            pru.BeginStaticPreview(new Rect(0, 0, IconSize, IconSize));
            pru.Render(true);
            return pru.EndStaticPreview();
        }

        [MenuItem("Escape/Build Item Icons")]
        static void BuildItemIcons()
        {
            Directory.CreateDirectory(IconDir);
            int made = 0;
            var pru = new PreviewRenderUtility();
            try
            {
                pru.cameraFieldOfView = 28f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                pru.camera.nearClipPlane = 0.01f;
                pru.camera.farClipPlane = 10f;
                pru.lights[0].intensity = 1.6f;
                pru.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
                pru.lights[1].intensity = 1.0f;
                pru.ambientColor = new Color(0.62f, 0.62f, 0.68f);

                foreach (string name in IconModels)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Items/{name}.prefab");
                    if (prefab == null) continue;

                    var go = Object.Instantiate(prefab);
                    pru.AddSingleGO(go);
                    var b = PropTools.Bounds(go);
                    float radius = Mathf.Max(b.extents.magnitude, 0.01f);

                    // 비스듬히 위에서 본다. 아이템이 화면을 가득 채우도록 거리를 맞춘다.
                    var dir = new Vector3(0.55f, 0.75f, -0.9f).normalized;
                    float dist = radius / Mathf.Tan(pru.cameraFieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
                    pru.camera.transform.position = b.center + dir * dist;
                    pru.camera.transform.LookAt(b.center);

                    // 검은 배경과 흰 배경으로 두 번 찍어서 투명한 아이콘을 만든다 (알파 = 1 - 두 이미지의 차이)
                    Texture2D black = RenderIcon(pru, Color.black), white = RenderIcon(pru, Color.white);
                    if (black != null && white != null)
                    {
                        var cb = black.GetPixels(); var cw = white.GetPixels(); var outPx = new Color[cb.Length];
                        for (int i = 0; i < cb.Length; i++)
                        {
                            float diff = ((cw[i].r - cb[i].r) + (cw[i].g - cb[i].g) + (cw[i].b - cb[i].b)) / 3f;
                            float a = Mathf.Clamp01(1f - diff);
                            outPx[i] = a > 0.01f ? new Color(Mathf.Clamp01(cb[i].r / a), Mathf.Clamp01(cb[i].g / a), Mathf.Clamp01(cb[i].b / a), a) : new Color(0, 0, 0, 0);
                        }
                        var res = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                        res.SetPixels(outPx); res.Apply();
                        File.WriteAllBytes($"{IconDir}/{name}.png", res.EncodeToPNG());
                        Object.DestroyImmediate(res);
                        made++;
                    }
                    if (black != null) Object.DestroyImmediate(black);
                    if (white != null) Object.DestroyImmediate(white);
                    Object.DestroyImmediate(go);
                }
            }
            finally { pru.Cleanup(); }

            AssetDatabase.Refresh();
            foreach (string name in IconModels)
            {
                var ti = AssetImporter.GetAtPath($"{IconDir}/{name}.png") as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            Debug.Log($"[ItemIcons] 아이템 아이콘 {made}/{IconModels.Length}개 생성");
        }
    }
}
