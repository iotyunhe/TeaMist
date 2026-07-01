using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 对话配置 — Yarn Spinner 对话的元数据和触发条件
    /// 不替代 .yarn 文件，而是描述何时/如何触发对话
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_", menuName = "TeaMist/对话配置", order = 6)]
    public class DialogueConfigSO : ScriptableObject
    {
        [Header("标识")]
        public string dialogueId = "D000";
        public string dialogueTitle = "未命名对话";
        public DialogueCategory category = DialogueCategory.日常故事;

        [Header("对话源")]
        [Tooltip("Yarn Spinner 节点名称")]
        public string yarnNodeName = "";

        [Tooltip(".yarn 文件名（不含扩展名）")]
        public string yarnFileName = "";

        [Header("触发条件")]
        [Tooltip("关联 NPC（空=任意NPC可触发）")]
        public string relatedNPC = "";

        [Tooltip("最低好感度")]
        [Range(0f, 1f)]
        public float minAffection = 0f;

        [Tooltip("最高好感度（1=无上限）")]
        [Range(0f, 1f)]
        public float maxAffection = 1f;

        [Tooltip("要求季节")]
        public Season[] requiredSeasons = System.Array.Empty<Season>();

        [Tooltip("要求天气")]
        public WeatherType[] requiredWeather = System.Array.Empty<WeatherType>();

        [Tooltip("要求时间带")]
        public DayTimeSlot[] requiredTimeSlots = System.Array.Empty<DayTimeSlot>();

        [Tooltip("要求碎片已收集")]
        public string[] requiredFragments = System.Array.Empty<string>();

        [Tooltip("要求店铺类型")]
        public ShopType requiredShop = ShopType.茶馆;

        [Header("优先级")]
        [Tooltip("触发优先级（同条件时优先级高的胜出）")]
        [Range(0, 100)]
        public int priority = 0;

        [Tooltip("是否为一次性对话")]
        public bool isOneShot = true;

        [Tooltip("可重复触发的冷却天数 (0=每次均可触发)")]
        [Range(0, 365)]
        public int cooldownDays = 0;

        [Header("回报")]
        [Tooltip("完成对话获得的碎片 ID")]
        public string[] rewardFragmentIds = System.Array.Empty<string>();

        [Tooltip("完成对话获得的好感度变化")]
        [Range(-5f, 5f)]
        public float affectionChange = 0.5f;

        [Tooltip("完成对话获得的经营经验")]
        [Range(0f, 100f)]
        public float reputationXP = 10f;
    }

    public enum DialogueCategory
    {
        [InspectorName("日常故事")]
        日常故事,
        [InspectorName("人物线主线")]
        人物线主线,
        [InspectorName("季节事件")]
        季节事件,
        [InspectorName("夜晚私聊")]
        夜晚私聊,
        [InspectorName("山中秘密")]
        山中秘密,
        [InspectorName("彩蛋")]
        彩蛋
    }
}
