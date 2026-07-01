using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 季节配置 — 四季节气驱动全局参数
    /// 控制 Shader 全局变量、天气概率、限时内容、采集资源
    /// </summary>
    [CreateAssetMenu(fileName = "Season_", menuName = "TeaMist/季节配置", order = 5)]
    public class SeasonConfigSO : ScriptableObject
    {
        [Header("季节标识")]
        public Season season = Season.春;

        [Tooltip("节气名称")]
        public string solarTerms = "立春·雨水·惊蛰";

        [Header("全局 Shader 参数")]
        [Tooltip("季节色调偏移 (InkMasterCombine._SeasonShift)")]
        [Range(-0.1f, 0.1f)]
        public float hueShift = 0f;

        [Tooltip("季节饱和度 (InkMasterCombine._SeasonSat)")]
        [Range(0f, 2f)]
        public float saturation = 0.8f;

        [Tooltip("季节暖色")]
        public Color warmTint = new Color(0.88f, 0.92f, 0.78f);

        [Tooltip("季节影响度")]
        [Range(0f, 1f)]
        public float blendFactor = 0.3f;

        [Header("天气概率 (%)")]
        [Range(0, 100)] public int sunnyChance  = 50;
        [Range(0, 100)] public int cloudyChance = 20;
        [Range(0, 100)] public int rainChance   = 15;
        [Range(0, 100)] public int snowChance   = 0;
        [Range(0, 100)] public int fogChance    = 10;
        [Range(0, 100)] public int windChance   = 5;

        [Header("季节过渡（天数）")]
        [Tooltip("季节过渡持续天数")]
        [Range(1, 10)]
        public int transitionDays = 3;

        [Tooltip("过渡过程中使用的色调偏移插值因子")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("可采集资源")]
        [Tooltip("本季节可采集的草药 ID 列表")]
        public string[] availableHerbs = System.Array.Empty<string>();

        [Tooltip("本季节可采集的茶叶 ID 列表")]
        public string[] availableTeaLeaves = System.Array.Empty<string>();

        [Header("限时内容")]
        [Tooltip("本季节专属的客人 NPC 名称")]
        public string[] seasonExclusiveNPCs = System.Array.Empty<string>();

        [Tooltip("本季节专属碎片 ID")]
        public string[] seasonExclusiveFragments = System.Array.Empty<string>();

        [Tooltip("本季节节日事件")]
        public SeasonalEvent[] seasonalEvents = System.Array.Empty<SeasonalEvent>();

        [Header("音频")]
        [Tooltip("季节 BGM 名称")]
        public string bgmName = "Spring_Light";

        [Tooltip("季节环境音名称")]
        public string ambientSound = "Spring_Birds";
    }

    [System.Serializable]
    public struct SeasonalEvent
    {
        [Tooltip("事件名称")]
        public string eventName;

        [Tooltip("事件触发日 (季节开始后第几天)")]
        [Range(1, 90)]
        public int triggerDay;

        [Tooltip("事件持续天数")]
        [Range(1, 30)]
        public int duration;

        [Tooltip("事件描述")]
        [TextArea(1, 3)]
        public string description;

        [Tooltip("事件触发 Yarn 节点名")]
        public string yarnNodeName;
    }
}
