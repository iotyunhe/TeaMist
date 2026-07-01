using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 店铺属性数据 — 茶馆/药材铺/客栈/画坊的共享经营属性
    /// 四店共用一套数据结构，运行时独立实例
    /// </summary>
    [CreateAssetMenu(fileName = "Shop_", menuName = "TeaMist/店铺配置", order = 4)]
    public class ShopPropertySO : ScriptableObject
    {
        [Header("店铺标识")]
        public ShopType shopType = ShopType.茶馆;
        public string shopName = "无名小店";

        [Header("经营状态")]
        [Tooltip("当前经营等级")]
        [Range(1, 10)]
        public int level = 1;

        [Tooltip("名声等级 (0-5)")]
        [Range(0, 5)]
        public int reputationLevel = 0;

        [Tooltip("名声经验值")]
        [Range(0f, 1000f)]
        public float reputationXP = 0f;

        [Tooltip("解锁所需总碎片数")]
        public int unlockFragmentTotal = 0;

        [Tooltip("已收集的经营碎片数")]
        public int collectedFragments = 0;

        [Header("装修 — 气质标签")]
        [Tooltip("幽静值")]
        [Range(0f, 100f)]
        public float sereneValue = 10f;

        [Tooltip("温暖值")]
        [Range(0f, 100f)]
        public float warmValue = 10f;

        [Tooltip("雅致值")]
        [Range(0f, 100f)]
        public float elegantValue = 10f;

        [Tooltip("趣味值")]
        [Range(0f, 100f)]
        public float playfulValue = 10f;

        [Tooltip("药香值（仅药材铺）")]
        [Range(0f, 100f)]
        public float herbalValue = 0f;

        [Header("容量")]
        [Tooltip("最多同时接待客人数")]
        [Range(1, 10)]
        public int maxGuests = 3;

        [Tooltip("已摆放坐席数")]
        [Range(0, 10)]
        public int seatCount = 3;

        [Header("每日营收（参考值）")]
        [Tooltip("单客基础营收")]
        public float baseRevenuePerGuest = 10f;

        [Tooltip("完美泡茶营收倍率")]
        [Range(1f, 3f)]
        public float perfectBrewMultiplier = 1.5f;

        [Header("升级")]
        [Tooltip("升级所需名声等级要求")]
        public int[] levelUpReputationRequirements = { 1, 2, 3, 4, 5 };

        [Tooltip("升级碎片消耗")]
        public int[] levelUpFragmentCosts = { 5, 10, 20, 40, 80 };

        [Header("装饰物品")]
        [Tooltip("已解锁装饰物 ID 列表")]
        public string[] unlockedDecorations = System.Array.Empty<string>();

        [Tooltip("当前装饰风格")]
        public string activeStyle = "默认";
    }
}
