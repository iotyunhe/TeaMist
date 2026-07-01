using System;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Gameplay;
using Random = UnityEngine.Random;

namespace TeaMist.Story
{
    /// <summary>
    /// 日常故事池 —— 三维索引（类型 × 季节 × 天气）的故事模板引擎
    /// 首期30条日常故事，含登场条件判定和防重复机制
    /// </summary>
    public class DailyStoryPool : MonoBehaviour
    {
        public static DailyStoryPool Instance { get; private set; }

        public enum StoryType
        {
            Wistful,    // 心事类
            Curious,    // 奇闻类
            Cozy,       // 日常类
            Seasonal,   // 季节限定
            Surprise,   // 意外惊喜
        }

        [Serializable]
        public class DailyStory
        {
            public string id;
            public string title;
            public StoryType type;
            public Season season;            // 适用季节
            public Weather weather;          // 适用天气
            [Header("登场条件")]
            public string[] requiredNPCs;    // 需要已在场的 NPC（空=所有NPC皆可）
            public int minGameDay;           // 最小游戏日
            public string prerequisiteStory; // 前置故事ID
            [Header("内容")]
            [TextArea(3, 8)]
            public string narrativeText;     // 叙事文本
            public string[] dialogueLines;   // 对话行
            public string teaHint;           // 茶品提示
            public string fragmentId;        // 关联碎片ID
            [Header("控制")]
            public bool canRepeat = false;   // 是否可重复触发
            public int cooldownDays = 7;     // 冷却天数
        }

        [Header("故事池")]
        [SerializeField] private List<DailyStory> storyPool = new List<DailyStory>();

        // 已触发过的故事（防重复）
        private HashSet<string> triggeredStories = new HashSet<string>();
        // 冷却中的故事
        private Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStoryPool();
        }

        private void InitializeStoryPool()
        {
            storyPool = new List<DailyStory>
            {
                // ===== 心事类 (Wistful) =====
                new DailyStory {
                    id = "daily_001", title = "没有说出口的话", type = StoryType.Wistful,
                    season = Season.All, weather = Weather.Rain,
                    narrativeText = "雨天，茶馆的客人比平时少。一个熟悉的身影坐在角落，对着窗外发呆。",
                    dialogueLines = new[] { "你知道吗，有些话就像这雨——在心里攒了很久，却始终落不下来。" },
                    teaHint = "雨天适合泡一壶陈年普洱——醇厚，可以慢慢喝。",
                },
                new DailyStory {
                    id = "daily_002", title = "故乡的茶", type = StoryType.Wistful,
                    season = Season.Autumn, weather = Weather.Clear,
                    narrativeText = "今天的客人说起了一种茶，说不记得名字，只记得味道——和他小时候在故乡喝到的一模一样。你能帮他找回那种味道吗？",
                    dialogueLines = new[] { "那种茶很淡……淡到你以为自己在喝水。但喝完以后，整个胸腔都是暖的。" },
                    teaHint = "淡而回甘的茶——考虑白毫银针或安吉白茶。水温不宜太高。",
                },
                new DailyStory {
                    id = "daily_003", title = "老物件的故事", type = StoryType.Wistful,
                    season = Season.All, weather = Weather.Cloudy,
                    requiredNPCs = new[] { "moyan" },
                    narrativeText = "墨砚带来了一块旧墨——他说这是他很久以前为一个人定制的，但那个人至今没有来取。",
                    dialogueLines = new[] { "这块墨等了很多年。今天我想请你帮我泡一杯茶，和它放在一起——也许茶香能告诉它，不用再等了。" },
                    teaHint = "用紫砂壶泡乌龙——乌龙的回甘能和旧墨的沉香呼应。",
                },
                new DailyStory {
                    id = "daily_004", title = "失眠的山精", type = StoryType.Wistful,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "今天来的是个陌生的山精。他说自己连着七天睡不着了，因为做了一个关于春天的梦——但现在明明是冬天。",
                    dialogueLines = new[] { "春天什么时候才来呢？我梦到花开的时候，就醒了。然后就再也睡不着了。" },
                    teaHint = "安神的甘菊茶，水温60度，加一点点蜂蜜。",
                },
                new DailyStory {
                    id = "daily_005", title = "旧伤口", type = StoryType.Wistful,
                    season = Season.Spring, weather = Weather.Rain,
                    narrativeText = "春天的雨落在竹帘上，滴滴答答。一个沉默的客人坐在窗边，袖子卷起来露出前臂——上面有一道很旧但很深的疤。",
                    dialogueLines = new[] { "这道疤不疼了。但每到下雨天，它就会痒。不是伤口在痒——是记忆。" },
                    teaHint = "春天的茶——明前龙井，清冽如泉。适合安静地喝。",
                },
                new DailyStory {
                    id = "daily_006", title = "一封没有寄出的信", type = StoryType.Wistful,
                    season = Season.Autumn, weather = Weather.Any,
                    narrativeText = "客人掏出一个黄色的信封，纸已经泛黄了。他说这是他写过但从未寄出的信。今天他想在茶馆里重新打开它。",
                    dialogueLines = new[] { "你能帮我泡一杯茶吗？一杯能让我有勇气看这封信的茶。" },
                    teaHint = "秋茶——武夷岩茶的岩韵，厚重如纸。",
                },

                // ===== 奇闻类 (Curious) =====
                new DailyStory {
                    id = "daily_010", title = "会说话的茶壶", type = StoryType.Curious,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "竹青冲进来，手里举着一把旧茶壶：\"掌柜的！这把壶在唱歌！真的，我刚才路过杂物间的时候听见的！\"",
                    dialogueLines = new[] { "你不信？你听——（她把壶贴在耳朵上）——它现在不唱了。可能是因为在你这儿它就害羞了。" },
                    teaHint = "不要用这把壶泡茶——竹青坚持要用它当八卦接收器。",
                },
                new DailyStory {
                    id = "daily_011", title = "倒流的茶烟", type = StoryType.Curious,
                    season = Season.Winter, weather = Weather.Clear,
                    narrativeText = "今早泡茶时，茶烟没有往上飘——它往下沉，像一条白蛇一样钻进杯底，然后就消失了。竹青说这是好兆头。云鹤老说这是灵脉在动。",
                    dialogueLines = new[] { "茶烟往下走——说明山在呼吸。它在从你的茶里吸东西。" },
                    teaHint = "今天适合泡一壶老茶——茶烟越沉越好。",
                },
                new DailyStory {
                    id = "daily_012", title = "从云里掉下来的种子", type = StoryType.Curious,
                    season = Season.Summer, weather = Weather.Cloudy,
                    narrativeText = "一阵奇怪的风吹过，有什么东西从云里飘了下来——一颗发着微光的种子，落在了茶馆门口。",
                    dialogueLines = new[] { "这是什么种子？我种了大半辈子东西，从来没见过。它……它在发光。" },
                    teaHint = "用隔夜的冷茶水浇种子。不要太烫——它看起来怕热。",
                },
                new DailyStory {
                    id = "daily_013", title = "镜湖倒影", type = StoryType.Curious,
                    season = Season.All, weather = Weather.Clear,
                    requiredNPCs = new[] { "yunhelao" },
                    narrativeText = "云鹤老说，今天镜湖的水面不对——倒影比真实的东西慢了一拍。他已经在那站了一个时辰了。",
                    dialogueLines = new[] { "镜湖的倒影慢了半拍。三百年来——这是头一次。山在变。" },
                    teaHint = "陪云鹤老坐一会儿。不需要茶——只需要耐心。",
                },
                new DailyStory {
                    id = "daily_014", title = "花香的源头", type = StoryType.Curious,
                    season = Season.Spring, weather = Weather.Any,
                    narrativeText = "整个茶馆突然充满了浓郁的花香——但附近一株花都没有。客人们面面相觑。当归说，这味道来自琉璃洞的方向。",
                    dialogueLines = new[] { "琉璃洞的花开了。一年只开一次，一次只开一个时辰。但今天——不是它该开的时候。" },
                    teaHint = "花香型茶——茉莉银针。让茶香和花香对话。",
                },

                // ===== 日常类 (Cozy) =====
                new DailyStory {
                    id = "daily_020", title = "白露学数数", type = StoryType.Cozy,
                    season = Season.All, weather = Weather.Any,
                    requiredNPCs = new[] { "bailu" },
                    narrativeText = "白露趴在柜台上，认真地数着茶罐——\"一、二、三、五、八……不对不对，四呢？\" 她把茶罐重新排了一遍，但还是数不清。",
                    dialogueLines = new[] { "掌柜的——茶罐太多了！你能不能泡杯茶，让我边喝边数？喝了茶脑子就清楚了。" },
                    teaHint = "给她一杯甜甜的桂花蜜茶——她喜欢。而且喝完她就会忘记数罐子这件事。",
                    canRepeat = true, cooldownDays = 5,
                },
                new DailyStory {
                    id = "daily_021", title = "茶馆里的诗句", type = StoryType.Cozy,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "墨砚今天在茶馆的墙上题了一首诗。字迹清秀，用的是茶汤。他说这诗会慢慢消失——\"喝茶的人应该读到它，不是路过的人。\"",
                    dialogueLines = new[] { "诗写在墙上，茶喝在嘴里。一个会褪色，一个会留香——其实是一样的。" },
                    teaHint = "给墨砚一杯清茶。他题诗时不喝有味道的茶——怕影响字迹。",
                    canRepeat = true, cooldownDays = 10,
                },
                new DailyStory {
                    id = "daily_022", title = "竹青的小生意", type = StoryType.Cozy,
                    season = Season.All, weather = Weather.Any,
                    requiredNPCs = new[] { "zhuqing" },
                    narrativeText = "竹青在茶馆门口支了个小摊，卖\"竹青独家情报\"——其实就是她把昨天听到的八卦写在竹叶上，一片一片地卖。生意还不错。",
                    dialogueLines = new[] { "三片竹叶换一碗茶！童叟无欺！——诶掌柜的你别生气，我在给你拉客呢！" },
                    teaHint = "给她一杯清口的竹叶青——\"哪有在茶馆门口卖情报不给掌柜分成的道理！\"",
                    canRepeat = true, cooldownDays = 7,
                },
                new DailyStory {
                    id = "daily_023", title = "妖怪们的棋局", type = StoryType.Cozy,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "茶馆角落的桌子上不知什么时候多了一副围棋。两个不认识的妖怪正在下棋。没人知道他们什么时候进来的，也没人知道谁赢了——因为他们下到一半就化作青烟散了，棋盘上留下一句话：\"下次再来下完。\"",
                    dialogueLines = new[] { "（棋盘上的字是用松烟写的）\"这盘棋我们下了三百年，在你这里喝了一杯茶，又走了十手。\"——落款：风孔二老"},
                    teaHint = "在棋盘旁放一壶松烟茶——也许下次他们来得快一些。",
                },
                new DailyStory {
                    id = "daily_024", title = "给小鸟泡茶", type = StoryType.Cozy,
                    season = Season.Spring, weather = Weather.Clear,
                    narrativeText = "一只小黄鸟飞进茶馆，停在柜台上不走。白露说它想喝茶。\"你看它的眼睛——它在看你泡茶！\"",
                    dialogueLines = new[] { "泡淡一点！太浓了它飞不起来的！" },
                    teaHint = "用最淡的绿茶——一杯茶用一片叶子就够了。在茶杯旁边放一个小碟子。",
                    canRepeat = true, cooldownDays = 15,
                },
                new DailyStory {
                    id = "daily_025", title = "日落时的茶馆", type = StoryType.Cozy,
                    season = Season.All, weather = Weather.Clear,
                    narrativeText = "傍晚，茶馆里很安静。夕阳从竹帘缝隙漏进来，在桌上画出金色的条纹。没有人说话——所有人都在看日落。",
                    dialogueLines = new[] { "（沉默）……今天的日落，比昨天的红一些。你觉得呢？" },
                    teaHint = "不需要泡新茶。把壶里剩下的温一温就好。日落不等人。",
                },

                // ===== 季节限定 (Seasonal) =====
                new DailyStory {
                    id = "daily_030", title = "春雪初融", type = StoryType.Seasonal,
                    season = Season.Spring, weather = Weather.Clear,
                    narrativeText = "立春后的第三天，栖霞山最后一块雪融了。融化的雪水从山顶流下来，经过茶馆门前，带着松针和冬青的味道。",
                    dialogueLines = new[] { "雪水是好东西——但你要快点接。春雪比冬雪活，走得也快。" },
                    teaHint = "用初融的雪水泡茶——任何茶都会多一层清冽。",
                },
                new DailyStory {
                    id = "daily_031", title = "蝉鸣茶馆", type = StoryType.Seasonal,
                    season = Season.Summer, weather = Weather.Clear,
                    narrativeText = "盛夏的午后，知了声震天响。茶馆里的客人被吵得心烦意乱——直到一只知了飞进来，落在了茶壶盖上。",
                    dialogueLines = new[] { "（知了叫得整间茶馆都在震）掌柜的，你能不能给它也泡一杯茶让它安静会儿？" },
                    teaHint = "用凉茶。蝉喝烫茶会更吵。",
                },
                new DailyStory {
                    id = "daily_032", title = "秋月下的野茶会", type = StoryType.Seasonal,
                    season = Season.Autumn, weather = Weather.Clear,
                    narrativeText = "中秋夜，月亮又圆又亮。不知是谁起的头，茶馆门口的坪上摆满了茶具——不认识的妖怪们带着各自的茶具和茶叶来了，一场没有预谋的野茶会。",
                    dialogueLines = new[] { "这是山上的老规矩了——月亮最圆的那晚，茶不问来处，人不问归处。来，尝尝我这饼老茶。" },
                    teaHint = "不需要特定的茶。把你的好茶都拿出来——今晚茶馆不打烊。",
                },
                new DailyStory {
                    id = "daily_033", title = "雪夜来客", type = StoryType.Seasonal,
                    season = Season.Winter, weather = Weather.Snow,
                    narrativeText = "大雪封山，茶馆的门被敲响了。门口站着一个满身是雪的陌生人——他的脚印在雪地上走了很远，一直延伸到看不见的地方。",
                    dialogueLines = new[] { "（呵着白气）打扰了……能在你这儿坐一会儿吗？走了一夜的路，只闻到了你的茶香。" },
                    teaHint = "先不急着泡茶——给他一条热毛巾，然后才是滚烫的红茶。",
                },
                new DailyStory {
                    id = "daily_034", title = "桂花落", type = StoryType.Seasonal,
                    season = Season.Autumn, weather = Weather.Any,
                    narrativeText = "茶馆门口的桂花树一夜之间全开了。风一吹，桂花像金色的小雨一样落下来，落进茶碗里、落在柜台上、落进客人的发间。",
                    dialogueLines = new[] { "桂花落在茶里——这叫天赐茶。不用加糖，就已经是甜的了。" },
                    teaHint = "不要扫掉桌上的桂花。今天的茶里都要放几朵——不要钱。",
                },

                // ===== 意外惊喜 (Surprise) =====
                new DailyStory {
                    id = "daily_040", title = "老朋友的礼物", type = StoryType.Surprise,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "早上开门时，门口放着一包用红纸包好的茶叶。没有名字，只有一行小字——\"老朋友树送的。它说谢谢你开的这间店。\"",
                    dialogueLines = new[] { "（红纸上写着）\"山里安静太久了。每次茶烟升起，山就多醒一分。\"——老朋友"},
                    teaHint = "这包茶的叶子前所未见——试着用最诚实的方式泡。不加任何技巧。",
                },
                new DailyStory {
                    id = "daily_041", title = "双彩虹", type = StoryType.Surprise,
                    season = Season.Summer, weather = Weather.Rain,
                    narrativeText = "一场急雨过后，栖霞山上空出现了双彩虹——一道在云上，一道在水中。所有的客人都跑出去看了，茶馆里只剩你和还在冒烟的茶壶。",
                    dialogueLines = new[] { "快到外面来！双彩虹——一百年都不一定看到一次！" },
                    teaHint = "把茶壶也端出去——在外面喝。彩虹不等人。",
                },
                new DailyStory {
                    id = "daily_042", title = "萤火虫茶会", type = StoryType.Surprise,
                    season = Season.Summer, weather = Weather.Clear,
                    narrativeText = "入夜后，茶馆周围围满了萤火虫。它们不是来喝茶的——它们是被茶香吸引来的。每只萤火虫都停在不同的茶杯边缘，像点了无数盏小灯。",
                    dialogueLines = new[] { "（低声）别吹气。它们会待到茶凉为止。" },
                    teaHint = "泡最淡的茶——茶越淡，萤火虫待得越久。",
                },
                new DailyStory {
                    id = "daily_043", title = "琉璃洞的回音", type = StoryType.Surprise,
                    season = Season.All, weather = Weather.Any,
                    narrativeText = "今天泡茶的时候，茶烟形成了一个奇怪的形状——像一座洞窟的轮廓。当归说那是琉璃洞。\"它在回应你的茶。\"",
                    dialogueLines = new[] { "琉璃洞有自己的意志。它很久没有回应过任何人——直到你来。" },
                    teaHint = "保持安静。不要问问题——只是泡茶。让它继续说话。",
                },
                new DailyStory {
                    id = "daily_044", title = "山的第一朵花", type = StoryType.Surprise,
                    season = Season.Spring, weather = Weather.Clear,
                    narrativeText = "茶馆的窗台上，不知什么时候开了一朵不认识的花。没有人种过它——它自己长出来的。白露说这是山的第一朵花，每年开在不同的地方。今年它选择了这里。",
                    dialogueLines = new[] { "（小声）山的花。它选了你。不要移它——让它自己决定什么时候走。" },
                    teaHint = "今天用最干净的水泡茶。不是给客人——是给这朵花。",
                },
            };
        }

        // ============ 故事选择 ============
        /// <summary>
        /// 根据当前条件获取可用故事列表
        /// </summary>
        public List<DailyStory> GetAvailableStories(
            Season season, Weather weather, float gameDay,
            List<string> presentNPCs, HashSet<string> unlockedFragments)
        {
            List<DailyStory> available = new List<DailyStory>();

            foreach (var story in storyPool)
            {
                // 季节/天气匹配（All表示全部适用）
                if (story.season != Season.All && story.season != season) continue;
                if (story.weather != Weather.Any && story.weather != weather) continue;

                // 最小游戏日
                if (gameDay < story.minGameDay) continue;

                // 前置故事
                if (!string.IsNullOrEmpty(story.prerequisiteStory) &&
                    !triggeredStories.Contains(story.prerequisiteStory)) continue;

                // 需要特定NPC在场
                if (story.requiredNPCs != null && story.requiredNPCs.Length > 0)
                {
                    bool allPresent = true;
                    foreach (var npc in story.requiredNPCs)
                    {
                        if (presentNPCs == null || !presentNPCs.Contains(npc))
                        { allPresent = false; break; }
                    }
                    if (!allPresent) continue;
                }

                // 防重复
                if (!story.canRepeat && triggeredStories.Contains(story.id)) continue;

                // 冷却检查
                if (cooldowns.TryGetValue(story.id, out float cooldownEnd) && gameDay < cooldownEnd)
                    continue;

                available.Add(story);
            }

            return available;
        }

        /// <summary>
        /// 随机选择一个可用故事
        /// </summary>
        public DailyStory PickRandomStory(
            Season season, Weather weather, float gameDay,
            List<string> presentNPCs, HashSet<string> unlockedFragments)
        {
            var available = GetAvailableStories(season, weather, gameDay, presentNPCs, unlockedFragments);
            if (available.Count == 0) return null;

            // 权重：季节限定 > 意外惊喜 > 奇闻 > 心事 > 日常
            var weighted = new List<DailyStory>();
            foreach (var story in available)
            {
                int weight;
                switch (story.type)
                {
                    case StoryType.Seasonal: weight = 5; break;
                    case StoryType.Surprise:  weight = 4; break;
                    case StoryType.Curious:   weight = 3; break;
                    case StoryType.Wistful:   weight = 2; break;
                    case StoryType.Cozy:      weight = 1; break;
                    default:                  weight = 1; break;
                }
                for (int i = 0; i < weight; i++) weighted.Add(story);
            }

            return weighted[Random.Range(0, weighted.Count)];
        }

        /// <summary>
        /// 触发故事后注册
        /// </summary>
        public void RegisterStoryTriggered(DailyStory story, float gameDay)
        {
            triggeredStories.Add(story.id);

            if (story.cooldownDays > 0)
            {
                cooldowns[story.id] = gameDay + story.cooldownDays;
            }
        }

        // ============ 保存/加载 ============
        public SaveData GetSaveData()
        {
            return new SaveData
            {
                triggeredStories = new List<string>(triggeredStories),
                cooldownEntries = new List<CooldownEntry>(),
            };
        }

        public void LoadSaveData(SaveData data)
        {
            if (data == null) return;
            triggeredStories = new HashSet<string>(data.triggeredStories ?? new List<string>());
            cooldowns.Clear();
            if (data.cooldownEntries != null)
            {
                foreach (var entry in data.cooldownEntries)
                {
                    cooldowns[entry.storyId] = entry.cooldownEnd;
                }
            }
        }

        [Serializable]
        public class SaveData
        {
            public List<string> triggeredStories;
            public List<CooldownEntry> cooldownEntries;
        }

        [Serializable]
        public class CooldownEntry
        {
            public string storyId;
            public float cooldownEnd;
        }
    }
}
