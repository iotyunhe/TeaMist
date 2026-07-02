using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using TeaMist.Core;

namespace TeaMist.UI
{
    /// <summary>
    /// 设置菜单面板 —— 音量 / 全屏 / 分辨率。
    /// 数据通过 SaveManager PlayerPrefs 持久化，运行时直接应用到 AudioManager 和 Screen。
    /// 挂载在 Canvas 子节点上，Bootstrap 自动创建。
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        // 懒加载：从 Resources/Fonts 加载中文字体 SDF，确保 TMP 能正确渲染中文
        private TMP_FontAsset _fontAsset;
        private TMP_FontAsset FontAsset
        {
            get
            {
                if (_fontAsset == null)
                    _fontAsset = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF");
                return _fontAsset;
            }
        }

        [Header("━━━ 面板 ━━━")]
        [SerializeField] private CanvasGroup _panelGroup;

        /// <summary>CanvasGroup，Bootstrap 可预赋值</summary>
        public CanvasGroup panelGroup
        {
            get => _panelGroup;
            set => _panelGroup = value;
        }

        [Header("━━━ 音频 Slider ━━━")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _ambientVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("━━━ 画面 Toggle ━━━")]
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;

        [Header("━━━ 按钮 ━━━")]
        [SerializeField] private Button _closeButton;

        [Header("━━━ 动画 ━━━")]
        [SerializeField] private float _fadeDuration = 0.3f;

        // ━━━ PlayerPrefs key ━━━
        private const string KEY_MASTER  = "Audio_Master";
        private const string KEY_AMBIENT = "Audio_Ambient";
        private const string KEY_SFX     = "Audio_SFX";
        private const string KEY_FULLSCREEN = "Screen_Fullscreen";
        private const string KEY_RESOLUTION = "Screen_Resolution";

        // 常见分辨率列表
        private static readonly ResolutionEntry[] Resolutions =
        {
            new ResolutionEntry(3840, 2160, "3840 x 2160 (4K)"),
            new ResolutionEntry(2560, 1440, "2560 x 1440 (2K)"),
            new ResolutionEntry(1920, 1080, "1920 x 1080"),
            new ResolutionEntry(1600, 900,  "1600 x 900"),
            new ResolutionEntry(1366, 768,  "1366 x 768"),
            new ResolutionEntry(1280, 720,  "1280 x 720"),
        };

        private bool _uiBuilt;

        // ━━━ 生命周期 ━━━

        private void Awake()
        {
            EnsureUIHierarchy();
        }

        private void Start()
        {
            LoadAndApply();
            HideImmediate();
        }

        // ━━━ UI 自动构建 ━━━

        private void EnsureUIHierarchy()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            // CanvasGroup
            if (_panelGroup == null)
            {
                _panelGroup = GetComponent<CanvasGroup>();
                if (_panelGroup == null)
                    _panelGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700, 600);

            // 半透明暗底
            var bg = CreateUIElement("BG", transform);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);

            // 标题 "设置"
            CreateLabel("Title", "设  置", 36, TextAlignmentOptions.Center, new Vector2(0, 250));

            // ── 音量区 ──
            CreateLabel("VolLabel", "音量", 28, TextAlignmentOptions.Left, new Vector2(-290, 180));
            _masterVolumeSlider  = CreateSlider("MasterVol",  "主音量",   new Vector2(0, 130));
            _ambientVolumeSlider = CreateSlider("AmbientVol", "环境音",   new Vector2(0, 60));
            _sfxVolumeSlider     = CreateSlider("SFXVol",     "音效",     new Vector2(0, -10));

            // ── 画面区 ──
            CreateLabel("DisplayLabel", "画面", 28, TextAlignmentOptions.Left, new Vector2(-290, -80));
            _fullscreenToggle = CreateToggle("Fullscreen", "全屏", new Vector2(0, -130));
            _resolutionDropdown = CreateResolutionDropdown("Resolution", new Vector2(0, -190));

            // ── 关闭按钮 ──
            _closeButton = CreateTextButton("CloseBtn", "返回游戏", new Vector2(0, -270));

            // 注册事件
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);

            if (_masterVolumeSlider != null)
                _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (_ambientVolumeSlider != null)
                _ambientVolumeSlider.onValueChanged.AddListener(OnAmbientVolumeChanged);
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            if (_fullscreenToggle != null)
                _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (_resolutionDropdown != null)
                _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        // ━━━ 公开 API ━━━

        public void Show()
        {
            EnsureUIHierarchy();
            StopAllCoroutines();
            LoadAndApply(); // 每次打开刷新为最新值
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        public void Toggle()
        {
            if (_panelGroup != null && _panelGroup.alpha > 0.1f)
                Hide();
            else
                Show();
        }

        private void HideImmediate()
        {
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 0f;
                _panelGroup.interactable = false;
                _panelGroup.blocksRaycasts = false;
            }
        }

        // ━━━ 持久化加载 / 保存 ━━━

        private void LoadAndApply()
        {
            float master  = Core.SaveManager.LoadSettingFloat(KEY_MASTER, 0.7f);
            float ambient = Core.SaveManager.LoadSettingFloat(KEY_AMBIENT, 0.6f);
            float sfx     = Core.SaveManager.LoadSettingFloat(KEY_SFX, 0.8f);
            bool  full    = Core.SaveManager.LoadSettingInt(KEY_FULLSCREEN, 1) == 1;
            int   resIdx  = Core.SaveManager.LoadSettingInt(KEY_RESOLUTION, FindDefaultResolutionIndex());

            if (_masterVolumeSlider != null)  _masterVolumeSlider.SetValueWithoutNotify(master);
            if (_ambientVolumeSlider != null) _ambientVolumeSlider.SetValueWithoutNotify(ambient);
            if (_sfxVolumeSlider != null)     _sfxVolumeSlider.SetValueWithoutNotify(sfx);
            if (_fullscreenToggle != null)    _fullscreenToggle.SetIsOnWithoutNotify(full);
            if (_resolutionDropdown != null && resIdx >= 0 && resIdx < Resolutions.Length)
                _resolutionDropdown.SetValueWithoutNotify(resIdx);

            ApplyVolume();
            ApplyScreen(full, resIdx);
        }

        private void ApplyVolume()
        {
            var audio = Core.AudioManager.Instance;
            if (audio == null) return;

            float master  = Core.SaveManager.LoadSettingFloat(KEY_MASTER, 0.7f);
            float ambient = Core.SaveManager.LoadSettingFloat(KEY_AMBIENT, 0.6f);
            float sfx     = Core.SaveManager.LoadSettingFloat(KEY_SFX, 0.8f);

            audio.SetMasterVolume(master);
            audio.SetAmbientVolume(ambient);
            audio.SetSFXVolume(sfx);
        }

        private void ApplyScreen(bool fullscreen, int resIdx)
        {
            if (resIdx < 0 || resIdx >= Resolutions.Length) return;
            var res = Resolutions[resIdx];
            Screen.SetResolution(res.width, res.height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // ━━━ 回调 ━━━

        private void OnMasterVolumeChanged(float v)
        {
            Core.SaveManager.SaveSettingFloat(KEY_MASTER, v);
            Core.AudioManager.Instance?.SetMasterVolume(v);
        }

        private void OnAmbientVolumeChanged(float v)
        {
            Core.SaveManager.SaveSettingFloat(KEY_AMBIENT, v);
            Core.AudioManager.Instance?.SetAmbientVolume(v);
        }

        private void OnSFXVolumeChanged(float v)
        {
            Core.SaveManager.SaveSettingFloat(KEY_SFX, v);
            Core.AudioManager.Instance?.SetSFXVolume(v);
        }

        private void OnFullscreenChanged(bool isOn)
        {
            Core.SaveManager.SaveSettingInt(KEY_FULLSCREEN, isOn ? 1 : 0);
            int resIdx = Core.SaveManager.LoadSettingInt(KEY_RESOLUTION, FindDefaultResolutionIndex());
            ApplyScreen(isOn, resIdx);
        }

        private void OnResolutionChanged(int idx)
        {
            Core.SaveManager.SaveSettingInt(KEY_RESOLUTION, idx);
            bool full = Core.SaveManager.LoadSettingInt(KEY_FULLSCREEN, 1) == 1;
            ApplyScreen(full, idx);
        }

        // ━━━ 动画 ━━━

        private System.Collections.IEnumerator FadeIn()
        {
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _panelGroup.alpha = Mathf.Lerp(0, 1, elapsed / _fadeDuration);
                yield return null;
            }
            _panelGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut()
        {
            float start = _panelGroup.alpha;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _panelGroup.alpha = Mathf.Lerp(start, 0, elapsed / _fadeDuration);
                yield return null;
            }
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        // ━━━ UI 工厂方法 ━━━

        private GameObject CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private void CreateLabel(string name, string text, int fontSize, TextAlignmentOptions align, Vector2 pos)
        {
            var go = CreateUIElement(name, transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600, 40);
            rt.anchoredPosition = pos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = FontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = new Color(0.98f, 0.95f, 0.90f);
        }

        private Slider CreateSlider(string name, string label, Vector2 pos)
        {
            var container = CreateUIElement(name, transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(520, 40);
            crt.anchoredPosition = pos;

            // 标签（左 0→25%）
            var lblGo = CreateUIElement("Label", container.transform);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.25f, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.font = FontAsset;
            lbl.text = label;
            lbl.fontSize = 22;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = new Color(0.98f, 0.95f, 0.90f);

            // Slider 区域（25%→82%）
            var sliderArea = CreateUIElement("Slider", container.transform);
            var srt = sliderArea.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.25f, 0); srt.anchorMax = new Vector2(0.82f, 1);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            // 数值标签（右 82%→100%）
            var valGo = CreateUIElement("Value", container.transform);
            var vrt = valGo.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.82f, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var valLbl = valGo.AddComponent<TextMeshProUGUI>();
            valLbl.font = FontAsset;
            valLbl.text = "70%";
            valLbl.fontSize = 20;
            valLbl.alignment = TextAlignmentOptions.MidlineRight;
            valLbl.color = new Color(0.98f, 0.95f, 0.90f);

            // Slider
            var bgGo = CreateUIElement("Background", sliderArea.transform);
            var brt = bgGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(0, 8); brt.offsetMax = new Vector2(0, -8);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.45f, 0.40f, 0.34f);

            var fillArea = CreateUIElement("FillArea", sliderArea.transform);
            var fat = fillArea.GetComponent<RectTransform>();
            fat.anchorMin = Vector2.zero; fat.anchorMax = Vector2.one;
            fat.offsetMin = new Vector2(0, 8); fat.offsetMax = new Vector2(0, -8);

            var fillGo = CreateUIElement("Fill", fillArea.transform);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.75f, 0.42f, 0.22f);  // 朱砂色

            var handleArea = CreateUIElement("HandleSlideArea", sliderArea.transform);
            var hat = handleArea.GetComponent<RectTransform>();
            hat.anchorMin = Vector2.zero; hat.anchorMax = Vector2.one;
            hat.offsetMin = new Vector2(-8, 0); hat.offsetMax = new Vector2(8, 0);

            var handleGo = CreateUIElement("Handle", handleArea.transform);
            var hrt = handleGo.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(16, 26);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.95f, 0.92f, 0.86f);

            var slider = sliderArea.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;
            slider.direction = Slider.Direction.LeftToRight;

            // 值变化时同步数值标签
            slider.onValueChanged.AddListener(v => valLbl.text = Mathf.RoundToInt(v * 100) + "%");

            return slider;
        }

        private Toggle CreateToggle(string name, string label, Vector2 pos)
        {
            var container = CreateUIElement(name, transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(520, 40);
            crt.anchoredPosition = pos;

            // 标签
            var lblGo = CreateUIElement("Label", container.transform);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.75f, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.font = FontAsset;
            lbl.text = label;
            lbl.fontSize = 22;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = new Color(0.98f, 0.95f, 0.90f);

            // Toggle
            var toggleGo = CreateUIElement("Toggle", container.transform);
            var trt = toggleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.75f, 0.5f); trt.anchorMax = new Vector2(0.75f, 0.5f);
            trt.pivot = new Vector2(0, 0.5f);
            trt.sizeDelta = new Vector2(40, 26);
            trt.anchoredPosition = Vector2.zero;

            var bgGo = CreateUIElement("Background", toggleGo.transform);
            var brt = bgGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.45f, 0.40f, 0.34f);

            var checkGo = CreateUIElement("Checkmark", toggleGo.transform);
            var crt2 = checkGo.GetComponent<RectTransform>();
            crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one;
            crt2.offsetMin = new Vector2(4, 4); crt2.offsetMax = new Vector2(-4, -4);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.62f, 0.34f, 0.18f);

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            return toggle;
        }

        private Button CreateTextButton(string name, string text, Vector2 pos)
        {
            var go = CreateUIElement(name, transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220, 48);
            rt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.52f, 0.42f, 0.28f);

            var lblGo = CreateUIElement("Label", go.transform);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(200, 30);
            lrt.anchoredPosition = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.font = FontAsset;
            lbl.text = text;
            lbl.fontSize = 24;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = new Color(0.98f, 0.95f, 0.90f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.52f, 0.42f, 0.28f);
            colors.highlightedColor = new Color(0.65f, 0.52f, 0.35f);
            colors.pressedColor = new Color(0.38f, 0.30f, 0.18f);
            btn.colors = colors;

            return btn;
        }

        private TMP_Dropdown CreateResolutionDropdown(string name, Vector2 pos)
        {
            var container = CreateUIElement(name, transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(520, 40);
            crt.anchoredPosition = pos;

            // 标签
            var lblGo = CreateUIElement("Label", container.transform);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.3f, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.font = FontAsset;
            lbl.text = "分辨率";
            lbl.fontSize = 22;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = new Color(0.98f, 0.95f, 0.90f);

            // Dropdown
            var ddGo = CreateUIElement("Dropdown", container.transform);
            var drt = ddGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.30f, 0); drt.anchorMax = new Vector2(1, 1);
            drt.offsetMin = new Vector2(10, 4); drt.offsetMax = new Vector2(0, -4);

            var ddImg = ddGo.AddComponent<Image>();
            ddImg.color = new Color(0.45f, 0.40f, 0.34f);

            var dropdown = ddGo.AddComponent<TMP_Dropdown>();
            dropdown.options = Resolutions.Select(r => new TMP_Dropdown.OptionData(r.label)).ToList();

            // 下拉列表模板（简化版：直接用文字列表）
            var template = CreateUIElement("Template", ddGo.transform);
            var trt = template.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(0, 200);
            trt.anchoredPosition = new Vector2(0, -2);
            var templateImg = template.AddComponent<Image>();
            templateImg.color = new Color(0.35f, 0.30f, 0.24f);
            var templateScroll = template.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateUIElement("Viewport", template.transform);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            var vpMask = viewport.AddComponent<Image>();
            vpMask.color = Color.white;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateUIElement("Content", viewport.transform);
            var cort = content.GetComponent<RectTransform>();
            cort.anchorMin = new Vector2(0, 1); cort.anchorMax = new Vector2(1, 1);
            cort.pivot = new Vector2(0.5f, 1);
            cort.sizeDelta = new Vector2(0, Resolutions.Length * 30);
            cort.anchoredPosition = Vector2.zero;

            // ScrollRect 连接
            templateScroll.viewport = vrt;
            templateScroll.content = cort;

            // 为每个分辨率创建选项
            for (int i = 0; i < Resolutions.Length; i++)
            {
                var itemGo = CreateUIElement($"Item_{i}", content.transform);
                var irt = itemGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(1, 1);
                irt.pivot = new Vector2(0.5f, 1);
                irt.sizeDelta = new Vector2(0, 30);
                irt.anchoredPosition = new Vector2(0, -i * 30);

                var itemBg = itemGo.AddComponent<Image>();
                itemBg.color = new Color(0, 0, 0, 0); // 透明默认

                var itemToggle = itemGo.AddComponent<Toggle>();
                itemToggle.targetGraphic = itemBg;

                var itemLbl = CreateUIElement("ItemLabel", itemGo.transform);
                var ilrt = itemLbl.GetComponent<RectTransform>();
                ilrt.anchorMin = Vector2.zero; ilrt.anchorMax = Vector2.one;
                ilrt.offsetMin = new Vector2(8, 0); ilrt.offsetMax = new Vector2(-8, 0);
                var ilbl = itemLbl.AddComponent<TextMeshProUGUI>();
                ilbl.font = FontAsset;
                ilbl.text = Resolutions[i].label;
                ilbl.fontSize = 18;
                ilbl.alignment = TextAlignmentOptions.Left;
                ilbl.color = new Color(0.98f, 0.95f, 0.90f);
            }

            dropdown.template = trt;
            dropdown.captionText = lbl;  // 复用标签来显示当前选择
            template.gameObject.SetActive(false); // 初始隐藏

            return dropdown;
        }

        // ━━━ 工具 ━━━

        private static int FindDefaultResolutionIndex()
        {
            int w = Screen.width;
            int h = Screen.height;
            for (int i = 0; i < Resolutions.Length; i++)
            {
                if (Resolutions[i].width == w && Resolutions[i].height == h)
                    return i;
            }
            return 2; // 默认 1920x1080
        }

        private struct ResolutionEntry
        {
            public int width, height;
            public string label;
            public ResolutionEntry(int w, int h, string l) { width = w; height = h; label = l; }
        }
    }
}
