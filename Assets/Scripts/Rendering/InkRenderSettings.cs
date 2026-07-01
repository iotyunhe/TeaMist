using UnityEngine;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 水墨渲染参数 —— 运行时可调的 Shader 全局参数。
    /// 挂载到任意场景 GameObject（Bootstrap 会自动创建），
    /// InkRenderPass 每帧从此读取参数注入 CommandBuffer。
    ///
    /// 调试模式（devMode = true）时显示 OnGUI 浮动调参面板。
    /// </summary>
    public class InkRenderSettings : MonoBehaviour
    {
        public static InkRenderSettings Instance { get; private set; }

        [Header("━━━ 墨色基调 (Pass 0: InkTone) ━━━")]
        [Tooltip("灰阶对比度，越高墨色越分明")]
        [Range(1.0f, 3.0f)]
        public float toneContrast = 1.8f;

        [Header("━━━ 水墨晕染 (Pass 1: InkWash) ━━━")]
        [Tooltip("模糊采样半径，越大晕染越散")]
        [Range(0.5f, 8.0f)]
        public float washBlurRadius = 3.5f;

        [Tooltip("晕染混合强度，0=无效果 1=完全模糊")]
        [Range(0f, 1f)]
        public float washIntensity = 0.65f;

        [Header("━━━ 季节染色 (Pass 2: SeasonTint) ━━━")]
        [Tooltip("季节色调叠加强度")]
        [Range(0f, 1f)]
        public float tintStrength = 0.55f;

        [Header("━━━ 墨线勾勒 (Pass 3: InkEdge) ━━━")]
        [Tooltip("Sobel 采样跨度，越大边缘越粗")]
        [Range(0.5f, 6.0f)]
        public float edgeWidth = 2.2f;

        [Tooltip("边缘检测阈值，越低越敏感（更多边缘）")]
        [Range(0.01f, 0.5f)]
        public float edgeThreshold = 0.06f;

        [Tooltip("描边墨色")]
        public Color edgeColor = new Color(0.08f, 0.06f, 0.04f, 0.85f);

        [Header("━━━ 暗角画框 (Pass 4: InkVignette) ━━━")]
        [Tooltip("暗角强度")]
        [Range(0f, 1f)]
        public float vignetteIntensity = 0.65f;

        [Header("━━━ 天气影响 ━━━")]
        [Tooltip("湿润度乘数（雨天/雾天画面更柔和）")]
        [Range(0f, 3f)]
        public float weatherWetnessMult = 1.5f;

        [Tooltip("飞白强度乘数（秋季枯笔效果）")]
        [Range(0f, 3f)]
        public float flyingWhiteMult = 1.0f;

        [Header("━━━ 调试 ━━━")]
        public bool showDebugPanel = true;
        public bool bypassInk = false; // 临时关闭水墨效果看原画面

        // ── 私有 ──

        private Rect _panelRect = new Rect(10, 10, 280, 420);
        private bool _panelDragging;
        private Vector2 _dragOffset;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnGUI()
        {
            if (!showDebugPanel) return;

            // 简单拖动
            var e = Event.current;
            if (e.type == EventType.MouseDown && _panelRect.Contains(e.mousePosition))
            {
                _panelDragging = true;
                _dragOffset = e.mousePosition - _panelRect.position;
            }
            if (e.type == EventType.MouseUp) _panelDragging = false;
            if (_panelDragging && e.type == EventType.MouseDrag)
                _panelRect.position = e.mousePosition - _dragOffset;

            GUILayout.BeginArea(_panelRect, "水墨调参", GUI.skin.window);

            // 开关
            bool newBypass = GUILayout.Toggle(bypassInk, " 关闭水墨效果（看原画面）");
            if (newBypass != bypassInk)
            {
                bypassInk = newBypass;
                Shader.SetGlobalFloat("_BypassInk", bypassInk ? 1f : 0f);
            }

            GUILayout.Space(8);

            // ── 墨色 ──
            GUILayout.Label("─ 墨色基调 ─", GUI.skin.label);
            toneContrast = LabeledSlider("对比度", toneContrast, 1f, 3f);

            GUILayout.Space(6);

            // ── 晕染 ──
            GUILayout.Label("─ 水墨晕染 ─", GUI.skin.label);
            washBlurRadius = LabeledSlider("模糊半径", washBlurRadius, 0.5f, 8f);
            washIntensity = LabeledSlider("晕染强度", washIntensity, 0f, 1f);

            GUILayout.Space(6);

            // ── 染色 ──
            GUILayout.Label("─ 季节染色 ─", GUI.skin.label);
            tintStrength = LabeledSlider("着色强度", tintStrength, 0f, 1f);

            GUILayout.Space(6);

            // ── 描边 ──
            GUILayout.Label("─ 墨线勾勒 ─", GUI.skin.label);
            edgeWidth = LabeledSlider("描边粗细", edgeWidth, 0.5f, 6f);
            edgeThreshold = LabeledSlider("边缘阈值", edgeThreshold, 0.01f, 0.5f);

            GUILayout.Space(6);

            // ── 暗角 ──
            GUILayout.Label("─ 暗角画框 ─", GUI.skin.label);
            vignetteIntensity = LabeledSlider("暗角强度", vignetteIntensity, 0f, 1f);

            GUILayout.Space(6);

            // ── 天气 ──
            GUILayout.Label("─ 天气影响 ─", GUI.skin.label);
            weatherWetnessMult = LabeledSlider("湿润乘数", weatherWetnessMult, 0f, 3f);
            flyingWhiteMult = LabeledSlider("飞白乘数", flyingWhiteMult, 0f, 3f);

            GUILayout.Space(8);

            // 预设按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置"))
            {
                toneContrast = 1.8f;
                washBlurRadius = 3.5f;
                washIntensity = 0.65f;
                tintStrength = 0.55f;
                edgeWidth = 2.2f;
                edgeThreshold = 0.06f;
                vignetteIntensity = 0.65f;
                weatherWetnessMult = 1.5f;
                flyingWhiteMult = 1.0f;
            }
            if (GUILayout.Button("浓墨"))
            {
                toneContrast = 2.5f;
                washBlurRadius = 1.5f;
                washIntensity = 0.3f;
                tintStrength = 0.3f;
                edgeWidth = 3.5f;
                edgeThreshold = 0.03f;
                vignetteIntensity = 0.8f;
            }
            if (GUILayout.Button("淡彩"))
            {
                toneContrast = 1.3f;
                washBlurRadius = 5.0f;
                washIntensity = 0.8f;
                tintStrength = 0.7f;
                edgeWidth = 1.0f;
                edgeThreshold = 0.12f;
                vignetteIntensity = 0.4f;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private float LabeledSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(65));
            float newVal = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label($"{newVal:F2}", GUILayout.Width(35));
            GUILayout.EndHorizontal();
            return newVal;
        }
    }
}
