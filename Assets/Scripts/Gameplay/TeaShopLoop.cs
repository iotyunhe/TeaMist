using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TeaMist.Core;
using TeaMist.NPC;
using TeaMist.Data;
using TeaMist.Story;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 茶馆核心循环控制器 —— 串联所有子系统，驱动一个完整的"客人进店→泡茶→对话→碎片→离店"循环。
    /// 
    /// 这是 Phase 1 MVP 的入口脚本，挂载在主场景的 GameLoop GameObject 上。
    /// </summary>
    public class TeaShopLoop : MonoBehaviour
    {
        public static TeaShopLoop Instance { get; private set; }

        [Header("━━━ 状态（只读）━━━")]
        [SerializeField] private ShopState currentState = ShopState.Idle;
        [SerializeField] private string currentNPCId;
        [SerializeField] private int currentSeatIndex = -1;
        [SerializeField] private int dailyCustomerCount;
        [Tooltip("基础客容量，实际由 TeaHouseManager 动态覆写")]
        [SerializeField] private int dailyMaxCustomersBase = 3;

        /// <summary>当天可接待的最大客人数（由 TeaHouseManager 动态决定）</summary>
        private int DailyMaxGuests => TeaHouseManager.Instance?.GetMaxGuests() ?? dailyMaxCustomersBase;

        /// <summary>今日 NPC 来访计划（由 NPCScheduleManager 生成）</summary>
        private List<NPCVisitPlan> _todayVisitPlans = new List<NPCVisitPlan>();

        /// <summary>当前来访 NPC 的心情（由日程计划传入，供 Yarn 条件使用）</summary>
        private MoodTag _currentMood = MoodTag.喜悦;

        [Header("━━━ 事件 ━━━")]
        public UnityEvent<string> OnCustomerEntered;    // 客人 ID
        public UnityEvent<string> OnCustomerLeft;       // 客人 ID（离场）
        public UnityEvent<string, int> OnFragmentReceived; // fragmentId, score
        public UnityEvent OnDayStarted;
        public UnityEvent OnDayEnded;

        public enum ShopState
        {
            Idle,           // 空闲，等待客人
            CustomerEntering, // 客人进场动画
            WaitingForTea,  // 等待泡茶
            Brewing,        // 泡茶中
            InDialogue,     // 对话中
            CustomerLeaving // 客人离场
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 初始化 UnityEvents（运行时 AddComponent 后可能为 null）
            if (OnCustomerEntered == null) OnCustomerEntered = new UnityEvent<string>();
            if (OnCustomerLeft == null) OnCustomerLeft = new UnityEvent<string>();
            if (OnFragmentReceived == null) OnFragmentReceived = new UnityEvent<string, int>();
            if (OnDayStarted == null) OnDayStarted = new UnityEvent();
            if (OnDayEnded == null) OnDayEnded = new UnityEvent();
        }

        void OnDestroy()
        {
            if (Core.TimeManager.Instance != null)
                Core.TimeManager.Instance.OnGameHourChanged -= OnNewHour;
        }

        void Start()
        {
            // 等待 Bootstrap 完成后开始
            StartCoroutine(InitializeAndStart());
        }

        private IEnumerator InitializeAndStart()
        {
            // 等一帧确保所有管理器就绪
            yield return null;

            // 注册事件
            if (Dialogue.DialogueManager.Instance != null)
            {
                Dialogue.DialogueManager.Instance.OnDialogueEnded.AddListener(OnDialogueFinished);
                Dialogue.DialogueManager.Instance.OnFragmentDropped.AddListener(OnFragmentDrop);
            }

            if (TeaBrewingManager.Instance != null)
            {
                TeaBrewingManager.Instance.OnBrewingComplete.AddListener(OnTeaBrewed);
            }

            // 连接时间管理器：每小时检查是否有客人来访
            if (Core.TimeManager.Instance != null)
            {
                Core.TimeManager.Instance.OnGameHourChanged += OnNewHour;
            }

            // 开始第一天
            StartDay();
        }

        // ━━━ 日循环 ━━━

        private void StartDay()
        {
            dailyCustomerCount = 0;
            OnDayStarted?.Invoke();

            if (TeaHouseSceneController.Instance != null)
                TeaHouseSceneController.Instance.OpenShop();

            // 清晨开门音效
            Core.AudioManager.Instance?.PlayDayStart();

            // 茶馆经营：新的一天
            TeaHouseManager.Instance?.OnNewDay();

            // 生成今日 NPC 来访计划（日程驱动）
            var season = SeasonManager.Instance != null ? SeasonManager.Instance.CurrentSeason : Season.Spring;
            var weather = ConvertWeather(WeatherManager.Instance?.CurrentWeather ?? WeatherType.晴);
            _todayVisitPlans = NPCScheduleManager.Instance?.GenerateDailyVisits(season, weather)
                ?? new List<NPCVisitPlan>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] 新的一天开始了。茶馆开门。今日计划: {_todayVisitPlans.Count} 位客人");
            foreach (var p in _todayVisitPlans)
                Debug.Log($"  {p.npcId} @ {p.arrivalHour}:00 (心情:{p.mood})");
#endif

            // 刷新今日八卦（GossipPool 每日更新活跃消息）
            float gameDay = (float)(Core.TimeManager.Instance?.TotalDaysPlayed ?? 1);
            var npcAffections = Dialogue.DialogueManager.Instance?.variableStorage?.affection
                ?? new Dictionary<string, int>();
            GossipPool.Instance?.RefreshDaily(season, weather, gameDay, npcAffections);
        }

        /// <summary>
        /// 由 TimeManager 的每日事件触发，或手动调用。
        /// </summary>
        public void OnNewHour(int hour)
        {
            if (hour < 8 || hour > 20) return; // 营业时间 8:00-20:00
            if (dailyCustomerCount >= DailyMaxGuests) return;
            if (currentState != ShopState.Idle) return;

            // ── 日程驱动：检查今日计划中是否有此刻应到访的 NPC ──
            for (int i = _todayVisitPlans.Count - 1; i >= 0; i--)
            {
                var plan = _todayVisitPlans[i];
                if (plan.arrivalHour != hour) continue;

                _currentMood = plan.mood;
                StartCustomerVisit(plan.npcId);
                _todayVisitPlans.RemoveAt(i);
                return;
            }

            // ── 后备随机逻辑：日程为空时仍能触发 NPC 来访 ──
            float visitChance = 0.15f + (dailyCustomerCount == 0 ? 0.35f : 0f);
            if (Random.value > visitChance) return;

            string npcId = SelectTodayNPC();
            if (string.IsNullOrEmpty(npcId)) return;

            StartCustomerVisit(npcId);
        }

        private string SelectTodayNPC()
        {
            // 首次来访 → 白露必来
            var storage = Dialogue.DialogueManager.Instance?.variableStorage;
            bool hasMetBailu = storage != null && storage.affection.ContainsKey("bailu");

            if (!hasMetBailu && dailyCustomerCount == 0)
            {
                return "bailu";
            }

            // 六人均匀随机（后备逻辑）
            string[] pool = { "bailu", "zhuqing", "danggui", "yunhelao", "xiaoshan", "qinglan", "hanlu", "qiaoweng" };
            return pool[Random.Range(0, pool.Length)];
        }

        // ━━━ 客人循环 ━━━

        private void StartCustomerVisit(string npcId)
        {
            currentNPCId = npcId;
            currentState = ShopState.CustomerEntering;

            // 找空座
            var scene = TeaHouseSceneController.Instance;
            currentSeatIndex = scene != null ? scene.GetAvailableSeat() : 0;

            if (currentSeatIndex >= 0 && scene != null)
                scene.OccupySeat(currentSeatIndex);

            // 入场提示
            string npcName = Core.DataManager.GetNpcDisplayName(npcId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] {npcName} 推门进来。");
#endif
            OnCustomerEntered?.Invoke(npcId);

            // 记录来访（叙事状态管理器）
            Core.NarrativeStateManager.Instance?.RecordVisit(npcId);

            // 门铃音效
            Core.AudioManager.Instance?.PlayDoorBell();

            // 茶馆声望：客人到访
            TeaHouseManager.Instance?.OnCustomerEntered();

            // 日程管理器：注册到访
            NPCScheduleManager.Instance?.RegisterArrival(npcId);

            // 启动对话
            StartCoroutine(BeginDialogueAfterDelay(1f));
        }

        private IEnumerator BeginDialogueAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            currentState = ShopState.InDialogue;

            // 将当前心情写入对话变量存储（供 Yarn if:mood=xxx 条件使用）
            var storage = Dialogue.DialogueManager.Instance?.variableStorage;
            if (storage != null)
                storage.SetVariable("mood", _currentMood.ToString());

            // 尝试获取日常故事作为对话前环境旁白（DailyStoryPool）
            var dm = Dialogue.DialogueManager.Instance;
            if (dm != null && DailyStoryPool.Instance != null)
            {
                var curSeason = SeasonManager.Instance != null ? SeasonManager.Instance.CurrentSeason : Season.Spring;
                var curWeather = ConvertWeather(WeatherManager.Instance?.CurrentWeather ?? WeatherType.晴);
                float gameDay = (float)(Core.TimeManager.Instance?.TotalDaysPlayed ?? 1);
                var presentNPCs = new List<string> { currentNPCId };
                var fragments = new HashSet<string>(
                    Core.NarrativeStateManager.Instance?.GetCollectedFragments() ?? new List<string>());

                var story = DailyStoryPool.Instance.PickRandomStory(
                    curSeason, curWeather, gameDay, presentNPCs, fragments);

                if (story != null)
                {
                    dm.AddPreDialogueNarration(story.narrativeText);
                    DailyStoryPool.Instance.RegisterStoryTriggered(story, gameDay);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[TeaShopLoop] 日常故事触发: {story.title} (类型:{story.type})");
#endif
                }
            }

            // 将 NPC ID 映射到 Yarn 剧本文件名
            string scriptName = GetNPCScriptName(currentNPCId);
            dm?.StartDialogue(scriptName);
        }

        /// <summary>NPC ID → Yarn 剧本文件名映射（根据来访次数选择不同剧本）</summary>
        private static string GetNPCScriptName(string npcId)
        {
            int visitCount = Core.NarrativeStateManager.Instance?.GetVisitCount(npcId) ?? 1;

            // 第三次及以后 → 日常闲聊剧本（季节/天气/好感度分支）
            if (visitCount >= 3)
            {
                string dailyName = npcId switch
                {
                    "bailu"    => "bai_lu_daily_chat",
                    "yunhelao" => "yunhelao_daily_chat",
                    _          => $"{npcId}_daily_chat"
                };

                if (Resources.Load<TextAsset>($"Yarn/Characters/{dailyName}") != null)
                    return dailyName;
            }

            // 第二次来访 → 尝试加载 second_visit 剧本
            if (visitCount >= 2)
            {
                string mapping = npcId switch
                {
                    "bailu" => "bai_lu_second_visit",
                    "zhuqing" => "zhuqing_second_visit",
                    "danggui" => "danggui_second_visit",
                    _ => $"{npcId}_second_visit"
                };

                if (Resources.Load<TextAsset>($"Yarn/Characters/{mapping}") != null)
                    return mapping;
            }

            // 首次来访 → first_visit 剧本
            return npcId switch
            {
                "bailu" => "bai_lu_first_visit",
                "zhuqing" => "zhuqing_secret_tea",
                "danggui" => "danggui_wounded_plant",
                "qinglan" => "qinglan_first_visit",
                "hanlu" => "hanlu_first_visit",
                "qiaoweng" => "qiaoweng_first_visit",
                _ => $"{npcId}_first_visit"
            };
        }

        // ━━━ 泡茶回调 ━━━

        public void OnTeaBrewingRequested(string request, string targetId)
        {
            currentState = ShopState.WaitingForTea;
            TeaBrewingManager.Instance?.StartBrewing(request, targetId);
        }

        private void OnTeaBrewed(int qualityScore)
        {
            currentState = ShopState.InDialogue;
            // 泡茶完成音效
            Core.AudioManager.Instance?.PlayTeaPour();
            // 茶馆声望：泡茶经验
            TeaHouseManager.Instance?.OnTeaBrewed(qualityScore);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] 泡茶完成，品质: {qualityScore}/100");
#endif
        }

        // ━━━ 对话结束 ━━━

        private void OnDialogueFinished(string scriptName)
        {
            // 对话结束后，有概率展示一条八卦闲聊（GossipPool）
            TryShowPostDialogueGossip();

            EndCustomerVisit();
        }

        /// <summary>对话结束后有概率展示八卦闲聊（30%概率，从今日活跃八卦中随机选一条）</summary>
        private void TryShowPostDialogueGossip()
        {
            if (GossipPool.Instance == null) return;
            var gossips = GossipPool.Instance.GetActiveGossips();
            if (gossips == null || gossips.Count == 0) return;
            if (Random.value > 0.3f) return; // 30% 概率展示

            var gossip = gossips[Random.Range(0, gossips.Count)];
            string note = GossipPool.Instance.GetGossipAsNote(gossip);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] 八卦闲聊: {gossip.senderName} → {gossip.subjectName}\n{gossip.content}");
#endif
            // TODO: 未来在 UI 上以"竹青小笺"形式展示，当前仅日志记录
        }

        private void OnFragmentDrop(string fragmentId, string npcId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] 获得碎片: {fragmentId} (来自 {npcId})");
#endif
            Core.NarrativeStateManager.Instance?.CollectFragment(fragmentId);
            // 同步到 DataManager，确保茶谱图鉴能显示
            Core.DataManager.Instance?.CollectFragment(fragmentId);
            OnFragmentReceived?.Invoke(fragmentId, Core.NarrativeStateManager.Instance?.GetAffection(npcId) ?? 0);
            // 碎片获得音效
            Core.AudioManager.Instance?.PlayFragmentGet();
            // 茶馆声望经验
            TeaHouseManager.Instance?.OnFragmentCollected(fragmentId);
        }

        private void EndCustomerVisit()
        {
            currentState = ShopState.CustomerLeaving;

            // 停止活跃对话
            Dialogue.DialogueManager.Instance?.StopDialogue();

            // 隐藏对话 UI
            Dialogue.DialogueManager.Instance?.dialogueUI?.HideImmediate();

            // 释放座位
            var scene = TeaHouseSceneController.Instance;
            if (currentSeatIndex >= 0 && scene != null)
                scene.FreeSeat(currentSeatIndex);

            string npcName = Core.DataManager.GetNpcDisplayName(currentNPCId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TeaShopLoop] {npcName} 离开了茶馆。");
#endif
            OnCustomerLeft?.Invoke(currentNPCId);

            // 日程管理器：注册离场
            NPCScheduleManager.Instance?.RegisterDeparture(currentNPCId);

            // 关门音效
            Core.AudioManager.Instance?.PlayDoorClose();

            currentNPCId = "";
            currentSeatIndex = -1;
            dailyCustomerCount++;
            currentState = ShopState.Idle;

            // 如果今日客满，结束营业
            if (dailyCustomerCount >= DailyMaxGuests)
            {
                EndDay();
            }
        }

        private void EndDay()
        {
            if (TeaHouseSceneController.Instance != null)
                TeaHouseSceneController.Instance.CloseShop();

            OnDayEnded?.Invoke();
            // 闭店音效
            Core.AudioManager.Instance?.PlayDayEnd();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[TeaShopLoop] 今日营业结束。");
#endif

            // 存盘
            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.SaveGame();
        }

        // ━━━ 公共 API ━━━

        public ShopState GetCurrentState() => currentState;
        public string GetCurrentNPC() => currentNPCId;

        /// <summary>WeatherType → Weather 枚举转换（Core 层 → Gameplay 层）</summary>
        private static Weather ConvertWeather(WeatherType wt) => wt switch
        {
            WeatherType.晴 => Weather.Clear,
            WeatherType.多云 => Weather.Cloudy,
            WeatherType.雨 => Weather.Rain,
            WeatherType.雪 => Weather.Snow,
            WeatherType.雾 => Weather.Mist,
            WeatherType.雷 => Weather.Storm,
            WeatherType.风 => Weather.Cloudy, // 风没有精确映射，归为多云
            _ => Weather.Any
        };

        /// <summary>强制结束当前访问（调试用）</summary>
        public void EndCustomerVisitPublic()
        {
            if (currentState != ShopState.Idle)
                EndCustomerVisit();
        }

        /// <summary>
        /// 手动触发客人来访（用于测试或事件驱动）
        /// </summary>
        public void ForceCustomerVisit(string npcId)
        {
            if (currentState != ShopState.Idle)
            {
                Debug.LogWarning("[TeaShopLoop] 当前有客人，无法强制来访");
                return;
            }
            StartCustomerVisit(npcId);
        }
    }
}
