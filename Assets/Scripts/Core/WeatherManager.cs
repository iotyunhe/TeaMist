using UnityEngine;
using TeaMist.Data;
using TeaMist.Rendering;

namespace TeaMist.Core
{
    /// <summary>
    /// 天气管理器 —— 响应 TimeManager 的天气变化，联动水墨渲染参数和天气粒子效果。
    /// 
    /// 架构角色：Bridge（桥接层）
    ///   上游：TimeManager.OnWeatherChanged → 天气事件
    ///   下游：InkRenderSettings（渲染参数）、天气粒子、TeaBrewingManager（泡茶影响）
    /// 
    /// 六种天气的视觉影响：
    ///   晴 → 默认参数
    ///   多云 → 略微柔和
    ///   雨 → 画面湿润（低对比+大晕染+细描边）+ 雨滴粒子
    ///   雪 → 画面提亮柔化（季节性粒子已有雪花）
    ///   雾 → 重度柔化+全屏雾色覆盖层
    ///   风 → 干笔飞白效果（高对比+粗描边）
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        /// <summary>当前天气（读自 TimeManager）</summary>
        public WeatherType CurrentWeather { get; private set; } = WeatherType.晴;

        // ── 天气特效 ──
        private ParticleSystem _rainParticles;
        private GameObject _fogOverlay;

        // ── 默认渲染参数快照（用于晴/多云恢复）──
        private float _defaultToneContrast;
        private float _defaultWashBlurRadius;
        private float _defaultWashIntensity;
        private float _defaultEdgeWidth;
        private float _defaultVignetteIntensity;
        private bool _snapshotTaken;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnWeatherChanged += OnWeatherChanged;
                // 立刻应用当前天气（可能 Bootstrap 已经推进了天数）
                ApplyWeather(TimeManager.Instance.CurrentWeather);
            }

            SnapshotInkDefaults();
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnWeatherChanged -= OnWeatherChanged;

            // 清理动态创建的纹理
            CleanupEffects();
        }

        // ── 天气切换入口 ──

        private void OnWeatherChanged(WeatherType newWeather)
        {
            ApplyWeather(newWeather);
        }

        /// <summary>应用天气（可手动调用）</summary>
        public void ApplyWeather(WeatherType weather)
        {
            if (CurrentWeather == weather && _snapshotTaken) return;

            CurrentWeather = weather;
            StopAllWeatherEffects();

            switch (weather)
            {
                case WeatherType.晴:  ApplyClear();   break;
                case WeatherType.多云: ApplyCloudy(); break;
                case WeatherType.雨:  ApplyRain();    break;
                case WeatherType.雪:  ApplySnow();    break;
                case WeatherType.雾:  ApplyFog();     break;
                case WeatherType.风:  ApplyWind();    break;
                default: break; // 雷 等未实现特效的天气
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WeatherManager] 天气切换: {WeatherName(weather)}");
#endif
        }

        /// <summary>手动强制切换天气（Bootstrap 测试按钮调用）</summary>
        public void ForceWeather(WeatherType weather)
        {
            CurrentWeather = weather;
            ApplyWeather(weather);
        }

        // ── 各天气实现 ──

        private void ApplyClear()
        {
            ResetInkToDefaults();
        }

        private void ApplyCloudy()
        {
            AdjustInk(toneContrast: -0.15f, washIntensity: +0.10f, vignetteIntensity: -0.08f);
        }

        private void ApplyRain()
        {
            // 湿润感：低对比、大晕染、细描边、深暗角（仿佛从窗内看雨）
            AdjustInk(
                toneContrast: -0.30f,
                washBlurRadius: +1.5f,
                washIntensity: +0.20f,
                edgeWidth: -0.4f,
                vignetteIntensity: +0.12f);

            EnsureRainParticles();
            _rainParticles?.Play();
        }

        private void ApplySnow()
        {
            // 雪景：画面提亮、柔化边缘（雪花粒子由 SeasonalParticles 负责）
            AdjustInk(
                toneContrast: -0.20f,
                washBlurRadius: +0.8f,
                washIntensity: +0.10f,
                vignetteIntensity: -0.10f);
        }

        private void ApplyFog()
        {
            // 浓雾：重度柔化 + 全屏雾色覆盖
            AdjustInk(
                toneContrast: -0.55f,
                washBlurRadius: +3.0f,
                washIntensity: +0.30f,
                edgeWidth: -1.0f,
                vignetteIntensity: +0.15f);

            EnsureFogOverlay();
            if (_fogOverlay != null) _fogOverlay.SetActive(true);
        }

        private void ApplyWind()
        {
            // 干笔飞白：高对比、粗描边、轻晕染（秋日枯笔感）
            AdjustInk(
                toneContrast: +0.15f,
                washIntensity: -0.12f,
                edgeWidth: +0.3f,
                vignetteIntensity: -0.05f);
        }

        // ── InkRenderSettings 参数调整 ──

        private void SnapshotInkDefaults()
        {
            var ink = InkRenderSettings.Instance;
            if (ink == null) return;

            _defaultToneContrast     = ink.toneContrast;
            _defaultWashBlurRadius   = ink.washBlurRadius;
            _defaultWashIntensity    = ink.washIntensity;
            _defaultEdgeWidth        = ink.edgeWidth;
            _defaultVignetteIntensity = ink.vignetteIntensity;
            _snapshotTaken = true;
        }

        private void AdjustInk(
            float toneContrast = 0f,
            float washBlurRadius = 0f,
            float washIntensity = 0f,
            float edgeWidth = 0f,
            float vignetteIntensity = 0f)
        {
            var ink = InkRenderSettings.Instance;
            if (ink == null) return;

            if (!_snapshotTaken) SnapshotInkDefaults();

            ink.toneContrast      = Mathf.Clamp(_defaultToneContrast + toneContrast, 1f, 3f);
            ink.washBlurRadius    = Mathf.Clamp(_defaultWashBlurRadius + washBlurRadius, 0.5f, 8f);
            ink.washIntensity     = Mathf.Clamp(_defaultWashIntensity + washIntensity, 0f, 1f);
            ink.edgeWidth         = Mathf.Clamp(_defaultEdgeWidth + edgeWidth, 0.5f, 6f);
            ink.vignetteIntensity = Mathf.Clamp(_defaultVignetteIntensity + vignetteIntensity, 0f, 1f);
        }

        private void ResetInkToDefaults()
        {
            if (!_snapshotTaken) return;
            AdjustInk(); // 全零参数 → 还原默认值
        }

        // ── 粒子系统 ──

        private void StopAllWeatherEffects()
        {
            _rainParticles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_fogOverlay != null) _fogOverlay.SetActive(false);
        }

        private void CleanupEffects()
        {
            if (_rainParticles != null)
            {
                Destroy(_rainParticles.gameObject);
                _rainParticles = null;
            }
            if (_fogOverlay != null)
            {
                var sr = _fogOverlay.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    if (sr.sprite.texture != null)
                        Destroy(sr.sprite.texture);
                    Destroy(sr.sprite);
                }
                Destroy(_fogOverlay);
                _fogOverlay = null;
            }
        }

        private void EnsureRainParticles()
        {
            if (_rainParticles != null) return;

            var cam = Camera.main;
            if (cam == null) return;

            float worldWidth  = cam.orthographicSize * 2f * cam.aspect;
            float worldHeight = cam.orthographicSize * 2f;

            var go = new GameObject("Weather_Rain");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor    = new Color(0.55f, 0.65f, 0.82f, 0.35f);
            main.maxParticles  = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake   = false;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(40f, 60f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(worldWidth * 1.5f, 1f, 1f);
            shape.position  = new Vector3(0, worldHeight * 0.5f, 0);

            // 雨滴直线下落
            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(-10f, -6f);
            velOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 渲染：拉丝模式（Stretch）模拟雨丝
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 0.5f;
            renderer.velocityScale = 0.15f;
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            _rainParticles = ps;
        }

        private void EnsureFogOverlay()
        {
            if (_fogOverlay != null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // 雾色覆盖层：使用大尺寸 Sprite 铺满视野
            _fogOverlay = new GameObject("Weather_Fog");
            _fogOverlay.transform.SetParent(cam.transform, false);
            _fogOverlay.transform.localPosition = new Vector3(0, 0, 0.5f);

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

            var sr = _fogOverlay.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color  = new Color(0.85f, 0.83f, 0.78f, 0.22f);
            sr.sortingLayerName = SortingLayers.Overlay;
            sr.sortingOrder = SortingLayers.OrderInLayer.Overlay_Weather;

            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth  = cam.orthographicSize * 2f * cam.aspect;
            float scale = Mathf.Max(worldWidth, worldHeight);
            _fogOverlay.transform.localScale = new Vector3(scale, scale, 1f);

            _fogOverlay.SetActive(false);
        }

        // ── 泡茶联动 ──

        /// <summary>天气对基础水温的影响（摄氏度偏移）</summary>
        public float GetWaterTempModifier()
        {
            switch (CurrentWeather)
            {
                case WeatherType.雨:  return -8f;  // 雨水寒凉
                case WeatherType.雪:  return -5f;  // 雪天稍冷
                case WeatherType.风:  return -3f;  // 大风散热
                case WeatherType.雾:  return -2f;  // 雾气微凉
                default: return 0f;
            }
        }

        // ── 工具 ──

        public static string WeatherName(WeatherType w)
        {
            switch (w)
            {
                case WeatherType.晴:  return "晴";
                case WeatherType.多云: return "多云";
                case WeatherType.雨:  return "雨";
                case WeatherType.雪:  return "雪";
                case WeatherType.雾:  return "雾";
                case WeatherType.风:  return "风";
                default: return "未知";
            }
        }
    }
}
