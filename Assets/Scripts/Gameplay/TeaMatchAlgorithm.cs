using UnityEngine;
using System.Collections.Generic;
using TeaMist.Data;

namespace TeaMist.Gameplay
{
/// <summary>
/// 茶品匹配算法 —— 根据玩家泡茶选择与目标茶谱，计算匹配分数。
/// 
/// 6 维属性权重：
/// - 茶具材料匹配 (25%) — 壶的材质是否适合该茶
/// - 茶叶正确性 (30%)     — 是否选了正确的茶叶
/// - 水温精准度 (20%)     — 水温是否在最佳区间（含保温衰减）
/// - 注水手法 (15%)       — 是否匹配该茶的冲泡法
/// - 出汤时机 (10%)       — 最佳窗口加分（太早太晚扣分）
/// - 隐藏加分 (0-5 bonus) — 特殊组合发现茶
/// </summary>
    public static class TeaMatchAlgorithm
    {
        /// <summary>
        /// 计算泡茶质量分数 (0-100)
        /// </summary>
        public static int CalculateMatchScore(
            BrewingChoices choices,
            TeaRecipeSO targetRecipe,
            TeawareData[] teawares,
            List<TeaRecipeSO> recipes)
        {
            if (targetRecipe == null || recipes == null || recipes.Count == 0)
            {
                // 无目标时纯凭手感打分
                return CalculateFreestyleScore(choices, teawares);
            }

            float score = 0f;

            // 1. 茶叶正确性 (30%)
            if (choices.leafIndex >= 0 && choices.leafIndex < recipes.Count)
            {
                var selectedLeaf = recipes[choices.leafIndex];
                if (selectedLeaf.recipeId == targetRecipe.recipeId)
                {
                    score += 30f;
                }
                else
                {
                    // 部分匹配：同类型茶叶有基础分
                    float similarity = CalculateLeafSimilarity(selectedLeaf, targetRecipe);
                    score += 30f * similarity;
                }
            }

            // 2. 茶具匹配 (25%)
            float teawareHeatRetention = 1f;
            if (choices.teawareIndex >= 0 && choices.teawareIndex < teawares.Length)
            {
                var teaware = teawares[choices.teawareIndex];
                float materialScore = targetRecipe.MatchMaterial(teaware.materialType);
                score += 25f * materialScore;

                // 保温系数：茶具保温越好，水温衰减越少
                teawareHeatRetention = teaware.heatRetention;
                float retentionMatch = 1f - Mathf.Abs(teawareHeatRetention - targetRecipe.idealHeatRetention);
                score += 5f * Mathf.Clamp01(retentionMatch);
            }

            // 3. 水温精准度 (20%) — 含保温衰减
            // 设定水温随时间递减：经过注水和等待，实际水温 = 设定温 × 保温系数
            float effectiveTemp = Mathf.Lerp(choices.temperature, 60f,
                (1f - Mathf.Clamp(teawareHeatRetention, 0.3f, 1.5f)) * 0.5f);
            float tempDiff = Mathf.Abs(effectiveTemp - targetRecipe.idealTemperature);
            float tempScore = 1f - Mathf.Clamp01(tempDiff / 20f);
            score += 20f * tempScore;

            // 4. 注水手法 (15%)
            float pourScore = targetRecipe.MatchPourStyle(choices.pourStyle);
            score += 15f * pourScore;

            // 5. 出汤时机 (10%) — 最佳窗口（甜点区）
            float timeDiff = Mathf.Abs(choices.steepTime - targetRecipe.idealSteepTime);
            float timeScore;
            if (timeDiff <= 3f)
                timeScore = 1f;                    // 完美窗口 ±3秒
            else if (timeDiff <= 8f)
                timeScore = 0.8f;                  // 不错窗口 ±8秒
            else if (timeDiff <= 15f)
                timeScore = 0.5f;                  // 勉强窗口 ±15秒
            else
                timeScore = 0.2f;                  // 太早或太晚
            score += 10f * timeScore;

            // 6. 隐藏发现茶加分
            float discoveryBonus = CheckDiscoveryTea(choices, teawares, recipes);
            score += discoveryBonus;

            return Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);
        }

        /// <summary>
        /// 无目标茶谱时的自由冲泡评分
        /// </summary>
        private static int CalculateFreestyleScore(BrewingChoices choices, TeawareData[] teawares)
        {
            float score = 50f; // 基础分

            // 壶和茶叶都选了 +20
            if (choices.teawareIndex >= 0) score += 10f;
            if (choices.leafIndex >= 0) score += 10f;

            // 水温在安全范围内 +10
            if (choices.temperature >= 70f && choices.temperature <= 95f)
                score += 10f;

            // 出汤时间不过长 +10
            if (choices.steepTime <= 30f)
                score += 10f;

            return Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);
        }

        /// <summary>
        /// 检查是否触发了发现茶（非标准组合但意外好喝）
        /// </summary>
        private static float CheckDiscoveryTea(
            BrewingChoices choices,
            TeawareData[] teawares,
            List<TeaRecipeSO> recipes)
        {
            // 白露 + 桂花蜜茶 用竹筒壶 + 冷水 → 冷泡桂花蜜（隐藏发现茶）
            if (choices.teawareIndex >= 0 && choices.teawareIndex < teawares.Length)
            {
                var teaware = teawares[choices.teawareIndex];
                if (teaware.materialType == "竹筒" && choices.temperature < 50f)
                {
                    return 10f; // 冷泡法发现加分
                }
            }
            return 0f;
        }

        private static float CalculateLeafSimilarity(TeaRecipeSO a, TeaRecipeSO b)
        {
            float sim = 0f;
            if (a.family == b.family) sim += 0.4f;
            if (a.flavorProfile == b.flavorProfile) sim += 0.3f;
            if (a.season == b.season) sim += 0.2f;
            return Mathf.Clamp01(sim);
        }
    }
}
