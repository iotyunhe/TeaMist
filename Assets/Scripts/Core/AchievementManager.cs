using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeaMist.Core
{
    /// <summary>
    /// 成就系统管理器
    /// 追踪玩家进度，解锁成就
    /// 
    /// 成就类别：
    /// - 碎片收集（收集特定数量/特定碎片）
    /// - NPC 深度线（完成首次/二次/三次来访）
    /// - 天气事件（经历极端天气）
    /// - 季节节点（经历节气事件）
    /// - 经营成就（茶馆等级/装饰）
    /// - 隐藏成就（特殊条件触发）
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        [Header("成就数据")]
        [SerializeField] private List<AchievementData> unlockedAchievements = new List<AchievementData>();

        /// <summary>成就解锁事件</summary>
        public event Action<AchievementType> OnAchievementUnlocked;

        /// <summary>已解锁成就集合（用于快速查询）</summary>
        private HashSet<AchievementType> _unlockedSet = new HashSet<AchievementType>();

        /// <summary>成就计数器（用于统计类成就）</summary>
        private Dictionary<string, int> _counters = new Dictionary<string, int>();

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
            // 从存档恢复已解锁成就
            LoadAchievements();
        }

        #region 公共 API

        /// <summary>检查成就是否已解锁</summary>
        public bool IsUnlocked(AchievementType type) => _unlockedSet.Contains(type);

        /// <summary>尝试解锁成就</summary>
        public bool Unlock(AchievementType type)
        {
            if (_unlockedSet.Contains(type)) return false;

            _unlockedSet.Add(type);
            unlockedAchievements.Add(new AchievementData
            {
                type = type,
                unlockedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });

            OnAchievementUnlocked?.Invoke(type);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Achievement] 成就解锁: {GetAchievementName(type)} - {GetAchievementDescription(type)}");
#endif

            // 自动存档
            SaveAchievements();
            return true;
        }

        /// <summary>增加计数器并检查相关成就</summary>
        public void IncrementCounter(string counterId, int amount = 1)
        {
            if (!_counters.ContainsKey(counterId))
                _counters[counterId] = 0;
            
            _counters[counterId] += amount;

            // 检查计数类成就
            CheckCounterAchievements(counterId, _counters[counterId]);
        }

        /// <summary>获取计数器值</summary>
        public int GetCounter(string counterId)
        {
            return _counters.TryGetValue(counterId, out int value) ? value : 0;
        }

        /// <summary>获取所有已解锁成就</summary>
        public List<AchievementData> GetUnlockedAchievements() => new List<AchievementData>(unlockedAchievements);

        /// <summary>获取成就进度（0-1）</summary>
        public float GetProgress(AchievementType type)
        {
            return type switch
            {
                // 碎片收集类
                AchievementType.初窥门径 => Mathf.Clamp01(GetCounter("fragments_collected") / 10f),
                AchievementType.博闻强记 => Mathf.Clamp01(GetCounter("fragments_collected") / 20f),
                AchievementType.学富五车 => Mathf.Clamp01(GetCounter("fragments_collected") / 41f),
                
                // NPC 深度线类
                AchievementType.初见 => Mathf.Clamp01(GetCounter("first_visits_completed") / 8f),
                AchievementType.再续前缘 => Mathf.Clamp01(GetCounter("second_visits_completed") / 8f),
                AchievementType.知己知彼 => Mathf.Clamp01(GetCounter("third_visits_completed") / 8f),
                
                // 天气类
                AchievementType.风雨同舟 => Mathf.Clamp01(GetCounter("extreme_weather_experienced") / 4f),
                
                // 季节类
                AchievementType.四季轮回 => Mathf.Clamp01(GetCounter("seasonal_events_experienced") / 4f),
                
                // 经营类
                AchievementType.小本经营 => Mathf.Clamp01(GetCounter("tea_brewed") / 10f),
                AchievementType.茶道大师 => Mathf.Clamp01(GetCounter("perfect_tea_brewed") / 10f),
                
                _ => IsUnlocked(type) ? 1f : 0f
            };
        }

        #endregion

        #region 成就触发检查

        /// <summary>检查碎片收集相关成就</summary>
        public void CheckFragmentAchievements(int totalFragments)
        {
            IncrementCounter("fragments_collected", totalFragments - GetCounter("fragments_collected"));

            if (totalFragments >= 10) Unlock(AchievementType.初窥门径);
            if (totalFragments >= 20) Unlock(AchievementType.博闻强记);
            if (totalFragments >= 41) Unlock(AchievementType.学富五车);
        }

        /// <summary>检查 NPC 来访相关成就</summary>
        public void CheckVisitAchievements(string npcId, int visitCount)
        {
            if (visitCount == 1)
            {
                IncrementCounter("first_visits_completed");
                if (GetCounter("first_visits_completed") >= 8)
                    Unlock(AchievementType.初见);
            }
            else if (visitCount == 2)
            {
                IncrementCounter("second_visits_completed");
                if (GetCounter("second_visits_completed") >= 8)
                    Unlock(AchievementType.再续前缘);
            }
            else if (visitCount >= 3)
            {
                IncrementCounter("third_visits_completed");
                if (GetCounter("third_visits_completed") >= 8)
                    Unlock(AchievementType.知己知彼);
            }
        }

        /// <summary>检查极端天气相关成就</summary>
        public void CheckExtremeWeatherAchievement(Data.WeatherType weather)
        {
            bool isExtreme = weather switch
            {
                Data.WeatherType.暴雨 or Data.WeatherType.暴风雪 or 
                Data.WeatherType.大雾 or Data.WeatherType.雷暴 => true,
                _ => false
            };

            if (isExtreme)
            {
                IncrementCounter("extreme_weather_experienced");
                if (GetCounter("extreme_weather_experienced") >= 4)
                    Unlock(AchievementType.风雨同舟);
            }
        }

        /// <summary>检查季节节点相关成就</summary>
        public void CheckSeasonalEventAchievement()
        {
            IncrementCounter("seasonal_events_experienced");
            if (GetCounter("seasonal_events_experienced") >= 4)
                Unlock(AchievementType.四季轮回);
        }

        /// <summary>检查泡茶相关成就</summary>
        public void CheckTeaBrewingAchievement(float qualityScore)
        {
            IncrementCounter("tea_brewed");
            
            if (qualityScore >= 95f)
            {
                IncrementCounter("perfect_tea_brewed");
                if (GetCounter("perfect_tea_brewed") >= 10)
                    Unlock(AchievementType.茶道大师);
            }

            if (GetCounter("tea_brewed") >= 10)
                Unlock(AchievementType.小本经营);
        }

        /// <summary>检查秘密结局成就</summary>
        public void CheckSecretEndingAchievement()
        {
            Unlock(AchievementType.山之心);
        }

        /// <summary>检查特定 NPC 深度线成就</summary>
        public void CheckNPCDepthAchievement(string npcId, int visitCount)
        {
            if (visitCount >= 3)
            {
                // 检查特定 NPC 的深度线成就
                var npcAchievement = npcId switch
                {
                    "bailu" => AchievementType.白露之歌,
                    "zhuqing" => AchievementType.竹青之画,
                    "danggui" => AchievementType.当归之处,
                    "yunhelao" => AchievementType.云鹤之 wait,
                    "xiaoshan" => AchievementType.小山之名,
                    "qinglan" => AchievementType.青岚之笔,
                    "hanlu" => AchievementType.寒露之霜,
                    "qiaoweng" => AchievementType.樵翁之庙,
                    _ => (AchievementType?)null
                };

                if (npcAchievement.HasValue)
                    Unlock(npcAchievement.Value);
            }
        }

        #endregion

        #region 内部方法

        private void CheckCounterAchievements(string counterId, int value)
        {
            // 根据计数器检查成就
            switch (counterId)
            {
                case "fragments_collected":
                    if (value >= 10) Unlock(AchievementType.初窥门径);
                    if (value >= 20) Unlock(AchievementType.博闻强记);
                    if (value >= 41) Unlock(AchievementType.学富五车);
                    break;
                case "first_visits_completed":
                    if (value >= 8) Unlock(AchievementType.初见);
                    break;
                case "second_visits_completed":
                    if (value >= 8) Unlock(AchievementType.再续前缘);
                    break;
                case "third_visits_completed":
                    if (value >= 8) Unlock(AchievementType.知己知彼);
                    break;
                case "extreme_weather_experienced":
                    if (value >= 4) Unlock(AchievementType.风雨同舟);
                    break;
                case "seasonal_events_experienced":
                    if (value >= 4) Unlock(AchievementType.四季轮回);
                    break;
                case "tea_brewed":
                    if (value >= 10) Unlock(AchievementType.小本经营);
                    break;
                case "perfect_tea_brewed":
                    if (value >= 10) Unlock(AchievementType.茶道大师);
                    break;
            }
        }

        #endregion

        #region 存档

        public void SaveAchievements()
        {
            if (GameManager.Instance == null) return;
            
            var save = GameManager.Instance.CurrentSaveData ?? new SaveData();
            save.unlockedAchievements = new List<string>();
            foreach (var ach in unlockedAchievements)
            {
                save.unlockedAchievements.Add($"{ach.type}:{ach.unlockedTime}");
            }
            
            // 保存计数器
            save.achievementCounters = new Dictionary<string, int>(_counters);
        }

        public void LoadAchievements()
        {
            if (GameManager.Instance == null) return;
            
            var save = GameManager.Instance.CurrentSaveData;
            if (save == null) return;

            _unlockedSet.Clear();
            unlockedAchievements.Clear();

            if (save.unlockedAchievements != null)
            {
                foreach (var str in save.unlockedAchievements)
                {
                    var parts = str.Split(':');
                    if (parts.Length >= 1 && Enum.TryParse<AchievementType>(parts[0], out var type))
                    {
                        _unlockedSet.Add(type);
                        unlockedAchievements.Add(new AchievementData
                        {
                            type = type,
                            unlockedTime = parts.Length > 1 ? parts[1] : ""
                        });
                    }
                }
            }

            if (save.achievementCounters != null)
            {
                _counters = new Dictionary<string, int>(save.achievementCounters);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Achievement] 加载成就: {_unlockedSet.Count} 个已解锁");
#endif
        }

        #endregion

        #region 成就信息

        /// <summary>获取成就名称</summary>
        public static string GetAchievementName(AchievementType type) => type switch
        {
            // 碎片收集
            AchievementType.初窥门径 => "初窥门径",
            AchievementType.博闻强记 => "博闻强记",
            AchievementType.学富五车 => "学富五车",
            
            // NPC 深度线
            AchievementType.初见 => "初见",
            AchievementType.再续前缘 => "再续前缘",
            AchievementType.知己知彼 => "知己知彼",
            
            // 天气
            AchievementType.风雨同舟 => "风雨同舟",
            
            // 季节
            AchievementType.四季轮回 => "四季轮回",
            
            // 经营
            AchievementType.小本经营 => "小本经营",
            AchievementType.茶道大师 => "茶道大师",
            
            // 秘密结局
            AchievementType.山之心 => "山之心",
            
            // NPC 专属
            AchievementType.白露之歌 => "白露之歌",
            AchievementType.竹青之画 => "竹青之画",
            AchievementType.当归之处 => "当归之处",
            AchievementType.云鹤之 wait => "云鹤之 wait",
            AchievementType.小山之名 => "小山之名",
            AchievementType.青岚之笔 => "青岚之笔",
            AchievementType.寒露之霜 => "寒露之霜",
            AchievementType.樵翁之庙 => "樵翁之庙",
            
            _ => "未知成就"
        };

        /// <summary>获取成就描述</summary>
        public static string GetAchievementDescription(AchievementType type) => type switch
        {
            // 碎片收集
            AchievementType.初窥门径 => "收集 10 个碎片",
            AchievementType.博闻强记 => "收集 20 个碎片",
            AchievementType.学富五车 => "收集全部 41 个碎片",
            
            // NPC 深度线
            AchievementType.初见 => "完成 8 位 NPC 的首次来访",
            AchievementType.再续前缘 => "完成 8 位 NPC 的二次来访",
            AchievementType.知己知彼 => "完成 8 位 NPC 的三次来访",
            
            // 天气
            AchievementType.风雨同舟 => "经历全部 4 种极端天气事件",
            
            // 季节
            AchievementType.四季轮回 => "经历全部 4 个季节节点事件",
            
            // 经营
            AchievementType.小本经营 => "泡茶 10 次",
            AchievementType.茶道大师 => "完美泡茶（品质≥95）10 次",
            
            // 秘密结局
            AchievementType.山之心 => "触发秘密结局「山之心」",
            
            // NPC 专属
            AchievementType.白露之歌 => "完成白露的三次来访深度线",
            AchievementType.竹青之画 => "完成竹青的三次来访深度线",
            AchievementType.当归之处 => "完成当归的三次来访深度线",
            AchievementType.云鹤之 wait => "完成云鹤老的三次来访深度线",
            AchievementType.小山之名 => "完成小山的三次来访深度线",
            AchievementType.青岚之笔 => "完成青岚的三次来访深度线",
            AchievementType.寒露之霜 => "完成寒露的三次来访深度线",
            AchievementType.樵翁之庙 => "完成樵翁的三次来访深度线",
            
            _ => ""
        };

        #endregion
    }

    /// <summary>成就类型枚举</summary>
    public enum AchievementType
    {
        // 碎片收集 (100-199)
        初窥门径 = 100,
        博闻强记 = 101,
        学富五车 = 102,
        
        // NPC 深度线 (200-299)
        初见 = 200,
        再续前缘 = 201,
        知己知彼 = 202,
        
        // 天气事件 (300-399)
        风雨同舟 = 300,
        
        // 季节节点 (400-499)
        四季轮回 = 400,
        
        // 经营成就 (500-599)
        小本经营 = 500,
        茶道大师 = 501,
        
        // 秘密结局 (600-699)
        山之心 = 600,
        
        // NPC 专属成就 (700-799)
        白露之歌 = 700,
        竹青之画 = 701,
        当归之处 = 702,
        云鹤之 wait = 703,
        小山之名 = 704,
        青岚之笔 = 705,
        寒露之霜 = 706,
        樵翁之庙 = 707,
    }

    /// <summary>成就数据</summary>
    [Serializable]
    public class AchievementData
    {
        public AchievementType type;
        public string unlockedTime;
    }
}
