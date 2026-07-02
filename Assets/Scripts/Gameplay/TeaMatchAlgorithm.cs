using UnityEngine;
using System.Collections.Generic;
using TeaMist.Data;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 泡茶评分逐维分解，用于结果面板展示
    /// </summary>
    public struct ScoreBreakdown
    {
        public float leafScore;      // 茶叶正确 (max 30)
        public float teawareScore;   // 茶具匹配 (max 30，含保温加成)
        public float tempScore;      // 水温精准 (max 20)
        public float pourScore;      // 注水手法 (max 15)
        public float timeScore;      // 出汤时机 (max 10)
        public float discoveryBonus; // 隐藏发现 (+0~10)
        public int total;
    }

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
            TeawareSO[] teawares,
            List<TeaRecipeSO> recipes,
            out ScoreBreakdown breakdown)
        {
            breakdown = new ScoreBreakdown();

            if (targetRecipe == null || recipes == null || recipes.Count == 0)
            {
                // 无目标时纯凭手感打分
                return CalculateFreestyleScore(choices, teawares, out breakdown);
            }

            float leaf = 0f, teaware = 0f, temp = 0f, pour = 0f, time = 0f, discovery = 0f;

            // 1. 茶叶正确性 (30%)
            if (choices.leafIndex >= 0 && choices.leafIndex < recipes.Count)
            {
                var selectedLeaf = recipes[choices.leafIndex];
                if (selectedLeaf.recipeId == targetRecipe.recipeId)
                {
                    leaf = 30f;
                }
                else
                {
                    float similarity = CalculateLeafSimilarity(selectedLeaf, targetRecipe);
                    leaf = 30f * similarity;
                }
            }

            // 2. 茶具匹配 (25% + 5% 保温加成)
            float teawareHeatRetention = 1f;
            if (choices.teawareIndex >= 0 && choices.teawareIndex < teawares.Length)
            {
                var tw = teawares[choices.teawareIndex];
                float materialScore = targetRecipe.MatchMaterial(tw.materialType);
                float baseScore = 25f * materialScore;

                teawareHeatRetention = tw.heatRetention;
                float retentionMatch = 1f - Mathf.Abs(teawareHeatRetention - targetRecipe.idealHeatRetention);
                float bonusScore = 5f * Mathf.Clamp01(retentionMatch);

                teaware = baseScore + bonusScore;
            }

            // 3. 水温精准度 (20%)
            float effectiveTemp = Mathf.Lerp(choices.temperature, 60f,
                (1f - Mathf.Clamp(teawareHeatRetention, 0.3f, 1.5f)) * 0.5f);
            float tempDiff = Mathf.Abs(effectiveTemp - targetRecipe.idealTemperature);
            float tempRatio = 1f - Mathf.Clamp01(tempDiff / 20f);
            temp = 20f * tempRatio;

            // 4. 注水手法 (15%)
            float pourRatio = targetRecipe.MatchPourStyle(choices.pourStyle);
            pour = 15f * pourRatio;

            // 5. 出汤时机 (10%)
            float timeDiff = Mathf.Abs(choices.steepTime - targetRecipe.idealSteepTime);
            float timeRatio;
            if (timeDiff <= 3f)      timeRatio = 1f;
            else if (timeDiff <= 8f)  timeRatio = 0.8f;
            else if (timeDiff <= 15f) timeRatio = 0.5f;
            else                      timeRatio = 0.2f;
            time = 10f * timeRatio;

            // 6. 隐藏发现茶
            discovery = CheckDiscoveryTea(choices, teawares, recipes);

            // 汇总
            float total = leaf + teaware + temp + pour + time + discovery;
            int finalScore = Mathf.Clamp(Mathf.RoundToInt(total), 0, 100);

            breakdown.leafScore = leaf;
            breakdown.teawareScore = teaware;
            breakdown.tempScore = temp;
            breakdown.pourScore = pour;
            breakdown.timeScore = time;
            breakdown.discoveryBonus = discovery;
            breakdown.total = finalScore;

            return finalScore;
        }

        /// <summary>
        /// 无目标茶谱时的自由冲泡评分
        /// </summary>
        private static int CalculateFreestyleScore(BrewingChoices choices, TeawareSO[] teawares,
            out ScoreBreakdown breakdown)
        {
            breakdown = new ScoreBreakdown();
            float leaf = 0f, teaware = 0f, temp = 0f, pour = 0f, time = 0f;

            // 壶和茶叶都选了
            if (choices.teawareIndex >= 0) teaware = 10f;
            if (choices.leafIndex >= 0) leaf = 10f;
            // 水温在安全范围内
            if (choices.temperature >= 70f && choices.temperature <= 95f) temp = 10f;
            // 注水手法选了就有基础分
            pour = 5f;
            // 出汤时间不过长
            if (choices.steepTime <= 30f) time = 10f;

            float total = leaf + teaware + temp + pour + time + 50f; // 基础分 50
            int final = Mathf.Clamp(Mathf.RoundToInt(total), 0, 100);

            breakdown.leafScore = leaf;
            breakdown.teawareScore = teaware;
            breakdown.tempScore = temp;
            breakdown.pourScore = pour;
            breakdown.timeScore = time;
            breakdown.discoveryBonus = 0f;
            breakdown.total = final;
            return final;
        }

        /// <summary>
        /// 检查是否触发了发现茶（非标准组合但意外好喝）
        /// </summary>
        private static float CheckDiscoveryTea(
            BrewingChoices choices,
            TeawareSO[] teawares,
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
