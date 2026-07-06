using UnityEngine;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 水墨精灵材质管理器 —— 单例，负责：
    ///   1. 生成程序化宣纸纹理（512×512，Perlin 噪点模拟纸纹）
    ///   2. 创建共享 InkSprite Material（所有场景精灵共用）
    ///   3. 注册全局纸纹给 InkRenderPass 使用
    ///
    /// 挂载到 ArtSceneRoot 或 Bootstrap 上。
    /// </summary>
    public class InkSpriteMaterial : MonoBehaviour
    {
        public static InkSpriteMaterial Instance { get; private set; }

        [Header("━━━ 纸纹参数 ━━━")]
        [Tooltip("纸纹噪点密度")]
        [Range(4, 32)]
        public int paperNoiseDensity = 12;

        [Tooltip("纸纹对比度")]
        [Range(0.02f, 0.2f)]
        public float paperContrast = 0.06f;

        [Tooltip("纤维方向性（0=等向, 1=水平纤维）")]
        [Range(0, 1)]
        public float fiberDirectionality = 0.4f;

        [Header("━━━ 精灵材质 ━━━")]
        [Tooltip("纸纹强度（所有精灵）")]
        [Range(0, 0.3f)]
        public float spritePaperStrength = 0.07f;

        [Tooltip("边缘柔化强度")]
        [Range(0, 0.15f)]
        public float spriteEdgeSoftness = 0.04f;

        // ── 运行时 ──
        private Texture2D _paperTexture;
        private Material _sharedSpriteMaterial;

        public Material SharedMaterial => _sharedSpriteMaterial;
        public Texture2D PaperTexture => _paperTexture;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 已有实例存在（场景重载时旧实例可能还在），复用旧的
                // 不销毁自身 gameObject，因为后续代码需要往上面挂子对象
                Debug.Log("[InkSpriteMaterial] 已有实例，复用现有纸纹和材质");
                return;
            }
            Instance = this;

            GeneratePaperTexture();
            CreateSharedMaterial();

            // 注册全局纸纹给 InkRenderPass
            Core.InkRenderPass.SetGlobalPaperTexture(_paperTexture);
        }

        void OnDestroy()
        {
            if (_paperTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(_paperTexture);
                else
                    DestroyImmediate(_paperTexture);
                _paperTexture = null;
            }

            if (_sharedSpriteMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_sharedSpriteMaterial);
                else
                    DestroyImmediate(_sharedSpriteMaterial);
                _sharedSpriteMaterial = null;
            }

            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 给 SpriteRenderer 分配共享水墨材质
        /// </summary>
        public void ApplyToSprite(SpriteRenderer sr, float depthLayer = 0.5f, float? customPaperStrength = null)
        {
            if (sr == null || _sharedSpriteMaterial == null) return;

            // 创建材质实例（每个精灵独立，允许不同 _DepthLayer）
            var mat = new Material(_sharedSpriteMaterial);
            mat.SetFloat("_DepthLayer", depthLayer);

            float paperStr = customPaperStrength ?? spritePaperStrength;
            mat.SetFloat("_PaperStrength", paperStr);
            mat.SetFloat("_EdgeSoftness", spriteEdgeSoftness);
            mat.SetFloat("_EdgeFeather", 3.0f);

            // 远景精灵：减弱纸纹、加大柔化
            if (depthLayer < 0.3f)
            {
                mat.SetFloat("_PaperStrength", paperStr * 0.5f);
                mat.SetFloat("_EdgeSoftness", spriteEdgeSoftness * 1.5f);
                mat.SetFloat("_InkSaturation", 0.05f); // 远景更灰
            }
            // 近景精灵：保留更多原色
            else if (depthLayer > 0.7f)
            {
                mat.SetFloat("_InkSaturation", 0.25f);
            }

            sr.material = mat;
        }

        // ── 私有 ──

        private void GeneratePaperTexture()
        {
            int size = 512;
            _paperTexture = new Texture2D(size, size, TextureFormat.R8, false);
            _paperTexture.filterMode = FilterMode.Bilinear;
            _paperTexture.wrapMode = TextureWrapMode.Repeat;
            _paperTexture.name = "_PaperTex_Procedural";

            var noise = GeneratePerlinNoise(size, paperNoiseDensity);
            float contrast = paperContrast;
            float directionality = fiberDirectionality;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 基础噪点
                    float n = noise[y * size + x];

                    // 纤维方向性：水平像素邻域平均模拟纸张纤维
                    float fiberX = (float)x / size;
                    float fiberY = (float)y / size;
                    float fiberNoise = FractalNoise(fiberX * paperNoiseDensity * 2f,
                                                      fiberY * paperNoiseDensity * 0.6f, 3);

                    // 混合：等向噪点 + 方向性纤维
                    float paper = Mathf.Lerp(n, fiberNoise, directionality);

                    // 对比度
                    paper = (paper - 0.5f) * (1f + contrast * 10f) + 0.5f;
                    paper = Mathf.Clamp01(paper);

                    // 映射到 0.7-1.0 范围（纸色偏亮）
                    float value = 0.7f + paper * 0.3f;

                    _paperTexture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            _paperTexture.Apply();
            Debug.Log($"[InkSpriteMaterial] 程序化宣纸纹理生成完成 ({size}x{size})");
        }

        private void CreateSharedMaterial()
        {
            var shader = Shader.Find("TeaMist/InkRender/InkSprite");
            if (shader == null)
            {
                Debug.LogError("[InkSpriteMaterial] 找不到 Shader 'TeaMist/InkRender/InkSprite'，" +
                    "请确认 InkSprite.shader 编译无错误");
                return;
            }

            _sharedSpriteMaterial = new Material(shader);
            _sharedSpriteMaterial.SetTexture("_PaperTex", _paperTexture);
            _sharedSpriteMaterial.SetFloat("_PaperTiling", 0.5f);
            _sharedSpriteMaterial.SetFloat("_InkSaturation", 0.15f);

            Debug.Log("[InkSpriteMaterial] 共享水墨精灵材质创建完成");
        }

        // ── 程序化噪点生成 ──

        private static float[] GeneratePerlinNoise(int size, int density)
        {
            // 简易 Perlin-like 噪点：多层叠加的平滑随机值
            int gridSize = density;
            float[] result = new float[size * size];

            // 创建随机梯度网格
            System.Random rng = new System.Random(42); // 固定种子，保证纹理一致
            float[,] gradients = new float[gridSize + 2, gridSize + 2];
            for (int gy = 0; gy < gridSize + 2; gy++)
                for (int gx = 0; gx < gridSize + 2; gx++)
                    gradients[gx, gy] = (float)rng.NextDouble();

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size * gridSize;
                    float fy = (float)y / size * gridSize;
                    int ix = Mathf.FloorToInt(fx);
                    int iy = Mathf.FloorToInt(fy);
                    float sx = fx - ix;
                    float sy = fy - iy;

                    // Smoothstep 插值
                    sx = sx * sx * (3f - 2f * sx);
                    sy = sy * sy * (3f - 2f * sy);

                    float n00 = gradients[ix, iy];
                    float n10 = gradients[ix + 1, iy];
                    float n01 = gradients[ix, iy + 1];
                    float n11 = gradients[ix + 1, iy + 1];

                    float nx0 = Mathf.Lerp(n00, n10, sx);
                    float nx1 = Mathf.Lerp(n01, n11, sx);
                    result[y * size + x] = Mathf.Lerp(nx0, nx1, sy);
                }
            }

            return result;
        }

        private static float FractalNoise(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * SimpleNoise(x * frequency, y * frequency);
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return value / maxValue;
        }

        private static float SimpleNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float n00 = Hash(ix, iy);
            float n10 = Hash(ix + 1, iy);
            float n01 = Hash(ix, iy + 1);
            float n11 = Hash(ix + 1, iy + 1);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, fx), Mathf.Lerp(n01, n11, fx), fy);
        }

        private static float Hash(int x, int y)
        {
            int n = x + y * 57;
            n = (n << 13) ^ n;
            return (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f) * 0.5f + 0.5f;
        }
    }
}
