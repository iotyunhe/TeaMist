using UnityEngine;
using TeaMist.Core;
using TeaMist.Data;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 茶馆经营声望管理器。
    /// 包裹 DataManager 中的 teaHouseConfig (ShopPropertySO)，驱动声望经验 → 升级 → 解锁更多坐席。
    /// 
    /// 挂载在 TeaHouseManager GameObject 上，由 Bootstrap 自动创建。
    /// </summary>
    public class TeaHouseManager : MonoBehaviour
    {
        public static TeaHouseManager Instance { get; private set; }

        [Header("━━━ 升级参数 ━━━")]
        [Tooltip("每级所需声望经验（对应 level 1→10）")]
        public float[] levelXPThresholds = { 50f, 120f, 220f, 360f, 550f, 800f, 1100f, 1500f, 2000f };

        [Tooltip("每升一级增加的客人数")]
        public int guestsPerLevel = 1;

        [Tooltip("基础客人数（等级 1）")]
        public int baseMaxGuests = 3;

        [Header("━━━ 经验获取 ━━━")]
        [Tooltip("泡茶品质基础经验")]
        public float baseTeaXP = 10f;

        [Tooltip("品质倍率：qualityScore/100 * multiplier")]
        public float qualityXPMultiplier = 1.5f;

        [Tooltip("每获得一个碎片额外经验")]
        public float fragmentXP = 15f;

        [Tooltip("每日首次接待客人额外经验")]
        public float firstCustomerBonusXP = 25f;

        // ── 运行时 ──
        private ShopPropertySO _config;
        private bool _firstCustomerToday;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 延迟一帧等 DataManager 就绪
            Invoke(nameof(InitFromDataManager), 0.1f);
        }

        private void InitFromDataManager()
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[TeaHouseManager] DataManager 未就绪，跳过初始化");
                return;
            }

            _config = DataManager.Instance.GetShopConfig(ShopType.茶馆);
            if (_config == null)
            {
                Debug.LogWarning("[TeaHouseManager] 茶馆配置为 null，使用默认值");
                return;
            }

            // 同步新的一天
            _firstCustomerToday = true;

            Debug.Log($"[TeaHouseManager] 茶馆「{_config.shopName}」等级 {_config.level} | " +
                      $"名声 {_config.reputationLevel} | 经验 {_config.reputationXP:F0}/{GetCurrentThreshold():F0} | " +
                      $"客容量 {GetMaxGuests()}");
        }

        // ── 公开 API ──

        /// <summary>泡茶完成时调用，品质越高经验越多</summary>
        public void OnTeaBrewed(int qualityScore)
        {
            if (_config == null) return;

            float xp = baseTeaXP + (qualityScore / 100f) * qualityXPMultiplier * baseTeaXP;
            // 完美泡茶额外奖励
            if (qualityScore >= 90) xp *= 1.5f;

            _config.reputationXP += xp;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaHouseManager] 泡茶品质 {qualityScore} → +{xp:F1} XP (总计 {_config.reputationXP:F0})");
#endif

            CheckLevelUp();
        }

        /// <summary>获得碎片时调用</summary>
        public void OnFragmentCollected(string fragmentId)
        {
            if (_config == null) return;
            _config.reputationXP += fragmentXP;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaHouseManager] 获得碎片 → +{fragmentXP} XP (总计 {_config.reputationXP:F0})");
#endif
            CheckLevelUp();
        }

        /// <summary>新客人到访时调用</summary>
        public void OnCustomerEntered()
        {
            if (_config == null) return;

            if (_firstCustomerToday)
            {
                _config.reputationXP += firstCustomerBonusXP;
                _firstCustomerToday = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TeaHouseManager] 今日首客 → +{firstCustomerBonusXP} XP");
#endif
            }
            CheckLevelUp();
        }

        /// <summary>新的一天开始</summary>
        public void OnNewDay()
        {
            _firstCustomerToday = true;
        }

        /// <summary>获取当前最大接待客人数</summary>
        public int GetMaxGuests()
        {
            if (_config == null) return baseMaxGuests;
            return baseMaxGuests + (_config.level - 1) * guestsPerLevel;
        }

        /// <summary>获取当前等级</summary>
        public int GetLevel() => _config?.level ?? 1;

        /// <summary>获取名声等级</summary>
        public int GetReputationLevel() => _config?.reputationLevel ?? 0;

        /// <summary>获取当前经验进度</summary>
        public float GetXP() => _config?.reputationXP ?? 0;

        /// <summary>获取当前等级所需经验阈值</summary>
        public float GetCurrentThreshold()
        {
            if (_config == null) return levelXPThresholds[0];
            int idx = Mathf.Clamp(_config.level - 1, 0, levelXPThresholds.Length - 1);
            return levelXPThresholds[idx];
        }

        // ── 内部 ──

        private void CheckLevelUp()
        {
            if (_config == null) return;
            if (_config.level >= 10) return; // 满级

            float threshold = GetCurrentThreshold();
            while (_config.reputationXP >= threshold && _config.level < 10)
            {
                _config.reputationXP -= threshold;
                _config.level++;
                _config.reputationLevel = Mathf.Min(5, (_config.level - 1) / 2);

                // 升级时同步 TeaShopLoop 的客容量（直接调用公共方法）
                if (TeaShopLoop.Instance != null)
                {
                    TeaShopLoop.Instance.UpdateMaxCustomers(GetMaxGuests());
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TeaHouseManager] 🎉 茶馆升级！等级 {_config.level} | " +
                          $"名声 {_config.reputationLevel} | 客容量 {GetMaxGuests()}");
#endif

                threshold = GetCurrentThreshold();
            }
        }

        // ── 调试面板 ──

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            if (_config == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.92f, 0.85f, 0.55f) }
            };

            float x = 10, y = Screen.height - 90;
            GUI.Label(new Rect(x, y, 250, 80),
                $"茶馆 Lv.{_config.level} | 名声 Lv.{_config.reputationLevel}\n" +
                $"经验: {_config.reputationXP:F0}/{GetCurrentThreshold():F0}\n" +
                $"客容量: {GetMaxGuests()} | 完美泡茶: {_config.perfectBrewMultiplier:F1}×",
                style);
        }
#endif
    }
}
