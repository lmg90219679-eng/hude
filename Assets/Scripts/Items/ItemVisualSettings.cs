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
}
