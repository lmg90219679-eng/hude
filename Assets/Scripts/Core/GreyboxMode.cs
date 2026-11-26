using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Escape.Core
{
    /// <summary>
    /// 그래픽 데이터 없이 "박스로만" 보이는 시제품 화면 (발표용 더미 모드).
    /// 메인 메뉴 설정에서 켜면 다음 게임부터 적용된다. 씬은 그대로이고 실행 중에 겉모습만 바꾼다:
    /// 벽·바닥·천장은 단색 회색, 문은 주황, 소품은 크기만 맞춘 상자, 서랍·옷장·숨는 곳은 색으로 구분,
    /// 추적자는 빨간 상자, 아이템은 노란 작은 상자. 게임 규칙과 충돌체, 길찾기는 건드리지 않는다.
    /// </summary>
    public class GreyboxMode : MonoBehaviour
    {
        readonly HashSet<int> done = new HashSet<int>();
        readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        float nextScan;

        static readonly Color Floor = new Color(0.30f, 0.31f, 0.33f);
        static readonly Color Ceiling = new Color(0.20f, 0.21f, 0.23f);
        static readonly Color Wall = new Color(0.58f, 0.60f, 0.64f);
        static readonly Color DoorC = new Color(0.85f, 0.50f, 0.15f);
        static readonly Color Prop = new Color(0.55f, 0.47f, 0.38f);
        static readonly Color Drawer = new Color(0.90f, 0.75f, 0.20f);
        static readonly Color Hide = new Color(0.25f, 0.55f, 0.85f);
        static readonly Color Chaser = new Color(0.85f, 0.10f, 0.10f);
        static readonly Color ItemC = new Color(1f, 0.95f, 0.30f);
        static readonly Color Other = new Color(0.50f, 0.50f, 0.52f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Spawn();   // 재시작으로 씬을 다시 불러와도 적용
            Spawn();
        }

        static void Spawn()
        {
            if (!GameSettings.Greybox) return;
            if (GameObject.Find("Tower") == null) return;   // 메인 메뉴 씬에는 적용하지 않는다
            if (FindFirstObjectByType<GreyboxMode>() != null) return;
            new GameObject("GreyboxMode").AddComponent<GreyboxMode>();
        }

        void Start()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);   // 상자 모양이 보일 만큼만 밝힌다
            RenderSettings.fog = false;
            Scan();
        }

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            Scan();   // 서랍을 열 때 나타나는 아이템 등 늦게 생기는 것을 처리한다
        }

        Material MatOf(Color c)
        {
            if (mats.TryGetValue(c, out var m)) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { color = c };
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.name = "Grey_" + ColorUtility.ToHtmlStringRGB(c);
            mats[c] = m;
            return m;
        }

        void Scan()
        {
            // 큰 건물 껍데기(벽, 바닥, 천장, 문)
            var tower = GameObject.Find("Tower");
            if (tower != null)
                foreach (var r in tower.GetComponentsInChildren<Renderer>(true))
                    Paint(r);

            // 추적자, 아이템: 이름이나 컴포넌트로 찾는다
            foreach (var ai in FindObjectsByType<Escape.Enemy.ChaserAI>(FindObjectsInactive.Include))
                Box(ai.gameObject, Chaser, new Vector3(0.7f, 1.8f, 0.5f), 0.9f);
            foreach (var go in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (go.name == "ItemVisual") Box(go.gameObject, ItemC, new Vector3(0.18f, 0.18f, 0.18f), 0f);
        }

        void Paint(Renderer r)
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return;
            if (!done.Add(r.GetInstanceID())) return;

            // 소품: 모델 하나(홀더)를 크기만 같은 상자 하나로 바꾼다
            var holder = PropHolder(r.transform);
            if (holder != null)
            {
                if (done.Add(holder.GetInstanceID()))
                {
                    var b = CombinedBounds(holder);
                    Color col = Prop;
                    if (holder.GetComponentInChildren<Escape.Interaction.HidingSpot>(true) != null) col = Hide;
                    else if (holder.GetComponentInChildren<Escape.Interaction.Container>(true) != null) col = Drawer;
                    Cube(holder, b, col);
                }
                r.enabled = false;
                return;
            }

            if (r is SkinnedMeshRenderer) return;   // 추적자는 위에서 따로 처리한다

            var bb = r.bounds;
            Color c;
            if (r.GetComponentInParent<Escape.Interaction.Door>(true) != null) c = DoorC;
            else if (bb.size.y < 0.6f && bb.size.x > 1.5f && bb.size.z > 1.5f) c = bb.center.y % TowerLayout.FloorHeight > 2f ? Ceiling : Floor;
            else if (bb.size.y > 1.8f) c = Wall;
            else c = Other;
            r.sharedMaterials = Same(r.sharedMaterials.Length, MatOf(c));
        }

        static Material[] Same(int n, Material m)
        {
            var a = new Material[Mathf.Max(1, n)];
            for (int i = 0; i < a.Length; i++) a[i] = m;
            return a;
        }

        // "HospitalProps" 바로 아래 자식이 소품 하나다
        static Transform PropHolder(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.parent != null && p.parent.name == "HospitalProps") return p;
            return null;
        }

        static Bounds CombinedBounds(Transform root)
        {
            bool first = true; var b = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void Cube(Transform parent, Bounds b, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GreyBox";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = b.center;
            go.transform.rotation = Quaternion.identity;
            var s = b.size;
            var ps = parent.lossyScale;
            go.transform.localScale = new Vector3(s.x / Mathf.Max(0.0001f, ps.x), s.y / Mathf.Max(0.0001f, ps.y), s.z / Mathf.Max(0.0001f, ps.z));
            go.GetComponent<Renderer>().sharedMaterial = MatOf(c);
            done.Add(go.GetComponent<Renderer>().GetInstanceID());
        }

        // 모델 대신 놓는 상자 (이미 놓았으면 건너뛴다)
        void Box(GameObject target, Color c, Vector3 size, float lift)
        {
            if (!done.Add(target.GetInstanceID())) return;
            foreach (var r in target.GetComponentsInChildren<Renderer>(true)) { done.Add(r.GetInstanceID()); r.enabled = false; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GreyBox";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            var ps = target.transform.lossyScale;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, ps.x), size.y / Mathf.Max(0.0001f, ps.y), size.z / Mathf.Max(0.0001f, ps.z));
            go.GetComponent<Renderer>().sharedMaterial = MatOf(c);
            done.Add(go.GetComponent<Renderer>().GetInstanceID());
        }
    }
}
