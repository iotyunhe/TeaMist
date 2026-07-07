using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TeaMist.Core;

namespace TeaMist.UI
{
    /// <summary>
    /// 存档面板 —— 5 槽位存取管理、新建游戏确认。
    /// 与 SaveManager / GameManager 直接对接。
    /// 挂载在 Canvas 子节点上，由 Bootstrap 或 SceneAutoSetup 创建。
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        [Header("━━━ 面板 ━━━")]
        [SerializeField] private CanvasGroup _panelGroup;
        public CanvasGroup panelGroup
        {
            get => _panelGroup;
            set => _panelGroup = value;
        }

        [Header("━━━ 动画 ━━━")]
        [SerializeField] private float _fadeDuration = 0.3f;

        [Header("━━━ 颜色 ━━━")]
        public Color panelBgColor = new Color(0.18f, 0.16f, 0.12f, 0.92f);
        public Color slotBgColor  = new Color(0.22f, 0.19f, 0.15f, 0.85f);
        public Color slotHoverColor = new Color(0.30f, 0.25f, 0.18f, 0.90f);
        public Color btnSaveColor  = new Color(0.35f, 0.55f, 0.35f, 0.85f);
        public Color btnLoadColor  = new Color(0.45f, 0.50f, 0.65f, 0.85f);
        public Color btnDeleteColor = new Color(0.65f, 0.25f, 0.22f, 0.85f);
        public Color btnNewGameColor = new Color(0.60f, 0.40f, 0.22f, 0.85f);
        public Color closeBtnColor = new Color(0.30f, 0.28f, 0.22f, 0.80f);
        public Color textColor = new Color(0.90f, 0.87f, 0.80f);
        public Color dimTextColor = new Color(0.55f, 0.50f, 0.42f);

        // ── 运行时 ──
        private bool _initialized;
        private List<SaveSlotEntry> _slotEntries = new List<SaveSlotEntry>();
        private bool _confirmingNewGame;
        private Button _newGameButton;
        private Text _newGameButtonLabel;

        // F5 快捷存档
        private const KeyCode QUICK_SAVE_KEY = KeyCode.F5;

        // ── 结构体：每个槽位的 UI ──
        private class SaveSlotEntry
        {
            public int slotIndex;
            public GameObject root;
            public Text slotLabel;      // "槽位 1"
            public Text infoText;       // 存档摘要
            public Button saveBtn;
            public Button loadBtn;
            public Button deleteBtn;
            public Text saveBtnLabel;
            public Text loadBtnLabel;
            public Text deleteBtnLabel;
        }

        void Awake()
        {
            if (_panelGroup == null)
            {
                _panelGroup = GetComponent<CanvasGroup>();
                if (_panelGroup == null) _panelGroup = gameObject.AddComponent<CanvasGroup>();
            }

            BuildUI();
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        void Update()
        {
            if (Input.GetKeyDown(QUICK_SAVE_KEY))
            {
                QuickSave();
            }
        }

        // ━━━ 公开 API ━━━

        public void Toggle()
        {
            if (_panelGroup.alpha < 0.5f) Show();
            else Hide();
        }

        public void Show()
        {
            RefreshSlots();
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        /// <summary>F5 快捷存档到当前槽位</summary>
        public void QuickSave()
        {
            int slot = GameManager.Instance?.CurrentSaveSlot ?? 1;
            GameManager.Instance?.SaveGame(slot);
            Debug.Log($"[SaveLoadUI] 快捷存档 → 槽位 {slot}");
        }

        // ━━━ 槽位操作 ━━━

        private void SaveToSlot(int slot)
        {
            GameManager.Instance?.SaveGame(slot);
            RefreshSlots();
        }

        private void LoadFromSlot(int slot)
        {
            if (!System.IO.File.Exists(SaveManager.GetSlotPathPublic(slot)))
            {
                Debug.LogWarning($"[SaveLoadUI] 槽位 {slot} 无存档");
                return;
            }

            var save = SaveManager.Load(slot);
            if (save != null)
                GameManager.Instance?.RestoreFromSave(save);
        }

        private void DeleteSlot(int slot)
        {
            SaveManager.DeleteSlot(slot);
            RefreshSlots();
        }

        private void NewGame()
        {
            if (!_confirmingNewGame)
            {
                _confirmingNewGame = true;
                if (_newGameButtonLabel != null)
                    _newGameButtonLabel.text = "⚠ 再次点击确认新建";
                return;
            }

            _confirmingNewGame = false;
            if (_newGameButtonLabel != null)
                _newGameButtonLabel.text = "🏠 开始新游戏";
            GameManager.Instance?.InitializeNewGameState();
            GameManager.Instance?.SaveGame();
            RefreshSlots();
            Hide();
        }

        // ━━━ UI 构建 ━━━

        private void BuildUI()
        {
            if (_initialized) return;

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.one * 0.5f;
            rt.pivot = Vector2.one * 0.5f;
            rt.sizeDelta = new Vector2(1728, 1080);

            // 半透明遮罩（点击关闭）
            var mask = MakeChild("Mask", rt);
            var maskRt = mask.GetComponent<RectTransform>();
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.sizeDelta = Vector2.zero;
            var maskImg = mask.AddComponent<Image>();
            ApplyInkStyle(maskImg, new Color(0f, 0f, 0f, 0.45f), 0.5f);
            var maskBtn = mask.AddComponent<Button>();
            maskBtn.onClick.AddListener(Hide);

            // 面板主体
            float panelW = 800, panelH = 720;
            var panel = MakeChild("Panel", rt);
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = Vector2.one * 0.5f;
            pRt.pivot = Vector2.one * 0.5f;
            pRt.sizeDelta = new Vector2(panelW, panelH);
            var pImg = panel.AddComponent<Image>();
            ApplyInkStyle(pImg, panelBgColor, 1.0f);

            // 标题
            var title = MakeTMP("Title", pRt, "— 存档管理 —", 32);
            var tRt = title.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 1f);
            tRt.anchorMax = new Vector2(0.5f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0, -20);
            tRt.sizeDelta = new Vector2(300, 42);
            title.color = new Color(0.92f, 0.85f, 0.55f);
            title.alignment = TextAnchor.MiddleCenter;

            // 快捷存档提示
            var hint = MakeTMP("QuickSaveHint", pRt, "F5 快捷存档 | 点击空白处关闭", 16);
            var hRt = hint.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.5f, 1f);
            hRt.anchorMax = new Vector2(0.5f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0, -55);
            hRt.sizeDelta = new Vector2(400, 24);
            hint.color = dimTextColor;
            hint.alignment = TextAnchor.MiddleCenter;

            // 5 个槽位
            float slotStartY = -90, slotH = 100, slotGap = 10;
            for (int i = 0; i < 5; i++)
            {
                var entry = BuildSlotEntry(panel.transform, i + 1,
                    new Vector2(0, slotStartY - i * (slotH + slotGap)),
                    new Vector2(panelW - 60, slotH));
                _slotEntries.Add(entry);
            }

            // 新建游戏按钮
            float btnY = slotStartY - 5 * (slotH + slotGap) - 20;
            var newGameBtn = MakeButton("NewGameBtn", pRt, "🏠 开始新游戏",
                new Vector2(0, btnY), new Vector2(panelW - 60, 42),
                btnNewGameColor, () => NewGame());
            newGameBtn.transform.GetChild(0).GetComponent<Text>().fontSize = 22;
            _newGameButton = newGameBtn;
            _newGameButtonLabel = newGameBtn.transform.GetChild(0).GetComponent<Text>();

            // 关闭按钮
            var closeBtn = MakeButton("CloseBtn", pRt, "✕ 关闭",
                new Vector2(0, btnY - 50), new Vector2(160, 38),
                closeBtnColor, () => Hide());
            closeBtn.transform.GetChild(0).GetComponent<Text>().fontSize = 18;

            _initialized = true;
        }

        private SaveSlotEntry BuildSlotEntry(Transform parent, int slot,
            Vector2 anchoredPos, Vector2 size)
        {
            var entry = new SaveSlotEntry { slotIndex = slot };

            // 槽位容器
            entry.root = MakeChild($"Slot_{slot}", parent as RectTransform);
            var sRt = entry.root.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = anchoredPos;
            sRt.sizeDelta = size;
            var sImg = entry.root.AddComponent<Image>();
            ApplyInkStyle(sImg, slotBgColor, 1.5f);

            // 槽位编号
            entry.slotLabel = MakeTMP("SlotLabel", sRt, $"槽位 {slot}", 20);
            var slRt = entry.slotLabel.GetComponent<RectTransform>();
            slRt.anchorMin = new Vector2(0, 1f);
            slRt.anchorMax = new Vector2(0, 1f);
            slRt.pivot = new Vector2(0, 1f);
            slRt.anchoredPosition = new Vector2(15, -8);
            slRt.sizeDelta = new Vector2(80, 28);
            entry.slotLabel.color = new Color(0.92f, 0.85f, 0.55f);

            // 存档摘要
            entry.infoText = MakeTMP("Info", sRt, "(空)", 18);
            var iRt = entry.infoText.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0, 1f);
            iRt.anchorMax = new Vector2(0, 1f);
            iRt.pivot = new Vector2(0, 1f);
            iRt.anchoredPosition = new Vector2(15, -38);
            iRt.sizeDelta = new Vector2(size.x - 280, 52);
            entry.infoText.color = dimTextColor;

            // 按钮组
            float btnW = 80, btnH = 34, btnGap = 6;
            float btnBaseX = size.x / 2f - 15 - btnW;

            entry.saveBtn = MakeButton("SaveBtn", sRt, "保存", new Vector2(btnBaseX, 0),
                new Vector2(btnW, btnH), btnSaveColor, () => SaveToSlot(slot));
            entry.saveBtnLabel = entry.saveBtn.transform.GetChild(0).GetComponent<Text>();
            entry.saveBtnLabel.fontSize = 17;
            entry.saveBtnLabel.color = textColor;

            entry.loadBtn = MakeButton("LoadBtn", sRt, "读档", new Vector2(btnBaseX - btnW - btnGap, 0),
                new Vector2(btnW, btnH), btnLoadColor, () => LoadFromSlot(slot));
            entry.loadBtnLabel = entry.loadBtn.transform.GetChild(0).GetComponent<Text>();
            entry.loadBtnLabel.fontSize = 17;
            entry.loadBtnLabel.color = textColor;

            entry.deleteBtn = MakeButton("DeleteBtn", sRt, "删除", new Vector2(btnBaseX - 2 * (btnW + btnGap), 0),
                new Vector2(btnW, btnH), btnDeleteColor, () => DeleteSlot(slot));
            entry.deleteBtnLabel = entry.deleteBtn.transform.GetChild(0).GetComponent<Text>();
            entry.deleteBtnLabel.fontSize = 17;
            entry.deleteBtnLabel.color = textColor;

            return entry;
        }

        // ━━━ 刷新槽位 ━━━

        private void RefreshSlots()
        {
            foreach (var entry in _slotEntries)
            {
                var info = SaveManager.GetSlotInfo(entry.slotIndex);
                if (info.isEmpty)
                {
                    entry.infoText.text = "— 空槽位 —";
                    entry.infoText.color = dimTextColor;
                    entry.loadBtn.interactable = false;
                    entry.deleteBtn.interactable = false;
                    entry.saveBtnLabel.text = "保存";
                    entry.saveBtn.interactable = true;
                }
                else
                {
                    // TeaMist.Data.Season 枚举值本身就是中文（春/夏/秋/冬），直接 ToString()
                    string seasonName = info.currentSeason.ToString();
                    entry.infoText.text = $"{info.playerName} | 第 {info.totalDays} 天 | {seasonName}季\n{info.saveName} · {info.timestamp}";
                    entry.infoText.color = textColor;
                    entry.loadBtn.interactable = true;
                    entry.deleteBtn.interactable = true;
                    entry.saveBtnLabel.text = "覆盖";
                    entry.saveBtn.interactable = true;
                }
            }
        }

        // ━━━ 动画 ━━━

        private System.Collections.IEnumerator FadeIn()
        {
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;
            for (float t = 0; t < _fadeDuration; t += Time.unscaledDeltaTime)
            {
                _panelGroup.alpha = Mathf.Clamp01(t / _fadeDuration);
                yield return null;
            }
            _panelGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut()
        {
            for (float t = 0; t < _fadeDuration; t += Time.unscaledDeltaTime)
            {
                _panelGroup.alpha = Mathf.Clamp01(1f - t / _fadeDuration);
                yield return null;
            }
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        // ━━━ UI 工具方法 ━━━

        private GameObject MakeChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private Text MakeTMP(string name, RectTransform parent, string text, int fontSize)
        {
            var go = MakeChild(name, parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = textColor;
            t.raycastTarget = false;
            var font = Core.FontManager.ChineseFont;
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        private Button MakeButton(string name, RectTransform parent, string label,
            Vector2 anchoredPos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = MakeChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            ApplyInkStyle(img, bgColor, 1.5f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var lbl = MakeTMP("Label", rt, label, 17);
            var lRt = lbl.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.sizeDelta = Vector2.zero;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.raycastTarget = false;

            return btn;
        }
        // ━━━ 水墨材质工具 ━━━

        private static void ApplyInkStyle(Image image, Color color, float paperTiling = 1.0f)
        {
            if (image == null) return;
            TeaMist.Rendering.InkUIHelper.ApplyToImage(image, color, 0.12f, paperTiling, 0.08f, 0.06f, 0.25f, 0.05f);
        }
    }
}
