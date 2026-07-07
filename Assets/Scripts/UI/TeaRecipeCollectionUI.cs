using UnityEngine;
using UnityEngine.UI;
// using TMPro; — 已迁移到 Legacy UI.Text
using System.Collections.Generic;
using System.Collections;
using TeaMist.Core;
using TeaMist.Data;
using TeaMist.Dialogue;

namespace TeaMist.UI
{
    /// <summary>
    /// 茶谱收集 UI —— 画册式可翻阅茶谱/碎片/好感图鉴。
    /// 三标签页：茶谱 · 碎片 · 雅客
    /// 数据来源：DataManager（茶谱/碎片）、DialogueManager.variableStorage（好感度）
    /// </summary>
    public class TeaRecipeCollectionUI : MonoBehaviour
    {
        [Header("━━━ 主面板 ━━━")]
        public CanvasGroup panelGroup;
        public float fadeDuration = 0.35f;

        [Header("━━━ 颜色 ━━━")]
        public Color inkColor = new Color(0.15f, 0.13f, 0.10f);
        public Color parchmentColor = new Color(0.94f, 0.91f, 0.84f, 0.97f);
        public Color sealRed = new Color(0.65f, 0.18f, 0.12f);
        public Color goldAccent = new Color(0.70f, 0.55f, 0.30f);
        public Color tabActiveColor = new Color(0.82f, 0.78f, 0.68f);
        public Color tabInactiveColor = new Color(0.55f, 0.50f, 0.42f);
        public Color unlockedGreen = new Color(0.30f, 0.55f, 0.30f);
        public Color lockedGray = new Color(0.45f, 0.42f, 0.38f);

        // ── 内部引用 ──
        private GameObject _titleBar;
        private GameObject _tabBar;
        private GameObject _contentArea;
        private RectTransform _contentRt;

        // 标签按钮
        private Button _tabRecipes;
        private Button _tabFragments;
        private Button _tabNPCs;
        private Text _tabRecipesLabel;
        private Text _tabFragmentsLabel;
        private Text _tabNPCLabel;

        // 内容视图
        private GameObject _recipeView;
        private GameObject _fragmentView;
        private GameObject _npcView;

        // 分页
        private int _recipePage;
        private int _fragmentPage;
        private const int ITEMS_PER_PAGE = 8;

        // 章节筛选
        private int _fragmentChapterFilter = -1; // -1=全部
        private bool _longScrollMode;              // 长卷模式
        private HashSet<int> _collapsedChapters = new HashSet<int>(); // 折叠状态
        private GameObject _fragmentDetailPopup;

        // 山的故事 9 章名称
        private static readonly string[] _chapterNames = {
            "", "一·太古", "二·灵脉", "三·金云", "四·裂隙",
            "五·断弦", "六·长夜", "七·微光", "八·醒", "九·名"
        };

        private bool _uiBuilt;
        private bool _isVisible;

        // ━━━ 生命周期 ━━━

        void Awake()
        {
            if (panelGroup == null)
                panelGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureUIHierarchy();
        }

        void Start()
        {
            HideImmediate();
        }

        void Update()
        {
            // Esc 关闭
            if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        // ━━━ 公共 API ━━━

        public void Show()
        {
            if (_isVisible) return;
            EnsureUIHierarchy(); // 防御：如果 Awake 被跳过则补建 UI
            _isVisible = true;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
            RefreshCurrentView();
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        public void Toggle()
        {
            if (_isVisible) Hide(); else Show();
        }

        private void HideImmediate()
        {
            _isVisible = false;
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        private IEnumerator FadeIn()
        {
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
            float start = panelGroup.alpha;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                panelGroup.alpha = Mathf.Lerp(start, 1f, t / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            float start = panelGroup.alpha;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                panelGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        // ━━━ UI 自建 ━━━

        private void EnsureUIHierarchy()
        {
            if (_uiBuilt) return;

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            // 宣纸背景
            var bgImg = GetComponent<Image>();
            if (bgImg == null) bgImg = gameObject.AddComponent<Image>();
            bgImg.color = parchmentColor;
            ApplyInkStyle(bgImg, parchmentColor, 1.0f);

            // ── 标题栏 ──
            BuildTitleBar(rt);

            // ── 标签栏 ──
            BuildTabBar(rt);

            // ── 内容区 ──
            BuildContentArea(rt);

            // 全部建造完成才标记，防止中途失败导致半初始化状态
            _uiBuilt = true;
            Debug.Log("[TeaRecipeCollectionUI] UI 层级已自动构建");
        }

        private void BuildTitleBar(RectTransform parentRt)
        {
            _titleBar = CreateChild("TitleBar", parentRt);
            var tbRt = _titleBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 1);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.pivot = new Vector2(0.5f, 1);
            tbRt.sizeDelta = new Vector2(0, 60);

            // 标题
            var title = CreateTMP("Title", tbRt, "茶  烟  录", 32,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -24), new Vector2(200, 24));
            title.alignment = TextAnchor.MiddleCenter;
            title.fontStyle = FontStyle.Bold;
            title.color = inkColor;

            // 下划线装饰
            var line = CreateChild("TitleLine", tbRt);
            var lineRt = line.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0.2f, 0);
            lineRt.anchorMax = new Vector2(0.8f, 0);
            lineRt.pivot = new Vector2(0.5f, 0);
            lineRt.sizeDelta = new Vector2(0, 2);
            var lineImg = line.AddComponent<Image>();
            ApplyInkStyle(lineImg, goldAccent, 4.0f);

            // 关闭按钮
            var closeBtn = CreateChild("CloseBtn", tbRt);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 1);
            closeRt.anchorMax = new Vector2(1, 1);
            closeRt.pivot = new Vector2(1, 1);
            closeRt.anchoredPosition = new Vector2(-15, -12);
            closeRt.sizeDelta = new Vector2(40, 40);
            var closeImg = closeBtn.AddComponent<Image>();
            ApplyInkStyle(closeImg, new Color(0.4f, 0.15f, 0.12f, 0.6f), 3.0f);
            var closeButton = closeBtn.AddComponent<Button>();
            closeButton.onClick.AddListener(Hide);
            var closeLabel = CreateTMP("X", closeRt, "✕", 22,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = new Color(0.92f, 0.88f, 0.78f);
        }

        private void BuildTabBar(RectTransform parentRt)
        {
            _tabBar = CreateChild("TabBar", parentRt);
            var tbRt = _tabBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 1);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.pivot = new Vector2(0.5f, 1);
            tbRt.anchoredPosition = new Vector2(0, -60);
            tbRt.sizeDelta = new Vector2(0, 50);

            // 三个标签
            _tabRecipes = CreateTabButton("Tab_Recipes", tbRt, "茶  谱", 0);
            _tabFragments = CreateTabButton("Tab_Fragments", tbRt, "碎  片", 1);
            _tabNPCs = CreateTabButton("Tab_NPCs", tbRt, "雅  客", 2);

            _tabRecipesLabel = _tabRecipes.GetComponentInChildren<Text>();
            _tabFragmentsLabel = _tabFragments.GetComponentInChildren<Text>();
            _tabNPCLabel = _tabNPCs.GetComponentInChildren<Text>();

            _tabRecipes.onClick.AddListener(() => SwitchTab(0));
            _tabFragments.onClick.AddListener(() => SwitchTab(1));
            _tabNPCs.onClick.AddListener(() => SwitchTab(2));
        }

        private void BuildContentArea(RectTransform parentRt)
        {
            _contentArea = CreateChild("ContentArea", parentRt);
            _contentRt = _contentArea.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0.03f, 0.03f);
            _contentRt.anchorMax = new Vector2(0.97f, 0.97f);
            _contentRt.offsetMin = new Vector2(10, 10);
            _contentRt.offsetMax = new Vector2(-10, -115);

            // 三个内容视图（初始全隐藏）
            _recipeView = CreateChild("RecipeView", _contentRt);
            _fragmentView = CreateChild("FragmentView", _contentRt);
            _npcView = CreateChild("NPCView", _contentRt);

            _recipeView.SetActive(false);
            _fragmentView.SetActive(false);
            _npcView.SetActive(false);
        }

        // ━━━ 标签切换 ━━━

        private enum Tab { Recipes, Fragments, NPCs }
        private Tab _currentTab = Tab.Recipes;

        private void SwitchTab(int tabIndex)
        {
            _currentTab = (Tab)tabIndex;
            UpdateTabStyles();
            RefreshCurrentView();
        }

        private void UpdateTabStyles()
        {
            _tabRecipesLabel.color = _currentTab == Tab.Recipes ? tabActiveColor : tabInactiveColor;
            _tabFragmentsLabel.color = _currentTab == Tab.Fragments ? tabActiveColor : tabInactiveColor;
            _tabNPCLabel.color = _currentTab == Tab.NPCs ? tabActiveColor : tabInactiveColor;
        }

        private void RefreshCurrentView()
        {
            if (!_isVisible) return;
            if (_recipeView == null || _fragmentView == null || _npcView == null)
            {
                // 尝试补建 UI（可能 Awake 阶段被 TMP 检查拦截了）
                EnsureUIHierarchy();
                if (_recipeView == null || _fragmentView == null || _npcView == null)
                {
                    Debug.LogError("[TeaRecipeCollectionUI] 视图容器未初始化，跳过刷新。" +
                        $"_recipeView={_recipeView != null}, _fragmentView={_fragmentView != null}, _npcView={_npcView != null}");
                    return;
                }
            }

            _recipeView.SetActive(false);
            _fragmentView.SetActive(false);
            _npcView.SetActive(false);

            switch (_currentTab)
            {
                case Tab.Recipes:
                    _recipeView.SetActive(true);
                    RefreshRecipeView();
                    break;
                case Tab.Fragments:
                    _fragmentView.SetActive(true);
                    RefreshFragmentView();
                    break;
                case Tab.NPCs:
                    _npcView.SetActive(true);
                    RefreshNPCView();
                    break;
            }
        }

        // ━━━ 茶谱视图中转 ━━━

        private void RefreshRecipeView()
        {
            ClearChildren(_recipeView);
            var rt = _recipeView.GetComponent<RectTransform>();

            if (DataManager.Instance == null) return;

            var allRecipes = DataManager.Instance.teaRecipes;
            var unlocked = DataManager.Instance.UnlockedTeaRecipes;
            int total = allRecipes.Count;
            int unlockedCount = unlocked.Count;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)total / ITEMS_PER_PAGE));

            // 进度提示
            var progressText = CreateTMP("RecipeProgress", rt,
                $"已收录 {unlockedCount}/{total} 品", 20,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -22), new Vector2(-12, 40));
            progressText.alignment = TextAnchor.UpperLeft;
            progressText.color = goldAccent;

            // 没有数据
            if (total == 0)
            {
                var empty = CreateTMP("NoRecipes", rt, "暂无茶谱", 22,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-100, -20), new Vector2(100, 20));
                empty.alignment = TextAnchor.MiddleCenter;
                empty.color = lockedGray;
                return;
            }

            // 翻页控制
            _recipePage = Mathf.Clamp(_recipePage, 0, totalPages - 1);
            BuildPagination(rt, _recipePage, totalPages, () => {
                _recipePage--; RefreshRecipeView();
            }, () => {
                _recipePage++; RefreshRecipeView();
            });

            // 茶谱卡片
            int start = _recipePage * ITEMS_PER_PAGE;
            int end = Mathf.Min(start + ITEMS_PER_PAGE, total);
            float cardHeight = 1f / ITEMS_PER_PAGE;

            for (int i = start; i < end; i++)
            {
                var recipe = allRecipes[i];
                bool isUnlocked = unlocked.Contains(recipe.teaName);
                int rowIndex = i - start;
                BuildRecipeCard(rt, recipe, isUnlocked, rowIndex, cardHeight, ITEMS_PER_PAGE - (end - start));
            }
        }

        private void BuildRecipeCard(RectTransform parent, TeaRecipeSO recipe, bool isUnlocked,
            int rowIndex, float cardHeight, int emptySlots)
        {
            float top = 1f - rowIndex * cardHeight;
            // 调整底部留空间给分页按钮
            float bottom = top - cardHeight;

            var card = CreateChild($"Recipe_{recipe.teaName}", parent);
            var cRt = card.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.02f, bottom);
            cRt.anchorMax = new Vector2(0.98f, top - 0.005f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            // 背景
            var bg = card.AddComponent<Image>();
            Color cardColor = isUnlocked
                ? new Color(0.92f, 0.95f, 0.88f, 0.5f)
                : new Color(0.85f, 0.83f, 0.80f, 0.35f);
            ApplyInkStyle(bg, cardColor, 2.0f);

            // 图标区域
            if (recipe.icon != null)
            {
                var iconGo = CreateChild("Icon", cRt);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0, 0.1f);
                iconRt.anchorMax = new Vector2(0, 0.9f);
                iconRt.sizeDelta = new Vector2(50, 0);
                iconRt.anchoredPosition = new Vector2(15, 0);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = recipe.icon;
                iconImg.preserveAspect = true;
            }

            // 茶名
            float nameX = recipe.icon != null ? 80 : 20;
            var nameText = CreateTMP("Name", cRt,
                isUnlocked ? recipe.teaName : "？？？", 24,
                new Vector2(0, 0.5f), new Vector2(0.72f, 0.5f),
                new Vector2(nameX, -18), new Vector2(0, 18));
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = isUnlocked ? inkColor : lockedGray;

            // 锁定状态标签
            var statusLabel = CreateTMP("Status", cRt,
                isUnlocked ? "✓ 已收录" : "未解锁", 18,
                new Vector2(0.72f, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -16), new Vector2(-12, 16));
            statusLabel.alignment = TextAnchor.MiddleRight;
            statusLabel.color = isUnlocked ? unlockedGreen : sealRed;

            // 稀有度标识
            if (isUnlocked)
            {
                string rarityStr = recipe.rarity switch
                {
                    TeaRarity.常见 => "·常见",
                    TeaRarity.少见 => "·少见",
                    TeaRarity.稀有 => "·稀有",
                    TeaRarity.传说 => "◇ 传说",
                    _ => ""
                };
                var rarityLabel = CreateTMP("Rarity", cRt, rarityStr, 16,
                    new Vector2(0, 0.15f), new Vector2(0.72f, 0.15f),
                    new Vector2(nameX, -18), new Vector2(0, 14));
                rarityLabel.alignment = TextAnchor.LowerLeft;
                rarityLabel.color = recipe.rarity == TeaRarity.传说 ? goldAccent : lockedGray;
            }
        }

        // ━━━ 碎片视图中转 ━━━

        private void RefreshFragmentView()
        {
            ClearChildren(_fragmentView);
            var rt = _fragmentView.GetComponent<RectTransform>();

            if (DataManager.Instance == null) return;

            var allFragments = DataManager.Instance.fragments;
            // 优先从 NarrativeStateManager 读取收集状态
            var collected = new HashSet<string>();
            var nsm = NarrativeStateManager.Instance;
            if (nsm != null)
            {
                foreach (var fid in nsm.GetCollectedFragments())
                    collected.Add(fid);
            }
            // 回退到 DataManager
            if (collected.Count == 0)
            {
                foreach (var fid in DataManager.Instance.CollectedFragments)
                    collected.Add(fid);
            }

            int collectedCount = collected.Count;
            int total = allFragments.Count;

            if (total == 0)
            {
                var empty = CreateTMP("NoFragments", rt, "暂无碎片", 22,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-100, -20), new Vector2(100, 20));
                empty.alignment = TextAnchor.MiddleCenter;
                empty.color = lockedGray;
                return;
            }

            // 进度 + 长卷切换
            var topBar = CreateChild("TopBar", rt);
            var tbRt = topBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 1);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.sizeDelta = new Vector2(0, 44);
            tbRt.anchoredPosition = new Vector2(0, -8);

            var progress = CreateTMP("FragProgress", tbRt,
                $"碎片：{collectedCount}/{total}", 20,
                new Vector2(0, 0), new Vector2(0.4f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
            progress.alignment = TextAnchor.MiddleLeft;
            progress.color = goldAccent;

            var scrollToggle = CreateChild("ScrollToggle", tbRt);
            var stRt = scrollToggle.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.7f, 0.1f);
            stRt.anchorMax = new Vector2(1, 0.9f);
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;
            var stImg = scrollToggle.AddComponent<Image>();
            Color stColor = _longScrollMode ? new Color(0.25f, 0.22f, 0.18f, 0.7f) : new Color(0.5f, 0.45f, 0.4f, 0.5f);
            ApplyInkStyle(stImg, stColor, 2.0f);
            var stBtn = scrollToggle.AddComponent<Button>();
            stBtn.onClick.AddListener(() => {
                _longScrollMode = !_longScrollMode;
                _fragmentPage = 0;
                _fragmentChapterFilter = -1;
                RefreshFragmentView();
            });
            var stLabel = CreateTMP("Label", stRt,
                _longScrollMode ? "◁ 列表" : "长卷 ▷", 18,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            stLabel.alignment = TextAnchor.MiddleCenter;
            stLabel.color = _longScrollMode ? tabActiveColor : new Color(0.82f, 0.78f, 0.68f);

            // ── 分支：长卷模式 or 列表模式 ──
            if (_longScrollMode)
            {
                BuildLongScrollView(rt, allFragments, collected);
                return;
            }

            // 章节筛选
            BuildChapterFilter(rt);

            // 筛选碎片
            var filtered = new List<FragmentSO>();
            foreach (var f in allFragments)
            {
                if (_fragmentChapterFilter < 0 || f.chapter == _fragmentChapterFilter)
                    filtered.Add(f);
            }

            // 分页
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)filtered.Count / ITEMS_PER_PAGE));
            _fragmentPage = Mathf.Clamp(_fragmentPage, 0, totalPages - 1);

            BuildPagination(rt, _fragmentPage, totalPages,
                () => { _fragmentPage--; RefreshFragmentView(); },
                () => { _fragmentPage++; RefreshFragmentView(); });

            // 卡片
            int start = _fragmentPage * ITEMS_PER_PAGE;
            int end = Mathf.Min(start + ITEMS_PER_PAGE, filtered.Count);
            float cardHeight = 1f / ITEMS_PER_PAGE;

            for (int i = start; i < end; i++)
            {
                var frag = filtered[i];
                bool isCollected = collected.Contains(frag.fragmentId);
                BuildFragmentCard(rt, frag, isCollected, i - start, cardHeight);
            }
        }

        private void BuildFragmentCard(RectTransform parent, FragmentSO frag, bool isCollected,
            int rowIndex, float cardHeight)
        {
            float top = 1f - rowIndex * cardHeight;
            float bottom = top - cardHeight;

            var card = CreateChild($"Frag_{frag.fragmentId}", parent);
            var cRt = card.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.02f, bottom);
            cRt.anchorMax = new Vector2(0.98f, top - 0.005f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var bg = card.AddComponent<Image>();
            Color fragCardColor = isCollected
                ? new Color(0.92f, 0.95f, 0.88f, 0.5f)
                : new Color(0.88f, 0.86f, 0.82f, 0.25f);
            ApplyInkStyle(bg, fragCardColor, 2.0f);

            // 碎片类型图标
            string typeIcon = frag.fragmentType switch
            {
                FragmentType.叙事 => "◆",
                FragmentType.经营 => "◇",
                FragmentType.彩蛋 => "★",
                FragmentType.记忆 => "○",
                _ => "·"
            };

            var typeLabel = CreateTMP("Type", cRt, typeIcon, 22,
                new Vector2(0, 0.3f), new Vector2(0, 0.7f),
                new Vector2(15, 0), new Vector2(40, 0));
            typeLabel.alignment = TextAnchor.MiddleCenter;
            typeLabel.color = isCollected ? goldAccent : lockedGray;

            // 标题
            var titleText = CreateTMP("Title", cRt,
                isCollected ? frag.fragmentTitle : "？？？", 22,
                new Vector2(0, 0.5f), new Vector2(0.72f, 0.5f),
                new Vector2(50, -16), new Vector2(0, 16));
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = isCollected ? inkColor : lockedGray;

            // 状态
            var statusLabel = CreateTMP("Status", cRt,
                isCollected ? "✓ 已收集" : $"第{frag.chapter}章", 16,
                new Vector2(0.72f, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -14), new Vector2(-12, 14));
            statusLabel.alignment = TextAnchor.MiddleRight;
            statusLabel.color = isCollected ? unlockedGreen : lockedGray;

            // 已收集卡片可点击查看详情
            if (isCollected)
            {
                var cardBtn = card.AddComponent<Button>();
                var captured = frag;
                cardBtn.onClick.AddListener(() => ShowFragmentDetail(captured));
            }
        }

        private void BuildChapterFilter(RectTransform parent)
        {
            var filterBar = CreateChild("ChapterFilter", parent);
            var fbRt = filterBar.GetComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0.5f, 1);
            fbRt.anchorMax = new Vector2(1, 1);
            fbRt.pivot = new Vector2(0.5f, 1);
            fbRt.sizeDelta = new Vector2(0, 36);
            fbRt.anchoredPosition = new Vector2(0, -24);

            // "全部"按钮
            var allBtn = CreateFilterButton(fbRt, "全部", 0, -1);
            allBtn.onClick.AddListener(() => {
                _fragmentChapterFilter = -1;
                _fragmentPage = 0;
                RefreshFragmentView();
            });

            // 章节按钮 1-9
            for (int ch = 1; ch <= 9; ch++)
            {
                int chapter = ch;
                var btn = CreateFilterButton(fbRt, $"第{ch}章", ch, ch);
                btn.onClick.AddListener(() => {
                    _fragmentChapterFilter = chapter;
                    _fragmentPage = 0;
                    RefreshFragmentView();
                });
            }
        }

        private Button CreateFilterButton(RectTransform parent, string label, int index, int chapterValue)
        {
            var go = CreateChild($"Filt_{index}", parent);
            var rt = go.GetComponent<RectTransform>();
            float w = 1f / 11f;
            rt.anchorMin = new Vector2(index * w, 0);
            rt.anchorMax = new Vector2((index + 1) * w, 1);
            rt.offsetMin = new Vector2(2, 0);
            rt.offsetMax = new Vector2(-2, 0);

            var img = go.AddComponent<Image>();
            Color filtColor = _fragmentChapterFilter == chapterValue
                ? new Color(0.75f, 0.70f, 0.60f, 0.6f)
                : new Color(0.55f, 0.50f, 0.42f, 0.3f);
            ApplyInkStyle(img, filtColor, 2.0f);

            var btn = go.AddComponent<Button>();
            var tmp = CreateTMP("Label", rt, label, 12,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            tmp.alignment = TextAnchor.MiddleCenter;
            tmp.color = _fragmentChapterFilter == chapterValue ? inkColor : lockedGray;
            return btn;
        }

        // ━━━ NPC 好感视图 ━━━

        private void RefreshNPCView()
        {
            ClearChildren(_npcView);
            var rt = _npcView.GetComponent<RectTransform>();

            if (DataManager.Instance == null) return;

            var npcList = DataManager.Instance.npcProfiles;
            if (npcList == null || npcList.Count == 0)
            {
                var empty = CreateTMP("NoNPCs", rt, "尚无雅客到访", 22,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-120, -20), new Vector2(120, 20));
                empty.alignment = TextAnchor.MiddleCenter;
                empty.color = lockedGray;
                return;
            }

            var storage = DialogueManager.Instance?.variableStorage;
            float cardHeight = Mathf.Min(1f / npcList.Count, 0.22f);

            for (int i = 0; i < npcList.Count; i++)
            {
                var npc = npcList[i];
                string npcPinyinId = NpcNameToId(npc.npcName);
                int affection = storage?.GetAffection(npcPinyinId) ?? 0;
                BuildNPCCard(rt, npc, affection, i, cardHeight, npcList.Count);
            }
        }

        /// <summary>NPC 中文名 → NPC 短 ID（委托 DataManager 权威来源）</summary>
        private static string NpcNameToId(string chineseName)
            => Core.DataManager.GetNpcId(chineseName);

        private void BuildNPCCard(RectTransform parent, NPCProfileSO npc, int affection,
            int index, float cardHeight, int total)
        {
            float spacing = (1f - cardHeight * total) / (total + 1);
            float bottom = spacing + index * (cardHeight + spacing);
            float top = bottom + cardHeight;

            var card = CreateChild($"NPC_{npc.npcName}", parent);
            var cRt = card.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.03f, bottom);
            cRt.anchorMax = new Vector2(0.97f, top);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var bg = card.AddComponent<Image>();
            ApplyInkStyle(bg, new Color(0.92f, 0.90f, 0.86f, 0.45f), 2.0f);

            // NPC 名称
            var nameText = CreateTMP("Name", cRt, npc.npcName, 26,
                new Vector2(0, 0.45f), new Vector2(0.35f, 0.75f),
                new Vector2(18, 0), new Vector2(0, 0));
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = inkColor;

            // 来访次数
            var visitText = CreateTMP("Visits", cRt,
                $"到访 {npc.runtimeVisitCount} 次", 16,
                new Vector2(0, 0.15f), new Vector2(0.35f, 0.45f),
                new Vector2(18, 0), new Vector2(0, 0));
            visitText.alignment = TextAnchor.MiddleLeft;
            visitText.color = lockedGray;

            // 好感度标签
            var affLabel = CreateTMP("AffLabel", cRt, "好感", 18,
                new Vector2(0.4f, 0.4f), new Vector2(0.55f, 0.7f),
                new Vector2(0, 0), new Vector2(0, 0));
            affLabel.alignment = TextAnchor.MiddleLeft;
            affLabel.color = goldAccent;

            // 好感度数值
            var affValue = CreateTMP("AffValue", cRt, affection.ToString(), 28,
                new Vector2(0.55f, 0.4f), new Vector2(0.68f, 0.7f),
                new Vector2(0, 0), new Vector2(0, 0));
            affValue.alignment = TextAnchor.MiddleLeft;
            affValue.color = affection >= 60 ? unlockedGreen
                : affection >= 30 ? goldAccent
                : inkColor;

            // 好感度条
            var barBg = CreateChild("AffBarBg", cRt);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0.72f, 0.4f);
            barBgRt.anchorMax = new Vector2(0.95f, 0.65f);
            barBgRt.offsetMin = Vector2.zero;
            barBgRt.offsetMax = Vector2.zero;
            var barBgImg = barBg.AddComponent<Image>();
            ApplyInkStyle(barBgImg, new Color(0.75f, 0.72f, 0.68f, 0.4f), 4.0f);

            var barFill = CreateChild("AffBarFill", barBgRt);
            var barFillRt = barFill.GetComponent<RectTransform>();
            float fillRatio = Mathf.Clamp01(affection / 100f);
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = new Vector2(fillRatio, 1);
            barFillRt.offsetMin = Vector2.zero;
            barFillRt.offsetMax = Vector2.zero;
            var barFillImg = barFill.AddComponent<Image>();
            Color affFillColor = affection >= 60
                ? new Color(0.3f, 0.6f, 0.3f)
                : affection >= 30
                    ? goldAccent
                    : new Color(0.6f, 0.5f, 0.3f);
            ApplyInkStyle(barFillImg, affFillColor, 5.0f);
        }

        // ━━━ 分页控件 ━━━

        private void BuildPagination(RectTransform parent, int currentPage, int totalPages,
            System.Action onPrev, System.Action onNext)
        {
            if (totalPages <= 1) return;

            float btnW = 80, btnH = 30, margin = 10;
            float bottomY = margin;

            // 页码文本
            var pageText = CreateTMP("PageInfo", parent,
                $"{currentPage + 1} / {totalPages}", 16,
                new Vector2(0.35f, 0), new Vector2(0.65f, 0),
                new Vector2(0, bottomY), new Vector2(0, bottomY + btnH));
            pageText.alignment = TextAnchor.MiddleCenter;
            pageText.color = lockedGray;

            // 上一页
            if (currentPage > 0)
            {
                var prevBtn = CreateChild("BtnPrev", parent);
                var prevRt = prevBtn.GetComponent<RectTransform>();
                prevRt.anchorMin = new Vector2(0, 0);
                prevRt.anchorMax = new Vector2(0, 0);
                prevRt.pivot = new Vector2(0, 0);
                prevRt.anchoredPosition = new Vector2(margin, bottomY);
                prevRt.sizeDelta = new Vector2(btnW, btnH);
                var prevImg = prevBtn.AddComponent<Image>();
                ApplyInkStyle(prevImg, new Color(0.25f, 0.22f, 0.18f, 0.6f), 2.0f);
                var prevButton = prevBtn.AddComponent<Button>();
                prevButton.onClick.AddListener(() => onPrev());
                var prevLabel = CreateTMP("Label", prevRt, "◁ 上一页", 14,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                prevLabel.alignment = TextAnchor.MiddleCenter;
                prevLabel.color = new Color(0.9f, 0.87f, 0.8f);
            }

            // 下一页
            if (currentPage < totalPages - 1)
            {
                var nextBtn = CreateChild("BtnNext", parent);
                var nextRt = nextBtn.GetComponent<RectTransform>();
                nextRt.anchorMin = new Vector2(1, 0);
                nextRt.anchorMax = new Vector2(1, 0);
                nextRt.pivot = new Vector2(1, 0);
                nextRt.anchoredPosition = new Vector2(-margin, bottomY);
                nextRt.sizeDelta = new Vector2(btnW, btnH);
                var nextImg = nextBtn.AddComponent<Image>();
                ApplyInkStyle(nextImg, new Color(0.25f, 0.22f, 0.18f, 0.6f), 2.0f);
                var nextButton = nextBtn.AddComponent<Button>();
                nextButton.onClick.AddListener(() => onNext());
                var nextLabel = CreateTMP("Label", nextRt, "下一页 ▷", 14,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                nextLabel.alignment = TextAnchor.MiddleCenter;
                nextLabel.color = new Color(0.9f, 0.87f, 0.8f);
            }
        }

        // ━━━ 工具方法 ━━━

        private Button CreateTabButton(string name, RectTransform parent, string label, int index)
        {
            var go = CreateChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            float w = 1f / 3f;
            rt.anchorMin = new Vector2(index * w, 0);
            rt.anchorMax = new Vector2((index + 1) * w, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            ApplyInkStyle(img, tabInactiveColor, 1.5f);

            var btn = go.AddComponent<Button>();

            var tmp = CreateTMP("Label", rt, label, 24,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            tmp.alignment = TextAnchor.MiddleCenter;
            tmp.fontStyle = FontStyle.Bold;
            tmp.color = tabInactiveColor;

            return btn;
        }

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
            tmp.color = inkColor;
            var chineseFont = Core.FontManager.ChineseFont;
            tmp.font = chineseFont != null ? chineseFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tmp.raycastTarget = false;  // 不拦截按钮点击
            return tmp;
        }

        private void ClearChildren(GameObject parent)
        {
            var rt = parent.GetComponent<RectTransform>();
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var child = rt.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }
        }

        // ━━━ 长卷视图（山的故事 9 章）━━━

        private void BuildLongScrollView(RectTransform parent,
            List<FragmentSO> allFragments, HashSet<string> collected)
        {
            var scroll = CreateChild("ScrollContainer", parent);
            var scRt = scroll.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(0.02f, 0.02f);
            scRt.anchorMax = new Vector2(0.98f, 0.96f);
            scRt.offsetMin = Vector2.zero;
            scRt.offsetMax = Vector2.zero;

            // 统计每章
            var chapterFrags = new List<FragmentSO>[10];
            for (int i = 1; i <= 9; i++) chapterFrags[i] = new List<FragmentSO>();
            foreach (var f in allFragments)
            {
                int ch = Mathf.Clamp(f.chapter, 1, 9);
                chapterFrags[ch].Add(f);
            }

            // 9 章，分 3 行 × 3 列
            float colW = 0.30f;
            float colGap = 0.02f;
            float rowH = 0.30f;
            float rowGap = 0.02f;

            for (int ch = 1; ch <= 9; ch++)
            {
                int row = (ch - 1) / 3;
                int col = (ch - 1) % 3;

                float x0 = col * (colW + colGap);
                float x1 = x0 + colW;
                float y1 = 1f - row * (rowH + rowGap);
                float y0 = y1 - rowH;

                var chapterBlock = CreateChild($"Chapter_{ch}", scRt);
                var cRt = chapterBlock.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(x0, y0);
                cRt.anchorMax = new Vector2(x1, y1);
                cRt.offsetMin = Vector2.zero;
                cRt.offsetMax = Vector2.zero;

                var bg = chapterBlock.AddComponent<Image>();
                ApplyInkStyle(bg, new Color(0.25f, 0.22f, 0.18f, 0.55f), 1.5f);

                // 章节点击折叠/展开
                int capturedCh = ch;
                var chBtn = chapterBlock.AddComponent<Button>();
                chBtn.onClick.AddListener(() => {
                    if (_collapsedChapters.Contains(capturedCh))
                        _collapsedChapters.Remove(capturedCh);
                    else
                        _collapsedChapters.Add(capturedCh);
                    RefreshFragmentView();
                });

                var frags = chapterFrags[ch];
                int colCount = 0;
                foreach (var f in frags)
                    if (collected.Contains(f.fragmentId)) colCount++;

                bool isCollapsed = _collapsedChapters.Contains(ch);

                // 章节名
                var chTitle = CreateTMP("ChapterTitle", cRt,
                    $"{(isCollapsed ? "▸" : "▾")} {_chapterNames[ch]}", 16,
                    new Vector2(0, 0.75f), new Vector2(1, 0.95f),
                    Vector2.zero, Vector2.zero);
                chTitle.alignment = TextAnchor.MiddleCenter;
                chTitle.color = sealRed;
                chTitle.fontStyle = FontStyle.Bold;

                // 碎片计数
                var chCount = CreateTMP("ChapterCount", cRt,
                    $"{colCount}/{frags.Count}", 13,
                    new Vector2(0, 0.58f), new Vector2(1, 0.72f),
                    Vector2.zero, Vector2.zero);
                chCount.alignment = TextAnchor.MiddleCenter;
                chCount.color = colCount > 0
                    ? unlockedGreen
                    : lockedGray;

                // 折叠状态下不显示网格
                if (isCollapsed) continue;

                // 碎片网格（每章最多 8 格，分 2 列）
                int gridCols = 2;
                int gridRows = 4;
                float cellW = 0.42f;
                float cellH = 0.10f;
                float cellGapX = 0.08f;
                float cellGapY = 0.02f;
                float startY = 0.52f;

                int count = 0;
                foreach (var f in frags)
                {
                    if (count >= gridCols * gridRows) break;
                    int gCol = count % gridCols;
                    int gRow = count / gridCols;

                    float cx0 = 0.04f + gCol * (cellW + cellGapX);
                    float cx1 = cx0 + cellW;
                    float cy1 = startY - gRow * (cellH + cellGapY);
                    float cy0 = cy1 - cellH;

                    var cell = CreateChild($"Cell_{f.fragmentId}", cRt);
                    var cellRt = cell.GetComponent<RectTransform>();
                    cellRt.anchorMin = new Vector2(cx0, cy0);
                    cellRt.anchorMax = new Vector2(cx1, cy1);
                    cellRt.offsetMin = Vector2.zero;
                    cellRt.offsetMax = Vector2.zero;

                    bool has = collected.Contains(f.fragmentId);
                    var cellBg = cell.AddComponent<Image>();
                    Color cellColor = has
                        ? new Color(0.30f, 0.55f, 0.30f, 0.6f)
                        : new Color(0.35f, 0.33f, 0.30f, 0.3f);
                    ApplyInkStyle(cellBg, cellColor, 4.0f);

                    var cellIcon = CreateTMP("Icon", cellRt,
                        has ? "◆" : "◇", 18,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    cellIcon.alignment = TextAnchor.MiddleCenter;
                    cellIcon.color = has ? goldAccent : lockedGray;

                    if (has)
                    {
                        var cellBtn = cell.AddComponent<Button>();
                        var captured = f;
                        cellBtn.onClick.AddListener(() => ShowFragmentDetail(captured));
                    }

                    count++;
                }
            }
        }

        private void ShowFragmentDetail(FragmentSO frag)
        {
            if (_fragmentDetailPopup != null)
                Destroy(_fragmentDetailPopup);

            _fragmentDetailPopup = CreateChild("FragmentDetail", _contentRt);
            var rt = _fragmentDetailPopup.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.15f);
            rt.anchorMax = new Vector2(0.92f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 弹入动画起点
            rt.localScale = new Vector3(0.7f, 0.7f, 1f);
            StartCoroutine(PopupScaleIn(rt));

            var popBg = _fragmentDetailPopup.AddComponent<Image>();
            ApplyInkStyle(popBg, new Color(0.12f, 0.10f, 0.08f, 0.95f), 1.2f);

            var outline = _fragmentDetailPopup.AddComponent<Outline>();
            outline.effectColor = goldAccent;
            outline.effectDistance = new Vector2(2, -2);

            // 章节标签
            var chLabel = CreateTMP("Chapter", rt,
                _chapterNames[Mathf.Clamp(frag.chapter, 1, 9)], 16,
                new Vector2(0, 0.85f), new Vector2(1, 0.95f),
                Vector2.zero, Vector2.zero);
            chLabel.alignment = TextAnchor.MiddleCenter;
            chLabel.color = sealRed;

            // 标题
            var title = CreateTMP("Title", rt,
                frag.fragmentTitle, 26,
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.84f),
                Vector2.zero, Vector2.zero);
            title.alignment = TextAnchor.MiddleLeft;
            title.color = goldAccent;

            // 内容
            var content = CreateTMP("Content", rt,
                frag.content, 18,
                new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.70f),
                Vector2.zero, Vector2.zero);
            content.alignment = TextAnchor.UpperLeft;
            content.color = new Color(0.82f, 0.80f, 0.76f);
            content.lineSpacing = 6f;

            // 关闭按钮
            var closeGo = CreateChild("CloseBtn", rt);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.92f, 0.88f);
            closeRt.anchorMax = new Vector2(1, 0.98f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;
            var closeBg = closeGo.AddComponent<Image>();
            ApplyInkStyle(closeBg, new Color(0.4f, 0.15f, 0.10f, 0.8f), 3.0f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(HideFragmentDetail);
            var closeX = CreateTMP("X", closeRt, "✕", 18,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            closeX.alignment = TextAnchor.MiddleCenter;
            closeX.color = Color.white;

            // 点击背景关闭
            var bgBtn = _fragmentDetailPopup.AddComponent<Button>();
            bgBtn.onClick.AddListener(HideFragmentDetail);
        }

        private void HideFragmentDetail()
        {
            if (_fragmentDetailPopup != null)
            {
                Destroy(_fragmentDetailPopup);
                _fragmentDetailPopup = null;
            }
        }

        private IEnumerator PopupScaleIn(RectTransform target)
        {
            float dur = 0.25f;
            float t = 0f;
            Vector3 startScale = new Vector3(0.7f, 0.7f, 1f);
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float smooth = 1f - Mathf.Pow(1f - p, 3f); // easeOutCubic
                target.localScale = Vector3.Lerp(startScale, Vector3.one, smooth);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        // ━━━ 水墨材质工具 ━━━

        private static void ApplyInkStyle(Image image, Color color, float paperTiling = 1.0f)
        {
            if (image == null) return;
            TeaMist.Rendering.InkUIHelper.ApplyToImage(image, color, 0.10f, paperTiling, 0.06f, 0.05f, 0.20f, 0.04f);
        }
    }
}
