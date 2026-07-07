using UnityEngine;
using UnityEngine.UI;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 水墨 UI 材质工具 —— 为 UGUI 的 Image 创建带宣纸纹理和墨边晕染的材质实例。
    /// 依赖 InkSpriteMaterial 生成程序化纸纹；若 InkSpriteMaterial 未就绪则使用临时纹理。
    /// </summary>
    public static class InkUIHelper
    {
        private static Material _baseMaterial;
        private static Texture2D _fallbackPaperTexture;
        private static readonly int PaperTexId = Shader.PropertyToID("_PaperTex");
        private static readonly int PaperStrengthId = Shader.PropertyToID("_PaperStrength");
        private static readonly int PaperTilingId = Shader.PropertyToID("_PaperTiling");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int EdgeNoiseId = Shader.PropertyToID("_EdgeNoise");
        private static readonly int InkDarkenId = Shader.PropertyToID("_InkDarken");
        private static readonly int InkBleedId = Shader.PropertyToID("_InkBleed");

        private const string ShaderName = "TeaMist/UI/InkUI";

        /// <summary>
        /// 获取已设置纸纹的 InkUI 材质实例（每个 Image 独立，用于不同颜色）。
        /// </summary>
        public static Material CreateMaterial(float paperStrength = 0.12f,
            float paperTiling = 1.0f, float edgeSoftness = 0.08f,
            float edgeNoise = 0.08f, float inkDarken = 0.25f, float inkBleed = 0.05f)
        {
            EnsureBaseMaterial();
            if (_baseMaterial == null) return null;

            var mat = new Material(_baseMaterial)
            {
                name = "InkUI_Instance"
            };
            mat.SetTexture(PaperTexId, GetPaperTexture());
            mat.SetFloat(PaperStrengthId, paperStrength);
            mat.SetFloat(PaperTilingId, paperTiling);
            mat.SetFloat(EdgeSoftnessId, edgeSoftness);
            mat.SetFloat(EdgeNoiseId, edgeNoise);
            mat.SetFloat(InkDarkenId, inkDarken);
            mat.SetFloat(InkBleedId, inkBleed);
            return mat;
        }

        /// <summary>
        /// 为已有 Image 应用水墨材质。
        /// </summary>
        public static void ApplyToImage(Image image, Color color,
            float paperStrength = 0.12f, float paperTiling = 1.0f,
            float edgeSoftness = 0.08f, float edgeNoise = 0.08f,
            float inkDarken = 0.25f, float inkBleed = 0.05f)
        {
            if (image == null) return;
            image.material = CreateMaterial(paperStrength, paperTiling,
                edgeSoftness, edgeNoise, inkDarken, inkBleed);
            image.color = color;
        }

        /// <summary>
        /// 重新应用 InkUI 材质给 Image（用于运行时动态更新）。
        /// </summary>
        public static void RefreshImage(Image image)
        {
            if (image == null || image.material == null) return;
            var mat = image.material;
            if (mat.shader == null || !mat.shader.name.Equals(ShaderName)) return;
            mat.SetTexture(PaperTexId, GetPaperTexture());
        }

        private static void EnsureBaseMaterial()
        {
            if (_baseMaterial != null) return;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[InkUIHelper] 找不到 Shader 'TeaMist/UI/InkUI'，" +
                               "请确认 InkUI.shader 编译无错误。");
                return;
            }
            _baseMaterial = new Material(shader)
            {
                name = "InkUI_Base"
            };
        }

        private static Texture2D GetPaperTexture()
        {
            if (InkSpriteMaterial.Instance != null && InkSpriteMaterial.Instance.PaperTexture != null)
                return InkSpriteMaterial.Instance.PaperTexture;

            if (_fallbackPaperTexture == null)
            {
                int size = 256;
                _fallbackPaperTexture = new Texture2D(size, size, TextureFormat.R8, false);
                _fallbackPaperTexture.filterMode = FilterMode.Bilinear;
                _fallbackPaperTexture.wrapMode = TextureWrapMode.Repeat;
                _fallbackPaperTexture.name = "InkUI_FallbackPaper";
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float v = 0.85f + (Mathf.PerlinNoise(x * 0.05f, y * 0.05f) - 0.5f) * 0.15f;
                        _fallbackPaperTexture.SetPixel(x, y, new Color(v, v, v, 1f));
                    }
                }
                _fallbackPaperTexture.Apply();
            }
            return _fallbackPaperTexture;
        }
    }
}
