using UnityEngine;
using UnityEngine.UI;
// using TMPro; — 已迁移到 Legacy UI.Text
using System.Collections;
using System.Collections.Generic;
using TeaMist.Core;
namespace TeaMist.Gameplay
{
    /// <summary>
    /// 泡茶交互 UI —— 5步仪式流的视觉呈现。
    /// 每步有不同的控件：择壶（列表）→ 选叶（列表）→ 控温（滑条）→ 注水（按钮）→ 出汤（计时器）
    /// 当子控件引用为空时，Awake 阶段自动创建完整 UI 层级。
    /// </summary>
    public class TeaBrewingUI : MonoBehaviour
    {
        public static TeaBrewingUI Instance { get; private set; }

        [Header("━━━ 主面板 ━━━")]
        public CanvasGroup panelGroup;
        public float fadeDuration = 0.3f;

        [Header("━━━ 步骤指示器 ━━━")]
        public Text stepTitleText;
        public Text stepHintText;
        public GameObject[] stepIndicators; // 5个圆点，当前步骤高亮

        [Header("━━━ 步骤1：择壶 ━━━")]
        public GameObject teawareGroup;
        public Button[] teawareButtons;
        public Text[] teawareNames;
        public Text teawareDescription;

        [Header("━━━ 步骤2：选叶 ━━━")]
        public GameObject leafGroup;
        public Button[] leafButtons;
        public Text[] leafNames;
        public Text leafDescription;

        [Header("━━━ 步骤3：控温 ━━━")]
        public GameObject tempGroup;
        public Slider temperatureSlider;
        public Text temperatureValue;
        public Image temperatureMeter;
        public Button temperatureConfirmBtn;

        [Header("━━━ 步骤4：注水 ━━━")]
        public GameObject pourGroup;
        public Button[] pourButtons;

        [Header("━━━ 步骤5：出汤 ━━━")]
        public GameObject pourTeaGroup;
        public Text steepTimerText;
        public Button stopSteepButton;
        private float steepStartTime;
        private bool isSteeping;

        [Header("━━━ 茶烟粒子 ━━━")]
        public ParticleSystem steamParticles;

        [Header("━━━ 茶汤显色 ━━━")]
        public Image teaSoupImage;       // 出汤后显示的茶汤色块

        [Header("━━━ 评分分解 ━━━")]
        private GameObject _breakdownPanel;
        private Text[] _breakdownLabels;
        private Image[] _breakdownBars;
        private const int BREAKDOWN_COUNT = 6;
        private static readonly string[] BREAKDOWN_NAMES =
            { "茶叶", "茶具", "水温", "注水", "出汤", "发现" };
        private static readonly float[] BREAKDOWN_MAX =
            { 30f, 30f, 20f, 15f, 10f, 10f };

        private bool _uiBuilt;
        private TeaMist.Rendering.TeaSteamEffect _steamEffect;  // 缓存蒸汽组件
        private float _normalEmissionRate = 4.5f;  // 正常蒸汽速率

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (panelGroup == null)
                panelGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureUIHierarchy();
        }

        /// <summary>
        /// 自动创建泡茶 UI 子对象层级，让组件挂载在空 GameObject 上即可工作。
        /// </summary>
        private void EnsureUIHierarchy()
        {
            if (_uiBuilt) return;
            if (stepTitleText != null && teawareGroup != null)
            {
                _uiBuilt = true;
                return;
            }

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            var ink = new Color(0.15f, 0.13f, 0.10f);
            var gold = new Color(0.70f, 0.55f, 0.30f);
            var parchment = new Color(0.94f, 0.91f, 0.84f, 0.92f);

            // ── 背景 ──
            var bg = CreateChild("BG", rt);
            bg.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.13f, 0.88f);
            SetAnchors(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ── 标题 ──
            stepTitleText = CreateTMP("Title", rt, "泡茶", 32,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -15), new Vector2(-30, -55));
            stepTitleText.alignment = TextAnchor.MiddleCenter;
            stepTitleText.fontStyle = FontStyle.Bold;
            stepTitleText.color = gold;

            // ── 步骤指示器（5个圆点）──
            var dotsGroup = CreateChild("StepDots", rt);
            var dotsRt = dotsGroup.GetComponent<RectTransform>();
            dotsRt.anchorMin = new Vector2(0.5f, 1);
            dotsRt.anchorMax = new Vector2(0.5f, 1);
            dotsRt.pivot = new Vector2(0.5f, 1);
            dotsRt.anchoredPosition = new Vector2(0, -65);
            dotsRt.sizeDelta = new Vector2(200, 16);

            stepIndicators = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                var dot = CreateChild($"Dot_{i}", dotsRt);
                var dotRt = dot.GetComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0, 0.5f);
                dotRt.anchorMax = new Vector2(0, 0.5f);
                dotRt.pivot = new Vector2(0, 0.5f);
                dotRt.anchoredPosition = new Vector2(10 + i * 45, 0);
                dotRt.sizeDelta = new Vector2(14, 14);
                var dotImg = dot.AddComponent<Image>();
                dotImg.color = new Color(0.3f, 0.3f, 0.3f);
                stepIndicators[i] = dot;
            }

            // ── 提示 ──
            stepHintText = CreateTMP("Hint", rt, "", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -90), new Vector2(-30, -120));
            stepHintText.alignment = TextAnchor.MiddleCenter;
            stepHintText.color = new Color(0.82f, 0.78f, 0.70f);

            // ── 步骤1：择壶 ──
            teawareGroup = CreateChild("TeawareGroup", rt);
            SetAnchors(teawareGroup, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);

            teawareButtons = new Button[7];
            teawareNames = new Text[7];
            float btnH = 0.9f / 7f;
            for (int i = 0; i < 7; i++)
            {
                var btn = CreateOptionButton($"Teaware_{i}", teawareGroup.GetComponent<RectTransform>(), i, btnH, 7);
                teawareButtons[i] = btn;
                teawareNames[i] = btn.GetComponentInChildren<Text>();
            }

            teawareDescription = CreateTMP("TeawareDesc", rt, "", 18,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.14f), Vector2.zero, Vector2.zero);
            teawareDescription.alignment = TextAnchor.MiddleCenter;
            teawareDescription.color = new Color(0.70f, 0.68f, 0.62f);

            // ── 步骤2：选叶 ──
            leafGroup = CreateChild("LeafGroup", rt);
            SetAnchors(leafGroup, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);
            leafGroup.SetActive(false);

            leafButtons = new Button[6];
            leafNames = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                var btn = CreateOptionButton($"Leaf_{i}", leafGroup.GetComponent<RectTransform>(), i, btnH, 6);
                leafButtons[i] = btn;
                leafNames[i] = btn.GetComponentInChildren<Text>();
            }

            leafDescription = CreateTMP("LeafDesc", rt, "", 18,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.14f), Vector2.zero, Vector2.zero);
            leafDescription.alignment = TextAnchor.MiddleCenter;
            leafDescription.color = new Color(0.70f, 0.68f, 0.62f);

            // ── 步骤3：控温 ──
            tempGroup = CreateChild("TempGroup", rt);
            SetAnchors(tempGroup, new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.8f), Vector2.zero, Vector2.zero);
            tempGroup.SetActive(false);

            temperatureValue = CreateTMP("TempValue", tempGroup.GetComponent<RectTransform>(), "85°C", 48,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), new Vector2(0, -70));
            temperatureValue.alignment = TextAnchor.MiddleCenter;
            temperatureValue.color = gold;

            // ━━ 温度滑条（标准 Slider 三件套：背景 / 填充 / 手柄）━━
            var sliderGo = CreateChild("Slider", tempGroup.GetComponent<RectTransform>());
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.1f, 0.45f);
            sliderRt.anchorMax = new Vector2(0.9f, 0.55f);
            sliderRt.sizeDelta = Vector2.zero;
            sliderRt.anchoredPosition = Vector2.zero;
            temperatureSlider = sliderGo.AddComponent<Slider>();
            temperatureSlider.minValue = 60f;
            temperatureSlider.maxValue = 100f;
            temperatureSlider.value = 85f;
            temperatureSlider.direction = Slider.Direction.LeftToRight;

            // 背景条（深色轨道）
            var bgGo = CreateChild("Background", sliderRt);
            bgGo.AddComponent<Image>().color = new Color(0.22f, 0.19f, 0.14f, 0.8f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(0, 4); bgRt.offsetMax = new Vector2(0, -4);

            // 填充区 + 填充条（蓝色→红色渐变）
            var fillAreaGo = CreateChild("Fill Area", sliderRt);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero; fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(4, 8); fillAreaRt.offsetMax = new Vector2(-4, -8);

            var fillGo = CreateChild("Fill", fillAreaRt);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            temperatureMeter = fillGo.AddComponent<Image>();
            temperatureMeter.color = new Color(0.3f, 0.65f, 1f);
            temperatureSlider.fillRect = fillRt;

            // 手柄区 + 手柄（金色圆点）
            var handleAreaGo = CreateChild("Handle Slide Area", sliderRt);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero; handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(8, 0); handleAreaRt.offsetMax = new Vector2(-8, 0);

            var handleGo = CreateChild("Handle", handleAreaRt);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.5f, 0.5f); handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(24, 34);
            handleRt.anchoredPosition = Vector2.zero;
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.75f, 0.62f, 0.35f);  // 金色
            temperatureSlider.handleRect = handleRt;

            // 确认按钮（锚点同时指定 X 和 Y 区间，确保宽高都大于 0）
            temperatureConfirmBtn = CreateTextButton("TempConfirm", tempGroup.GetComponent<RectTransform>(),
                "确认温度", new Vector2(0.3f, 0), new Vector2(0.7f, 0.12f), new Vector2(0, 5));
            temperatureConfirmBtn.onClick.AddListener(OnTemperatureConfirm);

            // 温度区段标签（在 UI 构建时一次性创建）
            CreateTempZoneLabels();

            // ── 步骤4：注水 ──
            pourGroup = CreateChild("PourGroup", rt);
            SetAnchors(pourGroup, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);
            pourGroup.SetActive(false);

            pourButtons = new Button[4];
            string[] pourStyles = { "高冲慢注", "低冲快注", "中位回旋", "沿壁注水" };
            float pourH = 0.8f / 4f;
            for (int i = 0; i < 4; i++)
            {
                pourButtons[i] = CreateTextButton($"Pour_{i}", pourGroup.GetComponent<RectTransform>(),
                    pourStyles[i], new Vector2(0.1f, 1f - (i + 1) * pourH), new Vector2(0.9f, 1f - i * pourH), new Vector2(0, -5));
                PourStyle style = (PourStyle)i;
                pourButtons[i].onClick.AddListener(() => TeaBrewingManager.Instance?.MakeChoice(style));
            }

            // ── 步骤5：出汤 ──
            pourTeaGroup = CreateChild("PourTeaGroup", rt);
            SetAnchors(pourTeaGroup, new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.8f), Vector2.zero, Vector2.zero);
            pourTeaGroup.SetActive(false);

            steepTimerText = CreateTMP("SteepTimer", pourTeaGroup.GetComponent<RectTransform>(), "0.0s", 56,
                new Vector2(0, 0.4f), new Vector2(1, 0.7f), Vector2.zero, Vector2.zero);
            steepTimerText.alignment = TextAnchor.MiddleCenter;
            steepTimerText.color = gold;

            stopSteepButton = CreateTextButton("StopSteep", pourTeaGroup.GetComponent<RectTransform>(),
                "停！出汤", new Vector2(0.2f, 0.05f), new Vector2(0.8f, 0.3f), Vector2.zero);
            // 提高按钮对比度：显眼的暗红底色，确保玩家能看到
            var stopImg = stopSteepButton.GetComponent<Image>();
            if (stopImg != null) stopImg.color = new Color(0.45f, 0.18f, 0.12f, 0.85f);
            stopSteepButton.onClick.AddListener(OnStopSteep);

            // ── 评分分解面板 ──
            _breakdownPanel = CreateChild("BreakdownPanel", rt);
            SetAnchors(_breakdownPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero);
            _breakdownPanel.SetActive(false);

            // 标题
            var bdTitle = CreateTMP("BDTitle", _breakdownPanel.GetComponent<RectTransform>(),
                "评分分解", 24, new Vector2(0, 0.88f), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 0));
            bdTitle.alignment = TextAnchor.MiddleCenter;
            bdTitle.color = new Color(0.82f, 0.70f, 0.50f);

            _breakdownLabels = new Text[BREAKDOWN_COUNT];
            _breakdownBars = new Image[BREAKDOWN_COUNT];
            Color[] barColors = {
                new Color(0.35f, 0.78f, 0.42f),  // 茶叶 - 绿
                new Color(0.65f, 0.45f, 0.28f),  // 茶具 - 褐
                new Color(0.30f, 0.65f, 1.00f),  // 水温 - 蓝
                new Color(0.55f, 0.72f, 0.88f),  // 注水 - 淡蓝
                new Color(0.82f, 0.62f, 0.35f),  // 出汤 - 金色
                new Color(0.85f, 0.45f, 0.72f)   // 发现 - 紫
            };

            for (int i = 0; i < BREAKDOWN_COUNT; i++)
            {
                float yTop = 0.82f - i * 0.12f;
                float yBot = yTop - 0.10f;

                // 标签
                var lbl = CreateTMP($"BDLabel_{i}", _breakdownPanel.GetComponent<RectTransform>(),
                    BREAKDOWN_NAMES[i], 18,
                    new Vector2(0.02f, yBot), new Vector2(0.20f, yTop), Vector2.zero, Vector2.zero);
                lbl.alignment = TextAnchor.MiddleRight;
                lbl.color = new Color(0.88f, 0.84f, 0.76f);
                _breakdownLabels[i] = lbl;

                // 分数条背景
                var barBg = CreateChild($"BDBarBg_{i}", _breakdownPanel.GetComponent<RectTransform>());
                var barBgRt = barBg.GetComponent<RectTransform>();
                barBgRt.anchorMin = new Vector2(0.22f, yBot + 0.02f);
                barBgRt.anchorMax = new Vector2(0.72f, yTop - 0.02f);
                barBgRt.offsetMin = barBgRt.offsetMax = Vector2.zero;
                barBg.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.13f);

                // 分数条填充
                var barFill = CreateChild($"BDBarFill_{i}", barBg.GetComponent<RectTransform>());
                var barFillRt = barFill.GetComponent<RectTransform>();
                barFillRt.anchorMin = Vector2.zero;
                barFillRt.anchorMax = Vector2.one;
                barFillRt.pivot = new Vector2(0, 0.5f);
                barFillRt.offsetMin = new Vector2(2, 2);
                barFillRt.offsetMax = new Vector2(-2, -2);
                var fillImg = barFill.AddComponent<Image>();
                fillImg.color = barColors[i];
                _breakdownBars[i] = fillImg;
            }

            _uiBuilt = true;
            Debug.Log("[TeaBrewingUI] UI 层级已自动构建 (5 步面板)");
        }

        // ── 便捷创建方法 ──

        private GameObject CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private Text CreateTMP(string name, RectTransform parent, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = CreateChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var tmp = go.AddComponent<Text>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = new Color(0.88f, 0.84f, 0.76f);  // 浅米色（与深色 BG 有足够对比度）
            // 使用系统中文字体，若不可用则回退到内置字体
            var chineseFont = Core.FontManager.ChineseFont;
            if (chineseFont != null)
                tmp.font = chineseFont;
            else
                tmp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tmp.raycastTarget = false;  // 不拦截按钮点击
            return tmp;
        }

        private Button CreateTextButton(string name, RectTransform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin)
        {
            var go = CreateChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.18f, 0.75f);
            var btn = go.AddComponent<Button>();
            var lbl = CreateTMP("Label", rt, label, 22,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, 0));
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = new Color(0.90f, 0.86f, 0.78f);
            return btn;
        }

        private Button CreateOptionButton(string name, RectTransform parent, int index, float height, int total)
        {
            var go = CreateChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            float bottom = 1f - (index + 1) * height;
            float top = 1f - index * height;
            rt.anchorMin = new Vector2(0, bottom);
            rt.anchorMax = new Vector2(1, top);
            rt.offsetMin = new Vector2(5, 3);
            rt.offsetMax = new Vector2(-5, -3);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.18f, 0.7f);
            var btn = go.AddComponent<Button>();
            var lbl = CreateTMP("Label", rt, "", 20,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, 0));
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = new Color(0.90f, 0.86f, 0.78f);
            return btn;
        }

        private void SetAnchors(GameObject go, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        void Start()
        {
            HideImmediate();

            // 连接场景中的茶壶蒸汽粒子
            if (steamParticles == null)
            {
                var teapot = GameObject.Find("Prop_Teaware");
                if (teapot != null)
                {
                    _steamEffect = teapot.GetComponent<TeaMist.Rendering.TeaSteamEffect>();
                    if (_steamEffect != null)
                        steamParticles = _steamEffect.steamParticles;
                }
            }

            if (TeaBrewingManager.Instance != null)
            {
                TeaBrewingManager.Instance.OnStepChanged.AddListener(OnStepChanged);
                TeaBrewingManager.Instance.OnBrewingComplete.AddListener(OnBrewingComplete);
            }
        }

        void Update()
        {
            if (isSteeping)
            {
                float elapsed = Time.time - steepStartTime;
                steepTimerText.text = $"{elapsed:F1}s";

                // 茶汤颜色实时渐变
                UpdateSteepingTeaColor(elapsed);
            }
        }

        private void UpdateSteepingTeaColor(float elapsed)
        {
            if (teaSoupImage == null) return;

            // 获取目标茶汤颜色
            Color targetColor = new Color(0.6f, 0.4f, 0.2f); // 默认琥珀色
            var recipe = TeaBrewingManager.Instance?.GetTargetRecipe();
            if (recipe != null) targetColor = recipe.liquorColor;

            // 浸泡0秒=清水，理想时间=推荐色，过久=深褐
            float idealTime = recipe?.idealSteepTime ?? 30f;
            float ratio = Mathf.Clamp01(elapsed / idealTime);

            // 颜色从浅(清水)到目标色，超时后加深变暗
            Color lightColor = Color.Lerp(new Color(0.9f, 0.85f, 0.7f, 0.3f), targetColor, ratio);
            if (elapsed > idealTime)
            {
                // 过久 → 向深褐偏移
                float overRatio = Mathf.Clamp01((elapsed - idealTime) / 15f);
                lightColor = Color.Lerp(targetColor, new Color(0.35f, 0.22f, 0.10f), overRatio);
            }

            teaSoupImage.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.45f);
        }

        void OnDestroy()
        {
            if (TeaBrewingManager.Instance != null)
            {
                if (TeaBrewingManager.Instance.OnStepChanged != null)
                    TeaBrewingManager.Instance.OnStepChanged.RemoveListener(OnStepChanged);
                if (TeaBrewingManager.Instance.OnBrewingComplete != null)
                    TeaBrewingManager.Instance.OnBrewingComplete.RemoveListener(OnBrewingComplete);
            }
        }

        // ━━━ 步骤切换 ━━━

        private void OnStepChanged(BrewingStep step)
        {
            HideAllStepGroups();

            // 步骤推进音效
            AudioManager.Instance?.PlayBrewStepAdvance();

            GameObject activeGroup = null;

            switch (step)
            {
                case BrewingStep.SelectTeaware:
                    ShowTeawareStep();
                    activeGroup = teawareGroup;
                    break;
                case BrewingStep.SelectLeaf:
                    ShowLeafStep();
                    activeGroup = leafGroup;
                    break;
                case BrewingStep.ControlTemp:
                    ShowTempStep();
                    activeGroup = tempGroup;
                    break;
                case BrewingStep.PourWater:
                    ShowPourStep();
                    activeGroup = pourGroup;
                    break;
                case BrewingStep.PourTea:
                    ShowPourTeaStep();
                    activeGroup = pourTeaGroup;
                    break;
            }

            // 步骤过渡动画：新步骤组从 0.9→1.0 快速弹入
            if (activeGroup != null)
            {
                activeGroup.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                StartCoroutine(StepScaleIn(activeGroup.transform));
            }

            UpdateStepIndicator(step);
            Show();
        }

        private System.Collections.IEnumerator StepScaleIn(Transform target)
        {
            float dur = 0.15f;
            float t = 0f;
            Vector3 start = target.localScale;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                // easeOutBack: overshoot and settle
                float s = 1f + 0.3f * Mathf.Sin(p * Mathf.PI * 0.5f);
                target.localScale = Vector3.Lerp(start, Vector3.one, Mathf.Min(p * 1.3f, 1f)) * s;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void UpdateStepIndicator(BrewingStep step)
        {
            int idx = (int)step - 1;
            for (int i = 0; i < stepIndicators.Length; i++)
            {
                if (stepIndicators[i] != null)
                {
                    var img = stepIndicators[i].GetComponent<Image>();
                    if (img != null)
                        img.color = i == idx ? new Color(0.82f, 0.70f, 0.50f) : new Color(0.3f, 0.3f, 0.3f);
                }
            }
        }

        // ━━━ 步骤1：择壶 ━━━

        private void ShowTeawareStep()
        {
            stepTitleText.text = "择壶";
            stepHintText.text = "选一把趁手的壶";

            teawareGroup.SetActive(true);

            var teawares = TeaBrewingManager.Instance?.availableTeawares;
            if (teawares == null) return;

            for (int i = 0; i < teawareButtons.Length; i++)
            {
                if (i < teawares.Length)
                {
                    teawareButtons[i].gameObject.SetActive(true);
                    teawareNames[i].text = teawares[i].displayName;
                    int idx = i;
                    teawareButtons[i].onClick.RemoveAllListeners();
                    teawareButtons[i].onClick.AddListener(() => {
                        teawareDescription.text = teawares[idx].description;
                        AudioManager.Instance?.PlayBrewSelect();
                        TeaBrewingManager.Instance?.MakeChoice(idx);
                    });
                }
                else
                {
                    teawareButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // ━━━ 步骤2：选叶 ━━━

        private void ShowLeafStep()
        {
            stepTitleText.text = "选叶";
            stepHintText.text = "挑一味合心的茶叶";

            leafGroup.SetActive(true);

            var recipes = TeaBrewingManager.Instance?.availableTeaRecipes;
            if (recipes == null) return;

            for (int i = 0; i < leafButtons.Length; i++)
            {
                if (i < recipes.Count)
                {
                    leafButtons[i].gameObject.SetActive(true);
                    leafNames[i].text = recipes[i].teaName;
                    int idx = i;
                    leafButtons[i].onClick.RemoveAllListeners();
                    leafButtons[i].onClick.AddListener(() => {
                        leafDescription.text = recipes[idx].description;
                        AudioManager.Instance?.PlayBrewSelect();
                        TeaBrewingManager.Instance?.MakeChoice(idx);
                    });
                }
                else
                {
                    leafButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // ━━━ 步骤3：控温 ━━━

        private void ShowTempStep()
        {
            stepTitleText.text = "控温";
            stepHintText.text = "调至合适的水温";

            tempGroup.SetActive(true);
            temperatureSlider.onValueChanged.RemoveAllListeners();
            temperatureSlider.onValueChanged.AddListener(v => {
                temperatureValue.text = $"{v:F0}°C";
                temperatureMeter.color = Color.Lerp(new Color(0.4f, 0.7f, 1f), new Color(1f, 0.4f, 0.2f), (v - 60f) / 40f);

                // 更新区段高亮
                UpdateTempZones(v);
            });

            // 读取目标茶谱的理想温度作为提示
            float defaultTemp = 85f;
            var recipe = TeaBrewingManager.Instance?.GetTargetRecipe();
            if (recipe != null)
            {
                defaultTemp = recipe.idealTemperature;
                stepHintText.text = $"推荐水温 {recipe.idealTemperature:F0}°C — 调至合适的水温";
            }

            temperatureSlider.value = defaultTemp;
            temperatureValue.text = $"{defaultTemp:F0}°C";
            UpdateTempZones(defaultTemp);
        }

        private Text[] _tempZoneLabels;

        private void CreateTempZoneLabels()
        {
            _tempZoneLabels = new Text[3];
            var parent = tempGroup.GetComponent<RectTransform>();
            string[] zoneNames = { "凉水", "适中", "沸水" };
            float[] zoneX = { 0.12f, 0.48f, 0.84f };
            for (int i = 0; i < 3; i++)
            {
                var lbl = CreateTMP($"TempZone_{i}", parent, zoneNames[i], 14,
                    new Vector2(zoneX[i], 0.28f), new Vector2(zoneX[i] + 0.08f, 0.38f),
                    Vector2.zero, Vector2.zero);
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = new Color(0.45f, 0.42f, 0.38f);
                _tempZoneLabels[i] = lbl;
            }
        }

        private void UpdateTempZones(float currentTemp)
        {

            // 高亮当前所在区段
            for (int i = 0; i < 3; i++)
            {
                bool active = i switch
                {
                    0 => currentTemp < 70f,
                    1 => currentTemp >= 70f && currentTemp <= 90f,
                    2 => currentTemp > 90f,
                    _ => false
                };
                _tempZoneLabels[i].color = active
                    ? new Color(0.95f, 0.85f, 0.55f)   // 金色高亮
                    : new Color(0.45f, 0.42f, 0.38f);  // 暗灰
            }
        }

        public void OnTemperatureConfirm()
        {
            AudioManager.Instance?.PlayBrewTempConfirm();
            TeaBrewingManager.Instance?.MakeChoice(temperatureSlider.value);
        }

        // ━━━ 步骤4：注水 ━━━

        private void ShowPourStep()
        {
            stepTitleText.text = "注水";

            // 读取推荐手法
            var recipe = TeaBrewingManager.Instance?.GetTargetRecipe();
            PourStyle ideal = recipe != null ? recipe.idealPourStyle : PourStyle.MidSpiral;
            string idealName = ideal switch
            {
                PourStyle.HighSlow => "高冲慢注",
                PourStyle.LowFast => "低冲快注",
                PourStyle.MidSpiral => "中位回旋",
                PourStyle.EdgePour => "沿壁注水",
                _ => "中位回旋"
            };
            stepHintText.text = $"推荐「{idealName}」— 选择注水手法";

            pourGroup.SetActive(true);

            string[] labels = { "高冲慢注", "低冲快注", "中位回旋", "沿壁注水" };
            PourStyle[] styles = { PourStyle.HighSlow, PourStyle.LowFast, PourStyle.MidSpiral, PourStyle.EdgePour };

            for (int i = 0; i < pourButtons.Length; i++)
            {
                if (i < labels.Length)
                {
                    pourButtons[i].gameObject.SetActive(true);
                    pourButtons[i].GetComponentInChildren<Text>().text = labels[i];
                    PourStyle style = styles[i];
                    pourButtons[i].onClick.RemoveAllListeners();
                    pourButtons[i].onClick.AddListener(() => {
                        AudioManager.Instance?.PlayBrewWaterPour();
                        TeaBrewingManager.Instance?.MakeChoice(style);
                    });
                }
                else
                {
                    pourButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // ━━━ 步骤5：出汤 ━━━

        private void ShowPourTeaStep()
        {
            stepTitleText.text = "出汤";
            stepHintText.text = "把握出汤时机，点「停」出汤";

            pourTeaGroup.SetActive(true);
            isSteeping = true;
            steepStartTime = Time.time;

            // 蒸汽加浓：出汤时热气最盛
            if (steamParticles != null)
            {
                var emission = steamParticles.emission;
                _normalEmissionRate = emission.rateOverTime.constant;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(10f, 18f);
                if (!steamParticles.isPlaying) steamParticles.Play();
            }

            // 茶汤颜色淡入
            if (teaSoupImage == null)
                EnsureTeaSoupImage();
            if (teaSoupImage != null)
                StartCoroutine(FadeTeaSoup(true));
        }

        public void OnStopSteep()
        {
            isSteeping = false;
            float steepTime = Time.time - steepStartTime;
            AudioManager.Instance?.PlayBrewPourTea();
            TeaBrewingManager.Instance?.MakeChoice(steepTime);

            // 蒸汽回到正常速率
            if (steamParticles != null)
            {
                var emission = steamParticles.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_normalEmissionRate, _normalEmissionRate * 1.5f);
            }
        }

        // ━━━ 泡茶完成 ━━━

        private void OnBrewingComplete(int score)
        {
            // 停止计时器并清理步骤组
            isSteeping = false;
            if (steamParticles != null) steamParticles.Stop();
            if (_steamEffect != null) _steamEffect.Stop();
            HideAllStepGroups();

            // 茶汤定色：根据品质显示不同茶色
            if (teaSoupImage != null)
            {
                teaSoupImage.color = score >= 90
                    ? new Color(0.82f, 0.55f, 0.18f, 0.6f)   // 完美 → 金黄透亮
                    : score >= 60
                    ? new Color(0.7f, 0.45f, 0.25f, 0.55f)    // 不错 → 琥珀色
                    : new Color(0.5f, 0.35f, 0.2f, 0.5f);     // 勉强 → 深褐
                StartCoroutine(FadeTeaSoup(false));
            }

            // 标题 & 评分
            stepTitleText.text = score >= 90 ? "完美！" : score >= 60 ? "不错" : "还行";
            stepHintText.text = $"品质评分: {score}/100";

            // 结果音效
            AudioManager.Instance?.PlayBrewResult(score);

            // 评分分解面板
            ShowBreakdown();

            // 延长显示时间（评分分解需要更多阅读时间）
            StartCoroutine(HideAfterDelay(3.5f));
        }

        private void ShowBreakdown()
        {
            var breakdown = TeaBrewingManager.Instance?.lastBreakdown;
            if (!breakdown.HasValue) return;
            var bd = breakdown.Value;

            _breakdownPanel.SetActive(true);

            float[] scores = { bd.leafScore, bd.teawareScore, bd.tempScore,
                               bd.pourScore, bd.timeScore, bd.discoveryBonus };

            for (int i = 0; i < BREAKDOWN_COUNT; i++)
            {
                // 更新标签：维度名 + 分数/满分
                _breakdownLabels[i].text = $"{BREAKDOWN_NAMES[i]}  {scores[i]:F0}/{BREAKDOWN_MAX[i]:F0}";

                // 更新条形填充宽度
                float ratio = Mathf.Clamp01(scores[i] / BREAKDOWN_MAX[i]);
                var barRt = _breakdownBars[i].GetComponent<RectTransform>();
                // 通过调整 anchorMax.x 来改变宽度
                barRt.anchorMax = new Vector2(ratio, 1f);

                // 发现加分特殊处理：没有加分时变灰
                if (i == 5 && bd.discoveryBonus <= 0f)
                    _breakdownBars[i].color = new Color(0.3f, 0.3f, 0.3f);
            }
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Hide();
            HideAllStepGroups();
        }

        // ━━━ 显示/隐藏 ━━━

        public void Show()
        {
            StartCoroutine(FadeTo(1f));
        }

        public void Hide()
        {
            StartCoroutine(FadeTo(0f));
        }

        private void HideImmediate()
        {
            isSteeping = false;
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            HideAllStepGroups();
        }

        // ━━━ 茶汤显色 ━━━

        private void EnsureTeaSoupImage()
        {
            if (teaSoupImage != null) return;
            var go = CreateChild("TeaSoup", pourTeaGroup.GetComponent<RectTransform>());
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 0.35f);
            rt.anchorMax = new Vector2(0.7f, 0.65f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            teaSoupImage = go.AddComponent<Image>();
            teaSoupImage.color = new Color(0.8f, 0.55f, 0.2f, 0f);  // 初始透明
            teaSoupImage.raycastTarget = false;
        }

        private System.Collections.IEnumerator FadeTeaSoup(bool fadeIn)
        {
            if (teaSoupImage == null) yield break;
            float dur = 0.6f;
            float t = 0f;
            float startA = teaSoupImage.color.a;
            float targetA = fadeIn ? 0.5f : 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                var c = teaSoupImage.color;
                c.a = Mathf.Lerp(startA, targetA, t / dur);
                teaSoupImage.color = c;
                yield return null;
            }
        }

        private IEnumerator FadeTo(float target)
        {
            panelGroup.interactable = target > 0.5f;
            panelGroup.blocksRaycasts = target > 0.5f;
            float elapsed = 0f;
            float start = panelGroup.alpha;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = target;
        }

        private void HideAllStepGroups()
        {
            teawareGroup.SetActive(false);
            leafGroup.SetActive(false);
            tempGroup.SetActive(false);
            pourGroup.SetActive(false);
            pourTeaGroup.SetActive(false);
            if (_breakdownPanel != null) _breakdownPanel.SetActive(false);
        }
    }
}
