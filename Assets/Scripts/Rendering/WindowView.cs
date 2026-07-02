using UnityEngine;
using TeaMist.Gameplay;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 窗外景色 —— 在茶馆背景层创建一扇纸窗，
    /// 窗外景色随四季/天气变化。
    /// 挂在 ArtSceneRoot 下，单例职责。
    /// </summary>
    public class WindowView : MonoBehaviour
    {
        [Header("━━━ 窗户参数 ━━━")]
        [SerializeField] private float windowWidth = 5.0f;
        [SerializeField] private float windowHeight = 3.2f;
        [SerializeField] private float frameThickness = 0.15f;
        [SerializeField] private float woodTint = 0.55f;

        [Header("━━━ 多层视差 ━━━")]
        [SerializeField] private int mountainLayers = 3;
        [SerializeField] private float[] layerSpeeds = { 0.05f, 0.12f, 0.25f };
        [SerializeField] private float cloudSpeed = 0.08f;
        [SerializeField] private float cloudDriftRange = 0.6f;

        // 子对象引用
        private SpriteRenderer _skyRenderer;
        private SpriteRenderer[] _mountainLayers;
        private Transform[] _mountainTransforms;
        private GameObject _cloudGroup;
        private SpriteRenderer[] _cloudRenderers;
        private float[] _cloudPhases;
        private GameObject _weatherGroup;
        private SpriteRenderer _weatherOverlay;
        private SpriteRenderer[] _frameRenderers; // top, bot, left, right

        // 原始颜色用于季节着色
        private Color _skyBaseColor = new Color(0.80f, 0.88f, 0.95f);

        // 视差滚动
        private float _globalOffset;

        void Awake()
        {
            BuildWindow();
        }

        void Start()
        {
            ApplySeason(SeasonManager.Instance != null
                ? SeasonManager.Instance.CurrentSeason
                : Season.Spring);
            ApplyWeather(SeasonManager.Instance != null
                ? SeasonManager.Instance.CurrentWeather
                : Weather.Clear);

            if (SeasonManager.Instance != null)
            {
                SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
                SeasonManager.Instance.OnWeatherChanged += OnWeatherChanged;
            }

            // 订阅 TimeManager 小时变化，模拟一天中光线角度变化
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnGameHourChanged += OnHourChanged;
        }

        void OnDestroy()
        {
            if (SeasonManager.Instance != null)
            {
                SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
                SeasonManager.Instance.OnWeatherChanged -= OnWeatherChanged;
            }
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnGameHourChanged -= OnHourChanged;
        }

        void Update()
        {
            // 视差微动：让远山有极缓慢的漂移感
            _globalOffset += Time.deltaTime * 0.001f;
            UpdateParallax();
            UpdateClouds();
        }

        // ━━ 构建窗户层级 ━━

        private void BuildWindow()
        {
            float w = windowWidth;
            float h = windowHeight;
            float t = frameThickness;

            // ── 天空背景（用 ArtLoader 水墨纸纹）──
            var paperSprite = ArtLoader.Find("宣纸") ?? ArtLoader.Find("茶馆");
            _skyRenderer = CreateChildSprite("Win_Sky", paperSprite ?? CreateWhitePixelSprite(), transform,
                SortingLayers.Background, SortingLayers.OrderInLayer.BG_Far + 5);
            SetSpriteNaturalSize(_skyRenderer, w - t * 2f, h - t * 2f);
            _skyRenderer.transform.localPosition = Vector3.zero;
            if (paperSprite == null) _skyRenderer.color = _skyBaseColor;

            // ── 多层远山（视差）──
            _mountainLayers = new SpriteRenderer[mountainLayers];
            _mountainTransforms = new Transform[mountainLayers];

            // 尝试加载远山水墨图
            var mountainSprite = ArtLoader.Find("远山");

            for (int i = 0; i < mountainLayers; i++)
            {
                string name = $"Win_Mountain_{i}";
                Sprite sprite = mountainSprite ?? CreateWhitePixelSprite();
                int order = SortingLayers.OrderInLayer.BG_Far + 6 + i;

                var sr = CreateChildSprite(name, sprite, transform,
                    SortingLayers.Background, order);

                if (mountainSprite != null)
                {
                    // 用实际图片：按层设置不同大小和透明度模拟远近
                    float scale = 1.0f + i * 0.15f; // 远层稍大（在后面）
                    SetSpriteNaturalSize(sr, (w - t * 2f) * scale, (h - t * 2f) * 0.6f);
                    sr.transform.localPosition = new Vector3(0, -h * 0.15f + i * 0.05f, -0.1f * (i + 1));
                    // 远层更淡（雾气效果）
                    float fogAlpha = 1.0f - i * 0.25f;
                    var c = sr.color;
                    c.a = Mathf.Clamp01(fogAlpha);
                    sr.color = c;
                }
                else
                {
                    // fallback：纯色山峦剪影
                    SetSpriteNaturalSize(sr, w - t * 2f, h * 0.45f);
                    sr.transform.localPosition = new Vector3(0, -h * 0.2f + i * 0.05f, -0.1f * (i + 1));
                    sr.color = new Color(0.55f, 0.62f, 0.50f, 1.0f - i * 0.2f);
                }

                _mountainLayers[i] = sr;
                _mountainTransforms[i] = sr.transform;
            }

            // ── 云层（程序化飘动）──
            CreateCloudLayer();

            // ── 窗框（四边）──
            CreateWindowFrame(w, h, t);

            // ── 天气覆盖层 ──
            _weatherGroup = new GameObject("Win_Weather");
            _weatherGroup.transform.SetParent(transform, false);
            _weatherOverlay = CreateChildSprite("Win_WeatherOverlay",
                CreateWhitePixelSprite(), _weatherGroup.transform,
                SortingLayers.Background, SortingLayers.OrderInLayer.BG_Far + 12);
            SetSpriteNaturalSize(_weatherOverlay, w - t * 2f, h - t * 2f);
            _weatherOverlay.transform.localPosition = Vector3.zero;
            _weatherOverlay.color = Color.clear;
            _weatherGroup.SetActive(false);
        }

        private void CreateWindowFrame(float w, float h, float t)
        {
            Color frameColor = new Color(woodTint * 0.6f, woodTint * 0.4f, woodTint * 0.25f, 1f);

            // 尝试用木纹 sprite
            var woodSprite = ArtLoader.Find("木") ?? CreateWhitePixelSprite();

            _frameRenderers = new SpriteRenderer[4];
            string[] frameNames = { "Win_FrameTop", "Win_FrameBot", "Win_FrameLeft", "Win_FrameRight" };
            for (int i = 0; i < 4; i++)
            {
                var sr = CreateChildSprite(frameNames[i], woodSprite, transform,
                    SortingLayers.Props, 0);
                sr.color = (woodSprite == null) ? frameColor : frameColor;
                sr.sortingOrder = SortingLayers.OrderInLayer.Props_Front + 5;
                _frameRenderers[i] = sr;
            }

            // top
            SetSpriteNaturalSize(_frameRenderers[0], w, t);
            _frameRenderers[0].transform.localPosition = new Vector3(0, h * 0.5f - t * 0.5f, -0.2f);
            // bottom
            SetSpriteNaturalSize(_frameRenderers[1], w, t);
            _frameRenderers[1].transform.localPosition = new Vector3(0, -(h * 0.5f - t * 0.5f), -0.2f);
            // left
            SetSpriteNaturalSize(_frameRenderers[2], t, h);
            _frameRenderers[2].transform.localPosition = new Vector3(-(w * 0.5f - t * 0.5f), 0, -0.2f);
            // right
            SetSpriteNaturalSize(_frameRenderers[3], t, h);
            _frameRenderers[3].transform.localPosition = new Vector3(w * 0.5f - t * 0.5f, 0, -0.2f);
        }

        private void CreateCloudLayer()
        {
            _cloudGroup = new GameObject("Win_Clouds");
            _cloudGroup.transform.SetParent(transform, false);

            int cloudCount = 4;
            _cloudRenderers = new SpriteRenderer[cloudCount];
            _cloudPhases = new float[cloudCount];

            for (int i = 0; i < cloudCount; i++)
            {
                var go = new GameObject($"Win_Cloud_{i}");
                go.transform.SetParent(_cloudGroup.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCloudSprite();
                sr.sortingLayerName = SortingLayers.Background;
                sr.sortingOrder = SortingLayers.OrderInLayer.BG_Far + 10;
                sr.color = new Color(1f, 1f, 1f, 0.18f);

                float x = (i - 1.5f) * 1.8f;
                float y = windowHeight * 0.25f + i * 0.15f;
                go.transform.localPosition = new Vector3(x, y, -0.15f);
                // 每层云大小略有不同
                float s = 0.8f + i * 0.12f;
                go.transform.localScale = new Vector3(s, s * 0.35f, 1f);

                _cloudRenderers[i] = sr;
                _cloudPhases[i] = i * 1.7f; // 不同相位
            }

            _cloudGroup.SetActive(true);
        }

        // ━━ 季节反应 ━━

        private void OnSeasonChanged(Season season) => ApplySeason(season);
        private void OnWeatherChanged(Weather weather) => ApplyWeather(weather);
        private void OnHourChanged(int hour) => ApplyTimeOfDay(hour);

        private void ApplySeason(Season season)
        {
            Color skyColor, mountainTint;
            switch (season)
            {
                case Season.Spring:
                    skyColor = new Color(0.80f, 0.88f, 0.95f);
                    mountainTint = new Color(0.55f, 0.72f, 0.55f);
                    break;
                case Season.Summer:
                    skyColor = new Color(0.75f, 0.85f, 0.92f);
                    mountainTint = new Color(0.35f, 0.58f, 0.38f);
                    break;
                case Season.Autumn:
                    skyColor = new Color(0.85f, 0.78f, 0.65f);
                    mountainTint = new Color(0.75f, 0.52f, 0.32f);
                    break;
                case Season.Winter:
                    skyColor = new Color(0.88f, 0.90f, 0.92f);
                    mountainTint = new Color(0.62f, 0.66f, 0.72f);
                    break;
                default:
                    skyColor = _skyBaseColor;
                    mountainTint = new Color(0.55f, 0.62f, 0.50f);
                    break;
            }

            _skyBaseColor = skyColor;
            if (_skyRenderer != null)
            {
                // 只有用纯色背景时才着色；用图片时保持原色
                if (_skyRenderer.sprite == null || _skyRenderer.sprite.texture.width <= 2)
                    _skyRenderer.color = skyColor;
            }

            // 给山层上季节色调
            if (_mountainLayers != null)
            {
                for (int i = 0; i < _mountainLayers.Length; i++)
                {
                    if (_mountainLayers[i] == null) continue;
                    var c = mountainTint;
                    c.a = 1.0f - i * 0.25f;
                    _mountainLayers[i].color = c;
                }
            }

            // 季节决定云层可见性（冬天云更明显）
            if (_cloudGroup != null)
                _cloudGroup.SetActive(season != Season.Winter);
        }

        private void ApplyWeather(Weather weather)
        {
            if (_weatherOverlay == null || _weatherGroup == null) return;

            // 天气影响云层
            bool hasCloud = weather != Weather.Clear;
            _cloudGroup.SetActive(hasCloud || true); // 云层始终可见，只是颜色变化

            switch (weather)
            {
                case Weather.Rain:
                    _weatherGroup.SetActive(true);
                    _weatherOverlay.color = new Color(0.42f, 0.48f, 0.55f, 0.30f);
                    SetCloudAlpha(0.35f);
                    break;
                case Weather.Mist:
                    _weatherGroup.SetActive(true);
                    _weatherOverlay.color = new Color(0.85f, 0.86f, 0.82f, 0.45f);
                    SetCloudAlpha(0.50f);
                    break;
                case Weather.Snow:
                    _weatherGroup.SetActive(true);
                    _weatherOverlay.color = new Color(0.92f, 0.94f, 0.97f, 0.25f);
                    SetCloudAlpha(0.20f);
                    break;
                case Weather.Storm:
                    _weatherGroup.SetActive(true);
                    _weatherOverlay.color = new Color(0.25f, 0.28f, 0.35f, 0.40f);
                    SetCloudAlpha(0.60f);
                    break;
                default:
                    _weatherGroup.SetActive(false);
                    _weatherOverlay.color = Color.clear;
                    SetCloudAlpha(0.18f);
                    break;
            }
        }

        private void ApplyTimeOfDay(int hour)
        {
            // 根据时刻调整天空明度（早晨偏暖、中午亮、傍晚橙红）
            if (_skyRenderer == null) return;

            float t = Mathf.InverseLerp(6f, 18f, hour); // 6→0, 12→0.5, 18→1
            Color morn = new Color(0.95f, 0.82f, 0.68f); // 晨光暖色
            Color noon = _skyBaseColor;
            Color eve = new Color(0.92f, 0.72f, 0.52f); // 暮色

            Color target;
            if (hour < 12) target = Color.Lerp(morn, noon, t * 2f);
            else target = Color.Lerp(noon, eve, (t - 0.5f) * 2f);

            if (_skyRenderer.sprite == null || _skyRenderer.sprite.texture.width <= 2)
                _skyRenderer.color = target;
        }

        // ━━ 动画更新 ━━

        private void UpdateParallax()
        {
            if (_mountainTransforms == null) return;
            for (int i = 0; i < _mountainTransforms.Length; i++)
            {
                if (_mountainTransforms[i] == null) continue;
                float speed = (i < layerSpeeds.Length) ? layerSpeeds[i] : 0.1f;
                float offsetX = Mathf.Sin(_globalOffset * speed * 10f) * 0.1f;
                var pos = _mountainTransforms[i].localPosition;
                _mountainTransforms[i].localPosition = new Vector3(pos.x + offsetX * Time.deltaTime, pos.y, pos.z);
            }
        }

        private void UpdateClouds()
        {
            if (_cloudRenderers == null) return;
            for (int i = 0; i < _cloudRenderers.Length; i++)
            {
                var sr = _cloudRenderers[i];
                if (sr == null) continue;

                _cloudPhases[i] += Time.deltaTime * cloudSpeed * (1f + i * 0.2f);
                float x = Mathf.Sin(_cloudPhases[i]) * cloudDriftRange;
                float y = Mathf.Cos(_cloudPhases[i] * 0.7f) * 0.08f;
                sr.transform.localPosition = new Vector3(x, sr.transform.localPosition.y + y * Time.deltaTime, sr.transform.localPosition.z);

                // 循环复位
                if (x > windowWidth * 0.8f) _cloudPhases[i] = 0f;
            }
        }

        private void SetCloudAlpha(float alpha)
        {
            if (_cloudRenderers == null) return;
            for (int i = 0; i < _cloudRenderers.Length; i++)
            {
                if (_cloudRenderers[i] == null) continue;
                var c = _cloudRenderers[i].color;
                c.a = alpha;
                _cloudRenderers[i].color = c;
            }
        }

        // ━━ 工具方法 ━━

        private static SpriteRenderer CreateChildSprite(string name, Sprite sprite, Transform parent,
            string sortingLayer, int orderInLayer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = orderInLayer;
            return sr;
        }

        /// <summary>
        /// 按 Sprite 原始尺寸等比缩放至目标大小
        /// </summary>
        private static void SetSpriteNaturalSize(SpriteRenderer sr, float targetW, float targetH)
        {
            var s = sr.sprite;
            if (s == null) return;
            float texW = s.rect.width;
            float texH = s.rect.height;
            float ppu = s.pixelsPerUnit;
            float scaleX = (targetW / (texW / ppu));
            float scaleY = (targetH / (texH / ppu));
            float scale = Mathf.Min(scaleX, scaleY);
            sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 创建水墨云朵 Sprite（程序化）
        /// </summary>
        private static Sprite CreateCloudSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float rx = (x - center.x) / (size * 0.45f);
                    float ry = (y - center.y) / (size * 0.25f);
                    float ellipse = rx * rx + ry * ry;
                    float alpha = Mathf.Clamp01(1f - ellipse);
                    alpha = Mathf.Pow(alpha, 1.5f); // 软化边缘
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.5f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
