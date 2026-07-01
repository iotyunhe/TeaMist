using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 碎片数据 — 叙事碎片或经营碎片的完整定义
    /// 存储在"墨笺录"和"山的故事"长卷中
    /// </summary>
    [CreateAssetMenu(fileName = "Fragment_", menuName = "TeaMist/碎片", order = 3)]
    public class FragmentSO : ScriptableObject
    {
        [Header("基础信息")]
        public string fragmentId = "F000";
        public string fragmentTitle = "未命名的碎片";
        public FragmentType fragmentType = FragmentType.叙事;

        [TextArea(3, 8)]
        public string content = "这是一片尚未被解读的记忆。";

        [Header("分类")]
        [Tooltip("所属章节（山的故事 9 章）")]
        [Range(1, 9)]
        public int chapter = 1;

        [Tooltip("碎片获取方式")]
        public FragmentSource source = FragmentSource.NPC对话;

        [Header("获取条件")]
        [Tooltip("关联 NPC（仅 source 为 NPC 时有效）")]
        public string relatedNPC = "";

        [Tooltip("最低好感度要求")]
        [Range(0f, 1f)]
        public float requiredAffection = 0f;

        [Tooltip("要求天气")]
        public WeatherType requiredWeather = WeatherType.晴;

        [Tooltip("要求季节")]
        public Season requiredSeason = Season.春;

        [Tooltip("要求时间带")]
        public DayTimeSlot requiredTimeSlot = DayTimeSlot.午后;

        [Tooltip("要求茶馆名声等级")]
        [Range(0, 5)]
        public int requiredReputation = 0;

        [Tooltip("前置碎片 ID（需要先获取的碎片）")]
        public string[] prerequisiteFragments = System.Array.Empty<string>();

        [Header("解锁连锁")]
        [Tooltip("获取此碎片后自动解锁的碎片 ID")]
        public string[] unlocksFragments = System.Array.Empty<string>();

        [Tooltip("获取此碎片后触发的世界状态变化")]
        public WorldStateChange[] worldChanges = System.Array.Empty<WorldStateChange>();

        [Header("经营碎片专用")]
        [Tooltip("是否为经营类碎片（用于解锁新店铺）")]
        public bool isBusinessFragment = false;

        [Tooltip("解锁店铺（仅 isBusinessFragment=true）")]
        public ShopType unlocksShop = ShopType.无;

        [Header("UI 展示")]
        public Sprite icon;
        public Color highlightColor = new Color(0.8f, 0.6f, 0.2f);

        [Header("特殊标记")]
        [Tooltip("是否为关键碎片（错过有提示）")]
        public bool isCritical = false;

        [Tooltip("是否为季节性限定")]
        public bool isSeasonal = false;
    }

    // ── 枚举 ──

    public enum FragmentType
    {
        [InspectorName("叙事碎片")]
        叙事,
        [InspectorName("经营碎片")]
        经营,
        [InspectorName("彩蛋碎片")]
        彩蛋,
        [InspectorName("记忆碎片")]
        记忆
    }

    public enum FragmentSource
    {
        [InspectorName("NPC对话")]
        NPC对话,
        [InspectorName("泡茶成功")]
        泡茶成功,
        [InspectorName("场所探索")]
        场所探索,
        [InspectorName("时间节点")]
        时间节点,
        [InspectorName("拼图解锁")]
        拼图解锁,
        [InspectorName("季节事件")]
        季节事件
    }

    public enum ShopType
    {
        无, 茶馆, 药材铺, 客栈, 画坊
    }

    [System.Serializable]
    public struct WorldStateChange
    {
        [Tooltip("改变的键")]
        public string key;

        [Tooltip("新值")]
        public string value;
    }
}
