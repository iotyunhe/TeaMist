using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TeaMist.Gameplay;

namespace TeaMist.Core
{
    /// <summary>
    /// 水墨五层渲染通道 (v2 — 深度感知) —— 注入 URP 渲染管线
    /// 使用 InkFullscreenBlit 材质的 5 个 Pass：
    ///   Pass 0 → InkTone (+深度雾气)    Pass 1 → InkWash (+深度模糊)
    ///   Pass 2 → SeasonTint              Pass 3 → InkEdge (+飞白)
    ///   Pass 4 → InkVignette (+宣纸纹理)
    /// 每帧从 SeasonManager 读取当前调色板，更新 Shader 全局参数
    ///
    /// v2 新增：
    ///   - 深度纹理输入 (ConfigureInput Color | Depth)
    ///   - 距离雾气参数
    ///   - 宣纸纹理传递（通过 InkSpriteMaterial 的全局纹理）
    /// </summary>
    public class InkRenderPass : ScriptableRenderPass
    {
        private Material inkMaterial;

        private RTHandle source;
        private RTHandle tempRT1;
        private RTHandle tempRT2;
        private RTHandle tempRT3;

        private const string ProfilerTag = "InkRenderPass";

        // ── 宣纸纹理 ──
        private static Texture2D _globalPaperTex;

        public InkRenderPass(RenderPassEvent renderPassEvent)
        {
            this.renderPassEvent = renderPassEvent;
        }

        public void Setup(Material material)
        {
            inkMaterial = material;
        }

        /// <summary>
        /// 设置全局宣纸纹理（由 InkSpriteMaterial.Initialize 调用）
        /// </summary>
        public static void SetGlobalPaperTexture(Texture2D paperTex)
        {
            _globalPaperTex = paperTex;
            if (paperTex != null)
            {
                Shader.SetGlobalTexture("_PaperTex", paperTex);
                Shader.SetGlobalFloat("_PaperOverlayStrength", 0.12f);
                Shader.SetGlobalFloat("_PaperTiling", 2.5f);
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 请求颜色 + 深度纹理（用于大气透视）
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempRT1, desc, name: "_TempInkRT1");
            RenderingUtils.ReAllocateIfNeeded(ref tempRT2, desc, name: "_TempInkRT2");
            RenderingUtils.ReAllocateIfNeeded(ref tempRT3, desc, name: "_TempInkRT3");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (inkMaterial == null) return;

            source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (source == null || tempRT1 == null || tempRT2 == null || tempRT3 == null)
            {
                Debug.LogWarning("[InkRenderPass] RTHandle 未就绪，跳过水墨后处理");
                return;
            }

            var cmd = CommandBufferPool.Get(ProfilerTag);
            cmd.Clear();

            // 检查是否绕过水墨效果（调试用）
            if (Rendering.InkRenderSettings.Instance != null &&
                Rendering.InkRenderSettings.Instance.bypassInk)
            {
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                return;
            }

            // 从 InkRenderSettings + SeasonManager 更新全局 Shader 参数
            UpdateShaderGlobals(cmd);

            // 更新宣纸纹理
            if (_globalPaperTex != null)
            {
                cmd.SetGlobalTexture("_PaperTex", _globalPaperTex);
            }

            if (Gameplay.SeasonManager.Instance == null)
            {
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                return;
            }

            // Layer 1: InkTone（灰度 → 墨色基调 + 深度雾气）
            Blitter.BlitCameraTexture(cmd, source, tempRT1, inkMaterial, 0);

            // Layer 2: InkWash（水墨晕染 + 深度模糊）
            Blitter.BlitCameraTexture(cmd, tempRT1, tempRT2, inkMaterial, 1);

            // Layer 3: SeasonTint（季节染色）
            Blitter.BlitCameraTexture(cmd, tempRT2, tempRT3, inkMaterial, 2);

            // Layer 4: InkEdge（墨线勾勒 + 飞白）
            Blitter.BlitCameraTexture(cmd, tempRT3, tempRT1, inkMaterial, 3);

            // Layer 5: InkVignette（暗角/画框/天气/宣纸纹理）
            Blitter.BlitCameraTexture(cmd, tempRT1, source, inkMaterial, 4);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void UpdateShaderGlobals(CommandBuffer cmd)
        {
            var s = Rendering.InkRenderSettings.Instance;
            float tc = s != null ? s.toneContrast : 1.8f;
            float wbr = s != null ? s.washBlurRadius : 3.5f;
            float wi = s != null ? s.washIntensity : 0.65f;
            float ts = s != null ? s.tintStrength : 0.55f;
            float ew = s != null ? s.edgeWidth : 2.2f;
            float et = s != null ? s.edgeThreshold : 0.06f;
            Color ec = s != null ? s.edgeColor : new Color(0.08f, 0.06f, 0.04f, 0.85f);
            float vi = s != null ? s.vignetteIntensity : 0.65f;
            float wwm = s != null ? s.weatherWetnessMult : 1.5f;
            float fwm = s != null ? s.flyingWhiteMult : 1.0f;

            if (Gameplay.SeasonManager.Instance == null) return;
            var colors = Gameplay.SeasonManager.Instance.GetCurrentBlendedColors();
            var season = Gameplay.SeasonManager.Instance.CurrentSeason;
            var weather = Gameplay.SeasonManager.Instance.CurrentWeather;
            float progress = Gameplay.SeasonManager.Instance.SeasonProgress / 100f;

            // ── Pass 0: InkTone + 深度雾气 ──
            cmd.SetGlobalColor("_InkBaseColor", colors.inkTone);
            cmd.SetGlobalFloat("_ToneContrast", tc);
            // 深度雾气参数：2-8 世界单位范围
            cmd.SetGlobalColor("_DistanceFogColor", new Color(0.85f, 0.83f, 0.78f)); // 远处纸色
            cmd.SetGlobalFloat("_DistanceFogStart", 2.5f);
            cmd.SetGlobalFloat("_DistanceFogEnd", 8.0f);

            // ── Pass 1: InkWash + 深度模糊 ──
            cmd.SetGlobalFloat("_WashBlurRadius", wbr);
            cmd.SetGlobalFloat("_WashIntensity", wi);
            cmd.SetGlobalFloat("_DistanceWashMult", 2.5f);   // 远处模糊倍率
            cmd.SetGlobalFloat("_DistanceBlurStart", 3.0f);

            // ── Pass 2: SeasonTint ──
            cmd.SetGlobalColor("_SeasonTintColor", colors.seasonTint);
            cmd.SetGlobalFloat("_TintStrength", ts + Mathf.Sin(progress * Mathf.PI) * 0.08f);

            // ── Pass 3: InkEdge + 深度衰减 ──
            cmd.SetGlobalFloat("_EdgeWidth", ew);
            cmd.SetGlobalFloat("_EdgeThreshold", et);
            cmd.SetGlobalColor("_EdgeColor", ec);
            cmd.SetGlobalFloat("_DistanceEdgeAtten", 0.7f);  // 远处边缘衰减强度

            // ── Pass 4: InkVignette + 纸纹 ──
            cmd.SetGlobalColor("_VignetteColor", colors.vignetteColor);
            cmd.SetGlobalFloat("_VignetteIntensity", vi);
            if (_globalPaperTex != null)
            {
                cmd.SetGlobalFloat("_PaperOverlayStrength", 0.12f);
                cmd.SetGlobalFloat("_PaperTiling", 2.5f);
            }

            // ── 天气影响 ──
            float baseWetness = 0f;
            if (weather == Gameplay.Weather.Rain || weather == Gameplay.Weather.Storm) baseWetness = 0.3f;
            if (weather == Gameplay.Weather.Mist) baseWetness = Mathf.Max(baseWetness, 0.6f);
            cmd.SetGlobalFloat("_WeatherWetness", baseWetness * wwm);

            // 飞白 — 秋季最明显
            float flyingWhite = season == Gameplay.Season.Autumn ? 0.25f : 0.10f;
            cmd.SetGlobalFloat("_FlyingWhiteStrength", flyingWhite * fwm);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose()
        {
            tempRT1?.Release();
            tempRT2?.Release();
            tempRT3?.Release();
        }
    }
}
