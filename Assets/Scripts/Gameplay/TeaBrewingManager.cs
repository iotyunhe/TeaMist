using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using TeaMist.Data;
using TeaMist.Rendering;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 泡茶交互系统 —— 5步仪式流状态机
    /// 
    /// 流程：择壶 → 选叶 → 控温 → 注水 → 出汤
    /// 每步选择影响最终茶品属性，最后与 NPC 需求匹配打分。
    /// </summary>
    public class TeaBrewingManager : MonoBehaviour
    {
        public static TeaBrewingManager Instance { get; private set; }

        public BrewingStep currentStep { get; private set; } = BrewingStep.Idle;
        private bool isBrewing = false;

        // NPC 本次需求
        private string npcRequest;
        private string targetTeaId;
        private TeaRecipeSO targetRecipe;

        // 玩家累计选择（最终用于匹配打分）
        private BrewingChoices playerChoices;

        [Header("━━━ 可用茶具 ━━━")]
        public TeawareData[] availableTeawares;

        [Header("━━━ 可用茶叶 ━━━")]
        public List<TeaRecipeSO> availableTeaRecipes;

        [Header("━━━ 事件 ━━━")]
        public UnityEvent<BrewingStep> OnStepChanged;
        public UnityEvent<int> OnBrewingComplete; // 传入 qualityScore (0-100)

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 初始化 UnityEvents（在运行时 AddComponent 后可能为 null）
            if (OnStepChanged == null) OnStepChanged = new UnityEvent<BrewingStep>();
            if (OnBrewingComplete == null) OnBrewingComplete = new UnityEvent<int>();

            // 注入默认茶具和茶谱（当 Inspector 未配置时）
            EnsureDefaultData();
        }

        /// <summary>
        /// 确保泡茶系统有默认数据。在 Inspector 未手动填入 ScriptableObject 时，
        /// 用硬编码的 MVP 数据填充，让核心循环可以直接跑通。
        /// </summary>
        private void EnsureDefaultData()
        {
            // ── 默认茶具（7种）──
            if (availableTeawares == null || availableTeawares.Length == 0)
            {
                availableTeawares = new TeawareData[]
                {
                    new TeawareData { displayName = "老紫砂", materialType = "紫砂", heatRetention = 1.8f, fragranceBoost = 0.9f, description = "一把用了多年的紫砂壶，壶身温润。最适合泡红茶和乌龙。" },
                    new TeawareData { displayName = "青瓷壶", materialType = "瓷",   heatRetention = 1.0f, fragranceBoost = 1.2f, description = "薄胎青瓷，透光如玉。绿茶和花茶的最佳搭档。" },
                    new TeawareData { displayName = "白瓷盖碗", materialType = "瓷",   heatRetention = 0.7f, fragranceBoost = 1.4f, description = "敞口盖碗，闻香方便。适合品鉴各类清茶。" },
                    new TeawareData { displayName = "玻璃壶", materialType = "玻璃", heatRetention = 0.6f, fragranceBoost = 1.0f, description = "透明的壶身，可观茶汤变幻。绿茶花茶皆宜。" },
                    new TeawareData { displayName = "粗陶壶", materialType = "粗陶", heatRetention = 1.6f, fragranceBoost = 0.6f, description = "山土烧制的陶壶，拙朴厚重。最适合煮黑茶和药茶。" },
                    new TeawareData { displayName = "银壶",   materialType = "银壶", heatRetention = 1.5f, fragranceBoost = 1.5f, description = "老银匠打的壶，传了三代。白茶和灵茶的最佳容具。" },
                    new TeawareData { displayName = "竹筒壶", materialType = "竹筒", heatRetention = 0.4f, fragranceBoost = 1.6f, description = "一截老竹掏成的壶，带着竹香。适合冷泡和药茶，可触发隐藏发现茶。" }
                };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[TeaBrewing] 已注入默认茶具 (7种)");
#endif
            }

            // ── 默认茶谱（6种）──
            if (availableTeaRecipes == null) availableTeaRecipes = new List<TeaRecipeSO>();
            if (availableTeaRecipes.Count == 0)
            {
                // 1. 桂花蜜茶 — 白露最爱（花茶/甜润/秋）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "guihuamicha", "桂花蜜茶",
                    "桂花与蜂蜜调和的花茶，甜香扑鼻。白露最喜欢的茶。",
                    TeaFamily.花茶, FlavorType.甜润, TeaSeason.秋,
                    85f, 30f, TeawareType.瓷壶, 0.8f, PourStyle.MidSpiral,
                    warmth: 8f, sweetness: 9f, smoothness: 7f));

                // 2. 清心茶 — 基础绿茶（绿茶/清香/春）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "qingxincha", "清心茶",
                    "最简单的清茶，只取山泉和嫩叶。越简单越见功夫。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春,
                    75f, 20f, TeawareType.玻璃壶, 0.5f, PourStyle.LowFast,
                    coolness: 7f, bitterness: 3f, smoothness: 8f));

                // 3. 薄荷茶 — 当归所需（药茶/药香/夏）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "bohecha", "薄荷茶",
                    "新鲜薄荷叶冲泡的药茶，清凉入喉。当归用来治伤的茶。",
                    TeaFamily.药茶, FlavorType.药香, TeaSeason.夏,
                    80f, 15f, TeawareType.盖碗, 0.6f, PourStyle.EdgePour,
                    coolness: 9f, bitterness: 4f, smoothness: 6f));

                // 4. 竹叶青 — 竹青所需（绿茶/清香/春）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "zhu_qing", "竹叶青",
                    "采自栖霞山竹间的嫩芽，色泽青翠如竹。竹青自己培育的茶。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春,
                    78f, 25f, TeawareType.玻璃壶, 0.5f, PourStyle.MidSpiral,
                    coolness: 6f, sweetness: 4f, smoothness: 8f, astringency: 3f));

                // 5. 老红袍 — 云鹤老所需（乌龙茶/醇厚/秋）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "laohongpao", "老红袍",
                    "岩骨花香的乌龙老茶，焙火深厚。云鹤老等了三百年的味道。",
                    TeaFamily.乌龙茶, FlavorType.醇厚, TeaSeason.秋,
                    95f, 45f, TeawareType.紫砂壶, 1.5f, PourStyle.HighSlow,
                    warmth: 7f, bitterness: 5f, smoothness: 9f, astringency: 4f));

                // 6. 山泉白茶 — 小山所需（白茶/清爽/冬）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "shanquanbaicha", "山泉白茶",
                    "日晒而成的白茶，只取最嫩的芽头。小山说石头也需要喝水。",
                    TeaFamily.白茶, FlavorType.清爽, TeaSeason.冬,
                    70f, 60f, TeawareType.银壶, 1.2f, PourStyle.LowFast,
                    coolness: 5f, sweetness: 6f, smoothness: 9f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[TeaBrewing] 已注入默认茶谱 (6种)");
#endif
            }
        }

        private TeaRecipeSO CreateRuntimeRecipe(
            string id, string name, string desc,
            TeaFamily family, FlavorType flavor, TeaSeason season,
            float temp, float steepTime, TeawareType teaware, float retention,
            PourStyle pour,
            float warmth = 5f, float coolness = 5f, float bitterness = 5f,
            float sweetness = 5f, float astringency = 5f, float smoothness = 5f)
        {
            var so = ScriptableObject.CreateInstance<TeaRecipeSO>();
            so.recipeId = id;
            so.teaName = name;
            so.description = desc;
            so.family = family;
            so.flavorProfile = flavor;
            so.season = season;
            so.idealTemperature = temp;
            so.idealSteepTime = steepTime;
            so.idealTeaware = teaware;
            so.idealHeatRetention = retention;
            so.idealPourStyle = pour;
            so.warmth = warmth;
            so.coolness = coolness;
            so.bitterness = bitterness;
            so.sweetness = sweetness;
            so.astringency = astringency;
            so.smoothness = smoothness;
            so.rarity = TeaRarity.常见;
            return so;
        }

        // ━━━ 公共 API ━━━

        // 缓存茶壶引用，避免重复 GameObject.Find
        private GameObject _cachedTeapot;

        /// <summary>
        /// 开始泡茶流程。由 DialogueManager 在遇到 << tea >> 指令时调用。
        /// </summary>
        public void StartBrewing(string request, string targetId)
        {
            if (isBrewing)
            {
                Debug.LogWarning("[TeaBrewing] 已经在泡茶中");
                return;
            }

            npcRequest = request;
            targetTeaId = targetId;
            targetRecipe = FindRecipe(targetId);
            playerChoices = new BrewingChoices();
            isBrewing = true;

            // 茶壶冒蒸汽
            if (_cachedTeapot == null)
                _cachedTeapot = GameObject.Find("Prop_Teaware");
            if (_cachedTeapot != null)
            {
                var steam = _cachedTeapot.GetComponent<TeaSteamEffect>();
                if (steam != null) steam.Play();
            }

            // 如果泡茶 UI 不可用，使用自动泡茶模式
            if (TeaBrewingUI.Instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TeaBrewing] 自动泡茶模式: {request} (目标: {targetId})");
#endif
                AutoBrew();
                return;
            }

            TransitionTo(BrewingStep.SelectTeaware);
        }

        /// <summary>
        /// MVP 自动泡茶：跳过交互 UI，直接模拟泡茶流程并返回结果。
        /// 当 TeaBrewingUI 尚未配置好时使用。
        /// </summary>
        private void AutoBrew()
        {
            // 模拟玩家选择
            playerChoices.teawareIndex = 0;  // 默认选第一个壶
            playerChoices.leafIndex = 0;     // 默认选第一个茶叶

            // 如果 targetRecipe 有最佳参数，尝试匹配
            if (targetRecipe != null)
            {
                playerChoices.temperature = targetRecipe.idealTemperature + Random.Range(-5f, 5f);
                playerChoices.pourStyle = targetRecipe.idealPourStyle;
                playerChoices.steepTime = targetRecipe.idealSteepTime + Random.Range(-3f, 3f);
            }
            else
            {
                playerChoices.temperature = 85f;
                playerChoices.pourStyle = PourStyle.MidSpiral;
                playerChoices.steepTime = 25f;
            }

            // 直接完成
            FinishBrewing();
        }

        /// <summary>
        /// 玩家做出当前步骤的选择
        /// </summary>
        public void MakeChoice(object choice)
        {
            if (!isBrewing) return;

            switch (currentStep)
            {
                case BrewingStep.SelectTeaware when choice is int teawareIndex:
                    playerChoices.teawareIndex = teawareIndex;
                    TransitionTo(BrewingStep.SelectLeaf);
                    break;

                case BrewingStep.SelectLeaf when choice is int leafIndex:
                    playerChoices.leafIndex = leafIndex;
                    TransitionTo(BrewingStep.ControlTemp);
                    break;

                case BrewingStep.ControlTemp when choice is float temperature:
                    playerChoices.temperature = Mathf.Clamp(temperature, 60f, 100f);
                    TransitionTo(BrewingStep.PourWater);
                    break;

                case BrewingStep.PourWater when choice is PourStyle pourStyle:
                    playerChoices.pourStyle = pourStyle;
                    TransitionTo(BrewingStep.PourTea);
                    break;

                case BrewingStep.PourTea when choice is float steepTime:
                    playerChoices.steepTime = Mathf.Clamp(steepTime, 5f, 60f);
                    FinishBrewing();
                    break;
            }
        }

        private void TransitionTo(BrewingStep nextStep)
        {
            currentStep = nextStep;
            OnStepChanged?.Invoke(nextStep);
        }

        // ━━━ 完成泡茶 ━━━

        private void FinishBrewing()
        {
            isBrewing = false;
            currentStep = BrewingStep.Idle;

            // 停止茶壶蒸汽
            if (_cachedTeapot != null)
            {
                var steam = _cachedTeapot.GetComponent<TeaSteamEffect>();
                if (steam != null) steam.Stop();
                _cachedTeapot = null; // 释放引用，下次泡茶重新查找（桶可能被销毁重建）
            }

            // 计算匹配分数
            int qualityScore = TeaMatchAlgorithm.CalculateMatchScore(
                playerChoices, targetRecipe, availableTeawares, availableTeaRecipes
            );

            OnBrewingComplete?.Invoke(qualityScore);

            // 通知对话系统继续
            Dialogue.DialogueManager.Instance?.AdvanceWithTeaQuality(qualityScore);
        }

        public TeaRecipeSO GetTargetRecipe() => targetRecipe;

        private TeaRecipeSO FindRecipe(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return availableTeaRecipes.Find(r => r.recipeId == id);
        }

        // ━━━ 查询当前可用选项 ━━━
        public List<string> GetCurrentOptions()
        {
            switch (currentStep)
            {
                case BrewingStep.SelectTeaware: return GetTeawareNames();
                case BrewingStep.SelectLeaf:    return GetLeafNames();
                default:                        return new List<string>();
            }
        }

        private List<string> GetTeawareNames()
        {
            var names = new List<string>();
            foreach (var t in availableTeawares)
                names.Add(t.displayName);
            return names;
        }

        private List<string> GetLeafNames()
        {
            var names = new List<string>();
            foreach (var r in availableTeaRecipes)
                names.Add(r.recipeName);
            return names;
        }
    }

    // ━━━ 数据结构 ━━━

    public enum BrewingStep
    {
        Idle,
        SelectTeaware,  // 择壶
        SelectLeaf,     // 选叶
        ControlTemp,    // 控温
        PourWater,      // 注水
        PourTea         // 出汤
    }

    public enum PourStyle
    {
        HighSlow,   // 高冲慢注 — 激发香气
        LowFast,    // 低冲快注 — 保持清甜
        MidSpiral,  // 中位回旋 — 均匀萃取
        EdgePour    // 沿壁注水 — 温和不惊叶
    }

    [System.Serializable]
    public struct TeawareData
    {
        public string displayName;
        public string materialType; // 紫砂/瓷/玻璃/粗陶/铁壶/竹筒/银壶
        [Range(0f, 2f)] public float heatRetention;   // 保温性
        [Range(0f, 2f)] public float fragranceBoost;  // 提香
        [TextArea(2, 4)] public string description;
    }

    /// <summary>
    /// 玩家泡茶累积选择
    /// </summary>
    [System.Serializable]
    public struct BrewingChoices
    {
        public int teawareIndex;    // 壶的索引
        public int leafIndex;       // 茶叶的索引
        public float temperature;   // 水温 60-100°C
        public PourStyle pourStyle; // 注水手法
        public float steepTime;     // 出汤时间 5-60秒
    }
}
