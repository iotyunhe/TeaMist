using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TeaMist.Gameplay;

namespace TeaMist.Core
{
    /// <summary>
    /// 水墨五层渲染通道 —— 注入 URP 渲染管线
    /// 使用 InkFullscreenBlit 材质的 5 个 Pass：
    ///   Pass 0 → InkTone     Pass 1 → InkWash
    ///   Pass 2 → SeasonTint  Pass 3 → InkEdge
    ///   Pass 4 → InkVignette
    /// 每帧从 SeasonManager 读取当前调色板，更新 Shader 全局参数
    /// </summary>
    public class InkRenderPass : ScriptableRenderPass
    {
        private Material inkMaterial;

        private RTHandle source;
        private RTHandle tempRT1;
        private RTHandle tempRT2;
        private RTHandle tempRT3;

        private const string ProfilerTag = "InkRenderPass";

        public InkRenderPass(RenderPassEvent renderPassEvent)
        {
            this.renderPassEvent = renderPassEvent;
        }

        public void Setup(Material material)
        {
            inkMaterial = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // URP 14：在 Execute 中再获取 camera target 更安全
            ConfigureInput(ScriptableRenderPassInput.Color);

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempRT1, desc, name: "_TempInkRT1");
            RenderingUtils.ReAllocateIfNeeded(ref tempRT2, desc, name: "_TempInkRT2");
            RenderingUtils.ReAllocateIfNeeded(ref tempRT3, desc, name: "_TempInkRT3");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (inkMaterial == null) return;

            // 在 Execute 中获取相机目标，避免 OnCameraSetup 时 handle 未就绪
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
            if (Gameplay.SeasonManager.Instance == null)
            {
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                return;
            }

            // Layer 1: InkTone（灰度 → 墨色基调）
            Blitter.BlitCameraTexture(cmd, source, tempRT1, inkMaterial, 0);

            // Layer 2: InkWash（水墨晕染）
            Blitter.BlitCameraTexture(cmd, tempRT1, tempRT2, inkMaterial, 1);

            // Layer 3: SeasonTint（季节染色）
            Blitter.BlitCameraTexture(cmd, tempRT2, tempRT3, inkMaterial, 2);

            // Layer 4: InkEdge（墨线勾勒）
            Blitter.BlitCameraTexture(cmd, tempRT3, tempRT1, inkMaterial, 3);

            // Layer 5: InkVignette（暗角/画框/天气）
            Blitter.BlitCameraTexture(cmd, tempRT1, source, inkMaterial, 4);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void UpdateShaderGlobals(CommandBuffer cmd)
        {
            // 从 InkRenderSettings 读取艺术家可调参数（有默认回退值）
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

            // 从 SeasonManager 读取季节颜色
            if (Gameplay.SeasonManager.Instance == null) return;
            var colors = Gameplay.SeasonManager.Instance.GetCurrentBlendedColors();
            var season = Gameplay.SeasonManager.Instance.CurrentSeason;
            var weather = Gameplay.SeasonManager.Instance.CurrentWeather;
            float progress = Gameplay.SeasonManager.Instance.SeasonProgress / 100f;

            // Pass 0: InkTone
            cmd.SetGlobalColor("_InkBaseColor", colors.inkTone);
            cmd.SetGlobalFloat("_ToneContrast", tc);

            // Pass 1: InkWash
            cmd.SetGlobalFloat("_WashBlurRadius", wbr);
            cmd.SetGlobalFloat("_WashIntensity", wi);

            // Pass 2: SeasonTint
            cmd.SetGlobalColor("_SeasonTintColor", colors.seasonTint);
            cmd.SetGlobalFloat("_TintStrength", ts + Mathf.Sin(progress * Mathf.PI) * 0.08f);

            // Pass 3: InkEdge
            cmd.SetGlobalFloat("_EdgeWidth", ew);
            cmd.SetGlobalFloat("_EdgeThreshold", et);
            cmd.SetGlobalColor("_EdgeColor", ec);

            // Pass 4: InkVignette
            cmd.SetGlobalColor("_VignetteColor", colors.vignetteColor);
            cmd.SetGlobalFloat("_VignetteIntensity", vi);

            // 天气影响
            float baseWetness = 0f;
            if (weather == Gameplay.Weather.Rain || weather == Gameplay.Weather.Storm) baseWetness = 0.3f;
            if (weather == Gameplay.Weather.Mist) baseWetness = Mathf.Max(baseWetness, 0.6f);
            cmd.SetGlobalFloat("_WeatherWetness", baseWetness * wwm);

            // 飞白 — 秋季最明显
            float flyingWhite = season == Gameplay.Season.Autumn ? 0.25f : 0.10f;
            cmd.SetGlobalFloat("_FlyingWhiteStrength", flyingWhite * fwm);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle 由 RenderingUtils.ReAllocateIfNeeded 管理
        }

        public void Dispose()
        {
            tempRT1?.Release();
            tempRT2?.Release();
            tempRT3?.Release();
        }
    }
}
