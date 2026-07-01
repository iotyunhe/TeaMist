using UnityEngine;
using System;
using TeaMist.Gameplay;

namespace TeaMist.Data
{
    /// <summary>
    /// 茶谱数据 — 定义一种可泡制的茶品
    /// 在 Inspector 中直接创建和管理，策划友好
    /// </summary>
    [CreateAssetMenu(fileName = "Tea_", menuName = "TeaMist/茶谱", order = 1)]
    public class TeaRecipeSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("茶品唯一ID（用于剧本引用）")]
        public string recipeId = "default_tea";

        [Tooltip("茶品名称")]
        public string teaName = "新茶";

        [Tooltip("茶品显示名（别名）")]
        public string recipeName => teaName;

        [Tooltip("茶品描述（展示给玩家）")]
        [TextArea(2, 4)]
        public string description = "一味尚未命名的茶。";

        [Tooltip("所属谱系")]
        public TeaFamily family = TeaFamily.基础;

        [Tooltip("茶品风味类型")]
        public FlavorType flavorProfile = FlavorType.清香;

        [Tooltip("季节关联（春/夏/秋/冬/不限）")]
        public TeaSeason season = TeaSeason.不限;

        [Tooltip("茶品稀有度")]
        public TeaRarity rarity = TeaRarity.常见;

        [Header("六维属性")]
        [Range(0f, 10f)] public float warmth    = 5f;  // 温性
        [Range(0f, 10f)] public float coolness   = 5f;  // 凉性
        [Range(0f, 10f)] public float bitterness = 5f;  // 苦
        [Range(0f, 10f)] public float sweetness  = 5f;  // 甘
        [Range(0f, 10f)] public float astringency = 5f; // 涩
        [Range(0f, 10f)] public float smoothness = 5f;  // 醇

        [Header("泡制参数")]
        [Tooltip("推荐水温 (℃)")]
        [Range(60f, 100f)]
        public float idealTemperature = 85f;

        [Tooltip("推荐冲泡时间 (秒)")]
        [Range(10f, 300f)]
        public float idealSteepTime = 60f;

        [Tooltip("最适合的茶具类型")]
        public TeawareType idealTeaware = TeawareType.瓷壶;

        [Tooltip("理想保温系数 (0~2)")]
        [Range(0f, 2f)]
        public float idealHeatRetention = 1.0f;

        [Tooltip("推荐注水手法")]
        public PourStyle idealPourStyle = PourStyle.MidSpiral;

        [Header("解锁条件")]
        [Tooltip("解锁此茶谱需要的经营碎片数")]
        public int unlockFragmentCost = 0;

        [Tooltip("解锁要求的茶馆名声等级 (0=初始可用)")]
        [Range(0, 5)]
        public int unlockReputationLevel = 0;

        [Tooltip("特殊解锁条件（如：连续三次泡对同一NPC的茶后解锁）")]
        [TextArea(1, 2)]
        public string specialUnlockHint = "";

        [Header("情绪加成")]
        [Tooltip("适合消除的心情标签")]
        public MoodTag[] curesMoods = System.Array.Empty<MoodTag>();

        [Tooltip("不适合的心情标签")]
        public MoodTag[] worsensMoods = System.Array.Empty<MoodTag>();

        [Header("UI")]
        [Tooltip("茶品图标")]
        public Sprite icon;

        [Tooltip("茶汤颜色")]
        public Color liquorColor = new Color(0.6f, 0.4f, 0.2f);

        // ━━━ 匹配方法 ━━━

        /// <summary>
        /// 计算茶具材质匹配度 (0~1)
        /// </summary>
        public float MatchMaterial(string materialType)
        {
            // 紫砂壶 → 红茶/乌龙茶最佳
            // 瓷壶 → 绿茶/花茶最佳
            // 玻璃壶 → 绿茶/花茶
            // 粗陶 → 黑茶/药茶
            // 竹筒 → 药茶/灵茶
            // 银壶 → 白茶/灵茶

            var materialMap = new System.Collections.Generic.Dictionary<TeawareType, string>
            {
                { TeawareType.紫砂壶, "紫砂" },
                { TeawareType.瓷壶, "瓷" },
                { TeawareType.盖碗, "瓷" },
                { TeawareType.玻璃壶, "玻璃" },
                { TeawareType.陶壶, "粗陶" },
                { TeawareType.银壶, "银壶" },
                { TeawareType.竹筒, "竹筒" }
            };

            string idealMaterial = materialMap.ContainsKey(idealTeaware)
                ? materialMap[idealTeaware] : "";

            if (materialType == idealMaterial) return 1f;

            // 退一步：盖碗和瓷壶都算"瓷"类
            if (materialType == "瓷" && (idealTeaware == TeawareType.瓷壶 || idealTeaware == TeawareType.盖碗))
                return 0.8f;
            if ((materialType == "紫砂" || materialType == "粗陶") &&
                (idealTeaware == TeawareType.紫砂壶 || idealTeaware == TeawareType.陶壶))
                return 0.7f;

            return 0.3f;
        }

        /// <summary>
        /// 计算注水手法匹配度 (0~1)
        /// </summary>
        public float MatchPourStyle(PourStyle style)
        {
            if (style == idealPourStyle) return 1f;

            // 相似手法部分匹配
            bool isSimilar = (style == PourStyle.HighSlow && idealPourStyle == PourStyle.MidSpiral) ||
                             (style == PourStyle.MidSpiral && idealPourStyle == PourStyle.HighSlow) ||
                             (style == PourStyle.LowFast && idealPourStyle == PourStyle.EdgePour) ||
                             (style == PourStyle.EdgePour && idealPourStyle == PourStyle.LowFast);
            if (isSimilar) return 0.6f;

            return 0.2f;
        }
    }

    // ── 枚举 ──

    public enum TeaFamily
    {
        [InspectorName("基础茶")]
        基础,
        [InspectorName("绿茶")]
        绿茶,
        [InspectorName("红茶")]
        红茶,
        [InspectorName("乌龙茶")]
        乌龙茶,
        [InspectorName("白茶")]
        白茶,
        [InspectorName("黑茶")]
        黑茶,
        [InspectorName("花茶")]
        花茶,
        [InspectorName("药茶")]
        药茶,
        [InspectorName("灵茶")]
        灵茶
    }

    public enum TeaRarity
    {
        [InspectorName("常见")]
        常见,
        [InspectorName("少见")]
        少见,
        [InspectorName("稀有")]
        稀有,
        [InspectorName("传说")]
        传说
    }

    public enum TeawareType
    {
        [InspectorName("紫砂壶")]
        紫砂壶,
        [InspectorName("瓷壶")]
        瓷壶,
        [InspectorName("盖碗")]
        盖碗,
        [InspectorName("玻璃壶")]
        玻璃壶,
        [InspectorName("陶壶")]
        陶壶,
        [InspectorName("银壶")]
        银壶,
        [InspectorName("竹筒")]
        竹筒
    }

    public enum MoodTag
    {
        [InspectorName("忧愁")]
        忧愁,
        [InspectorName("烦躁")]
        烦躁,
        [InspectorName("疲惫")]
        疲惫,
        [InspectorName("思念")]
        思念,
        [InspectorName("喜悦")]
        喜悦,
        [InspectorName("迷茫")]
        迷茫
    }

    public enum TeaSeason
    {
        [InspectorName("不限")]
        不限,
        [InspectorName("春")]
        春,
        [InspectorName("夏")]
        夏,
        [InspectorName("秋")]
        秋,
        [InspectorName("冬")]
        冬
    }

    public enum FlavorType
    {
        [InspectorName("清香")]
        清香,
        [InspectorName("花香")]
        花香,
        [InspectorName("果香")]
        果香,
        [InspectorName("甜润")]
        甜润,
        [InspectorName("醇厚")]
        醇厚,
        [InspectorName("清爽")]
        清爽,
        [InspectorName("药香")]
        药香
    }
}
