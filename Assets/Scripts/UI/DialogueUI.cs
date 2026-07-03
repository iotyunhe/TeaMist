using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TeaMist.Core;

namespace TeaMist.Dialogue
{
    /// <summary>
    /// 对话 UI 视图 —— 水墨卷轴式对话框。
    /// 连接 DialogueManager，负责所有对话视觉呈现。
    /// 当子控件引用为空时，Awake 阶段自动创建完整 UI 层级。
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("━━━ 主面板 ━━━")]
        public CanvasGroup panelGroup;
        public float fadeDuration = 0.4f;

        [Header("━━━ 印章色（选项按钮）━━━")]
        public Color sealColor = new Color(0.65f, 0.18f, 0.12f);  // 朱砂红
        public Color sealBgColor = new Color(0.25f, 0.22f, 0.18f, 0.75f);

        [Header("━━━ NPC 对话区域 ━━━")]
        public Text speakerNameText;
        public Text dialogueText;
        public GameObject npcDialogueGroup;
        public Button continueButton;

        [Header("━━━ 选项区域 ━━━")]
        public GameObject optionsGroup;
        public Button[] optionButtons;
        public Text[] optionLabels;

        [Header("━━━ 泡茶中 ━━━")]
        public GameObject teaBrewingGroup;
        public Text teaRequestText;

        [Header("━━━ 写效果 ━━━")]
        public float typewriterSpeed = 0.04f;
        public AudioClip typewriterSfx;

        private Coroutine typewriterCoroutine;
        private AudioSource audioSource;
        private bool _uiBuilt;
        private string _currentTypewriterText;  // 打字机快进时用

        // 颜色常量
        private static readonly Color InkBlack = new Color(0.15f, 0.13f, 0.10f);
        private static readonly Color GoldAccent = new Color(0.70f, 0.55f, 0.30f);

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // 确保 panelGroup
            if (panelGroup == null)
                panelGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // 自动创建 UI 子层级
            EnsureUIHierarchy();

            // 自动加载宣纸纹理作对话面板背景
            ApplyParchmentBackground();
        }

        /// <summary>
        /// 当 Inspector 引用为空时，自动创建完整的对话 UI 子对象层级。
        /// 让 DialogueUI 可以挂载在一个空 GameObject 上即可工作。
        /// </summary>
        private void EnsureUIHierarchy()
        {
            if (_uiBuilt) return;

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            // 强制清除 DialoguePanel 下所有旧子对象，从零重建，避免序列化残留
#if UNITY_EDITOR
            for (int i = rt.childCount - 1; i >= 0; i--)
                DestroyImmediate(rt.GetChild(i).gameObject);
#else
            for (int i = rt.childCount - 1; i >= 0; i--)
                Destroy(rt.GetChild(i).gameObject);
#endif
            npcDialogueGroup = null; speakerNameText = null; dialogueText = null;
            optionsGroup = null; optionButtons = null; optionLabels = null;
            teaBrewingGroup = null; teaRequestText = null; continueButton = null;

            // 参考分辨率 1920x1080，中心锚点 + 显式像素坐标

            // ── NPC 对话组 ──
            npcDialogueGroup = CreateChild("NPCGroup", rt);
            var npcRt = npcDialogueGroup.GetComponent<RectTransform>();
            npcRt.anchorMin = npcRt.anchorMax = new Vector2(0.5f, 0.5f);
            npcRt.pivot = new Vector2(0.5f, 0.5f);
            npcRt.anchoredPosition = new Vector2(0, -100);
            npcRt.sizeDelta = new Vector2(1600, 480);

            if (speakerNameText == null)
            {
                speakerNameText = CreateTMP_Explicit("SpeakerName", npcRt, "???", 28,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                // 位置由 FixSpeakerNameLayout() 动态计算，这里只设初始值
                var snRt = speakerNameText.GetComponent<RectTransform>();
                snRt.pivot = new Vector2(0, 1);
                snRt.anchoredPosition = new Vector2(-650, 230);
                snRt.sizeDelta = new Vector2(300, 38);
            }
            speakerNameText.alignment = TextAnchor.UpperLeft;
            speakerNameText.fontStyle = FontStyle.Bold;

            if (dialogueText == null)
            {
                dialogueText = CreateTMP_Explicit("DialogueText", npcRt, "", 24,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var dtRt = dialogueText.GetComponent<RectTransform>();
                dtRt.pivot = new Vector2(0.5f, 0.5f);
                dtRt.anchoredPosition = new Vector2(0, 30);
                dtRt.sizeDelta = new Vector2(1500, 340);
            }
            dialogueText.alignment = TextAnchor.MiddleCenter;
            dialogueText.lineSpacing = 1.5f;
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Overflow;

            // ── 选项组 ──
            optionsGroup = CreateChild("OptionsGroup", rt);
            var optRt = optionsGroup.GetComponent<RectTransform>();
            optRt.anchorMin = optRt.anchorMax = new Vector2(0.5f, 0.5f);
            optRt.pivot = new Vector2(0.5f, 0.5f);
            optRt.anchoredPosition = new Vector2(0, 80);
            optRt.sizeDelta = new Vector2(1728, 650);

            if (optionButtons == null) optionButtons = new Button[4];
            if (optionLabels == null) optionLabels = new Text[4];
            float btnH = 130f, startY = 70f, gap = 10f;
            for (int i = 0; i < 4; i++)
            {
                var btnGo = CreateChild($"Option_{i}", optRt);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0, startY - i * (btnH + gap));
                btnRt.sizeDelta = new Vector2(1580, btnH);

                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = sealBgColor;
                btnGo.AddComponent<Outline>().effectColor = sealColor;
                btnGo.GetComponent<Outline>().effectDistance = new Vector2(2, -2);

                var btn = btnGo.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = sealBgColor;
                colors.highlightedColor = new Color(0.35f, 0.28f, 0.22f, 0.85f);
                colors.pressedColor = new Color(0.5f, 0.15f, 0.10f, 0.9f);
                colors.selectedColor = sealBgColor;
                btn.colors = colors;
                optionButtons[i] = btn;

                var lbl = CreateTMP_Explicit("Label", btnRt, "", 22,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = new Color(0.92f, 0.88f, 0.78f);
                var lblRt = lbl.GetComponent<RectTransform>();
                lblRt.anchorMin = lblRt.anchorMax = new Vector2(0.5f, 0.5f);
                lblRt.pivot = new Vector2(0.5f, 0.5f);
                lblRt.anchoredPosition = Vector2.zero;
                lblRt.sizeDelta = new Vector2(1500, btnH - 20);
                optionLabels[i] = lbl;
            }

            // ── 泡茶组 ──
            teaBrewingGroup = CreateChild("TeaBrewingGroup", rt);
            var teaRt = teaBrewingGroup.GetComponent<RectTransform>();
            teaRt.anchorMin = teaRt.anchorMax = new Vector2(0.5f, 0.5f);
            teaRt.pivot = new Vector2(0.5f, 0.5f);
            teaRt.anchoredPosition = new Vector2(0, 80);
            teaRt.sizeDelta = new Vector2(1000, 300);

            if (teaRequestText == null)
                teaRequestText = CreateTMP_Explicit("TeaRequest", teaRt, "泡茶中...", 26,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            teaRequestText.alignment = TextAnchor.MiddleCenter;
            teaRequestText.color = GoldAccent;

            // ── 继续按钮 ──
            if (continueButton == null)
            {
                var contGo = CreateChild("ContinueBtn", rt);
                var contRt = contGo.GetComponent<RectTransform>();
                contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 0.06f);
                contRt.pivot = new Vector2(0.5f, 0.5f);
                contRt.anchoredPosition = Vector2.zero;
                contRt.sizeDelta = new Vector2(180, 48);
                var contImg = contGo.AddComponent<Image>();
                contImg.color = new Color(0.2f, 0.18f, 0.14f, 0.65f);
                continueButton = contGo.AddComponent<Button>();
                continueButton.onClick.AddListener(OnContinueClicked);

                var contLabel = CreateTMP_Explicit("Label", contRt, "▶ 继续", 20,
                    new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                contLabel.alignment = TextAnchor.MiddleCenter;
                contLabel.color = new Color(0.92f, 0.88f, 0.78f);
            }

            _uiBuilt = true;
        }

        /// <summary>创建 Text 子对象</summary>
        private Text CreateTMP_Explicit(string name, RectTransform parent, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = CreateChild(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = InkBlack;
            var chineseFont = Core.FontManager.ChineseFont;
            txt.font = chineseFont != null ? chineseFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
            return txt;
        }

        private GameObject CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private void ApplyParchmentBackground()
        {
            ArtLoader.LoadAll();
            var parchment = ArtLoader.Find("宣纸");
            if (parchment == null) return;

            var img = GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            img.sprite = parchment;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.92f);
        }

        void Start()
        {
            HideImmediate();
        }

        // ━━━ 显示/隐藏（纯 alpha 淡入淡出）━━━

        public void Show()
        {
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        public void HideImmediate()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            if (npcDialogueGroup != null) npcDialogueGroup.SetActive(false);
            if (optionsGroup != null) optionsGroup.SetActive(false);
            if (teaBrewingGroup != null) teaBrewingGroup.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
        }

        private IEnumerator FadeIn()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            float startAlpha = panelGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        // ━━━ NPC 对话 ━━━

        public void ShowNPC(string speaker, string text, string emotion = "neutral")
        {
            HideAllGroups();
            npcDialogueGroup.SetActive(true);

            // 覆写 RectTransform，确保位置正确
            FixSpeakerNameLayout();
            FixDialogueTextLayout();

            // NPC 对话：显示说话人名 + 文字左对齐，打字机从左到右
            speakerNameText.gameObject.SetActive(true);
            speakerNameText.text = speaker;
            speakerNameText.color = GetSpeakerColor(speaker);
            dialogueText.alignment = TextAnchor.UpperLeft;
            dialogueText.fontStyle = FontStyle.Normal;
            dialogueText.color = InkBlack;

            // 打字机效果
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterEffect(text));

            // 继续按钮在打字机结束后才显示
            if (continueButton != null)
                continueButton.gameObject.SetActive(false);

            // TODO: 根据 emotion 切换立绘表情（Spine 动画）
        }

        /// <summary>打字机完成回调：显示继续按钮</summary>
        private void OnTypewriterComplete()
        {
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);
        }

        private IEnumerator TypewriterEffect(string text)
        {
            _currentTypewriterText = text;
            dialogueText.text = "";
            for (int i = 0; i < text.Length; i++)
            {
                dialogueText.text += text[i];
                if (typewriterSfx != null && i % 3 == 0)
                    audioSource.PlayOneShot(typewriterSfx, 0.3f);
                yield return new WaitForSeconds(typewriterSpeed);
            }
            _currentTypewriterText = null;
            OnTypewriterComplete();
        }

        public void HideNPC()
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (npcDialogueGroup != null) npcDialogueGroup.SetActive(false);
        }

        // ━━━ 叙述 ━━━

        public void ShowNarration(string text)
        {
            HideAllGroups();
            npcDialogueGroup.SetActive(true);

            // 覆写 RectTransform，确保位置正确
            FixDialogueTextLayout();

            // 旁白：隐藏说话人名 + 居中对齐 + 偏灰色调
            speakerNameText.gameObject.SetActive(false);
            dialogueText.alignment = TextAnchor.MiddleCenter;
            dialogueText.fontStyle = FontStyle.Normal;
            dialogueText.color = new Color(0.35f, 0.32f, 0.28f);
            dialogueText.text = text;

            // 旁白直接显示继续按钮（无打字机）
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);
        }

        // ━━━ 选项 ━━━

        public void ShowOptions(List<DialogueOption> options)
        {
            HideAllGroups();
            optionsGroup.SetActive(true);

            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (i < options.Count)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    optionLabels[i].text = options[i].label;
                    int idx = i; // 闭包捕获
                    optionButtons[i].onClick.RemoveAllListeners();
                    optionButtons[i].onClick.AddListener(() => OnOptionClicked(idx));
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void HideOptions()
        {
            if (optionsGroup != null) optionsGroup.SetActive(false);
        }

        private void OnOptionClicked(int index)
        {
            DialogueManager.Instance?.SelectOption(index);
        }

        /// <summary>玩家点击"继续"按钮</summary>
        public void OnContinueClicked()
        {
            if (typewriterCoroutine != null)
            {
                // 打字机进行中：快进 —— 直接显示完整文本
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
                if (dialogueText != null && _currentTypewriterText != null)
                    dialogueText.text = _currentTypewriterText;
                _currentTypewriterText = null;
                OnTypewriterComplete();
                return;
            }

            // 正常继续
            DialogueManager.Instance?.OnContinueClicked();
        }

        // ━━━ 泡茶 ━━━

        public void ShowTeaRequest(string request)
        {
            HideAllGroups();
            teaBrewingGroup.SetActive(true);
            teaRequestText.text = $"「{request}」";
        }

        public void HideTeaUI()
        {
            if (teaBrewingGroup != null) teaBrewingGroup.SetActive(false);
        }

        // ━━━ 工具 ━━━

        /// <summary>直接覆写 SpeakerName RectTransform —— 动态读取 NPCGroup 实际尺寸计算位置</summary>
        private void FixSpeakerNameLayout()
        {
            if (speakerNameText == null) return;

            var npcRt = npcDialogueGroup.GetComponent<RectTransform>();
            var rt = speakerNameText.GetComponent<RectTransform>();

            // 确保父节点是 NPCGroup
            if (rt.parent != npcDialogueGroup.transform)
                rt.SetParent(npcDialogueGroup.transform, false);

            // 动态读取 NPCGroup 实际 rect 尺寸，计算卷轴内左上角位置
            float npcW = npcRt.rect.width;
            float npcH = npcRt.rect.height;
            const float padLeft = 150f;   // 避开左侧轴头，进入宣纸区
            const float padTop  = 10f;    // 靠近顶部卷边，进入宣纸区
            const float nameW   = 300f;
            const float nameH   = 38f;

            // center-anchor + pivot(0,1) → 左上角定位
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(-(npcW / 2f - padLeft), npcH / 2f - padTop);
            rt.sizeDelta = new Vector2(nameW, nameH);
        }

        /// <summary>直接覆写 DialogueText 的 RectTransform</summary>
        private void FixDialogueTextLayout()
        {
            if (dialogueText == null) return;
            var rt = dialogueText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 30);
            rt.sizeDelta = new Vector2(1500, 340);
        }

        private void HideAllGroups()
        {
            if (npcDialogueGroup != null) npcDialogueGroup.SetActive(false);
            if (optionsGroup != null) optionsGroup.SetActive(false);
            if (teaBrewingGroup != null) teaBrewingGroup.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
        }

        private Color GetSpeakerColor(string speaker)
        {
            switch (speaker)
            {
                case "白露": return new Color(0.92f, 0.88f, 0.82f);
                case "竹青": return new Color(0.45f, 0.65f, 0.45f);
                case "云鹤老": return new Color(0.70f, 0.72f, 0.78f);
                case "墨砚": return new Color(0.20f, 0.22f, 0.28f);
                case "当归": return new Color(0.75f, 0.60f, 0.55f);
                case "小山": return new Color(0.55f, 0.58f, 0.62f);
                case "霜降": return new Color(0.65f, 0.68f, 0.78f);
                case "栖迟": return new Color(0.35f, 0.30f, 0.40f);
                default:  return new Color(0.82f, 0.80f, 0.76f);
            }
        }
    }
}
