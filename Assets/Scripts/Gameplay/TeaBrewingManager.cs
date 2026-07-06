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

        /// <summary>最近一次泡茶的逐维评分分解（供 UI 展示）</summary>
        public ScoreBreakdown lastBreakdown { get; private set; }

        [Header("━━━ 可用茶具 ━━━")]
        public TeawareSO[] availableTeawares;

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
                availableTeawares = new TeawareSO[]
                {
                    CreateRuntimeTeaware("老紫砂", "紫砂", 1.8f, 0.9f, "一把用了多年的紫砂壶，壶身温润。最适合泡红茶和乌龙。"),
                    CreateRuntimeTeaware("青瓷壶", "瓷",   1.0f, 1.2f, "薄胎青瓷，透光如玉。绿茶和花茶的最佳搭档。"),
                    CreateRuntimeTeaware("白瓷盖碗", "瓷", 0.7f, 1.4f, "敞口盖碗，闻香方便。适合品鉴各类清茶。"),
                    CreateRuntimeTeaware("玻璃壶", "玻璃", 0.6f, 1.0f, "透明的壶身，可观茶汤变幻。绿茶花茶皆宜。"),
                    CreateRuntimeTeaware("粗陶壶", "粗陶", 1.6f, 0.6f, "山土烧制的陶壶，拙朴厚重。最适合煮黑茶和药茶。"),
                    CreateRuntimeTeaware("银壶",   "银壶", 1.5f, 1.5f, "老银匠打的壶，传了三代。白茶和灵茶的最佳容具。"),
                    CreateRuntimeTeaware("竹筒壶", "竹筒", 0.4f, 1.6f, "一截老竹掏成的壶，带着竹香。适合冷泡和药茶，可触发隐藏发现茶。")
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
                    warmth: 8f, sweetness: 9f, smoothness: 7f,
                    liquorColor: new Color(0.82f, 0.65f, 0.28f)));  // 金黄

                // 2. 清心茶 — 基础绿茶（绿茶/清香/春）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "qingxincha", "清心茶",
                    "最简单的清茶，只取山泉和嫩叶。越简单越见功夫。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春,
                    75f, 20f, TeawareType.玻璃壶, 0.5f, PourStyle.LowFast,
                    coolness: 7f, bitterness: 3f, smoothness: 8f,
                    liquorColor: new Color(0.55f, 0.78f, 0.42f)));  // 嫩绿

                // 3. 薄荷茶 — 当归所需（药茶/药香/夏）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "bohecha", "薄荷茶",
                    "新鲜薄荷叶冲泡的药茶，清凉入喉。当归用来治伤的茶。",
                    TeaFamily.药茶, FlavorType.药香, TeaSeason.夏,
                    80f, 15f, TeawareType.盖碗, 0.6f, PourStyle.EdgePour,
                    coolness: 9f, bitterness: 4f, smoothness: 6f,
                    liquorColor: new Color(0.62f, 0.85f, 0.58f)));  // 浅绿偏黄

                // 4. 竹叶青 — 竹青所需（绿茶/清香/春）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "zhu_qing", "竹叶青",
                    "采自栖霞山竹间的嫩芽，色泽青翠如竹。竹青自己培育的茶。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春,
                    78f, 25f, TeawareType.玻璃壶, 0.5f, PourStyle.MidSpiral,
                    coolness: 6f, sweetness: 4f, smoothness: 8f, astringency: 3f,
                    liquorColor: new Color(0.45f, 0.72f, 0.38f)));  // 翠绿

                // 5. 老红袍 — 云鹤老所需（乌龙茶/醇厚/秋）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "laohongpao", "老红袍",
                    "岩骨花香的乌龙老茶，焙火深厚。云鹤老等了三百年的味道。",
                    TeaFamily.乌龙茶, FlavorType.醇厚, TeaSeason.秋,
                    95f, 45f, TeawareType.紫砂壶, 1.5f, PourStyle.HighSlow,
                    warmth: 7f, bitterness: 5f, smoothness: 9f, astringency: 4f,
                    liquorColor: new Color(0.65f, 0.32f, 0.15f)));  // 深褐

                // 6. 山泉白茶 — 小山所需（白茶/清爽/冬）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "shanquanbaicha", "山泉白茶",
                    "日晒而成的白茶，只取最嫩的芽头。小山说石头也需要喝水。",
                    TeaFamily.白茶, FlavorType.清爽, TeaSeason.冬,
                    70f, 60f, TeawareType.银壶, 1.2f, PourStyle.LowFast,
                    coolness: 5f, sweetness: 6f, smoothness: 9f,
                    liquorColor: new Color(0.88f, 0.85f, 0.72f)));  // 浅米白

                // ═══════════════ 新增 6 种 (2026-07-03) ═══════════════

                // 7. 秋露白 — 寒露专属（白茶/花香/秋）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "qiulubai", "秋露白",
                    "待到秋露凝结时，采灵雾山巅的白毫银针。寒露在雾中采摘的稀有白茶，带着一丝灵气的清冷。",
                    TeaFamily.白茶, FlavorType.花香, TeaSeason.秋,
                    72f, 50f, TeawareType.银壶, 1.3f, PourStyle.EdgePour,
                    coolness: 7f, sweetness: 7f, smoothness: 8f, astringency: 2f,
                    liquorColor: new Color(0.82f, 0.80f, 0.68f), rarity: TeaRarity.少见));

                // 8. 墨韵乌龙 — 青岚专属（乌龙茶/果香/秋）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "moyun_wulong", "墨韵乌龙",
                    "青岚在画室中陈化的乌龙茶，常年浸染松烟墨香。茶汤如淡墨晕开，回甘悠长。",
                    TeaFamily.乌龙茶, FlavorType.果香, TeaSeason.秋,
                    92f, 40f, TeawareType.紫砂壶, 1.4f, PourStyle.HighSlow,
                    warmth: 6f, sweetness: 5f, smoothness: 8f, bitterness: 4f,
                    liquorColor: new Color(0.42f, 0.35f, 0.28f), rarity: TeaRarity.少见));

                // 9. 松针茶 — 樵翁专属（药茶/药香/冬）
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "songzhencha", "松针茶",
                    "樵翁从深山里采的老松针，松脂的清香里有山野的粗犷。驱寒暖身，最适合冬日的山中人。",
                    TeaFamily.药茶, FlavorType.药香, TeaSeason.冬,
                    90f, 35f, TeawareType.陶壶, 1.6f, PourStyle.MidSpiral,
                    warmth: 9f, bitterness: 5f, smoothness: 5f, astringency: 3f,
                    liquorColor: new Color(0.55f, 0.48f, 0.32f)));  // 松褐色

                // 10. 茉莉春雪（花茶/花香/春）— 春日限定
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "molichunxue", "茉莉春雪",
                    "春天第一场雪后采摘的茉莉花，窨制七次而成的极品花茶。花香透骨而不掩茶韵。",
                    TeaFamily.花茶, FlavorType.花香, TeaSeason.春,
                    82f, 25f, TeawareType.瓷壶, 0.7f, PourStyle.MidSpiral,
                    sweetness: 8f, smoothness: 8f, coolness: 4f,
                    liquorColor: new Color(0.78f, 0.75f, 0.52f), rarity: TeaRarity.稀有));

                // 11. 陈年普洱（黑茶/醇厚/冬）— 老茶客之选
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "chennianpuer", "陈年普洱",
                    "云鹤老珍藏了五十年的普洱熟茶，茶饼已陈化出蜜枣的甜香。入口绵滑如丝，是真正老茶客才懂的滋味。",
                    TeaFamily.黑茶, FlavorType.醇厚, TeaSeason.冬,
                    98f, 55f, TeawareType.陶壶, 1.8f, PourStyle.HighSlow,
                    warmth: 9f, smoothness: 9f, bitterness: 3f, sweetness: 5f,
                    liquorColor: new Color(0.25f, 0.12f, 0.05f), rarity: TeaRarity.稀有));

                // 12. 月华灵芽（灵茶/清香/不限）— 传说级
                availableTeaRecipes.Add(CreateRuntimeRecipe(
                    "yuehualingya", "月华灵芽",
                    "只在月圆之夜绽放的灵茶，每一片芽叶都凝结月华。喝下后眼前会浮现前世的模糊记忆——茶馆传说中藏着的那一味茶。",
                    TeaFamily.灵茶, FlavorType.清香, TeaSeason.不限,
                    68f, 90f, TeawareType.银壶, 1.0f, PourStyle.EdgePour,
                    warmth: 5f, coolness: 5f, sweetness: 7f, smoothness: 10f,
                    liquorColor: new Color(0.72f, 0.80f, 0.90f), rarity: TeaRarity.传说));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[TeaBrewing] 已注入默认茶谱 (12种)");
#endif
            }
        }

        private TeaRecipeSO CreateRuntimeRecipe(
            string id, string name, string desc,
            TeaFamily family, FlavorType flavor, TeaSeason season,
            float temp, float steepTime, TeawareType teaware, float retention,
            PourStyle pour,
            float warmth = 5f, float coolness = 5f, float bitterness = 5f,
            float sweetness = 5f, float astringency = 5f, float smoothness = 5f,
            Color liquorColor = default, TeaRarity rarity = TeaRarity.常见)
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
            so.rarity = rarity;
            so.liquorColor = liquorColor == default ? new Color(0.6f, 0.4f, 0.2f) : liquorColor;
            return so;
        }

        private TeawareSO CreateRuntimeTeaware(
            string name, string material, float retention, float fragrance, string desc)
        {
            var so = ScriptableObject.CreateInstance<TeawareSO>();
            so.displayName = name;
            so.materialType = material;
            so.heatRetention = retention;
            so.fragranceBoost = fragrance;
            so.description = desc;
            return so;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在 Editor 中一键生成默认茶具 .asset 文件。
        /// 右键 TeaBrewingManager 组件 → "创建默认茶具资产"
        /// </summary>
        [ContextMenu("创建默认茶具资产")]
        private void CreateDefaultTeawareAssets()
        {
            string dir = "Assets/ScriptableObjects/Teawares";
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var defaults = new (string name, string mat, float hr, float fb, string desc)[]
            {
                ("老紫砂", "紫砂", 1.8f, 0.9f, "一把用了多年的紫砂壶，壶身温润。最适合泡红茶和乌龙。"),
                ("青瓷壶", "瓷",   1.0f, 1.2f, "薄胎青瓷，透光如玉。绿茶和花茶的最佳搭档。"),
                ("白瓷盖碗","瓷",  0.7f, 1.4f, "敞口盖碗，闻香方便。适合品鉴各类清茶。"),
                ("玻璃壶", "玻璃", 0.6f, 1.0f, "透明的壶身，可观茶汤变幻。绿茶花茶皆宜。"),
                ("粗陶壶", "粗陶", 1.6f, 0.6f, "山土烧制的陶壶，拙朴厚重。最适合煮黑茶和药茶。"),
                ("银壶",   "银壶", 1.5f, 1.5f, "老银匠打的壶，传了三代。白茶和灵茶的最佳容具。"),
                ("竹筒壶", "竹筒", 0.4f, 1.6f, "一截老竹掏成的壶，带着竹香。适合冷泡和药茶，可触发隐藏发现茶。")
            };

            foreach (var d in defaults)
            {
                string path = $"{dir}/Teaware_{d.name}.asset";
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<TeawareSO>(path);
                if (existing != null)
                {
                    existing.displayName = d.name;
                    existing.materialType = d.mat;
                    existing.heatRetention = d.hr;
                    existing.fragranceBoost = d.fb;
                    existing.description = d.desc;
                    UnityEditor.EditorUtility.SetDirty(existing);
                    Debug.Log($"[TeaBrewing] 已更新茶具资产: {path}");
                }
                else
                {
                    var so = CreateRuntimeTeaware(d.name, d.mat, d.hr, d.fb, d.desc);
                    UnityEditor.AssetDatabase.CreateAsset(so, path);
                    Debug.Log($"[TeaBrewing] 已创建茶具资产: {path}");
                }
            }

            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("[TeaBrewing] 7 种默认茶具资产创建/更新完成");
        }

        /// <summary>
        /// 在 Editor 中一键生成默认茶谱 .asset 文件。
        /// 右键 TeaBrewingManager 组件 → "创建默认茶谱资产"
        /// </summary>
        [ContextMenu("创建默认茶谱资产")]
        private void CreateDefaultRecipeAssets()
        {
            string dir = "Assets/ScriptableObjects/TeaRecipes";
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var defaults = new (string id, string name, string desc, TeaFamily family, FlavorType flavor,
                TeaSeason season, float temp, float steep, TeawareType teaware, float retention,
                PourStyle pour, float w, float c, float b, float sw, float a, float sm,
                TeaRarity rarity, Color liquor)[]
            {
                // ── 原有 6 种 ──
                ("guihuamicha", "桂花蜜茶", "桂花与蜂蜜调和的花茶，甜香扑鼻。白露最喜欢的茶。",
                    TeaFamily.花茶, FlavorType.甜润, TeaSeason.秋, 85f, 30f, TeawareType.瓷壶, 0.8f, PourStyle.MidSpiral,
                    8f, 5f, 5f, 9f, 5f, 7f, TeaRarity.常见, new Color(0.82f, 0.65f, 0.28f)),
                ("qingxincha", "清心茶", "最简单的清茶，只取山泉和嫩叶。越简单越见功夫。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春, 75f, 20f, TeawareType.玻璃壶, 0.5f, PourStyle.LowFast,
                    5f, 7f, 3f, 5f, 5f, 8f, TeaRarity.常见, new Color(0.55f, 0.78f, 0.42f)),
                ("bohecha", "薄荷茶", "新鲜薄荷叶冲泡的药茶，清凉入喉。当归用来治伤的茶。",
                    TeaFamily.药茶, FlavorType.药香, TeaSeason.夏, 80f, 15f, TeawareType.盖碗, 0.6f, PourStyle.EdgePour,
                    5f, 9f, 4f, 5f, 5f, 6f, TeaRarity.常见, new Color(0.62f, 0.85f, 0.58f)),
                ("zhu_qing", "竹叶青", "采自栖霞山竹间的嫩芽，色泽青翠如竹。竹青自己培育的茶。",
                    TeaFamily.绿茶, FlavorType.清香, TeaSeason.春, 78f, 25f, TeawareType.玻璃壶, 0.5f, PourStyle.MidSpiral,
                    5f, 6f, 5f, 4f, 3f, 8f, TeaRarity.常见, new Color(0.45f, 0.72f, 0.38f)),
                ("laohongpao", "老红袍", "岩骨花香的乌龙老茶，焙火深厚。云鹤老等了三百年的味道。",
                    TeaFamily.乌龙茶, FlavorType.醇厚, TeaSeason.秋, 95f, 45f, TeawareType.紫砂壶, 1.5f, PourStyle.HighSlow,
                    7f, 5f, 5f, 5f, 4f, 9f, TeaRarity.常见, new Color(0.65f, 0.32f, 0.15f)),
                ("shanquanbaicha", "山泉白茶", "日晒而成的白茶，只取最嫩的芽头。小山说石头也需要喝水。",
                    TeaFamily.白茶, FlavorType.清爽, TeaSeason.冬, 70f, 60f, TeawareType.银壶, 1.2f, PourStyle.LowFast,
                    5f, 5f, 5f, 6f, 5f, 9f, TeaRarity.常见, new Color(0.88f, 0.85f, 0.72f)),
                // ── 新增 6 种 ──
                ("qiulubai", "秋露白", "待到秋露凝结时，采灵雾山巅的白毫银针。寒露在雾中采摘的稀有白茶。",
                    TeaFamily.白茶, FlavorType.花香, TeaSeason.秋, 72f, 50f, TeawareType.银壶, 1.3f, PourStyle.EdgePour,
                    5f, 7f, 5f, 7f, 2f, 8f, TeaRarity.少见, new Color(0.82f, 0.80f, 0.68f)),
                ("moyun_wulong", "墨韵乌龙", "青岚在画室中陈化的乌龙茶，常年浸染松烟墨香。茶汤如淡墨晕开。",
                    TeaFamily.乌龙茶, FlavorType.果香, TeaSeason.秋, 92f, 40f, TeawareType.紫砂壶, 1.4f, PourStyle.HighSlow,
                    6f, 5f, 4f, 5f, 5f, 8f, TeaRarity.少见, new Color(0.42f, 0.35f, 0.28f)),
                ("songzhencha", "松针茶", "樵翁从深山里采的老松针，松脂的清香里有山野的粗犷。",
                    TeaFamily.药茶, FlavorType.药香, TeaSeason.冬, 90f, 35f, TeawareType.陶壶, 1.6f, PourStyle.MidSpiral,
                    9f, 5f, 5f, 5f, 3f, 5f, TeaRarity.常见, new Color(0.55f, 0.48f, 0.32f)),
                ("molichunxue", "茉莉春雪", "春天第一场雪后采摘的茉莉花，窨制七次而成的极品花茶。",
                    TeaFamily.花茶, FlavorType.花香, TeaSeason.春, 82f, 25f, TeawareType.瓷壶, 0.7f, PourStyle.MidSpiral,
                    5f, 4f, 5f, 8f, 5f, 8f, TeaRarity.稀有, new Color(0.78f, 0.75f, 0.52f)),
                ("chennianpuer", "陈年普洱", "云鹤老珍藏了五十年的普洱熟茶，茶饼已陈化出蜜枣的甜香。",
                    TeaFamily.黑茶, FlavorType.醇厚, TeaSeason.冬, 98f, 55f, TeawareType.陶壶, 1.8f, PourStyle.HighSlow,
                    9f, 5f, 3f, 5f, 5f, 9f, TeaRarity.稀有, new Color(0.25f, 0.12f, 0.05f)),
                ("yuehualingya", "月华灵芽", "只在月圆之夜绽放的灵茶，每一片芽叶都凝结月华。喝下后眼前会浮现前世记忆。",
                    TeaFamily.灵茶, FlavorType.清香, TeaSeason.不限, 68f, 90f, TeawareType.银壶, 1.0f, PourStyle.EdgePour,
                    5f, 5f, 5f, 7f, 5f, 10f, TeaRarity.传说, new Color(0.72f, 0.80f, 0.90f))
            };

            foreach (var d in defaults)
            {
                string path = $"{dir}/Tea_{d.id}.asset";
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<TeaRecipeSO>(path);
                if (existing != null)
                {
                    existing.teaName = d.name;
                    existing.description = d.desc;
                    existing.family = d.family;
                    existing.flavorProfile = d.flavor;
                    existing.season = d.season;
                    existing.idealTemperature = d.temp;
                    existing.idealSteepTime = d.steep;
                    existing.idealTeaware = d.teaware;
                    existing.idealHeatRetention = d.retention;
                    existing.idealPourStyle = d.pour;
                    existing.warmth = d.w;
                    existing.coolness = d.c;
                    existing.bitterness = d.b;
                    existing.sweetness = d.sw;
                    existing.astringency = d.a;
                    existing.smoothness = d.sm;
                    existing.rarity = d.rarity;
                    existing.liquorColor = d.liquor;
                    UnityEditor.EditorUtility.SetDirty(existing);
                    Debug.Log($"[TeaBrewing] 已更新茶谱资产: {path}");
                }
                else
                {
                    var so = CreateRuntimeRecipe(d.id, d.name, d.desc, d.family, d.flavor, d.season,
                        d.temp, d.steep, d.teaware, d.retention, d.pour, d.w, d.c, d.b, d.sw, d.a, d.sm,
                        d.liquor, d.rarity);
                    UnityEditor.AssetDatabase.CreateAsset(so, path);
                    Debug.Log($"[TeaBrewing] 已创建茶谱资产: {path}");
                }
            }

            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("[TeaBrewing] 12 种默认茶谱资产创建/更新完成");
        }
#endif

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
                if (steam != null)
                {
                    steam.SetTeaName(request); // 蒸汽带茶色
                    steam.Play();
                }
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
                playerChoices, targetRecipe, availableTeawares, availableTeaRecipes,
                out ScoreBreakdown breakdown
            );
            lastBreakdown = breakdown;

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
