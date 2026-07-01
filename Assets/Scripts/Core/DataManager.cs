using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 数据管理器 — ScriptableObject 加载 + 运行时数据 + 存档/读档桥接
    /// 所有数据查询通过此管理器，不直接访问 ScriptableObject
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        [Header("ScriptableObject 数据库")]
        [Tooltip("所有茶谱")]
        public List<TeaRecipeSO> teaRecipes = new List<TeaRecipeSO>();

        [Tooltip("所有 NPC 档案")]
        public List<NPCProfileSO> npcProfiles = new List<NPCProfileSO>();

        [Tooltip("所有碎片")]
        public List<FragmentSO> fragments = new List<FragmentSO>();

        [Tooltip("所有对话配置")]
        public List<DialogueConfigSO> dialogueConfigs = new List<DialogueConfigSO>();

        [Tooltip("店铺配置")]
        public ShopPropertySO teaHouseConfig;
        public ShopPropertySO herbShopConfig;
        public ShopPropertySO innConfig;
        public ShopPropertySO studioConfig;

        [Tooltip("四季配置")]
        public SeasonConfigSO springConfig;
        public SeasonConfigSO summerConfig;
        public SeasonConfigSO autumnConfig;
        public SeasonConfigSO winterConfig;

        // ── 运行时数据 ──
        private Dictionary<string, TeaRecipeSO> _teaDict;
        private Dictionary<string, NPCProfileSO> _npcDict;
        private Dictionary<string, FragmentSO> _fragmentDict;
        private Dictionary<string, DialogueConfigSO> _dialogueDict;
        private Dictionary<ShopType, ShopPropertySO> _shopDict;
        private Dictionary<Season, SeasonConfigSO> _seasonDict;

        // ── 已解锁内容 ──
        public HashSet<string> UnlockedTeaRecipes { get; private set; } = new HashSet<string>();
        public HashSet<string> CollectedFragments { get; private set; } = new HashSet<string>();
        public HashSet<string> CompletedDialogues { get; private set; } = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 如果没有在 Inspector 中配置数据，运行时生成默认数据
            try
            {
                if (npcProfiles.Count == 0)
                    CreateDefaultNPCProfiles();
                if (teaRecipes.Count == 0)
                    CreateDefaultTeaRecipes();
                if (fragments.Count == 0)
                    CreateDefaultFragments();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] 默认数据生成失败: {e.Message}\n{e.StackTrace}");
            }

            BuildDictionaries();
        }

        // ── 初始化 ──

        private void BuildDictionaries()
        {
            _teaDict = teaRecipes.ToDictionary(t => t.teaName, t => t);
            _npcDict = npcProfiles.ToDictionary(n => n.npcName, n => n);
            _fragmentDict = fragments.ToDictionary(f => f.fragmentId, f => f);
            _dialogueDict = dialogueConfigs.ToDictionary(d => d.dialogueId, d => d);

            _shopDict = new Dictionary<ShopType, ShopPropertySO>
            {
                { ShopType.茶馆, teaHouseConfig },
                { ShopType.药材铺, herbShopConfig },
                { ShopType.客栈, innConfig },
                { ShopType.画坊, studioConfig }
            };

            _seasonDict = new Dictionary<Season, SeasonConfigSO>
            {
                { Season.春, springConfig },
                { Season.夏, summerConfig },
                { Season.秋, autumnConfig },
                { Season.冬, winterConfig }
            };
        }

        // ── 查询 API ──

        public TeaRecipeSO GetTeaRecipe(string name) =>
            _teaDict.TryGetValue(name, out var t) ? t : null;

        public TeaRecipeSO GetTeaRecipeById(string id) =>
            teaRecipes.FirstOrDefault(t => t.teaName == id);

        public List<TeaRecipeSO> GetAllTeaRecipes() => teaRecipes;

        public List<TeaRecipeSO> GetUnlockedTeaRecipes() =>
            teaRecipes.Where(t => UnlockedTeaRecipes.Contains(t.teaName)).ToList();

        public NPCProfileSO GetNPCProfile(string name) =>
            _npcDict.TryGetValue(name, out var n) ? n : null;

        public List<NPCProfileSO> GetAllNPCProfiles() => npcProfiles;

        public FragmentSO GetFragment(string id) =>
            _fragmentDict.TryGetValue(id, out var f) ? f : null;

        public List<FragmentSO> GetFragmentsByChapter(int chapter) =>
            fragments.Where(f => f.chapter == chapter).ToList();

        public List<FragmentSO> GetCollectedFragments() =>
            fragments.Where(f => CollectedFragments.Contains(f.fragmentId)).ToList();

        public List<FragmentSO> GetUncollectedFragments() =>
            fragments.Where(f => !CollectedFragments.Contains(f.fragmentId)).ToList();

        public DialogueConfigSO GetDialogueConfig(string id) =>
            _dialogueDict.TryGetValue(id, out var d) ? d : null;

        public List<DialogueConfigSO> GetAvailableDialogues(string npcName, float affection, Season season, WeatherType weather, DayTimeSlot timeSlot)
        {
            return dialogueConfigs.Where(d =>
            {
                // 基础过滤
                if (!string.IsNullOrEmpty(d.relatedNPC) && d.relatedNPC != npcName) return false;
                if (affection < d.minAffection || affection > d.maxAffection) return false;
                if (d.requiredSeasons.Length > 0 && !d.requiredSeasons.Contains(season)) return false;
                if (d.requiredWeather.Length > 0 && !d.requiredWeather.Contains(weather)) return false;
                if (d.requiredTimeSlots.Length > 0 && !d.requiredTimeSlots.Contains(timeSlot)) return false;
                if (d.isOneShot && CompletedDialogues.Contains(d.dialogueId)) return false;
                // TODO: 检查冷却、前置碎片
                return true;
            })
            .OrderByDescending(d => d.priority)
            .ToList();
        }

        public ShopPropertySO GetShopConfig(ShopType type) =>
            _shopDict.TryGetValue(type, out var s) ? s : null;

        public SeasonConfigSO GetSeasonConfig(Season season) =>
            _seasonDict.TryGetValue(season, out var s) ? s : null;

        // ── 内容解锁 ──

        public bool UnlockTeaRecipe(string name)
        {
            if (!_teaDict.ContainsKey(name)) return false;
            if (UnlockedTeaRecipes.Contains(name)) return false;
            UnlockedTeaRecipes.Add(name);
            Debug.Log($"[DataManager] 解锁茶谱: {name}");
            return true;
        }

        public bool CollectFragment(string fragmentId)
        {
            if (!_fragmentDict.ContainsKey(fragmentId)) return false;
            if (CollectedFragments.Contains(fragmentId)) return false;
            CollectedFragments.Add(fragmentId);

            // 连锁解锁
            var frag = _fragmentDict[fragmentId];
            foreach (var unlockId in frag.unlocksFragments)
            {
                if (_fragmentDict.ContainsKey(unlockId) && !CollectedFragments.Contains(unlockId))
                {
                    Debug.Log($"[DataManager] 连锁解锁碎片: {unlockId}");
                    // 不自动收集，只标记为"可探索"
                }
            }

            Debug.Log($"[DataManager] 收集碎片: {fragmentId} — {frag.fragmentTitle}");
            return true;
        }

        public bool MarkDialogueComplete(string dialogueId)
        {
            if (CompletedDialogues.Contains(dialogueId)) return false;
            CompletedDialogues.Add(dialogueId);
            return true;
        }

        // ── 存档桥接 ──

        public void LoadFromSave(SaveData save)
        {
            // 解锁内容
            UnlockedTeaRecipes = new HashSet<string>(save.unlockedTeaRecipes);
            CollectedFragments = new HashSet<string>(save.collectedFragments);
            CompletedDialogues = new HashSet<string>(save.completedDialogues);

            // 店铺状态
            foreach (var shopSave in save.shops)
            {
                var config = GetShopConfig(shopSave.shopType);
                if (config == null) continue;
                config.level = shopSave.level;
                config.reputationLevel = shopSave.reputationLevel;
                config.reputationXP = shopSave.reputationXP;
                config.collectedFragments = shopSave.collectedFragments;
                config.maxGuests = shopSave.maxGuests;
                config.seatCount = shopSave.seatCount;
                config.sereneValue = shopSave.sereneValue;
                config.warmValue = shopSave.warmValue;
                config.elegantValue = shopSave.elegantValue;
                config.playfulValue = shopSave.playfulValue;
                config.herbalValue = shopSave.herbalValue;
                config.unlockedDecorations = shopSave.unlockedDecorations.ToArray();
                config.activeStyle = shopSave.activeStyle;
            }

            // NPC 运行时状态
            foreach (var npcSave in save.npcs)
            {
                var profile = GetNPCProfile(npcSave.npcId);
                if (profile == null) continue;
                profile.runtimeAffection = npcSave.affection;
                profile.currentChapter = npcSave.chapter;
                profile.runtimeVisitCount = npcSave.visitCount;
            }
        }

        public void SaveToSave(SaveData save)
        {
            save.unlockedTeaRecipes = UnlockedTeaRecipes.ToList();
            save.collectedFragments = CollectedFragments.ToList();
            save.completedDialogues = CompletedDialogues.ToList();

            // 店铺状态
            save.shops.Clear();
            foreach (var kvp in _shopDict)
            {
                var cfg = kvp.Value;
                if (cfg == null) continue;
                save.shops.Add(new ShopSaveData
                {
                    shopType = kvp.Key,
                    shopName = cfg.shopName,
                    level = cfg.level,
                    reputationLevel = cfg.reputationLevel,
                    reputationXP = cfg.reputationXP,
                    collectedFragments = cfg.collectedFragments,
                    maxGuests = cfg.maxGuests,
                    seatCount = cfg.seatCount,
                    sereneValue = cfg.sereneValue,
                    warmValue = cfg.warmValue,
                    elegantValue = cfg.elegantValue,
                    playfulValue = cfg.playfulValue,
                    herbalValue = cfg.herbalValue,
                    unlockedDecorations = cfg.unlockedDecorations.ToList(),
                    activeStyle = cfg.activeStyle
                });
            }

            // NPC 状态
            save.npcs.Clear();
            foreach (var profile in npcProfiles)
            {
                save.npcs.Add(new NPCSaveData
                {
                    npcId = profile.npcName,
                    affection = profile.runtimeAffection,
                    chapter = profile.currentChapter,
                    visitCount = profile.runtimeVisitCount
                });
            }
        }

        /// <summary>运行时生成默认 NPC 档案（当 Inspector 未配置 NPCProfileSO 资产时）</summary>
        private void CreateDefaultNPCProfiles()
        {
            var defaults = new (string name, string bio)[]
            {
                ("白露", "栖霞山的小狐妖。活泼好奇，喜欢甜的东西。住在山脚柳渡附近。"),
                ("竹青", "山中的竹妖。安静寡言，正在秘密编写一部栖霞山志。"),
                ("当归", "山腰药材铺的药师。右手有旧伤，性格温和但藏着心事。"),
                ("云鹤老", "山里的老神仙——或者说，一只活得太久的鹤。在找三百年前丢失的东西。"),
                ("小山", "一块会说简单词语的石头。也许比这座山还要老。"),
                ("青岚", "云游画师，为栖霞山的风景而停留。喜欢在茶馆窗边作画——把客人、茶汤和窗外的山都收进纸里。"),
                ("寒露", "节气灵——寒露的化身。只在秋天出现。她开口的时候，山谷里的雾会变浓。每句话都像一句诗，但很少有人真的听懂。"),
                ("樵翁", "山中老樵夫。每天上山砍柴，实际在看守一座被遗忘的古庙。知道很多山里的故事——不是妖怪的故事，是人的。"),
            };

            foreach (var (name, bio) in defaults)
            {
                var npc = ScriptableObject.CreateInstance<NPCProfileSO>();
                npc.npcName = name;
                npc.bio = bio;
                npcProfiles.Add(npc);
            }
            Debug.Log($"[DataManager] 已生成 {npcProfiles.Count} 个默认 NPC 档案");
        }

        /// <summary>运行时生成默认茶谱数据（当 Inspector 未配置 TeaRecipeSO 资产时）</summary>
        private void CreateDefaultTeaRecipes()
        {
            var defaults = new (string id, string name, string description, TeaRarity rarity)[]
            {
                ("guihuamicha", "桂花蜜茶", "桂花与蜂蜜的甘甜交融，是栖霞山最温柔的配方。秋日桂花入蜜封存，冬日取出冲泡——一口下去，是九月阳光的味道。", TeaRarity.常见),
                ("qingxincha", "清心茶", "以山泉冲泡的净叶茶，不增不减，不甜不浓。初饮无味，回甘在喉。竹青说这茶适合在想事情的时候喝——它会帮你把念头摊开，晾一晾。", TeaRarity.稀有),
                ("xueshancha", "雪山银针", "云鹤老带来的北方高山茶。干茶条索如银针般挺直，毫白如雪。用85°C水冲泡最宜，太热则苦。入口清冽如山泉，回甘悠长——老人说，这茶配得上月圆之夜。", TeaRarity.传说),
            };

            foreach (var (id, name, description, rarity) in defaults)
            {
                var recipe = ScriptableObject.CreateInstance<TeaRecipeSO>();
                recipe.teaName = name;
                recipe.recipeId = id;
                recipe.description = description;
                recipe.rarity = rarity;
                recipe.idealTemperature = 85f;
                recipe.idealSteepTime = 20f;
                teaRecipes.Add(recipe);
            }
            Debug.Log($"[DataManager] 已生成 {teaRecipes.Count} 个默认茶谱");
        }

        /// <summary>运行时生成默认碎片数据（当 Inspector 未配置 FragmentSO 资产时）</summary>
        private void CreateDefaultFragments()
        {
            var defaults = new (string id, string title, string content, int chapter, FragmentType type)[]
            {
                ("fragment_bailu_first_tea", "第一杯桂花蜜", "白露小时候记忆中的桂花树。每年秋天，风一吹，整条溪都香了。那是她第一次喝到甜的茶——不是糖的甜，是有人特意为你准备的那种甜。", 1, FragmentType.叙事),
                ("fragment_zhuqing_old_xiantai", "旧仙台之夜", "云鹤老每晚都去旧仙台。竹青说他在找一个三百年前丢失的东西。竹子们什么都能看见，但它们选择沉默——除非有人问对了问题。", 3, FragmentType.叙事),
                ("fragment_zhuqing_self_secret", "栖霞山志", "竹青在秘密编写一部关于栖霞山一切人和事的记录。她说不是正儿八经的史书——是从茶馆听到的、山里看到的、八卦里筛出来的。她也不知道为什么要写。也许因为这座山现在发生的事，再不记就没人记得了。", 3, FragmentType.叙事),
                ("fragment_danggui_herb_leaf", "不会枯的当归叶", "一片即使离开枝干也不会枯萎的叶子。当归说这是药铺的钥匙。也许真正的钥匙不是这片叶子，而是她重新被需要的感觉。", 2, FragmentType.叙事),
                ("fragment_danggui_old_wound", "当归的旧伤", "当归的手在微微颤抖——那是多年前留下的伤。她没有细说，但她的眼睛说了一些她没说出口的话。有些根断了，会自己再长。有些不会。", 2, FragmentType.记忆),
                ("fragment_nine_towers", "九座楼台", "东边三座——迎仙台、凌霄阁、望海楼。南边三座——清音阁、摘星台、流云榭。西边三座——问心台、忘尘阁、归雁楼。现在一座都不剩了。不是倒了，是消失了。被雨冲走的，被风卷走的，被日子带走的。", 3, FragmentType.叙事),
                ("fragment_crane_pearls", "鹤羽珠", "云鹤老留下的三颗莹白珠子。触手微凉，在月夜里会自己发光。传说鹤的羽毛落在地上时，如果它想起了什么人，就会变成珠子。", 3, FragmentType.记忆),
                ("fragment_xiaoshan_portrait", "石画·小山", "石头消失后，地上多了一道极浅的水墨痕迹——像一个小孩画的小山。那些纹路弯弯曲曲地延伸着，像一条盘山的古道。又像一个名字的笔画。", 6, FragmentType.叙事),
                ("fragment_zhuqing_cloud_night", "云鹤的夜行", "竹青注意到云鹤老最近总是在夜间外出。他以为没人知道——但竹叶的眼睛遍布整座山。至于他去哪——竹青没说，但让你留意。", 4, FragmentType.叙事),
                ("fragment_bailu_singing", "溪谷的歌", "白露每天傍晚在溪谷泡着脚唱歌。唱的是山下人的歌。竹青说肯定有人教她，她一定要搞清楚是谁。但白露只说是风里听来的。", 5, FragmentType.记忆),
                ("fragment_danggui_heal_time", "愈合的时间", "当归说她的手好多了——不是完全好了，但已经不再疼得睡不着觉。有些伤需要时间，有些人需要耐心。她把新采的薄荷留在茶馆里，说「不卖钱，是还礼」。", 2, FragmentType.记忆),
                ("fragment_zhuqing_mountain_secret", "山的秘密", "竹青发现了栖霞山的秘密。不是每个人都能知道的——只有那些真正在乎这座山的人，才能看见山的记忆。你选了它，意味着你愿意成为守护者之一。", 3, FragmentType.叙事),
                ("fragment_zhuqing_spirit_secret", "妖的秘密", "竹青揭示了栖霞山的妖怪隐藏的秘密。那些离开的妖怪们去了哪里？竹青知道真相。你选择了追问——这意味着你已经不是局外人。", 3, FragmentType.叙事),
                ("fragment_qinglan_invisible_tower", "青岚的千禧楼", "画师青岚在旧仙台附近画了一座县志上说已经不存在的楼台。不是倒掉后画的——是它完整地出现在他画里，每一根窗棂都在。他说，不是消失了——是藏起来了。藏在只有黄昏的光才能照到的地方。", 4, FragmentType.叙事),
                ("fragment_hanlu_frost_poem", "寒露的霜诗", "节气灵寒露离开时，在你椅面上留下了一行霜。不是冷的——是秋天本身在手写一句话。那行霜化得很快——但你记住了。不是用眼睛记的。是用皮肤上微凉的感觉记的。", 2, FragmentType.记忆),
                ("fragment_qiaoweng_temple_map", "樵翁的庙图", "老樵夫给你一张手绘的古庙图。不是地图——是有人一笔一笔画的一座庙。角落有褪色的字迹：「记得」。不是你写的，不是樵翁的笔迹——是更老的手。比樵翁守着这座庙的时间，还要久。", 4, FragmentType.叙事),
            };

            foreach (var (id, title, content, chapter, type) in defaults)
            {
                var frag = ScriptableObject.CreateInstance<FragmentSO>();
                frag.fragmentId = id;
                frag.fragmentTitle = title;
                frag.content = content;
                frag.chapter = chapter;
                frag.fragmentType = type;
                fragments.Add(frag);
            }
            Debug.Log($"[DataManager] 已生成 {fragments.Count} 个默认碎片");
        }

        // ── NPC ID 映射（权威来源）────────────────────────────────────────

        /// <summary>NPC 短 ID → 中文显示名。项目中所有名称查询必须经过此字典。</summary>
        private static readonly Dictionary<string, string> NpcIdToName = new Dictionary<string, string>
        {
            { "bailu",       "白露" },
            { "zhuqing",     "竹青" },
            { "danggui",     "当归" },
            { "yunhelao",    "云鹤老" },
            { "xiaoshan",    "小山" },
            { "qinglan",     "青岚" },
            { "hanlu",       "寒露" },
            { "qiaoweng",    "樵翁" },
        };

        /// <summary>中文显示名 → NPC 短 ID 反向查询</summary>
        private static readonly Dictionary<string, string> NameToNpcId;

        static DataManager()
        {
            NameToNpcId = new Dictionary<string, string>();
            foreach (var kv in NpcIdToName)
                NameToNpcId[kv.Value] = kv.Key;
        }

        /// <summary>根据 NPC 短 ID 获取中文显示名</summary>
        public static string GetNpcDisplayName(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return "???";
            return NpcIdToName.TryGetValue(npcId, out var name) ? name : npcId;
        }

        /// <summary>根据中文显示名获取 NPC 短 ID</summary>
        public static string GetNpcId(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            return NameToNpcId.TryGetValue(displayName, out var id) ? id : displayName.ToLowerInvariant();
        }
    }
}
