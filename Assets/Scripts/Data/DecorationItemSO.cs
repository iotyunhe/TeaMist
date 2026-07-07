using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 装饰物品定义 —— 每件装饰物提供气质加成和特殊效果。
    /// 四店通用：茶馆/药材铺/客栈/画坊都可以摆放装饰。
    /// </summary>
    [CreateAssetMenu(fileName = "Decoration_", menuName = "TeaMist/装饰物品", order = 5)]
    public class DecorationItemSO : ScriptableObject
    {
        [Header("标识")]
        public string decorationId;
        public string displayName = "未命名装饰";
        public string description = "";

        [Header("解锁条件")]
        [Tooltip("需要茶馆等级达到此值才能解锁")]
        [Range(1, 10)]
        public int requiredLevel = 1;

        [Tooltip("需要收集的碎片总数（0=无碎片要求）")]
        public int requiredFragments = 0;

        [Tooltip("需要特定季节才能解锁（-1=不限，0=春，1=夏，2=秋，3=冬）")]
        public int requiredSeason = -1;

        [Header("气质加成")]
        [Tooltip("幽静值加成")]
        public float sereneBonus = 0f;

        [Tooltip("温暖值加成")]
        public float warmBonus = 0f;

        [Tooltip("雅致值加成")]
        public float elegantBonus = 0f;

        [Tooltip("趣味值加成")]
        public float playfulBonus = 0f;

        [Tooltip("药香值加成（仅药材铺有效）")]
        public float herbalBonus = 0f;

        [Header("经营效果")]
        [Tooltip("额外经验获取百分比加成（0-1）")]
        [Range(0f, 1f)]
        public float xpBonusPercent = 0f;

        [Tooltip("额外营收加成百分比（0-1）")]
        [Range(0f, 1f)]
        public float revenueBonusPercent = 0f;

        [Tooltip("额外好感度获取加成百分比（0-1）")]
        [Range(0f, 1f)]
        public float affectionBonusPercent = 0f;

        [Header("视觉")]
        [Tooltip("适用的店铺类型（空=所有店铺）")]
        public ShopType[] applicableShops = System.Array.Empty<ShopType>();

        [Tooltip("装饰风格标签")]
        public string styleTag = "默认";

        [Tooltip("稀有度")]
        public DecorationRarity rarity = DecorationRarity.Common;

        /// <summary>
        /// 计算该装饰在指定店铺类型的总加成
        /// </summary>
        public float GetTotalBonus()
        {
            return sereneBonus + warmBonus + elegantBonus + playfulBonus + herbalBonus;
        }
    }

    /// <summary>
    /// 装饰物稀有度
    /// </summary>
    public enum DecorationRarity
    {
        Common,     // 普通 — 初始可解锁
        Uncommon,   // 精良 — 需要一定等级
        Rare,       // 稀有 — 需要高等级+碎片
        Epic,       // 史诗 — 需要特殊条件
        Legend      // 传说 — 隐藏装饰
    }
}
