using UnityEngine;
using UnityEngine.UI;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 水墨 Image 组件 —— 挂载在 Image 上，自动为其分配 InkUI 材质。
    /// 作为无代码的 Inspector 开关使用：直接给 Image 添加本组件即可水墨化。
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class InkUIImage : MonoBehaviour
    {
        [Header("━━━ 水墨参数 ━━━")]
        [Range(0, 0.5f)]
        [Tooltip("宣纸纹理强度")]
        public float paperStrength = 0.12f;

        [Range(0.1f, 4f)]
        [Tooltip("纸纹平铺密度")]
        public float paperTiling = 1.0f;

        [Range(0, 0.5f)]
        [Tooltip("边缘柔化/晕染范围")]
        public float edgeSoftness = 0.08f;

        [Range(0, 0.3f)]
        [Tooltip("边缘不规则程度")]
        public float edgeNoise = 0.08f;

        [Range(0, 1f)]
        [Tooltip("墨色加深强度")]
        public float inkDarken = 0.25f;

        [Range(0, 0.5f)]
        [Tooltip("墨晕扩散暗角")]
        public float inkBleed = 0.05f;

        [Header("━━━ 调试 ━━━")]
        [Tooltip("运行时每帧刷新材质参数")]
        public bool updateInEditor = false;

        private Image _image;
        private Color _lastColor;

        void Awake()
        {
            _image = GetComponent<Image>();
            ApplyMaterial();
        }

        void OnEnable()
        {
            ApplyMaterial();
        }

        void Update()
        {
            if (_image == null) return;

            // 如果颜色被外部修改，需要确保材质 Color 同步
            if (_image.color != _lastColor)
            {
                _lastColor = _image.color;
            }

#if UNITY_EDITOR
            if (updateInEditor)
                ApplyMaterial();
#endif
        }

        /// <summary>
        /// 重新应用/刷新水墨材质。
        /// </summary>
        public void ApplyMaterial()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            InkUIHelper.ApplyToImage(_image, _image.color, paperStrength, paperTiling,
                edgeSoftness, edgeNoise, inkDarken, inkBleed);
            _lastColor = _image.color;
        }
    }
}
