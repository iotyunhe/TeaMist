using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 音频管理器 — 环境音 + BGM + 音效 完整系统。
    /// 
    /// 功能：
    /// - 环境音：昼夜/天气/季节自动切换，交叉淡出
    /// - BGM：场景音乐，情绪联动，交叉淡出
    /// - 音效：门铃/泡茶/碎片/UI 等交互音效
    /// - 程序化音频：无实际音频文件时自动生成占位音效
    /// - 音量控制：主音量/环境音/BGM/音效 独立控制
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("━━━ 环境音（占位）━━━")]
        public AudioClip ambientDay;      // 白天茶馆：鸟鸣+远处人声
        public AudioClip ambientEvening;  // 傍晚：虫鸣+风铃
        public AudioClip ambientNight;    // 夜间：寂静+偶尔风声
        public AudioClip ambientRain;     // 雨天：雨声+屋檐滴水
        public AudioClip ambientSnow;     // 雪天：寂静+踏雪声

        [Header("━━━ BGM ━━━")]
        public AudioClip bgmTeahouse;     // 茶馆日常 BGM
        public AudioClip bgmDialogue;     // 对话 BGM
        public AudioClip bgmBrewing;     // 泡茶 BGM
        public AudioClip bgmEmotional;    // 情感场景 BGM
        public AudioClip bgmTitle;        // 标题画面 BGM

        [Header("━━━ 音效（占位）━━━")]
        public AudioClip doorBell;        // 门铃：客人进店
        public AudioClip doorClose;       // 关门：客人离店
        public AudioClip teaPour;         // 倒茶声
        public AudioClip fragmentGet;     // 获得碎片
        public AudioClip dayStart;        // 清晨开门
        public AudioClip dayEnd;          // 晚间闭店
        public AudioClip pageTurn;        // 翻页声（茶谱图鉴）
        public AudioClip brewSelect;      // 泡茶选择：择壶/选叶
        public AudioClip brewTempConfirm; // 控温确认
        public AudioClip brewWaterPour;   // 注水声
        public AudioClip brewPourTea;     // 出汤声
        public AudioClip brewStepAdvance; // 步骤推进
        public AudioClip brewResultGreat; // 结果：完美
        public AudioClip brewResultGood;  // 结果：不错
        public AudioClip brewResultOk;    // 结果：还行

        [Header("━━━ 参数 ━━━")]
        [Range(0f, 1f)]
        public float masterVolume = 0.7f;
        [Range(0f, 1f)]
        public float ambientVolume = 0.6f;
        [Range(0f, 1f)]
        public float sfxVolume = 0.8f;
        [Range(0f, 1f)]
        public float bgmVolume = 0.4f;
        [Range(0.5f, 3f)]
        public float crossFadeDuration = 1.5f;

        private AudioSource _ambientSource;
        private AudioSource _ambientSourceB;   // 用于交叉淡出
        private AudioSource _bgmSource;
        private AudioSource _bgmSourceB;       // BGM 交叉淡出
        private AudioSource _sfxSource;
        private bool _usingSourceA = true;
        private bool _usingBgmA = true;
        private string _currentBgmName;        // 当前 BGM 名称

        // PlayerPrefs keys
        private const string KEY_MASTER  = "Audio_Master";
        private const string KEY_AMBIENT = "Audio_Ambient";
        private const string KEY_SFX     = "Audio_SFX";
        private const string KEY_BGM     = "Audio_BGM";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 创建音频源
            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.volume = 0f;
            _ambientSource.playOnAwake = false;

            _ambientSourceB = gameObject.AddComponent<AudioSource>();
            _ambientSourceB.loop = true;
            _ambientSourceB.volume = 0f;
            _ambientSourceB.playOnAwake = false;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.volume = 0f;
            _bgmSource.playOnAwake = false;

            _bgmSourceB = gameObject.AddComponent<AudioSource>();
            _bgmSourceB.loop = true;
            _bgmSourceB.volume = 0f;
            _bgmSourceB.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = 0.8f;
        }

        void Start()
        {
            // 从 PlayerPrefs 加载音量设置
            masterVolume  = SaveManager.LoadSettingFloat(KEY_MASTER, 0.7f);
            ambientVolume = SaveManager.LoadSettingFloat(KEY_AMBIENT, 0.6f);
            sfxVolume     = SaveManager.LoadSettingFloat(KEY_SFX, 0.8f);
            bgmVolume     = SaveManager.LoadSettingFloat(KEY_BGM, 0.4f);

            // 生成程序化占位音频（当 Inspector 未配置实际音频时）
            GenerateProceduralAudio();

            // 默认播放白天环境音
            PlayAmbient(ambientDay);

            // 连接时间管理器
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnGameHourChanged += OnHourChanged;
                TimeManager.Instance.OnSeasonChanged += OnSeasonChanged;
                TimeManager.Instance.OnWeatherChanged += OnWeatherChanged;
            }

            Debug.Log("[AudioManager] 音频系统初始化完成（含程序化占位音频）");
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnGameHourChanged -= OnHourChanged;
                TimeManager.Instance.OnSeasonChanged -= OnSeasonChanged;
                TimeManager.Instance.OnWeatherChanged -= OnWeatherChanged;
            }
        }

        // ━━━ 公共 API ━━━

        /// <summary>设置主音量（0-1）</summary>
        public void SetMasterVolume(float v)
        {
            masterVolume = Mathf.Clamp01(v);
            _ambientSource.volume = masterVolume * ambientVolume;
            if (_ambientSourceB.isPlaying)
                _ambientSourceB.volume = masterVolume * ambientVolume;
        }

        /// <summary>设置环境音量（0-1），相对于主音量</summary>
        public void SetAmbientVolume(float v)
        {
            ambientVolume = Mathf.Clamp01(v);
            _ambientSource.volume = masterVolume * ambientVolume;
            if (_ambientSourceB.isPlaying)
                _ambientSourceB.volume = masterVolume * ambientVolume;
        }

        /// <summary>设置BGM音量（0-1），相对于主音量</summary>
        public void SetBGMVolume(float v)
        {
            bgmVolume = Mathf.Clamp01(v);
            _bgmSource.volume = masterVolume * bgmVolume;
            if (_bgmSourceB.isPlaying)
                _bgmSourceB.volume = masterVolume * bgmVolume;
        }

        /// <summary>设置音效音量（0-1），相对于主音量</summary>
        public void SetSFXVolume(float v)
        {
            sfxVolume = Mathf.Clamp01(v);
        }

        /// <summary>切换到指定环境音（交叉淡出）</summary>
        public void PlayAmbient(AudioClip clip)
        {
            if (clip == null) return;
            StopAllCoroutines();
            StartCoroutine(CrossFadeAmbient(clip));
        }

        /// <summary>播放/切换 BGM（交叉淡出）</summary>
        public void PlayBGM(AudioClip clip, string bgmName = "")
        {
            if (clip == null) return;
            if (_currentBgmName == bgmName && !string.IsNullOrEmpty(bgmName)) return;
            _currentBgmName = bgmName;
            StopAllCoroutines();
            StartCoroutine(CrossFadeBGM(clip));
        }

        /// <summary>停止 BGM（淡出）</summary>
        public void StopBGM(float fadeTime = 1f)
        {
            _currentBgmName = "";
            StartCoroutine(FadeOutBGM(fadeTime));
        }

        /// <summary>播放茶馆日常 BGM</summary>
        public void PlayTeahouseBGM() => PlayBGM(bgmTeahouse, "teahouse");

        /// <summary>播放对话 BGM</summary>
        public void PlayDialogueBGM() => PlayBGM(bgmDialogue, "dialogue");

        /// <summary>播放泡茶 BGM</summary>
        public void PlayBrewingBGM() => PlayBGM(bgmBrewing, "brewing");

        /// <summary>播放情感场景 BGM</summary>
        public void PlayEmotionalBGM() => PlayBGM(bgmEmotional, "emotional");

        /// <summary>一次性音效播放</summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, volumeScale * sfxVolume * masterVolume);
        }

        /// <summary>门铃声 — 客人进店</summary>
        public void PlayDoorBell()
        {
            if (doorBell != null)
                _sfxSource.PlayOneShot(doorBell, 0.7f * sfxVolume * masterVolume);
        }

        /// <summary>关门声 — 客人离店</summary>
        public void PlayDoorClose()
        {
            if (doorClose != null)
                _sfxSource.PlayOneShot(doorClose, 0.6f * sfxVolume * masterVolume);
        }

        /// <summary>倒茶声</summary>
        public void PlayTeaPour()
        {
            if (teaPour != null)
                _sfxSource.PlayOneShot(teaPour, 0.5f * sfxVolume * masterVolume);
        }

        /// <summary>获得碎片提示音</summary>
        public void PlayFragmentGet()
        {
            if (fragmentGet != null)
                _sfxSource.PlayOneShot(fragmentGet, 0.8f * sfxVolume * masterVolume);
        }

        /// <summary>清晨开门</summary>
        public void PlayDayStart()
        {
            if (dayStart != null)
                _sfxSource.PlayOneShot(dayStart, 0.6f * sfxVolume * masterVolume);
        }

        /// <summary>晚间闭店</summary>
        public void PlayDayEnd()
        {
            if (dayEnd != null)
                _sfxSource.PlayOneShot(dayEnd, 0.5f * sfxVolume * masterVolume);
        }

        // ━━━ 泡茶音效 ━━━

        /// <summary>择壶/选叶点击</summary>
        public void PlayBrewSelect()
        {
            if (brewSelect != null)
                _sfxSource.PlayOneShot(brewSelect, 0.4f * sfxVolume * masterVolume);
        }

        /// <summary>控温确认</summary>
        public void PlayBrewTempConfirm()
        {
            if (brewTempConfirm != null)
                _sfxSource.PlayOneShot(brewTempConfirm, 0.45f * sfxVolume * masterVolume);
        }

        /// <summary>注水</summary>
        public void PlayBrewWaterPour()
        {
            if (brewWaterPour != null)
                _sfxSource.PlayOneShot(brewWaterPour, 0.55f * sfxVolume * masterVolume);
        }

        /// <summary>出汤</summary>
        public void PlayBrewPourTea()
        {
            if (brewPourTea != null)
                _sfxSource.PlayOneShot(brewPourTea, 0.5f * sfxVolume * masterVolume);
        }

        /// <summary>步骤推进</summary>
        public void PlayBrewStepAdvance()
        {
            if (brewStepAdvance != null)
                _sfxSource.PlayOneShot(brewStepAdvance, 0.35f * sfxVolume * masterVolume);
        }

        /// <summary>泡茶结果反馈</summary>
        public void PlayBrewResult(int score)
        {
            AudioClip clip = score >= 90 ? brewResultGreat :
                             score >= 60 ? brewResultGood : brewResultOk;
            if (clip != null)
                _sfxSource.PlayOneShot(clip, 0.6f * sfxVolume * masterVolume);
        }

        // ━━━ 内部 ━━━

        private void OnHourChanged(int hour)
        {
            AudioClip target = null;
            if (hour >= 6 && hour < 17) target = ambientDay;
            else if (hour >= 17 && hour < 20) target = ambientEvening;
            else target = ambientNight;

            // 天气优先：雨天/雪天覆盖昼夜环境音
            if (WeatherManager.Instance != null)
            {
                var w = WeatherManager.Instance.CurrentWeather;
                if (w == WeatherType.雨 || w == WeatherType.雷) target = ambientRain;
                else if (w == WeatherType.雪) target = ambientSnow;
            }

            if (target != null && _ambientSource.clip != target)
                PlayAmbient(target);
        }

        private void OnWeatherChanged(Data.WeatherType weather)
        {
            AudioClip target = null;
            switch (weather)
            {
                case WeatherType.雨:
                case WeatherType.雷:
                case WeatherType.暴雨:
                case WeatherType.雷暴:
                    target = ambientRain;
                    break;
                case WeatherType.雪:
                case WeatherType.暴风雪:
                    target = ambientSnow;
                    break;
                case WeatherType.雾:
                case WeatherType.大雾:
                    target = ambientRain; // 大雾用轻柔环境音
                    break;
                default:
                    // 晴天/多云/雾/风 → 根据时间选择
                    if (TimeManager.Instance != null)
                    {
                        int hour = TimeManager.Instance.CurrentGameHour;
                        if (hour >= 6 && hour < 17) target = ambientDay;
                        else if (hour >= 17 && hour < 20) target = ambientEvening;
                        else target = ambientNight;
                    }
                    else target = ambientDay;
                    break;
            }

            if (target != null && _ambientSource.clip != target)
                PlayAmbient(target);
        }

        private void OnSeasonChanged(Data.Season season)
        {
            // 季节变化时尝试切换到季节专属环境音 / BGM
            var cfg = DataManager.Instance?.GetSeasonConfig(season);
            if (cfg == null) return;

            // 环境音切换 — SeasonConfigSO.ambientSound 目前是字符串引用，
            // 未来可以扩展为从 Addressable 或 Resources 加载对应 AudioClip；
            // 当前保留钩子，不做强绑定。
            Debug.Log($"[AudioManager] 季节切换至 {season}，推荐环境音: {cfg.ambientSound}");
        }

        private System.Collections.IEnumerator CrossFadeAmbient(AudioClip newClip)
        {
            var fadeOutSource = _usingSourceA ? _ambientSource : _ambientSourceB;
            var fadeInSource = _usingSourceA ? _ambientSourceB : _ambientSource;
            _usingSourceA = !_usingSourceA;

            fadeInSource.clip = newClip;
            fadeInSource.volume = 0f;
            fadeInSource.Play();

            float baseVol = masterVolume * ambientVolume;
            float elapsed = 0f;
            while (elapsed < crossFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / crossFadeDuration;
                fadeOutSource.volume = baseVol * (1f - t);
                fadeInSource.volume = baseVol * t;
                yield return null;
            }

            fadeOutSource.volume = 0f;
            fadeOutSource.Stop();
            fadeInSource.volume = baseVol;
        }

        private System.Collections.IEnumerator CrossFadeBGM(AudioClip newClip)
        {
            var fadeOutSource = _usingBgmA ? _bgmSource : _bgmSourceB;
            var fadeInSource = _usingBgmA ? _bgmSourceB : _bgmSource;
            _usingBgmA = !_usingBgmA;

            fadeInSource.clip = newClip;
            fadeInSource.volume = 0f;
            fadeInSource.Play();

            float baseVol = masterVolume * bgmVolume;
            float elapsed = 0f;
            float bgmFadeDuration = crossFadeDuration * 1.5f; // BGM 淡变稍慢
            while (elapsed < bgmFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bgmFadeDuration;
                fadeOutSource.volume = baseVol * (1f - t);
                fadeInSource.volume = baseVol * t;
                yield return null;
            }

            fadeOutSource.volume = 0f;
            fadeOutSource.Stop();
            fadeInSource.volume = baseVol;
        }

        private System.Collections.IEnumerator FadeOutBGM(float fadeTime)
        {
            var source = _usingBgmA ? _bgmSource : _bgmSourceB;
            float startVol = source.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime && source.volume > 0f)
            {
                elapsed += Time.deltaTime;
                source.volume = startVol * (1f - elapsed / fadeTime);
                yield return null;
            }
            source.volume = 0f;
            source.Stop();
        }

        // ━━━ 程序化音频生成 ━━━

        private void GenerateProceduralAudio()
        {
            // 环境音占位
            if (ambientDay == null) ambientDay = ProceduralAudio.CreateAmbientBirds();
            if (ambientEvening == null) ambientEvening = ProceduralAudio.CreateAmbientCrickets();
            if (ambientNight == null) ambientNight = ProceduralAudio.CreateAmbientNight();
            if (ambientRain == null) ambientRain = ProceduralAudio.CreateAmbientRain();
            if (ambientSnow == null) ambientSnow = ProceduralAudio.CreateAmbientNight(10f);

            // BGM 占位
            if (bgmTeahouse == null) bgmTeahouse = ProceduralAudio.CreateSimpleBGM("BGM_Teahouse", 20f, 262f);
            if (bgmDialogue == null) bgmDialogue = ProceduralAudio.CreateSimpleBGM("BGM_Dialogue", 16f, 294f);
            if (bgmBrewing == null) bgmBrewing = ProceduralAudio.CreateSimpleBGM("BGM_Brewing", 12f, 330f);
            if (bgmEmotional == null) bgmEmotional = ProceduralAudio.CreateSimpleBGM("BGM_Emotional", 18f, 220f);

            // 音效占位
            if (doorBell == null) doorBell = ProceduralAudio.CreateDoorBell();
            if (doorClose == null) doorClose = ProceduralAudio.CreateDoorClose();
            if (teaPour == null) teaPour = ProceduralAudio.CreateWaterPour();
            if (fragmentGet == null) fragmentGet = ProceduralAudio.CreateFragmentGet();
            if (brewSelect == null) brewSelect = ProceduralAudio.CreateUIClick();
            if (brewStepAdvance == null) brewStepAdvance = ProceduralAudio.CreateUIClick();
            if (brewTempConfirm == null) brewTempConfirm = ProceduralAudio.CreateSineTone("TempConfirm", 880f, 0.15f, 0.05f, 0.3f);
            if (brewWaterPour == null) brewWaterPour = ProceduralAudio.CreateWaterPour(2f);
            if (brewPourTea == null) brewPourTea = ProceduralAudio.CreateWaterPour(1f);
            if (brewResultGreat == null) brewResultGreat = ProceduralAudio.CreateFragmentGet();
            if (brewResultGood == null) brewResultGood = ProceduralAudio.CreateSineTone("Good", 660f, 0.3f, 0.1f, 0.3f);
            if (brewResultOk == null) brewResultOk = ProceduralAudio.CreateSineTone("Ok", 440f, 0.2f, 0.1f, 0.2f);

            Debug.Log("[AudioManager] 程序化占位音频生成完成");
        }
    }
}
