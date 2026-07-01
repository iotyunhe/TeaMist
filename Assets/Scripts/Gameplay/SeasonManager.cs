using UnityEngine;
using System;
using TeaMist.Core;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 四季管理器 —— 驱动游戏世界的季节、节气、天气变化。
    /// 与 TimeManager 配合，无压力的真实时间流。
    /// </summary>
    public enum Season { All = -1, Spring, Summer, Autumn, Winter }
    public enum Weather { Any = -1, Clear, Cloudy, Rain, Snow, Mist, Storm }

    [System.Serializable]
    public struct SeasonColorSet
    {
        [Header("季节色调")]
        public Color inkTone;      // 墨色基调
        public Color seasonTint;   // 季节叠色（给 SeasonTint Shader 用）
        public Color vignetteColor;// 暗角色调
        public Color uiAccent;     // UI 强调色
    }

    public class SeasonManager : MonoBehaviour
    {
        public static SeasonManager Instance { get; private set; }

        [Header("当前状态（只读）")]
        [SerializeField] private Season currentSeason = Season.Spring;
        [SerializeField] private Weather currentWeather = Weather.Clear;
        [SerializeField, Range(0, 100)] private float seasonProgress; // 0=刚进入, 100=快结束

        [Header("四季调色板")]
        public SeasonColorSet springColors = new SeasonColorSet
        {
            inkTone = new Color(0.18f, 0.22f, 0.18f),
            seasonTint = new Color(0.55f, 0.72f, 0.55f),
            vignetteColor = new Color(0.08f, 0.12f, 0.08f),
            uiAccent = new Color(0.45f, 0.65f, 0.45f)
        };
        public SeasonColorSet summerColors = new SeasonColorSet
        {
            inkTone = new Color(0.12f, 0.18f, 0.12f),
            seasonTint = new Color(0.35f, 0.55f, 0.35f),
            vignetteColor = new Color(0.05f, 0.08f, 0.05f),
            uiAccent = new Color(0.30f, 0.50f, 0.30f)
        };
        public SeasonColorSet autumnColors = new SeasonColorSet
        {
            inkTone = new Color(0.35f, 0.28f, 0.20f),
            seasonTint = new Color(0.75f, 0.55f, 0.35f),
            vignetteColor = new Color(0.15f, 0.10f, 0.06f),
            uiAccent = new Color(0.70f, 0.45f, 0.25f)
        };
        public SeasonColorSet winterColors = new SeasonColorSet
        {
            inkTone = new Color(0.30f, 0.32f, 0.35f),
            seasonTint = new Color(0.65f, 0.70f, 0.78f),
            vignetteColor = new Color(0.12f, 0.14f, 0.18f),
            uiAccent = new Color(0.60f, 0.65f, 0.72f)
        };

        // 渐变过渡
        private SeasonColorSet previousColors;
        private SeasonColorSet targetColors;
        private float colorLerpFactor;
        private const float SEASON_TRANSITION_DAYS = 4f; // 季节交替过渡天数

        public event Action<Season> OnSeasonChanged;
        public event Action<Weather> OnWeatherChanged;
        public event Action<SeasonColorSet> OnColorsUpdated;

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
            // 首次运行，从 TimeManager 同步
            if (TimeManager.Instance != null)
            {
                SyncFromTimeManager();
            }
            UpdateColorSets(currentSeason);
        }

        void Update()
        {
            if (TimeManager.Instance == null) return;

            // 每帧驱动颜色渐变
            if (colorLerpFactor < 1f)
            {
                colorLerpFactor += Time.deltaTime / (SEASON_TRANSITION_DAYS * TimeManager.Instance.secondsPerDay);
                colorLerpFactor = Mathf.Clamp01(colorLerpFactor);
                OnColorsUpdated?.Invoke(GetCurrentBlendedColors());
            }

            // 从 TimeManager 同步季节
            SyncFromTimeManager();
        }

        private void SyncFromTimeManager()
        {
            if (TimeManager.Instance == null) return;

            int dayOfYear = TimeManager.Instance.DayOfYear;
            Season newSeason = DayToSeason(dayOfYear);

            if (newSeason != currentSeason)
            {
                previousColors = GetColorSet(currentSeason);
                currentSeason = newSeason;
                targetColors = GetColorSet(currentSeason);
                colorLerpFactor = 0f;
                OnSeasonChanged?.Invoke(currentSeason);
            }

            // 每日天气在 TimeManager 的每日事件里触发
            if (TimeManager.Instance.DayOfYear != lastDayOfYear)
            {
                lastDayOfYear = TimeManager.Instance.DayOfYear;
                RollDailyWeather();
            }

            seasonProgress = GetSeasonProgress(dayOfYear);
        }
        private int lastDayOfYear = -1;

        private Season DayToSeason(int day)
        {
            // 简化四等分：每季 91 天
            int d = day % 365;
            if (d < 91) return Season.Spring;
            if (d < 182) return Season.Summer;
            if (d < 273) return Season.Autumn;
            return Season.Winter;
        }

        private float GetSeasonProgress(int day)
        {
            int d = day % 365;
            int seasonStart = (d / 91) * 91;
            return Mathf.Clamp01((float)(d - seasonStart) / 90f) * 100f;
        }

        private void RollDailyWeather()
        {
            // 四季天气概率不同
            float roll = UnityEngine.Random.value;
            Weather newWeather;
            switch (currentSeason)
            {
                case Season.Spring:
                    newWeather = roll < 0.30f ? Weather.Clear :
                                 roll < 0.55f ? Weather.Cloudy :
                                 roll < 0.80f ? Weather.Rain :
                                 roll < 0.95f ? Weather.Mist : Weather.Storm;
                    break;
                case Season.Summer:
                    newWeather = roll < 0.45f ? Weather.Clear :
                                 roll < 0.65f ? Weather.Cloudy :
                                 roll < 0.80f ? Weather.Rain :
                                 roll < 0.95f ? Weather.Mist : Weather.Storm;
                    break;
                case Season.Autumn:
                    newWeather = roll < 0.35f ? Weather.Clear :
                                 roll < 0.60f ? Weather.Cloudy :
                                 roll < 0.80f ? Weather.Mist :
                                 roll < 0.95f ? Weather.Rain : Weather.Storm;
                    break;
                case Season.Winter:
                    newWeather = roll < 0.25f ? Weather.Clear :
                                 roll < 0.50f ? Weather.Cloudy :
                                 roll < 0.70f ? Weather.Snow :
                                 roll < 0.90f ? Weather.Mist : Weather.Storm;
                    break;
                default:
                    newWeather = Weather.Clear;
                    break;
            }
            if (newWeather != currentWeather)
            {
                currentWeather = newWeather;
                OnWeatherChanged?.Invoke(currentWeather);
            }
        }

        private void UpdateColorSets(Season season)
        {
            targetColors = GetColorSet(season);
            previousColors = targetColors;
            colorLerpFactor = 1f;
        }

        public SeasonColorSet GetColorSet(Season season)
        {
            switch (season)
            {
                case Season.Spring: return springColors;
                case Season.Summer: return summerColors;
                case Season.Autumn: return autumnColors;
                case Season.Winter: return winterColors;
                default:            return springColors;
            }
        }

        public SeasonColorSet GetCurrentBlendedColors()
        {
            return new SeasonColorSet
            {
                inkTone = Color.Lerp(previousColors.inkTone, targetColors.inkTone, colorLerpFactor),
                seasonTint = Color.Lerp(previousColors.seasonTint, targetColors.seasonTint, colorLerpFactor),
                vignetteColor = Color.Lerp(previousColors.vignetteColor, targetColors.vignetteColor, colorLerpFactor),
                uiAccent = Color.Lerp(previousColors.uiAccent, targetColors.uiAccent, colorLerpFactor)
            };
        }

        // -- 公共查询 --
        public Season CurrentSeason => currentSeason;
        public Weather CurrentWeather => currentWeather;
        public float SeasonProgress => seasonProgress;
        public bool IsRaining => currentWeather == Weather.Rain || currentWeather == Weather.Storm;
        public bool IsSnowing => currentWeather == Weather.Snow;

        /// <summary>
        /// 获取当前天气聊天开场白变体标签
        /// </summary>
        public string GetWeatherGreetingTag()
        {
            switch (currentWeather)
            {
                case Weather.Clear: return "晴天";
                case Weather.Cloudy: return "阴天";
                case Weather.Rain: return "雨天";
                case Weather.Snow: return "雪天";
                case Weather.Mist: return "雾天";
                case Weather.Storm: return "风雨";
                default: return "平常";
            }
        }
    }
}
