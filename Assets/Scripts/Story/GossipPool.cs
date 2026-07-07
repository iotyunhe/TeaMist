using System;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Gameplay;
using Random = UnityEngine.Random;

namespace TeaMist.Story
{
    /// <summary>
    /// 竹青八卦消息池
    /// 按季节/天气/好感度三维索引，动态生成 NPC 社交网络消息
    /// 
    /// 增强功能：
    /// - 动态八卦生成（基于游戏事件）
    /// - 八卦-对话联动（NPC来访前注入相关八卦）
    /// - 八卦-碎片联动（特定八卦触发碎片收集）
    /// </summary>
    public class GossipPool : MonoBehaviour
    {
        public static GossipPool Instance { get; private set; }

        /// <summary>
        /// 一条八卦消息
        /// </summary>
        [Serializable]
        public class GossipMessage
        {
            public string id;                // 唯一ID
            public string senderId;          // 消息来源NPC
            public string senderName;        // 消息来源显示名
            public string subjectNpcId;      // 消息主角NPC
            public string subjectName;       // 消息主角显示名
            public string content;           // 八卦文本
            public GossipTone tone;          // 语气
            public Season seasonRelevance;   // 季节关联
            public Weather weatherRelevance; // 天气关联
            [Range(0, 5)]
            public int minAffection;         // 最小好感度需求
            public bool isLocked;            // 是否被锁（剧情锁）
            public string unlockCondition;   // 解锁条件描述
            public float expireDay;          // 过期游戏日（0=不过期）
            public string linkedFragmentId;   // 关联碎片ID（揭示后自动收集）
        }

        public enum GossipTone
        {
            Secret,       // "嘘——别告诉别人"
            Casual,       // "诶你知道吗"
            Concerned,    // "说起来有点担心"
            Excited,      // "天大的消息！"
            Mysterious,   // "有件事很奇怪……"
            Sad,          // "说起来让人难过"
        }

        [Header("八卦消息池")]
        [SerializeField] private List<GossipMessage> gossipPool = new List<GossipMessage>();

        [Header("已揭示的八卦")]
        [SerializeField] private List<string> revealedGossipIds = new List<string>();

        [Header("当前活跃消息")]
        [SerializeField] private List<GossipMessage> activeGossips = new List<GossipMessage>();

        // 消息冷却时间（同一个NPC不会在短时间内重复产生八卦）
        private Dictionary<string, float> cooldownMap = new Dictionary<string, float>();
        private const float CooldownDays = 2f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGossipPool();
        }

        // ============ 初始化消息池 ============
        private void InitializeGossipPool()
        {
            gossipPool = new List<GossipMessage>
            {
                // --- 竹青的八卦（她是最主要的八卦源） ---
                new GossipMessage {
                    id = "gossip_001", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "bailu", subjectName = "白露",
                    content = "白露最近总往山腰跑，说是去采桂花——但我闻到她身上有茶香。这丫头肯定发现好地方了。",
                    tone = GossipTone.Casual, seasonRelevance = Season.Autumn, weatherRelevance = Weather.Any, minAffection = 0
                },
                new GossipMessage {
                    id = "gossip_002", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "yunhelao", subjectName = "云鹤老",
                    content = "云鹤爷爷最近晚上不睡觉，一直站在旧仙台边上。我说\u201c爷爷你站着干嘛\u201d，他说\u201c等一个声音\u201d。什么声音呢？他不肯说。",
                    tone = GossipTone.Mysterious, seasonRelevance = Season.All, weatherRelevance = Weather.Clear, minAffection = 1
                },
                new GossipMessage {
                    id = "gossip_003", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "danggui", subjectName = "当归",
                    content = "山脚那个药材铺你知道吧？门一直锁着，但灯偶尔会亮。当归姐姐还在——只是她不怎么见人了。",
                    tone = GossipTone.Sad, seasonRelevance = Season.All, weatherRelevance = Weather.Rain, minAffection = 0
                },
                new GossipMessage {
                    id = "gossip_004", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "moyan", subjectName = "墨砚",
                    content = "墨砚那家伙最近不画画了。整天对着云海发呆，说什么\u201c颜色不够\u201d。他自己就是砚台变的，什么颜色调不出来啊？",
                    tone = GossipTone.Casual, seasonRelevance = Season.All, weatherRelevance = Weather.Cloudy, minAffection = 2
                },
                new GossipMessage {
                    id = "gossip_005", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "shuangjiang", subjectName = "霜降",
                    content = "我跟你说个事——别害怕——霜降大人这两天一直在茶馆附近。他不进来，就在竹林边站着。我去搭话他也不理，冷死了。",
                    tone = GossipTone.Concerned, seasonRelevance = Season.Winter, weatherRelevance = Weather.Any, minAffection = 3
                },
                new GossipMessage {
                    id = "gossip_006", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "xiaoshan", subjectName = "小山",
                    content = "云肩坪东边多了块石头。我路过的时候它在动——不是风吹的动，是它自己动了。竹青我活了三百年没见过这种事。",
                    tone = GossipTone.Excited, seasonRelevance = Season.Spring, weatherRelevance = Weather.Any, minAffection = 1
                },
                new GossipMessage {
                    id = "gossip_007", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "qichi", subjectName = "栖迟",
                    content = "昨天我在山径上碰到一个人——不认识。他回头看了我一眼，然后就不见了。你知道吗，那个人身上没有气味。妖怪有气味，人也有气味，但他没有。",
                    tone = GossipTone.Mysterious, seasonRelevance = Season.All, weatherRelevance = Weather.Mist, minAffection = 4, isLocked = true,
                    unlockCondition = "云鹤老好感度≥3，且解锁至少3个'山的故事'碎片"
                },

                // --- 白露的八卦（偶尔无意中透露） ---
                new GossipMessage {
                    id = "gossip_010", senderId = "bailu", senderName = "白露",
                    subjectNpcId = "zhuqing", subjectName = "竹青",
                    content = "竹青姐姐昨天爬上了最高的那棵竹子，在上面挂了一整天。我喊她下来吃饭她也不理，说在\u201c收集情报\u201d。",
                    tone = GossipTone.Casual, seasonRelevance = Season.Summer, weatherRelevance = Weather.Any, minAffection = 1
                },
                new GossipMessage {
                    id = "gossip_011", senderId = "bailu", senderName = "白露",
                    subjectNpcId = "yunhelao", subjectName = "云鹤老",
                    content = "云鹤爷爷说，栖霞山的灵气变了。我问他怎么变的，他摸了摸我的头说\u201c等你长大就知道了\u201d。哼，我已经长大了。",
                    tone = GossipTone.Casual, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 2
                },

                // --- 云鹤老的八卦（极少，但极重） ---
                new GossipMessage {
                    id = "gossip_020", senderId = "yunhelao", senderName = "云鹤老",
                    subjectNpcId = "qichi", subjectName = "栖迟",
                    content = "（沉默很久）那个孩子……他长得太像一个人了。一个我以为再也不会见到的人。",
                    tone = GossipTone.Sad, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 4, isLocked = true,
                    unlockCondition = "云鹤老好感度≥4"
                },

                // --- 墨砚的八卦 ---
                new GossipMessage {
                    id = "gossip_030", senderId = "moyan", senderName = "墨砚",
                    subjectNpcId = "danggui", subjectName = "当归",
                    content = "当归姑娘上次来画坊，看了一幅画很久。画里是一间药材铺——和山脚那间一模一样。她什么也没说，就走了。",
                    tone = GossipTone.Concerned, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 2
                },

                // --- 当归的八卦 ---
                new GossipMessage {
                    id = "gossip_040", senderId = "danggui", senderName = "当归",
                    subjectNpcId = "shuangjiang", subjectName = "霜降",
                    content = "霜降大人的寒气最近弱了。我不是说这是坏事——但对他来说……寒气弱了，就意味着……（当归没有说完）。",
                    tone = GossipTone.Concerned, seasonRelevance = Season.Winter, weatherRelevance = Weather.Snow, minAffection = 2, isLocked = true,
                    unlockCondition = "当归好感度≥2，且当前季节为冬季"
                },

                // --- 霜降的八卦（极少开口，但每句话都是地震） ---
                new GossipMessage {
                    id = "gossip_050", senderId = "shuangjiang", senderName = "霜降",
                    subjectNpcId = "qichi", subjectName = "栖迟",
                    content = "（冷眼望着远方）他出现那天——栖霞台的雾散了。三百年来第一次。",
                    tone = GossipTone.Secret, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 5, isLocked = true,
                    unlockCondition = "霜降好感度≥5"
                },

                // --- 三次来访后的八卦（深度线联动） ---
                new GossipMessage {
                    id = "gossip_100", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "bailu", subjectName = "白露",
                    content = "你知道吗？白露昨天教溪边的石头唱歌。我没开玩笑。石头真的在听。",
                    tone = GossipTone.Excited, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 3
                },
                new GossipMessage {
                    id = "gossip_101", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "danggui", subjectName = "当归",
                    content = "当归姐姐在茶馆旁边开了间小铺子！卖一种叫「归处」的茶。我喝了一口——不苦不甜，但很安心。",
                    tone = GossipTone.Casual, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 3
                },
                new GossipMessage {
                    id = "gossip_102", senderId = "bailu", senderName = "白露",
                    subjectNpcId = "yunhelao", subjectName = "云鹤老",
                    content = "云鹤爷爷把发光的珠子放在茶馆了！晚上好漂亮！像月亮掉进了屋里。",
                    tone = GossipTone.Excited, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 3
                },
                new GossipMessage {
                    id = "gossip_103", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "xiaoshan", subjectName = "小山",
                    content = "小山今天开口说了两个字。两个字！我活了三百年，第一次听见石头说话。你猜他说了什么？「我在」。就这两个字。我哭了。",
                    tone = GossipTone.Sad, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 2
                },
                new GossipMessage {
                    id = "gossip_104", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "qinglan", subjectName = "青岚",
                    content = "青岚在茶馆墙上画了一扇窗。那扇窗……真的能看见远山。我不知道怎么做到的，但他做到了。",
                    tone = GossipTone.Mysterious, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 2
                },
                new GossipMessage {
                    id = "gossip_105", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "hanlu", subjectName = "寒露",
                    content = "寒露姐姐越来越淡了。她说节气灵靠人的记忆活着。山下的人越来越不记得节气了……所以她在变。",
                    tone = GossipTone.Concerned, seasonRelevance = Season.Autumn, weatherRelevance = Weather.Any, minAffection = 2
                },
                new GossipMessage {
                    id = "gossip_106", senderId = "qiaoweng", senderName = "樵翁",
                    subjectNpcId = "xiaoshan", subjectName = "小山",
                    content = "小山今天在茶馆门口站了一整天。不动。但你能感觉到它在听。听茶馆里的声音。",
                    tone = GossipTone.Casual, seasonRelevance = Season.All, weatherRelevance = Weather.Any, minAffection = 2
                },

                // --- 季节联动八卦 ---
                new GossipMessage {
                    id = "gossip_200", senderId = "zhuqing", senderName = "竹青",
                    subjectNpcId = "", subjectName = "山",
                    content = "春天了。整座山都在呼吸。泥土松软了，溪水变清了。连茶馆的柱子都发出了叹息——像从冬天的梦里醒过来。",
                    tone = GossipTone.Casual, seasonRelevance = Season.Spring, weatherRelevance = Weather.Any, minAffection = 0
                },
                new GossipMessage {
                    id = "gossip_201", senderId = "bailu", senderName = "白露",
                    subjectNpcId = "", subjectName = "溪谷",
                    content = "夏天了！溪谷的水变温了！晚上有萤火虫！我抓了一只放在瓶子里，但它不亮了。是不是因为……它想回家了？",
                    tone = GossipTone.Casual, seasonRelevance = Season.Summer, weatherRelevance = Weather.Clear, minAffection = 1
                },
                new GossipMessage {
                    id = "gossip_202", senderId = "yunhelao", senderName = "云鹤老",
                    subjectNpcId = "", subjectName = "旧仙台",
                    content = "秋天的月亮最圆的时候，旧仙台会发出一种很轻的声音。不是钟声——是回忆的声音。三百年前我就听见了。",
                    tone = GossipTone.Mysterious, seasonRelevance = Season.Autumn, weatherRelevance = Weather.Clear, minAffection = 2
                },
                new GossipMessage {
                    id = "gossip_203", senderId = "qiaoweng", senderName = "樵翁",
                    subjectNpcId = "", subjectName = "山道",
                    content = "雪落了。山道封了。但茶馆的灯还亮着。走夜路的人看见灯，就知道前面有人。",
                    tone = GossipTone.Casual, seasonRelevance = Season.Winter, weatherRelevance = Weather.Snow, minAffection = 1
                },
            };
        }

        // ============ 获取可用八卦 ============
        /// <summary>
        /// 获取当前条件下可用的八卦消息列表
        /// </summary>
        public List<GossipMessage> GetAvailableGossips(
            Season season, Weather weather, float gameDay,
            Dictionary<string, int> npcAffections)
        {
            List<GossipMessage> available = new List<GossipMessage>();

            foreach (var gossip in gossipPool)
            {
                // 已揭示的跳过
                if (revealedGossipIds.Contains(gossip.id)) continue;
                // 锁定的跳过
                if (gossip.isLocked) continue;
                // 已过期的跳过
                if (gossip.expireDay > 0 && gameDay > gossip.expireDay) continue;
                // 季节检查
                if (gossip.seasonRelevance != Season.All && gossip.seasonRelevance != season) continue;
                // 天气检查
                if (gossip.weatherRelevance != Weather.Any && gossip.weatherRelevance != weather) continue;
                // 好感度检查（对消息来源NPC）
                int affection = 0;
                npcAffections?.TryGetValue(gossip.senderId, out affection);
                if (affection < gossip.minAffection) continue;

                available.Add(gossip);
            }

            return available;
        }

        /// <summary>
        /// 揭示一条八卦
        /// </summary>
        public void RevealGossip(string gossipId)
        {
            if (!revealedGossipIds.Contains(gossipId))
            {
                revealedGossipIds.Add(gossipId);
            }
        }

        /// <summary>
        /// 尝试解锁被剧情锁的八卦
        /// </summary>
        public List<GossipMessage> TryUnlockConditionalGossips(Dictionary<string, int> npcAffections, int totalFragmentsUnlocked)
        {
            List<GossipMessage> unlocked = new List<GossipMessage>();

            foreach (var gossip in gossipPool)
            {
                if (!gossip.isLocked) continue;
                if (revealedGossipIds.Contains(gossip.id)) continue;

                // 检查各条解锁条件
                bool conditionMet = false;
                switch (gossip.id)
                {
                    case "gossip_007": // 栖迟 - 需要云鹤好感3 + 3个山的故事碎片
                        conditionMet = GetAffection(npcAffections, "yunhelao") >= 3 && totalFragmentsUnlocked >= 3;
                        break;
                    case "gossip_020": // 云鹤老关于栖迟
                        conditionMet = GetAffection(npcAffections, "yunhelao") >= 4;
                        break;
                    case "gossip_040": // 当归关于霜降
                        conditionMet = GetAffection(npcAffections, "danggui") >= 2;
                        break;
                    case "gossip_050": // 霜降关于栖迟
                        conditionMet = GetAffection(npcAffections, "shuangjiang") >= 5;
                        break;
                    default:
                        conditionMet = false;
                        break;
                }

                if (conditionMet)
                {
                    gossip.isLocked = false;
                    unlocked.Add(gossip);
                }
            }

            return unlocked;
        }

        private int GetAffection(Dictionary<string, int> affections, string npcId)
        {
            if (affections == null) return 0;
            return affections.TryGetValue(npcId, out int val) ? val : 0;
        }

        // ============ 每日刷新 ============
        /// <summary>
        /// 游戏日更新时刷新活跃八卦
        /// </summary>
        public void RefreshDaily(Season season, Weather weather, float gameDay,
            Dictionary<string, int> npcAffections)
        {
            activeGossips.Clear();

            var available = GetAvailableGossips(season, weather, gameDay, npcAffections);
            // 每天最多3条活跃八卦
            int count = Mathf.Min(3, available.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, available.Count);
                activeGossips.Add(available[idx]);
                available.RemoveAt(idx);
            }
        }

        public List<GossipMessage> GetActiveGossips() => activeGossips;

        // ============ 八卦-对话联动 ============

        /// <summary>
        /// 获取关于指定NPC的八卦（用于对话前注入叙事）
        /// </summary>
        public List<GossipMessage> GetGossipsAboutNpc(string npcId)
        {
            var result = new List<GossipMessage>();
            foreach (var g in activeGossips)
            {
                if (g.subjectNpcId == npcId || g.senderId == npcId)
                    result.Add(g);
            }
            return result;
        }

        /// <summary>
        /// 生成关于指定NPC的对话前叙事文本
        /// </summary>
        public string GeneratePreDialogueNarration(string npcId, string npcName)
        {
            var gossips = GetGossipsAboutNpc(npcId);
            if (gossips.Count == 0) return null;

            var gossip = gossips[Random.Range(0, gossips.Count)];

            // 根据消息来源生成不同风格的叙事
            if (gossip.senderId == "zhuqing")
            {
                return $"【柜台上放着一片竹叶，叶脉里似乎写着什么。你拿起来一看——是竹青的小笺：】\n{GetGossipAsNote(gossip)}";
            }
            else if (gossip.senderId == "bailu")
            {
                return $"【门口有几颗野莓，排成了一排。像是白露特意留给你的消息。旁边还有一行歪歪扭扭的字：】\n{gossip.content}\n\n——{gossip.senderName}";
            }
            else if (gossip.senderId == "yunhelao")
            {
                return $"【茶馆角落的桌上多了一颗莹白的珠子。旁边有一张旧纸条：】\n{gossip.content}\n\n——{gossip.senderName}";
            }
            else if (gossip.senderId == "qiaoweng")
            {
                return $"【门口多了一捆柴，柴上刻着几个字：】\n{gossip.content}\n\n——{gossip.senderName}";
            }
            else
            {
                return $"【你注意到一些关于{npcName}的消息：】\n{gossip.content}\n\n——{gossip.senderName}";
            }
        }

        // ============ 动态八卦生成 ============

        /// <summary>
        /// 根据游戏事件动态生成八卦并加入消息池
        /// </summary>
        public void OnGameEvent(string eventType, Dictionary<string, object> eventData)
        {
            switch (eventType)
            {
                case "npc_third_visit":
                    GenerateThirdVisitGossip(eventData);
                    break;
                case "fragment_collected":
                    GenerateFragmentGossip(eventData);
                    break;
                case "perfect_brew":
                    GenerateBrewGossip();
                    break;
                case "season_changed":
                    // 季节变化时刷新八卦池
                    break;
            }
        }

        private void GenerateThirdVisitGossip(Dictionary<string, object> data)
        {
            string npcId = data.ContainsKey("npcId") ? data["npcId"].ToString() : "";
            string gossipId = $"dynamic_third_{npcId}";

            // 避免重复
            foreach (var g in gossipPool)
                if (g.id == gossipId) return;

            string content = npcId switch
            {
                "bailu" => "白露今天哭了。不是伤心——是那种很高兴很高兴的高兴。她说她终于把一首歌的秘密告诉了对的人。",
                "zhuqing" => "竹青把一本书放在了茶馆里。她说那是整座山的记忆。我翻了翻——里面连我昨天打了几个喷嚏都记了。",
                "danggui" => "当归在茶馆旁边开了间铺子。卖的茶不苦不甜，但喝完之后手不抖了。她说这叫「归处」。",
                "yunhelao" => "云鹤老把三颗珠子留在了茶馆。他说那是他等了三百年的人留下的。现在珠子发光了——像一盏灯。",
                "xiaoshan" => "小山说话了。不是纹路——是真的说话了。它说了一个字：「谢」。就一个字。但整座山都听见了。",
                "qinglan" => "青岚给茶馆看了一幅三百年前的画。画上的茶馆和现在一模一样。柜台后面站着的人——也一模一样。",
                "hanlu" => "寒露把一片永远不会融化的霜放在了窗台上。她说只要有人记得她，她就不会消失。",
                "qiaoweng" => "樵翁说庙后面的山上有一棵树，是山的命。他说只要有人记得那棵树，山就不会消失。",
                _ => null
            };

            if (content == null) return;

            gossipPool.Add(new GossipMessage
            {
                id = gossipId,
                senderId = "zhuqing",
                senderName = "竹青",
                subjectNpcId = npcId,
                subjectName = Core.DataManager.GetNpcDisplayName(npcId),
                content = content,
                tone = GossipTone.Excited,
                seasonRelevance = Season.All,
                weatherRelevance = Weather.Any,
                minAffection = 0
            });
        }

        private void GenerateFragmentGossip(Dictionary<string, object> data)
        {
            // 当收集到特定碎片时，生成关联八卦
            string fragmentId = data.ContainsKey("fragmentId") ? data["fragmentId"].ToString() : "";
            string gossipId = $"frag_gossip_{fragmentId}";

            foreach (var g in gossipPool)
                if (g.id == gossipId) return;

            // 某些碎片收集后自动解锁关联八卦
            string linkedContent = fragmentId switch
            {
                "fragment_nine_towers" => "云鹤老说的九座楼台——我查了县志。真的存在过。九座。一座不少。但县志上说它们不是被毁的——是「自行消失」。",
                "fragment_crane_pearls" => "你知道鹤羽珠在夜里会发光吗？我看见了。光里面好像有一个人影。一个很温柔的人影。",
                "fragment_xiaoshan_portrait" => "小山消失后留下的那道水墨痕迹——我临摹了下来。放在竹林里一晚，第二天痕迹变成了一个字：「在」。",
                _ => null
            };

            if (linkedContent == null) return;

            gossipPool.Add(new GossipMessage
            {
                id = gossipId,
                senderId = "zhuqing",
                senderName = "竹青",
                subjectNpcId = "",
                subjectName = "山",
                content = linkedContent,
                tone = GossipTone.Mysterious,
                seasonRelevance = Season.All,
                weatherRelevance = Weather.Any,
                minAffection = 0
            });
        }

        private void GenerateBrewGossip()
        {
            string gossipId = $"dynamic_brew_{DateTime.Now.DayOfYear}";
            foreach (var g in gossipPool)
                if (g.id == gossipId) return;

            gossipPool.Add(new GossipMessage
            {
                id = gossipId,
                senderId = "bailu",
                senderName = "白露",
                subjectNpcId = "",
                subjectName = "茶馆",
                content = "今天的茶……发光了！不是普通的发光——是那种很温柔的、像月亮掉进了杯子里的光。我从来没见过泡茶能泡出这种效果！",
                tone = GossipTone.Excited,
                seasonRelevance = Season.All,
                weatherRelevance = Weather.Any,
                minAffection = 0,
                expireDay = Core.TimeManager.Instance != null ?
                    (float)(TimeManager.Instance.TotalDaysPlayed + 3) : 0
            });
        }

        /// <summary>获取竹青小笺格式的八卦文本</summary>
        public string GetGossipAsNote(GossipMessage gossip)
        {
            string toneEmoji;
            switch (gossip.tone)
            {
                case GossipTone.Secret:     toneEmoji = "🤫"; break;
                case GossipTone.Casual:     toneEmoji = "🍃"; break;
                case GossipTone.Concerned:  toneEmoji = "😟"; break;
                case GossipTone.Excited:    toneEmoji = "✨"; break;
                case GossipTone.Mysterious: toneEmoji = "🌫"; break;
                case GossipTone.Sad:        toneEmoji = "🍂"; break;
                default:                    toneEmoji = "📝"; break;
            }

            return $"{toneEmoji} {gossip.content}\n\n——{gossip.senderName}";
        }
    }
}
