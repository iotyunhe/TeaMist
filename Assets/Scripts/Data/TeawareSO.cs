using UnityEngine;

namespace TeaMist.Data
{
    /// <summary>
    /// 茶具数据 — 定义一种可用于泡茶的壶具。
    /// 每种茶具独立为 .asset 文件，可在 Inspector 中创建和管理。
    /// </summary>
    [CreateAssetMenu(fileName = "Teaware_", menuName = "TeaMist/茶具", order = 2)]
    public class TeawareSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("茶具显示名称")]
        public string displayName = "新壶";

        [Tooltip("材质类型 — 紫砂/瓷/玻璃/粗陶/竹筒/银壶")]
        public string materialType = "瓷";

        [Header("属性")]
        [Tooltip("保温性 (0~2)。越高水温衰减越慢。")]
        [Range(0f, 2f)]
        public float heatRetention = 1.0f;

        [Tooltip("提香 (0~2)。越高香气保留越好。")]
        [Range(0f, 2f)]
        public float fragranceBoost = 1.0f;

        [Header("描述")]
        [Tooltip("茶具描述（展示给玩家）")]
        [TextArea(2, 4)]
        public string description = "一把普通的壶。";
    }
}
