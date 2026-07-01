using System;
using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 时间管理器 — 无惩罚真实时间流
    /// - 读取系统时间判定季节/昼夜
    /// - 支持离线天数计算和批量推进
    /// - 不处罚玩家缺席，只是世界在默默变化
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Header("季节定义")]
        [Tooltip("春: 3-5月, 夏: 6-8月, 秋: 9-11月, 冬: 12-2月")]
        public bool useRealSeasons = true;

        [Header("时间参数")]
        [Tooltip("游戏中一天对应的真实秒数（默认120=2分钟）")]
        public float secondsPerDay = 120f;

        [Header("昼夜定义")]
        public int morningStart   = 5;   // 清晨 5:00
        public int forenoonStart  = 8;   // 上午 8:00
        public int afternoonStart = 12;  // 午后 12:00
        public int eveningStart   = 17;  // 傍晚 17:00
        public int nightStart     = 21;  // 深夜 21:00

        [Header("时间缩放 (仅测试用，正式版为1)")]
        [Range(0.1f, 60f)]
        public float timeScale = 1f;

        // ── 运行时状态 ──
        public Season CurrentSeason { get; private set; }
        public int DayInSeason { get; private set; }
        /// <summary>年内第几天（1-360，每季90天）</summary>
        public int DayOfYear => (int)CurrentSeason * 90 + DayInSeason + 1;
        public WeatherType CurrentWeather { get; private set; }
        public DayTimeSlot CurrentTimeSlot { get; private set; }
        public int TotalDaysPlayed { get; private set; }
        public DateTime LastPlayDate { get; private set; }

        // ── 事件 ──
        public event Action<Season> OnSeasonChanged;
        public event Action<WeatherType> OnWeatherChanged;
        public event Action<DayTimeSlot> OnTimeSlotChanged;
        public event Action<int> OnNewDay;            // 参数：已过天数
        public event Action<int> OnDaysSkipped;       // 参数：跳过的天数
        public event Action<int> OnGameHourChanged;   // 参数：当前游戏小时 (0-23)

        private DateTime _currentGameTime;
        private int _lastGameHour = -1;
        private int _daySeed; // 每日随机种子

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _currentGameTime = DateTime.Now;
            UpdateTimeState();
        }

        private void Update()
        {
            // 真实时间累进（乘以 timeScale 支持测试加速）
            if (timeScale != 1f)
            {
                _currentGameTime = _currentGameTime.AddSeconds(Time.deltaTime * (timeScale - 1f));
            }
            else
            {
                _currentGameTime = DateTime.Now;
            }

            // 检查游戏小时变化（用于驱动 NPC 来访等每小时事件）
            int gameHour = CalculateGameHour(_currentGameTime);
            if (gameHour != _lastGameHour)
            {
                _lastGameHour = gameHour;
                OnGameHourChanged?.Invoke(gameHour);

                // 新的一天
                if (gameHour == 0)
                {
                    AdvanceOneDay();
                }
            }

            // 检查时间槽变化
            var newSlot = CalculateTimeSlot(_currentGameTime);
            if (newSlot != CurrentTimeSlot)
            {
                CurrentTimeSlot = newSlot;
                OnTimeSlotChanged?.Invoke(newSlot);
            }
        }

        /// <summary>初始化或从存档恢复</summary>
        public void Initialize(SaveData save)
        {
            if (save == null)
            {
                // 新游戏：从系统时间开始
                _currentGameTime = DateTime.Now;
                TotalDaysPlayed = 1;
                DayInSeason = CalculateDayInSeason(_currentGameTime);
                CurrentSeason = CalculateSeason(_currentGameTime.Month);
                UpdateTimeState();
                return;
            }

            // 读档
            _currentGameTime = DateTime.Now;
            TotalDaysPlayed = save.totalDaysPlayed;
            CurrentSeason = save.currentSeason;
            DayInSeason = save.dayInSeason;
            CurrentWeather = save.currentWeather;
            CurrentTimeSlot = CalculateTimeSlot(_currentGameTime);

            if (!string.IsNullOrEmpty(save.lastPlayDate))
            {
                if (DateTime.TryParse(save.lastPlayDate, out DateTime lastDate))
                {
                    LastPlayDate = lastDate;

                    // 计算离线天数
                    int daysAway = (DateTime.Now.Date - lastDate.Date).Days;
                    if (daysAway > 0)
                    {
                        ProcessDaysAway(daysAway, save);
                    }
                }
            }
        }

        /// <summary>处理离线天数 — 不惩罚，仅推进世界状态</summary>
        private void ProcessDaysAway(int days, SaveData save)
        {
            Debug.Log($"[TimeManager] 你离开了 {days} 天，山里的日子照常流转。");

            // 推进季节
            for (int i = 0; i < days; i++)
            {
                AdvanceOneDay(isOffline: true);
            }

            OnDaysSkipped?.Invoke(days);
        }

        /// <summary>推进一天</summary>
        public void AdvanceOneDay(bool isOffline = false)
        {
            TotalDaysPlayed++;
            DayInSeason = (DayInSeason % GetSeasonLength(CurrentSeason)) + 1;

            // 季节切换
            if (DayInSeason == 1)
            {
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
                OnSeasonChanged?.Invoke(CurrentSeason);
            }

            // 每日天气
            CurrentWeather = CalculateDailyWeather();
            OnWeatherChanged?.Invoke(CurrentWeather);

            // 每日随机种子
            _daySeed = TotalDaysPlayed * 1000 + (int)CurrentSeason * 100 + DayInSeason;

            if (!isOffline)
            {
                OnNewDay?.Invoke(TotalDaysPlayed);
            }
        }

        /// <summary>获取每日随机种子</summary>
        public int GetDaySeed() => _daySeed;

        /// <summary>计算当前游戏内小时（0-23），从加速后的 DateTime 提取</summary>
        public int CurrentGameHour => CalculateGameHour(_currentGameTime);

        private int CalculateGameHour(DateTime time) => time.Hour;

        /// <summary>获取季节长度</summary>
        private int GetSeasonLength(Season s) => 90; // 一季90天 ≈ 现实一季度

        /// <summary>更新全部时间状态</summary>
        private void UpdateTimeState()
        {
            var now = DateTime.Now;
            CurrentSeason = useRealSeasons ? CalculateSeason(now.Month) : CurrentSeason;
            DayInSeason = CalculateDayInSeason(now);
            CurrentTimeSlot = CalculateTimeSlot(_currentGameTime);
            CurrentWeather = CalculateDailyWeather();
        }

        // ── 计算 ──

        private Season CalculateSeason(int month)
        {
            switch (month)
            {
                case 3:
                case 4:
                case 5:  return Season.春;
                case 6:
                case 7:
                case 8:  return Season.夏;
                case 9:
                case 10:
                case 11: return Season.秋;
                default: return Season.冬;
            }
        }

        private int CalculateDayInSeason(DateTime now) => now.DayOfYear % 90 + 1;

        private DayTimeSlot CalculateTimeSlot(DateTime time)
        {
            int h = time.Hour;
            if (h < 5)  return DayTimeSlot.深夜;
            if (h < 8)  return DayTimeSlot.清晨;
            if (h < 12) return DayTimeSlot.上午;
            if (h < 17) return DayTimeSlot.午后;
            if (h < 21) return DayTimeSlot.傍晚;
            return DayTimeSlot.深夜;
        }

        private WeatherType CalculateDailyWeather()
        {
            // 使用每日种子做伪随机
            UnityEngine.Random.InitState(_daySeed);
            int roll = UnityEngine.Random.Range(0, 100);

            // 默认概率（实际应从 SeasonConfigSO 读取）
            switch (CurrentSeason)
            {
                case Season.春: return PickWeather(roll, 45, 20, 20, 0, 10, 5);
                case Season.夏: return PickWeather(roll, 55, 15, 20, 0, 5,  5);
                case Season.秋: return PickWeather(roll, 50, 20, 15, 0, 10, 5);
                case Season.冬: return PickWeather(roll, 40, 15, 10, 25, 5,  5);
                default:        return WeatherType.晴;
            }
        }

        private WeatherType PickWeather(int roll, int sun, int cloud, int rain, int snow, int fog, int wind)
        {
            int acc = 0;
            acc += sun;  if (roll < acc) return WeatherType.晴;
            acc += cloud; if (roll < acc) return WeatherType.多云;
            acc += rain;  if (roll < acc) return WeatherType.雨;
            acc += snow;  if (roll < acc) return WeatherType.雪;
            acc += fog;   if (roll < acc) return WeatherType.雾;
            return WeatherType.风;
        }
    }
}
