using System;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Gameplay;
using MoodTag = TeaMist.Data.MoodTag;
using Random = UnityEngine.Random;

namespace TeaMist.NPC
{
    /// <summary>
    /// NPC 每日日程条目
    /// </summary>
    [Serializable]
    public class NPCScheduleEntry
    {
        [Tooltip("事件名称（调试用）")]
        public string eventName = "闲逛";

        [Tooltip("开始时辰（0=子时, 6=卯时, 12=午时, 18=酉时）")]
        [Range(0, 23)]
        public int startHour = 8;

        [Tooltip("结束时辰")]
        [Range(0, 23)]
        public int endHour = 12;

        [Tooltip("行为类型")]
        public NPCBehaviorType behaviorType = NPCBehaviorType.Idle;

        [Tooltip("行为地点")]
        public NPCLocation location = NPCLocation.Teahouse;

        [Tooltip("该行为是否可被打断（玩家互动优先）")]
        public bool interruptible = true;

        [Tooltip("仅在特定季节触发（空=全季）")]
        public Season seasonFilter = Season.All;

        [Tooltip("仅在特定天气触发（空=全天候）")]
        public Weather weatherFilter = Weather.Any;

        [Tooltip("需要好感度≥此值才触发")]
        [Range(0, 5)]
        public int minAffectionLevel = 0;

        [Tooltip("此行为的描述文本（用于竹青八卦）")]
        public string gossipDescription = "";
    }

    /// <summary>
    /// NPC 行为类型
    /// </summary>
    public enum NPCBehaviorType
    {
        Idle,           // 闲逛/发呆
        VisitTeahouse,  // 来茶馆喝茶
        VisitHerbShop,  // 来药材铺
        VisitInn,       // 来客栈
        VisitGallery,   // 来画坊
        GatherHerb,     // 山中采药
        Patrol,         // 巡视/守护
        Social,         // 与其他NPC社交
        Meditate,       // 修炼/冥想
        Sleep,          // 休息
        Special,        // 特殊剧情事件
    }

    /// <summary>
    /// NPC 所在地点
    /// </summary>
    public enum NPCLocation
    {
        Home,           // 家中
        Teahouse,       // 茶馆
        HerbShop,       // 药材铺
        Inn,            // 客栈
        Gallery,        // 画坊
        MountainPath,   // 山径
        WillowFerry,    // 柳渡
        CloudShoulder,  // 云肩坪
        SunsetTerrace,  // 栖霞台
        OldImmortal,    // 旧仙台
        Hidden,         // 隐藏区域
        Anywhere,       // 任意地点
    }

    /// <summary>
    /// NPC 自主生态状态
    /// </summary>
    [Serializable]
    public class NPCAutonomousState
    {
        public string npcId;
        public NPCLocation currentLocation;
        public NPCBehaviorType currentBehavior;
        public string currentActivity;       // 当前正在做的具体事
        public string lastGossip;            // 最近的八卦
        public float moodValue;              // 0-1 心情值
        public float energyValue;            // 0-1 精力值
        public List<string> recentInteractions = new List<string>(); // 最近与谁互动过
        public int daysSinceLastVisit;       // 距离上次来店的天数
    }

    /// <summary>
    /// 离线自治事件
    /// </summary>
    [Serializable]
    public class OfflineEvent
    {
        public string eventId;               // 唯一ID
        public float gameDayOccurred;        // 发生的游戏天数
        public string description;           // 事件描述
        public string[] involvedNPCs;        // 参与NPC
        public OfflineEventType eventType;
    }

    public enum OfflineEventType
    {
        NPCInteraction,  // NPC之间的互动
        WorldChange,     // 世界状态变化
        Discovery,       // 某NPC发现了什么
        Conflict,        // 冲突事件
        Celebration,     // 庆祝事件
    }

    /// <summary>
    /// NPC 日程管理器
    /// 管理所有核心 NPC 的每日日程、自主行为和离线事件
    /// </summary>
    public class NPCScheduleManager : MonoBehaviour
    {
        public static NPCScheduleManager Instance { get; private set; }

        [Header("NPC 日程配置")]
        [Tooltip("所有核心 NPC 的日程配置")]
        public List<NPCProfileData> npcProfiles = new List<NPCProfileData>();

        [Header("运行时状态")]
        [SerializeField] private List<NPCAutonomousState> npcStates = new List<NPCAutonomousState>();
        [SerializeField] private List<OfflineEvent> offlineEvents = new List<OfflineEvent>();

        // 当前在场的 NPC
        private List<string> presentNPCs = new List<string>();

        // ============ 初始化 ============
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 延迟初始化：等 DataManager 就绪后再填充默认日程
            if (npcProfiles.Count == 0)
                Invoke(nameof(PopulateFromDataManager), 0.05f);
        }

        /// <summary>
        /// 从 DataManager 的 NPCProfileSO 自动创建 NPCScheduleManager 的 NPCProfileData。
        /// 为每个 NPC 生成基于其身份的默认日程表。
        /// </summary>
        public void PopulateFromDataManager()
        {
            var dm = Core.DataManager.Instance;
            if (dm == null)
            {
                Debug.LogWarning("[NPCScheduleManager] DataManager 未就绪，无法填充默认日程");
                return;
            }

            npcProfiles.Clear();
            npcStates.Clear();

            foreach (var npcSO in dm.npcProfiles)
            {
                var profile = CreateDefaultProfile(npcSO.npcName);
                if (profile != null)
                    npcProfiles.Add(profile);
            }

            InitializeNPCStates();
            Debug.Log($"[NPCScheduleManager] 已从 DataManager 填充 {npcProfiles.Count} 个 NPC 的默认日程");
        }

        private NPCProfileData CreateDefaultProfile(string npcName)
        {
            var profile = new NPCProfileData();
            profile.displayName = npcName;

            switch (npcName)
            {
                case "白露":
                    profile.npcId = "bailu";
                    profile.species = NPCSpecies.FoxSpirit;
                    profile.baseVisitChance = 0.75f;
                    profile.minVisitInterval = 0;
                    profile.defaultMood = TeaMist.Data.MoodTag.喜悦;
                    profile.favoriteSeason = Season.Autumn;
                    profile.rainsMood = TeaMist.Data.MoodTag.忧愁;
                    profile.isGossipSource = true;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "来茶馆", startHour = 9, endHour = 11, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                        new NPCScheduleEntry { eventName = "山径闲逛", startHour = 14, endHour = 17, behaviorType = NPCBehaviorType.Idle, location = NPCLocation.MountainPath },
                    };
                    break;

                case "竹青":
                    profile.npcId = "zhuqing";
                    profile.species = NPCSpecies.PlantSpirit;
                    profile.baseVisitChance = 0.55f;
                    profile.minVisitInterval = 1;
                    profile.defaultMood = TeaMist.Data.MoodTag.喜悦;
                    profile.favoriteSeason = Season.Spring;
                    profile.isGossipSource = true;
                    profile.isGossipTarget = true;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "巡视山径", startHour = 6, endHour = 9, behaviorType = NPCBehaviorType.Patrol, location = NPCLocation.MountainPath },
                        new NPCScheduleEntry { eventName = "来茶馆喝茶", startHour = 10, endHour = 12, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse, seasonFilter = Season.Spring },
                        new NPCScheduleEntry { eventName = "编撰山志", startHour = 15, endHour = 18, behaviorType = NPCBehaviorType.Meditate, location = NPCLocation.OldImmortal },
                    };
                    break;

                case "当归":
                    profile.npcId = "danggui";
                    profile.species = NPCSpecies.PlantSpirit;
                    profile.baseVisitChance = 0.5f;
                    profile.minVisitInterval = 1;
                    profile.defaultMood = TeaMist.Data.MoodTag.思念;
                    profile.rainsMood = TeaMist.Data.MoodTag.忧愁;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "采药", startHour = 7, endHour = 10, behaviorType = NPCBehaviorType.GatherHerb, location = NPCLocation.MountainPath },
                        new NPCScheduleEntry { eventName = "药材铺坐诊", startHour = 11, endHour = 16, behaviorType = NPCBehaviorType.VisitHerbShop, location = NPCLocation.HerbShop },
                        new NPCScheduleEntry { eventName = "来茶馆休息", startHour = 17, endHour = 19, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;

                case "云鹤老":
                    profile.npcId = "yunhelao";
                    profile.species = NPCSpecies.CraneImmortal;
                    profile.baseVisitChance = 0.3f;
                    profile.minVisitInterval = 2;
                    profile.defaultMood = TeaMist.Data.MoodTag.迷茫;
                    profile.favoriteSeason = Season.Winter;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "夜游", startHour = 20, endHour = 23, behaviorType = NPCBehaviorType.Patrol, location = NPCLocation.OldImmortal },
                        new NPCScheduleEntry { eventName = "偶尔来茶馆", startHour = 16, endHour = 20, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;

                case "小山":
                    profile.npcId = "xiaoshan";
                    profile.species = NPCSpecies.StoneSpirit;
                    profile.baseVisitChance = 0.25f;
                    profile.minVisitInterval = 3;
                    profile.defaultMood = TeaMist.Data.MoodTag.迷茫;
                    profile.isGossipTarget = true;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "晒太阳", startHour = 6, endHour = 18, behaviorType = NPCBehaviorType.Idle, location = NPCLocation.MountainPath },
                        new NPCScheduleEntry { eventName = "来茶馆（罕见）", startHour = 12, endHour = 15, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;

                case "青岚":
                    profile.npcId = "qinglan";
                    profile.species = NPCSpecies.Human;
                    profile.baseVisitChance = 0.6f;
                    profile.minVisitInterval = 0;
                    profile.defaultMood = TeaMist.Data.MoodTag.喜悦;
                    profile.favoriteSeason = Season.Autumn;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "户外写生", startHour = 8, endHour = 11, behaviorType = NPCBehaviorType.Idle, location = NPCLocation.SunsetTerrace },
                        new NPCScheduleEntry { eventName = "来茶馆作画", startHour = 13, endHour = 17, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse, seasonFilter = Season.Autumn },
                        new NPCScheduleEntry { eventName = "来茶馆作画", startHour = 14, endHour = 16, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;

                case "寒露":
                    profile.npcId = "hanlu";
                    profile.species = NPCSpecies.SeasonSpirit;
                    profile.baseVisitChance = 0.45f;
                    profile.minVisitInterval = 2;
                    profile.defaultMood = TeaMist.Data.MoodTag.忧愁;
                    profile.favoriteSeason = Season.Autumn;
                    profile.favoriteSeasonMood = TeaMist.Data.MoodTag.忧愁;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "雾中漫步", startHour = 6, endHour = 9, behaviorType = NPCBehaviorType.Patrol, location = NPCLocation.CloudShoulder },
                        new NPCScheduleEntry { eventName = "茶馆避世", startHour = 16, endHour = 19, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse, seasonFilter = Season.Autumn },
                        new NPCScheduleEntry { eventName = "霜降巡游", startHour = 20, endHour = 23, behaviorType = NPCBehaviorType.Special, location = NPCLocation.MountainPath, seasonFilter = Season.Autumn },
                    };
                    break;

                case "樵翁":
                    profile.npcId = "qiaoweng";
                    profile.species = NPCSpecies.Human;
                    profile.baseVisitChance = 0.5f;
                    profile.minVisitInterval = 1;
                    profile.defaultMood = TeaMist.Data.MoodTag.思念;
                    profile.rainsMood = TeaMist.Data.MoodTag.忧愁;
                    profile.isGossipSource = true;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "上山砍柴", startHour = 5, endHour = 9, behaviorType = NPCBehaviorType.GatherHerb, location = NPCLocation.MountainPath },
                        new NPCScheduleEntry { eventName = "守庙", startHour = 10, endHour = 14, behaviorType = NPCBehaviorType.Meditate, location = NPCLocation.OldImmortal },
                        new NPCScheduleEntry { eventName = "茶馆歇脚", startHour = 15, endHour = 18, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;

                default:
                    profile.npcId = npcName.ToLowerInvariant().Replace(" ", "_");
                    profile.species = NPCSpecies.Human;
                    profile.baseVisitChance = 0.5f;
                    profile.defaultMood = TeaMist.Data.MoodTag.喜悦;
                    profile.schedules = new List<NPCScheduleEntry>
                    {
                        new NPCScheduleEntry { eventName = "来茶馆", startHour = 10, endHour = 14, behaviorType = NPCBehaviorType.VisitTeahouse, location = NPCLocation.Teahouse },
                    };
                    break;
            }

            return profile;
        }

        private void InitializeNPCStates()
        {
            npcStates.Clear();
            foreach (var profile in npcProfiles)
            {
                npcStates.Add(new NPCAutonomousState
                {
                    npcId = profile.npcId,
                    currentLocation = NPCLocation.Home,
                    currentBehavior = NPCBehaviorType.Sleep,
                    currentActivity = "在住处休息",
                    moodValue = 0.7f,
                    energyValue = 1f,
                    daysSinceLastVisit = Random.Range(0, 3),
                });
            }
        }

        // ============ 每日更新 ============
        /// <summary>
        /// 游戏日开始时调用，决定今天哪些 NPC 会来
        /// </summary>
        public List<NPCVisitPlan> GenerateDailyVisits(Season season, Weather weather)
        {
            List<NPCVisitPlan> plans = new List<NPCVisitPlan>();
            presentNPCs.Clear();

            foreach (var state in npcStates)
            {
                var profile = GetProfile(state.npcId);
                if (profile == null) continue;

                // 检查今日是否应出场
                var schedule = GetActiveSchedule(profile, season, weather, state);
                if (schedule == null) continue;

                // 决定到访时间（考虑好感度偏移）
                float affectionBonus = state.daysSinceLastVisit * 0.15f;
                float visitChance = profile.baseVisitChance + affectionBonus;
                visitChance = Mathf.Clamp01(visitChance);

                if (Random.value > visitChance) continue;

                // 创建到访计划
                var plan = new NPCVisitPlan
                {
                    npcId = state.npcId,
                    arrivalHour = schedule.startHour + Random.Range(0, 2), // 前后浮动1小时
                    departureHour = schedule.endHour,
                    behavior = schedule.behaviorType,
                    location = schedule.location,
                    mood = GetCurrentMoodTag(state, season, weather),
                    hasQuest = Random.value < profile.questChance,
                    gossipTags = GetGossipTags(state, profile),
                };

                plans.Add(plan);
                state.currentBehavior = schedule.behaviorType;
                state.daysSinceLastVisit = 0;
            }

            // 按到达时间排序
            plans.Sort((a, b) => a.arrivalHour.CompareTo(b.arrivalHour));
            return plans;
        }

        /// <summary>
        /// 获取 NPC 当前有效日程
        /// </summary>
        private NPCScheduleEntry GetActiveSchedule(
            NPCProfileData profile, Season season, Weather weather, NPCAutonomousState state)
        {
            if (profile.schedules == null) return null;
            if (state.daysSinceLastVisit < profile.minVisitInterval) return null;

            foreach (var sched in profile.schedules)
            {
                // 季节性/天气/情绪过滤（不再按 currentHour 过滤——生成全天计划）
                if (sched.seasonFilter != Season.All && sched.seasonFilter != season) continue;
                if (sched.weatherFilter != Weather.Any && sched.weatherFilter != weather) continue;
                if (state.moodValue < 0.3f && sched.interruptible == false) continue;
                return sched;
            }
            return null;
        }

        // ============ NPC 进出场 ============
        public void RegisterArrival(string npcId)
        {
            if (!presentNPCs.Contains(npcId))
                presentNPCs.Add(npcId);
        }

        public void RegisterDeparture(string npcId)
        {
            presentNPCs.Remove(npcId);
            var state = npcStates.Find(s => s.npcId == npcId);
            if (state != null)
            {
                state.currentLocation = NPCLocation.Home;
                state.currentBehavior = NPCBehaviorType.Idle;
                state.daysSinceLastVisit = 0;
            }
        }

        public List<string> GetPresentNPCs() => presentNPCs;

        // ============ 离线模拟 ============
        /// <summary>
        /// 当玩家离线多日后回来，模拟 NPC 之间发生的事
        /// </summary>
        public List<OfflineEvent> SimulateOfflineDays(float daysElapsed)
        {
            List<OfflineEvent> events = new List<OfflineEvent>();

            for (int d = 0; d < Mathf.CeilToInt(daysElapsed); d++)
            {
                // 每天有一定几率发生 NPC 交互事件
                if (Random.value < 0.4f)
                {
                    var e = GenerateOfflineEvent();
                    if (e != null)
                    {
                        events.Add(e);
                        offlineEvents.Add(e);
                    }
                }

                // 更新 NPC 状态
                foreach (var state in npcStates)
                {
                    state.daysSinceLastVisit++;
                    // 离线期间心情缓慢变化
                    state.moodValue += Random.Range(-0.1f, 0.1f);
                    state.moodValue = Mathf.Clamp01(state.moodValue);
                    // 精力恢复
                    state.energyValue = Mathf.Min(1f, state.energyValue + 0.3f);
                }
            }

            return events;
        }

        private OfflineEvent GenerateOfflineEvent()
        {
            if (npcProfiles.Count < 2) return null;

            // 随机选两个 NPC
            var npc1 = npcProfiles[Random.Range(0, npcProfiles.Count)];
            var npc2 = npcProfiles[Random.Range(0, npcProfiles.Count)];
            if (npc1.npcId == npc2.npcId) return null;

            // 查关系
            var relation = npc1.GetRelation(npc2.npcId);
            OfflineEventType eventType = OfflineEventType.NPCInteraction;

            string desc;
            if (relation >= 3)
            {
                desc = $"你不在的时候，{npc1.displayName}和{npc2.displayName}一起在山径散步。{npc2.displayName}似乎很开心。";
                eventType = OfflineEventType.Celebration;
            }
            else if (relation <= -1)
            {
                desc = $"{npc1.displayName}和{npc2.displayName}在茶馆门口遇到了，气氛有点微妙。竹青后来说他们在为一条山径的归属争执。";
                eventType = OfflineEventType.Conflict;
            }
            else
            {
                desc = $"{npc1.displayName}在柳渡遇到了{npc2.displayName}，两人简短地交谈了几句。";
                eventType = OfflineEventType.NPCInteraction;
            }

            return new OfflineEvent
            {
                eventId = $"offline_{DateTime.Now.Ticks}_{Random.Range(0, 9999)}",
                gameDayOccurred = -1, // 由模拟器填充
                description = desc,
                involvedNPCs = new[] { npc1.npcId, npc2.npcId },
                eventType = eventType,
            };
        }

        // ============ 辅助 ============
        public NPCProfileData GetProfile(string npcId)
        {
            return npcProfiles.Find(p => p.npcId == npcId);
        }

        public NPCAutonomousState GetState(string npcId)
        {
            return npcStates.Find(s => s.npcId == npcId);
        }

        private MoodTag GetCurrentMoodTag(NPCAutonomousState state, Season season, Weather weather)
        {
            var profile = GetProfile(state.npcId);
            if (profile == null) return MoodTag.迷茫;

            // 基础心情由NPC性格决定
            MoodTag baseMood = profile.defaultMood;
            // 季节影响
            if (profile.favoriteSeason == season)
                return profile.favoriteSeasonMood;
            // 天气影响
            if (weather == Weather.Rain && profile.rainsMood != MoodTag.迷茫)
                return profile.rainsMood;
            // 精力影响
            if (state.energyValue < 0.3f) return MoodTag.疲惫;

            return baseMood;
        }

        private string[] GetGossipTags(NPCAutonomousState state, NPCProfileData profile)
        {
            List<string> tags = new List<string>();
            // 基于 NPC 最近动态生成八卦标签
            if (state.moodValue < 0.3f) tags.Add("心事重重");
            if (state.moodValue > 0.8f) tags.Add("心情很好");
            if (state.daysSinceLastVisit > 5) tags.Add("好久不见");
            if (profile.isGossipSource) tags.Add("消息灵通");
            return tags.ToArray();
        }

        // ============ NPC 间社交 ============
        public void RecordInteraction(string npcIdA, string npcIdB)
        {
            var stateA = GetState(npcIdA);
            var stateB = GetState(npcIdB);
            if (stateA != null) stateA.recentInteractions.Add(npcIdB);
            if (stateB != null) stateB.recentInteractions.Add(npcIdA);
        }

        public void CheckNPCPresenceInteraction()
        {
            // 当两个 NPC 同时在茶馆时，有几率触发社交事件
            if (presentNPCs.Count < 2) return;
            if (Random.value > 0.25f) return; // 25% 几率

            var a = presentNPCs[Random.Range(0, presentNPCs.Count)];
            var b = presentNPCs[Random.Range(0, presentNPCs.Count)];
            if (a == b) return;

            RecordInteraction(a, b);
        }
    }

    // ============ 数据结构 ============
    [Serializable]
    public class NPCProfileData
    {
        [Header("基础信息")]
        public string npcId;
        public string displayName;
        public NPCSpecies species;
        public string title;

        [Header("出场参数")]
        [Range(0f, 1f)]
        public float baseVisitChance = 0.6f;
        [Tooltip("最小来访间隔（天）")]
        public int minVisitInterval = 0;
        [Range(0f, 1f)]
        public float questChance = 0.15f;

        [Header("性格与偏好")]
        public MoodTag defaultMood = MoodTag.喜悦;
        public Season favoriteSeason = Season.Spring;
        public MoodTag favoriteSeasonMood = MoodTag.喜悦;
        public Weather rainResponse = Weather.Any;
        public MoodTag rainsMood = MoodTag.忧愁;

        [Header("社交属性")]
        public bool isGossipSource = false;
        public bool isGossipTarget = false;

        [Header("日程")]
        public List<NPCScheduleEntry> schedules = new List<NPCScheduleEntry>();

        [Header("关系网")]
        public List<NPCRelationEntry> relations = new List<NPCRelationEntry>();

        public int GetRelation(string otherNpcId)
        {
            var entry = relations.Find(r => r.targetNpcId == otherNpcId);
            return entry?.relationValue ?? 0;
        }
    }

    public enum NPCSpecies
    {
        FoxSpirit,     // 狐妖
        CraneImmortal, // 鹤仙
        ObjectSpirit,  // 器物妖
        PlantSpirit,   // 草木妖
        StoneSpirit,   // 石灵
        SeasonSpirit,  // 节气灵
        Human,         // 人类
        Unknown,       // 未知
    }

    [Serializable]
    public class NPCRelationEntry
    {
        public string targetNpcId;
        [Range(-5, 5)]
        public int relationValue;    // -5 敌对 ~ 5 挚友
        public string relationDescription; // 如 "旧识"、"师徒"、"有过误会"
    }

    [Serializable]
    public class NPCVisitPlan
    {
        public string npcId;
        public int arrivalHour;
        public int departureHour;
        public NPCBehaviorType behavior;
        public NPCLocation location;
        public MoodTag mood;
        public bool hasQuest;
        public string[] gossipTags;
    }
}
