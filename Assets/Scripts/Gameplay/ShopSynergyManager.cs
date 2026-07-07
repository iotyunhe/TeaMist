using UnityEngine;
using System.Collections.Generic;
using TeaMist.Core;
using TeaMist.Data;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 四业共享管理器 —— 茶馆/药材铺/客栈/画坊的联动经营系统。
    /// 
    /// 核心机制：
    /// - 四店各有独立等级和气质，但共享"山间名声"
    /// - 店铺之间的气质互补产生"共鸣加成"
    /// - 特定 NPC 对特定店铺有偏好，偏好店铺中接待获得额外好感
    /// - 升级解锁跨店任务和内容
    /// </summary>
    public class ShopSynergyManager : MonoBehaviour
    {
        public static ShopSynergyManager Instance { get; private set; }

        [Header("━━━ 共鸣参数 ━━━")]
        [Tooltip("共鸣触发阈值：两店气质差值小于此值时触发共鸣")]
        public float resonanceThreshold = 15f;

        [Tooltip("共鸣加成倍率")]
        public float resonanceMultiplier = 1.2f;

        [Header("━━━ 山间名声 ━━━")]
        [Tooltip("山间名声（四店共享的全局声望）")]
        public float mountainReputation = 0f;

        [Tooltip("山间名声等级")]
        public int mountainReputationLevel = 0;

        [Tooltip("山间名声等级阈值")]
        public float[] mountainLevelThresholds = { 100f, 300f, 600f, 1000f, 1500f };

        // ── 运行时 ──
        private Dictionary<string, float> _npcShopPreference = new Dictionary<string, float>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ━━━ 公开 API ━━━

        /// <summary>
        /// 获取当前共鸣加成（基于四店气质均衡度）
        /// </summary>
        public float GetResonanceMultiplier()
        {
            if (DataManager.Instance == null) return 1f;

            var shops = new ShopPropertySO[]
            {
                DataManager.Instance.GetShopConfig(ShopType.茶馆),
                DataManager.Instance.GetShopConfig(ShopType.药材铺),
                DataManager.Instance.GetShopConfig(ShopType.客栈),
                DataManager.Instance.GetShopConfig(ShopType.画坊)
            };

            // 计算四店总气质的标准差，标准差越小越均衡
            float[] totalAuras = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (shops[i] == null) { totalAuras[i] = 0; continue; }
                totalAuras[i] = shops[i].sereneValue + shops[i].warmValue +
                                shops[i].elegantValue + shops[i].playfulValue;
            }

            float avg = 0f;
            foreach (var v in totalAuras) avg += v;
            avg /= 4f;

            float variance = 0f;
            foreach (var v in totalAuras) variance += (v - avg) * (v - avg);
            variance /= 4f;
            float stdDev = Mathf.Sqrt(variance);

            // 标准差越小，共鸣越强
            if (stdDev < resonanceThreshold)
            {
                float t = 1f - (stdDev / resonanceThreshold);
                return 1f + t * (resonanceMultiplier - 1f);
            }
            return 1f;
        }

        /// <summary>
        /// 获取 NPC 对当前接待店铺的偏好加成
        /// </summary>
        public float GetNPCShopPreference(string npcId, ShopType shopType)
        {
            // 基于 NPC 性格和店铺气质的匹配度
            if (DataManager.Instance == null) return 1f;

            var shop = DataManager.Instance.GetShopConfig(shopType);
            var npc = DataManager.Instance.GetNPCProfile(npcId);
            if (shop == null || npc == null) return 1f;

            // NPC 口味偏好与店铺气质匹配
            // 甘甜 → 温暖值，清香 → 幽静值，醇厚 → 雅致值，苦涩 → 药香值
            float matchScore = 0f;
            float totalWeight = 0f;

            // 甘甜偏好 → 温暖
            float sweetW = Mathf.Abs(npc.prefSweetness);
            if (sweetW > 0.1f)
            {
                matchScore += (npc.prefSweetness > 0 ? shop.warmValue : 100f - shop.warmValue) * sweetW / 5f;
                totalWeight += sweetW;
            }
            // 清香偏好 → 幽静
            float astrW = Mathf.Abs(npc.prefAstringency);
            if (astrW > 0.1f)
            {
                matchScore += (npc.prefAstringency > 0 ? shop.sereneValue : 100f - shop.sereneValue) * astrW / 5f;
                totalWeight += astrW;
            }
            // 醇厚偏好 → 雅致
            float warmW = Mathf.Abs(npc.prefWarmth);
            if (warmW > 0.1f)
            {
                matchScore += (npc.prefWarmth > 0 ? shop.elegantValue : 100f - shop.elegantValue) * warmW / 5f;
                totalWeight += warmW;
            }
            // 苦涩偏好 → 药香
            float bitterW = Mathf.Abs(npc.prefBitterness);
            if (bitterW > 0.1f)
            {
                matchScore += (npc.prefBitterness > 0 ? shop.herbalValue : 100f - shop.herbalValue) * bitterW / 5f;
                totalWeight += bitterW;
            }

            if (totalWeight <= 0f) return 1f;
            float normalized = matchScore / (totalWeight * 20f); // 归一化到 0-1
            return 1f + Mathf.Clamp01(normalized) * 0.5f; // 最高 1.5 倍
        }

        /// <summary>
        /// 当任何店铺获得经验时，同步更新山间名声
        /// </summary>
        public void OnShopXPGained(ShopType shopType, float xp)
        {
            // 山间名声获得所有店铺经验的 20%
            float sharedXP = xp * 0.2f;
            mountainReputation += sharedXP;
            CheckMountainLevelUp();
        }

        /// <summary>
        /// 获取山间名声等级
        /// </summary>
        public int GetMountainLevel() => mountainReputationLevel;

        /// <summary>
        /// 获取山间名声进度
        /// </summary>
        public float GetMountainProgress()
        {
            if (mountainReputationLevel >= mountainLevelThresholds.Length)
                return mountainLevelThresholds[mountainLevelThresholds.Length - 1];
            return mountainLevelThresholds[mountainReputationLevel];
        }

        /// <summary>
        /// 山间名声等级是否已达到（用于解锁跨店内容）
        /// </summary>
        public bool IsMountainLevelReached(int level)
        {
            return mountainReputationLevel >= level;
        }

        // ━━━ 内部 ━━━

        private void CheckMountainLevelUp()
        {
            while (mountainReputationLevel < mountainLevelThresholds.Length &&
                   mountainReputation >= mountainLevelThresholds[mountainReputationLevel])
            {
                mountainReputation -= mountainLevelThresholds[mountainReputationLevel];
                mountainReputationLevel++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ShopSynergy] 山间名声升级！等级 {mountainReputationLevel}");
#endif
                // 解锁跨店内容
                OnMountainLevelUnlocked(mountainReputationLevel);
            }
        }

        private void OnMountainLevelUnlocked(int level)
        {
            switch (level)
            {
                case 1:
                    Debug.Log("[ShopSynergy] 解锁：跨店八卦 —— NPC 开始在其他店铺间传递消息");
                    break;
                case 2:
                    Debug.Log("[ShopSynergy] 解锁：联合经营 —— 茶馆客人可以去药材铺买药");
                    break;
                case 3:
                    Debug.Log("[ShopSynergy] 解锁：山间盛事 —— 四店联合举办节日活动");
                    break;
                case 4:
                    Debug.Log("[ShopSynergy] 解锁：传说装饰 —— 解锁传说级装饰物品");
                    break;
                case 5:
                    Debug.Log("[ShopSynergy] 解锁：栖霞之巅 —— 全部四业共鸣，最终形态");
                    break;
            }
        }

        // ━━━ 调试面板 ━━━

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.82f, 0.70f) }
            };

            float x = 10, y = Screen.height - 130;
            float resonance = GetResonanceMultiplier();

            GUI.Label(new Rect(x, y, 280, 40),
                $"山间名声 Lv.{mountainReputationLevel} | " +
                $"经验 {mountainReputation:F0}/{GetMountainProgress():F0}\n" +
                $"四业共鸣: {resonance:F2}×" +
                (resonance > 1f ? " ✨" : ""),
                style);
        }
#endif
    }
}
