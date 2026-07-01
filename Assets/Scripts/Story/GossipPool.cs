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

        /// <summary>
        /// 获取竹青小笺格式的八卦文本
        /// </summary>
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
