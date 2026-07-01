using UnityEngine;
using System;
using System.Collections.Generic;

namespace TeaMist.Data
{
    /// <summary>
    /// NPC 角色数据 — 定义一位客人的全部属性
    /// 驱动 NPC 生态系统中出场、对话、好感度变化
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_", menuName = "TeaMist/NPC档案", order = 2)]
    public class NPCProfileSO : ScriptableObject
    {
        [Header("基础身份")]
        public string npcName = "无名";
        public NPCSpecies species = NPCSpecies.凡人;
        public NPCRole role = NPCRole.核心;

        [TextArea(3, 6)]
        public string bio = "一位路过的旅人。";

        [Header("视觉印象")]
        [TextArea(1, 3)]
        public string visualKeywords = "素衣，竹杖，斗笠";

        public Sprite portrait;
        public Color signatureColor = Color.white; // 角色代表色，用于 UI 和特效

        [Header("日程与偏好")]
        [Tooltip("最常出现的季节")]
        public Season[] preferredSeasons = { Season.春, Season.秋 };

        [Tooltip("偏好天气")]
        public WeatherType[] preferredWeather = { WeatherType.晴, WeatherType.多云 };

        [Tooltip("偏好的茶馆时间带")]
        public DayTimeSlot[] preferredTimeSlots = { DayTimeSlot.午后 };

        [Tooltip("每周最大出现次数")]
        [Range(1, 7)]
        public int maxVisitsPerWeek = 3;

        [Tooltip("连续出现后的强制休息天数")]
        [Range(0, 7)]
        public int cooldownDays = 1;

        [Header("口味 — 六维偏好")]
        [Range(-5f, 5f)] public float prefWarmth     = 0f;
        [Range(-5f, 5f)] public float prefCoolness    = 0f;
        [Range(-5f, 5f)] public float prefBitterness  = 0f;
        [Range(-5f, 5f)] public float prefSweetness   = 0f;
        [Range(-5f, 5f)] public float prefAstringency = 0f;
        [Range(-5f, 5f)] public float prefSmoothness  = 0f;

        [Tooltip("最爱的茶具")]
        public TeawareType favoriteTeaware = TeawareType.瓷壶;

        [Header("关系网络")]
        [Tooltip("与哪些 NPC 有特殊关系")]
        public RelationshipEntry[] relationships = Array.Empty<RelationshipEntry>();

        [Header("故事线")]
        [Tooltip("对话 .yarn 文件名（不含扩展名）")]
        public string yarnNodePrefix = "";

        [Tooltip("个人故事总章节数")]
        [Range(0, 12)]
        public int totalChapters = 0;

        [Tooltip("当前解锁的最高章节")]
        [Range(0, 12)]
        public int currentChapter = 0;

        [Header("碎片产出")]
        [Tooltip("此 NPC 可产出的叙事碎片 ID 列表")]
        public string[] fragmentIds = Array.Empty<string>();

        [Header("深访标识")]
        [Tooltip("是否会出现深夜单独来访")]
        public bool canNightVisit = false;

        [Tooltip("夜访触发的最低好感度")]
        [Range(0f, 1f)]
        public float nightVisitAffectionThreshold = 0.6f;

        [Header("特殊能力 / 机制（游戏性）")]
        [Tooltip("此 NPC 对经营系统的特殊效果描述")]
        [TextArea(1, 2)]
        public string specialAbility = "";

        // ── 运行时字段（存档时不存 ScriptableObject，存于 SaveData） ──
        // 这些标记为 NonSerialized，仅用于编辑器预览
        [System.NonSerialized] public float runtimeAffection = 0f;
        [System.NonSerialized] public int  runtimeVisitCount = 0;
        [System.NonSerialized] public System.DateTime runtimeLastVisit;
    }

    // ── 枚举 ──

    public enum NPCSpecies
    {
        [InspectorName("凡人")]
        凡人,
        [InspectorName("狐妖")]
        狐妖,
        [InspectorName("鹤仙")]
        鹤仙,
        [InspectorName("器物妖")]
        器物妖,
        [InspectorName("草木妖")]
        草木妖,
        [InspectorName("石灵")]
        石灵,
        [InspectorName("节气灵")]
        节气灵,
        [InspectorName("未知")]
        未知
    }

    public enum NPCRole
    {
        [InspectorName("核心角色")]
        核心,
        [InspectorName("过路客")]
        过路客,
        [InspectorName("山中灵")]
        山中灵
    }

    public enum Season
    {
        春, 夏, 秋, 冬
    }

    public enum WeatherType
    {
        晴, 多云, 雨, 雪, 雾, 风, 雷
    }

    public enum DayTimeSlot
    {
        清晨, 上午, 午后, 傍晚, 深夜
    }

    [System.Serializable]
    public struct RelationshipEntry
    {
        [Tooltip("关联的 NPC 名称")]
        public string npcName;

        [Tooltip("关系描述")]
        public string relationDescription;

        [Tooltip("初始关系值 -1到1")]
        [Range(-1f, 1f)]
        public float initialAffinity;
    }
}
