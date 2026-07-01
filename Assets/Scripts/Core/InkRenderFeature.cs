using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 水墨渲染 URP Renderer Feature
    /// 将五层水墨后处理注入 URP 渲染管线
    ///
    /// 使用方式：
    ///   1. 在 URP Renderer Data 的 Renderer Features 列表中添加此 Feature
    ///   2. 将 InkFullscreenBlit Shader 生成的 Material 拖入 inkMaterial 槽位
    ///   3. Feature 会自动在 AfterRenderingTransparents 阶段执行水墨处理
    /// </summary>
    public class InkRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("InkFullscreenBlit Shader 生成的 Material（含 5 个 Pass：Tone/Wash/Tint/Edge/Vignette）")]
        public Material inkMaterial;

        private InkRenderPass _renderPass;

        private Material _runtimeMaterial; // 运行时自动创建的 Material

        public override void Create()
        {
            Material mat = inkMaterial;
            if (mat == null)
            {
                // 尝试运行时自动创建
                var shader = Shader.Find("TeaMist/InkRender/InkFullscreenBlit");
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader);
                    _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
                    mat = _runtimeMaterial;
                    Debug.Log("[InkRenderFeature] 自动创建 InkFullscreenBlit Material（Shader 编译成功）");
                }
            }

            if (mat == null)
            {
                Debug.LogWarning("[InkRenderFeature] 找不到 Shader 'TeaMist/InkRender/InkFullscreenBlit'，" +
                    "请确认 .shader 文件编译无错误。可以运行菜单 TeaMist > Setup Ink Render Pipeline 诊断。");
                return;
            }

            _renderPass = new InkRenderPass(RenderPassEvent.AfterRenderingTransparents);
            _renderPass.Setup(mat);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null) return;
            renderer.EnqueuePass(_renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderPass?.Dispose();
                _renderPass = null;
            }

            if (_runtimeMaterial != null)
            {
                CoreUtils.Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }
    }
}
