using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TeaMist.Core;
using TeaMist.Data;
using TeaMist.Gameplay;
using TeaMist.Rendering;

namespace TeaMist.UI
{
    /// <summary>
    /// 碎片获得通知 —— 屏幕中央淡入淡出的"获得碎片"提示。
    /// 监听 TeaShopLoop.OnFragmentReceived，自动查询 FragmentSO 展示标题和内容预览。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FragmentNotification : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private Text _titleText;
        private Text _subtitleText;
        private Text _previewText;
        private Image _background;

        private Coroutine _showCoroutine;
        private const float DISPLAY_DURATION = 3.5f;
        private const float FADE_DURATION = 0.5f;

        // 配色
        private static readonly Color BgColor = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        private static readonly Color TitleColor = new Color(0.85f, 0.72f, 0.35f);
        private static readonly Color SubtitleColor = new Color(0.75f, 0.70f, 0.62f);
        private static readonly Color PreviewColor = new Color(0.82f, 0.80f, 0.76f);

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            BuildUI();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        void OnEnable()
        {
            var loop = TeaShopLoop.Instance;
            if (loop != null)
                loop.OnFragmentReceived.AddListener(OnFragmentReceived);
        }

        void OnDisable()
        {
            var loop = TeaShopLoop.Instance;
            if (loop != null)
                loop.OnFragmentReceived.RemoveListener(OnFragmentReceived);
        }

        private void OnFragmentReceived(string fragmentId, int affection)
        {
            // 查询碎片详情
            FragmentSO frag = null;
            var dm = DataManager.Instance;
            if (dm != null)
                frag = dm.GetFragment(fragmentId);

            string title = frag != null ? frag.fragmentTitle : "未知碎片";
            string subtitle = "获得碎片";
            string preview = frag != null ? TruncateText(frag.content, 60) : "";

            if (frag != null)
            {
                subtitle = frag.fragmentType switch
                {
                    FragmentType.叙事 => "获得叙事碎片",
                    FragmentType.经营 => "获得经营碎片",
                    FragmentType.记忆 => "获得记忆碎片",
                    FragmentType.彩蛋 => "发现彩蛋碎片",
                    _ => "获得碎片"
                };
            }

            Show(title, subtitle, preview);
        }

        private void Show(string title, string subtitle, string preview)
        {
            if (_showCoroutine != null)
                StopCoroutine(_showCoroutine);

            _titleText.text = title;
            _subtitleText.text = subtitle;
            _previewText.text = preview;

            gameObject.SetActive(true);
            _showCoroutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            // 淡入
            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / FADE_DURATION);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // 停留
            yield return new WaitForSeconds(DISPLAY_DURATION);

            // 淡出
            elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / FADE_DURATION);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _showCoroutine = null;
        }

        private void BuildUI()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 100f);
            rt.sizeDelta = new Vector2(700, 180);

            // 背景
            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            _background = bgGo.AddComponent<Image>();
            ApplyInkStyle(_background, BgColor, 1.5f);
            var bgOutline = bgGo.AddComponent<Outline>();
            bgOutline.effectColor = TitleColor;
            bgOutline.effectDistance = new Vector2(2, -2);

            // 副标题（"获得叙事碎片"）
            var subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(transform, false);
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0, -15f);
            subRt.sizeDelta = new Vector2(600, 28);
            _subtitleText = subGo.AddComponent<Text>();
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.fontSize = 18;
            _subtitleText.color = SubtitleColor;
            _subtitleText.font = GetChineseFont();
            _subtitleText.raycastTarget = false;

            // 标题（碎片名称）
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0, 10f);
            titleRt.sizeDelta = new Vector2(640, 42);
            _titleText = titleGo.AddComponent<Text>();
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.fontSize = 28;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.color = TitleColor;
            _titleText.font = GetChineseFont();
            _titleText.raycastTarget = false;

            // 预览文字
            var previewGo = new GameObject("Preview", typeof(RectTransform));
            previewGo.transform.SetParent(transform, false);
            var previewRt = previewGo.GetComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0, 0);
            previewRt.anchorMax = new Vector2(1, 0);
            previewRt.pivot = new Vector2(0.5f, 0f);
            previewRt.anchoredPosition = new Vector2(0, 15f);
            previewRt.sizeDelta = new Vector2(-40, 50);
            _previewText = previewGo.AddComponent<Text>();
            _previewText.alignment = TextAnchor.MiddleCenter;
            _previewText.fontSize = 16;
            _previewText.color = PreviewColor;
            _previewText.lineSpacing = 4;
            _previewText.font = GetChineseFont();
            _previewText.raycastTarget = false;
        }

        private static Font GetChineseFont()
        {
            var cf = FontManager.ChineseFont;
            return cf != null ? cf : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "……";
        }

        private static void ApplyInkStyle(Image image, Color color, float paperTiling = 1.0f)
        {
            if (image == null) return;
            InkUIHelper.ApplyToImage(image, color, 0.12f, paperTiling, 0.08f, 0.06f, 0.25f, 0.05f);
        }
    }
}
