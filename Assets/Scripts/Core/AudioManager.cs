using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 音频管理器 — 环境音效骨架。
    /// 当前使用空 Clip 占位，策划可后续替换为实际音频资源。
    /// 切换场景/时间/天气时平滑过渡环境音。
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

        [Header("━━━ 音效（占位）━━━")]
        public AudioClip doorBell;        // 门铃：客人进店
        public AudioClip doorClose;       // 关门：客人离店
        public AudioClip teaPour;         // 倒茶声
        public AudioClip fragmentGet;     // 获得碎片
        public AudioClip dayStart;        // 清晨开门
        public AudioClip dayEnd;          // 晚间闭店
        public AudioClip pageTurn;        // 翻页声（茶谱图鉴）

        [Header("━━━ 参数 ━━━")]
        [Range(0f, 1f)]
        public float masterVolume = 0.7f;
        [Range(0f, 1f)]
        public float ambientVolume = 0.6f;
        [Range(0f, 1f)]
        public float sfxVolume = 0.8f;
        [Range(0.5f, 3f)]
        public float crossFadeDuration = 1.5f;

        private AudioSource _ambientSource;
        private AudioSource _ambientSourceB;   // 用于交叉淡出
        private AudioSource _sfxSource;
        private bool _usingSourceA = true;

        // PlayerPrefs keys
        private const string KEY_MASTER  = "Audio_Master";
        private const string KEY_AMBIENT = "Audio_Ambient";
        private const string KEY_SFX     = "Audio_SFX";

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

            // 默认播放白天环境音
            PlayAmbient(ambientDay);

            // 连接时间管理器
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnGameHourChanged += OnHourChanged;
                TimeManager.Instance.OnSeasonChanged += OnSeasonChanged;
            }
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnGameHourChanged -= OnHourChanged;
                TimeManager.Instance.OnSeasonChanged -= OnSeasonChanged;
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

        /// <summary>播放一次性音效</summary>
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

        // ━━━ 内部 ━━━

        private void OnHourChanged(int hour)
        {
            AudioClip target = null;
            if (hour >= 6 && hour < 17) target = ambientDay;
            else if (hour >= 17 && hour < 20) target = ambientEvening;
            else target = ambientNight;

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
    }
}
