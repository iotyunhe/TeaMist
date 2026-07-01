using System;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 存档数据结构 — JSON 序列化的全部运行时状态。
    /// 注意：JsonUtility 不支持 Dictionary，所有键值对均用 [Serializable] List 存。
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int saveVersion = 1;
        public string saveName = "自动存档";
        public string saveTimestamp;

        // ── 时间 ──
        public string lastPlayDate;    // yyyy-MM-dd
        public int totalDaysPlayed;
        public Season currentSeason;
        public int dayInSeason;        // 1-90
        public WeatherType currentWeather;
        public DayTimeSlot currentTimeSlot;

        // ── 玩家 ──
        public PlayerSaveData player = new PlayerSaveData();

        // ── 店铺 ──
        public List<ShopSaveData> shops = new List<ShopSaveData>();

        // ── NPC ──
        public List<NPCSaveData> npcs = new List<NPCSaveData>();

        // ── 碎片 ──
        public List<string> collectedFragments = new List<string>();

        // ── 世界状态（可序列化的键值对列表） ──
        public List<StringPair> worldState = new List<StringPair>();

        // ── 对话历史 ──
        public List<string> completedDialogues = new List<string>();
        public List<StringPair> dialogueCooldowns = new List<StringPair>(); // nodeId → last triggered date

        // ── 对话变量存储（好感度 / flags / vars / 玩家特质） ──
        public List<IntPair> savedAffection = new List<IntPair>();    // npcId → value
        public List<string> savedFlags = new List<string>();
        public List<StringPair> savedVars = new List<StringPair>();
        public int playerWarmth;
        public int playerWit;
        public int playerCalmness;
        public int playerCuriosity;

        // ── 经营 ──
        public float totalRevenue;
        public int totalGuestsServed;
        public int perfectBrews;
        public List<string> unlockedTeaRecipes = new List<string>();
        public List<string> unlockedHerbRecipes = new List<string>();
        public List<string> collectedHerbs = new List<string>();
    }

    // ━━━ 可序列化键值对（JsonUtility 兼容） ━━━

    /// <summary>两个字符串的键值对</summary>
    [System.Serializable]
    public struct StringPair
    {
        public string key;
        public string value;
    }

    /// <summary>字符串→整数的键值对</summary>
    [System.Serializable]
    public struct IntPair
    {
        public string key;
        public int value;
    }

    /// <summary>字符串→浮点数的键值对</summary>
    [System.Serializable]
    public struct FloatPair
    {
        public string key;
        public float value;
    }

    // ━━━ 子结构 ━━━

    [System.Serializable]
    public class PlayerSaveData
    {
        public string playerName = "掌柜";
        public int selectedBackground = 0; // 0=逃避, 1=宿命, 2=召唤
        public float totalPlayTimeMinutes;
    }

    [System.Serializable]
    public class ShopSaveData
    {
        public ShopType shopType;
        public string shopName;
        public int level;
        public int reputationLevel;
        public float reputationXP;
        public int collectedFragments;
        public int maxGuests;
        public int seatCount;
        public float sereneValue;
        public float warmValue;
        public float elegantValue;
        public float playfulValue;
        public float herbalValue;
        public List<string> unlockedDecorations = new List<string>();
        public string activeStyle;
    }

    [System.Serializable]
    public class NPCSaveData
    {
        public string npcId;         // matches NPCProfileSO.npcName
        public float affection;
        public int chapter;
        public int visitCount;
        public string lastVisitDate;
        public List<string> memories = new List<string>(); // 记忆 key
        public List<string> completedChapters = new List<string>();
        public List<FloatPair> relationshipValues = new List<FloatPair>(); // npcName → value
    }
}
