using UnityEngine;
using System.Collections.Generic;
using TeaMist.Core;
using TeaMist.Data;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 茶馆经营声望管理器。
    /// 包裹 DataManager 中的 teaHouseConfig (ShopPropertySO)，驱动声望经验 → 升级 → 解锁更多坐席。
    /// 
    /// 新增功能：
    /// - 装饰物品系统：摆放装饰物提升气质，获得经营加成
    /// - 等级解锁内容：每级解锁新茶谱、新NPC、新装饰
    /// - 四业联动：与 ShopSynergyManager 配合，获得共鸣加成
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
        private List<DecorationItemSO> _placedDecorations = new List<DecorationItemSO>();
        private HashSet<string> _unlockedLevelRewards = new HashSet<string>();

        // ── 等级解锁内容 ──
        private static readonly Dictionary<int, LevelUnlock> LevelUnlocks = new Dictionary<int, LevelUnlock>
        {
            { 2, new LevelUnlock("新茶谱：清心茶", "解锁清心茶配方", "qingxincha") },
            { 3, new LevelUnlock("新坐席：窗边位", "客容量+1", null) },
            { 4, new LevelUnlock("装饰：竹帘", "解锁竹帘装饰", "deco_bamboo_curtain") },
            { 5, new LevelUnlock("新NPC：青岚来访", "云游画师开始到访", null) },
            { 6, new LevelUnlock("新茶谱：雪山银针", "解锁雪山银针配方", "xueshancha") },
            { 7, new LevelUnlock("装饰：石灯笼", "解锁石灯笼装饰", "deco_stone_lantern") },
            { 8, new LevelUnlock("四业联动", "解锁跨店共鸣系统", null) },
            { 9, new LevelUnlock("装饰：古画", "解锁古画装饰", "deco_ancient_painting") },
            { 10, new LevelUnlock("栖霞名茶馆", "最高等级，解锁全部装饰", null) },
        };

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

            // 装饰加成
            xp *= GetDecorationXPMultiplier();

            // 四业共鸣加成
            if (ShopSynergyManager.Instance != null)
                xp *= ShopSynergyManager.Instance.GetResonanceMultiplier();

            _config.reputationXP += xp;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaHouseManager] 泡茶品质 {qualityScore} → +{xp:F1} XP (总计 {_config.reputationXP:F0})");
#endif

            // 同步山间名声
            ShopSynergyManager.Instance?.OnShopXPGained(ShopType.茶馆, xp);

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
        public void OnCustomerEntered(string npcId = null)
        {
            if (_config == null) return;

            if (_firstCustomerToday)
            {
                float bonusXP = firstCustomerBonusXP;
                // 装饰加成
                bonusXP *= GetDecorationXPMultiplier();
                _config.reputationXP += bonusXP;
                _firstCustomerToday = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TeaHouseManager] 今日首客 → +{bonusXP:F1} XP");
#endif
            }

            // NPC 店铺偏好加成
            if (!string.IsNullOrEmpty(npcId))
            {
                float prefMultiplier = ShopSynergyManager.Instance != null
                    ? ShopSynergyManager.Instance.GetNPCShopPreference(npcId, ShopType.茶馆)
                    : 1f;
                if (prefMultiplier > 1f)
                {
                    float prefXP = baseTeaXP * (prefMultiplier - 1f);
                    _config.reputationXP += prefXP;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[TeaHouseManager] NPC 偏好加成 → +{prefXP:F1} XP ({prefMultiplier:F2}×)");
#endif
                }
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

        // ── 装饰系统 ──

        /// <summary>摆放装饰物</summary>
        public bool PlaceDecoration(DecorationItemSO decoration)
        {
            if (_config == null || decoration == null) return false;

            // 检查等级
            if (_config.level < decoration.requiredLevel)
            {
                Debug.LogWarning($"[TeaHouseManager] 等级不足，需要 Lv.{decoration.requiredLevel}");
                return false;
            }

            // 检查是否已摆放
            if (_placedDecorations.Contains(decoration))
            {
                Debug.LogWarning($"[TeaHouseManager] 装饰已摆放: {decoration.displayName}");
                return false;
            }

            _placedDecorations.Add(decoration);

            // 应用气质加成
            _config.sereneValue = Mathf.Min(100f, _config.sereneValue + decoration.sereneBonus);
            _config.warmValue = Mathf.Min(100f, _config.warmValue + decoration.warmBonus);
            _config.elegantValue = Mathf.Min(100f, _config.elegantValue + decoration.elegantBonus);
            _config.playfulValue = Mathf.Min(100f, _config.playfulValue + decoration.playfulBonus);
            _config.herbalValue = Mathf.Min(100f, _config.herbalValue + decoration.herbalBonus);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaHouseManager] 摆放装饰: {decoration.displayName} | " +
                      $"幽静+{decoration.sereneBonus} 温暖+{decoration.warmBonus} " +
                      $"雅致+{decoration.elegantBonus} 趣味+{decoration.playfulBonus}");
#endif
            return true;
        }

        /// <summary>移除装饰物</summary>
        public void RemoveDecoration(DecorationItemSO decoration)
        {
            if (_config == null || decoration == null) return;
            if (!_placedDecorations.Contains(decoration)) return;

            _placedDecorations.Remove(decoration);

            // 移除气质加成
            _config.sereneValue = Mathf.Max(0f, _config.sereneValue - decoration.sereneBonus);
            _config.warmValue = Mathf.Max(0f, _config.warmValue - decoration.warmBonus);
            _config.elegantValue = Mathf.Max(0f, _config.elegantValue - decoration.elegantBonus);
            _config.playfulValue = Mathf.Max(0f, _config.playfulValue - decoration.playfulBonus);
            _config.herbalValue = Mathf.Max(0f, _config.herbalValue - decoration.herbalBonus);
        }

        /// <summary>获取已摆放的装饰列表</summary>
        public List<DecorationItemSO> GetPlacedDecorations() => _placedDecorations;

        /// <summary>获取装饰经验加成倍率</summary>
        public float GetDecorationXPMultiplier()
        {
            float bonus = 0f;
            foreach (var deco in _placedDecorations)
                bonus += deco.xpBonusPercent;
            return 1f + bonus;
        }

        /// <summary>获取装饰营收加成倍率</summary>
        public float GetDecorationRevenueMultiplier()
        {
            float bonus = 0f;
            foreach (var deco in _placedDecorations)
                bonus += deco.revenueBonusPercent;
            return 1f + bonus;
        }

        /// <summary>获取当前总气质值</summary>
        public float GetTotalAura()
        {
            if (_config == null) return 0f;
            return _config.sereneValue + _config.warmValue + _config.elegantValue + _config.playfulValue;
        }

        // ── 等级解锁 ──

        /// <summary>获取指定等级的解锁内容</summary>
        public static LevelUnlock GetLevelUnlock(int level)
        {
            return LevelUnlocks.TryGetValue(level, out var unlock) ? unlock : null;
        }

        /// <summary>检查是否已领取等级奖励</summary>
        public bool IsLevelRewardClaimed(int level) => _unlockedLevelRewards.Contains($"lv{level}");

        /// <summary>领取等级奖励</summary>
        public string ClaimLevelReward(int level)
        {
            string key = $"lv{level}";
            if (_unlockedLevelRewards.Contains(key)) return null;

            var unlock = GetLevelUnlock(level);
            if (unlock == null) return null;

            _unlockedLevelRewards.Add(key);

            // 解锁茶谱
            if (!string.IsNullOrEmpty(unlock.teaRecipeId))
            {
                DataManager.Instance?.UnlockTeaRecipe(unlock.teaRecipeId);
            }

            return unlock.description;
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

                // 升级时同步 TeaShopLoop 的客容量
                if (TeaShopLoop.Instance != null)
                {
                    TeaShopLoop.Instance.UpdateMaxCustomers(GetMaxGuests());
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TeaHouseManager] 🎉 茶馆升级！等级 {_config.level} | " +
                          $"名声 {_config.reputationLevel} | 客容量 {GetMaxGuests()}");
#endif

                // 显示解锁内容
                var unlock = GetLevelUnlock(_config.level);
                if (unlock != null)
                {
                    Debug.Log($"[TeaHouseManager] 🔓 解锁: {unlock.title} — {unlock.description}");
                }

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

            float x = 10, y = Screen.height - 110;
            float xpMult = GetDecorationXPMultiplier();
            float revMult = GetDecorationRevenueMultiplier();

            GUI.Label(new Rect(x, y, 300, 100),
                $"茶馆 Lv.{_config.level} | 名声 Lv.{_config.reputationLevel}\n" +
                $"经验: {_config.reputationXP:F0}/{GetCurrentThreshold():F0}\n" +
                $"客容量: {GetMaxGuests()} | 完美泡茶: {_config.perfectBrewMultiplier:F1}×\n" +
                $"气质: 幽{_config.sereneValue:F0} 暖{_config.warmValue:F0} " +
                $"雅{_config.elegantValue:F0} 趣{_config.playfulValue:F0}\n" +
                $"装饰加成: 经验{xpMult:F2}× 营收{revMult:F2}×",
                style);
        }
#endif
    }

    /// <summary>
    /// 等级解锁内容定义
    /// </summary>
    [System.Serializable]
    public class LevelUnlock
    {
        public string title;
        public string description;
        public string teaRecipeId; // 如果解锁茶谱

        public LevelUnlock(string title, string description, string teaRecipeId)
        {
            this.title = title;
            this.description = description;
            this.teaRecipeId = teaRecipeId;
        }
    }
}
