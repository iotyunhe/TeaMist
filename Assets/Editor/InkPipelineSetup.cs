using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TeaMist.Rendering;

namespace TeaMist.Editor
{
    /// <summary>
    /// 一键配置 URP 水墨渲染管线
    /// 菜单：TeaMist > Setup Ink Render Pipeline
    ///
    /// 自动化流程：
    ///   1. 查找/创建 URP Pipeline Asset + Forward Renderer
    ///   2. 找到 InkFullscreenBlit Shader，创建 Material
    ///   3. 将 InkRenderFeature 挂到 Forward Renderer 上
    ///   4. 将 Pipeline Asset 设为项目的激活渲染管线
    /// </summary>
    public static class InkPipelineSetup
    {
        private const string MenuPath = "TeaMist/Setup Ink Render Pipeline";
        private const string PipelineAssetPath = "Assets/Settings/TeaMist_URP.asset";
        private const string RendererAssetPath = "Assets/Settings/TeaMist_Renderer.asset";
        private const string MaterialPath = "Assets/Materials/InkFullscreenBlit.mat";

        [MenuItem(MenuPath, false, 0)]
        public static void Setup()
        {
            EditorUtility.DisplayProgressBar("TeaMist", "配置水墨渲染管线...", 0.1f);

            try
            {
                // ── Step 1: 确保目录 ──
                EnsureDirectory("Assets/Settings");
                EnsureDirectory("Assets/Materials");

                // ── Step 2: 查找/创建 Forward Renderer ──
                EditorUtility.DisplayProgressBar("TeaMist", "创建 Forward Renderer...", 0.2f);
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
                    Debug.Log("[InkPipelineSetup] 创建 Forward Renderer");
                }

                // ── Step 3: 添加 InkRenderFeature ──
                EditorUtility.DisplayProgressBar("TeaMist", "挂载 InkRenderFeature...", 0.4f);
                AddOrUpdateInkFeature(rendererData);

                // ── Step 4: 查找/创建 URP Pipeline Asset ──
                EditorUtility.DisplayProgressBar("TeaMist", "创建 Pipeline Asset...", 0.6f);
                var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
                if (pipelineAsset == null)
                {
                    pipelineAsset = UniversalRenderPipelineAsset.Create();
                    AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
                    Debug.Log("[InkPipelineSetup] 创建 URP Pipeline Asset");
                }

                // 挂接 Renderer 到 Pipeline（通过 SerializedObject，兼容所有 URP 14 版本）
                {
                    var so = new SerializedObject(pipelineAsset);
                    var rendererListProp = so.FindProperty("m_RendererDataList");
                    if (rendererListProp != null)
                    {
                        if (rendererListProp.arraySize == 0)
                            rendererListProp.arraySize = 1;
                        rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                        so.ApplyModifiedProperties();
                    }
                }

                // ── Step 5: 设为激活管线 ──
                EditorUtility.DisplayProgressBar("TeaMist", "设置为激活渲染管线...", 0.8f);
                GraphicsSettings.renderPipelineAsset = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;
                EditorUtility.SetDirty(pipelineAsset);

                // ── Step 6: 创建 InkFullscreenBlit Material ──
                EditorUtility.DisplayProgressBar("TeaMist", "创建水墨 Material...", 0.9f);
                CreateInkMaterial();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("<color=#88cc88>[InkPipelineSetup] ✅ 水墨渲染管线配置完成！</color>");
                EditorUtility.DisplayDialog("TeaMist",
                    "水墨渲染管线配置完成！\n\n" +
                    "• URP Pipeline: Settings/TeaMist_URP\n" +
                    "• Forward Renderer: Settings/TeaMist_Renderer\n" +
                    "• Ink Material: Materials/InkFullscreenBlit\n" +
                    "• InkRenderFeature 已挂载\n\n" +
                    "现在可以 Play 查看水墨效果。",
                    "好的");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AddOrUpdateInkFeature(UniversalRendererData rendererData)
        {
            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");

            // 检查是否已存在
            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var elem = featuresProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue is InkRenderFeature)
                {
                    Debug.Log("[InkPipelineSetup] InkRenderFeature 已存在，跳过");
                    so.ApplyModifiedProperties();
                    return;
                }
            }

            // 创建 InkRenderFeature 实例
            var inkFeature = ScriptableObject.CreateInstance<InkRenderFeature>();
            inkFeature.name = "InkRenderFeature";

            // 关联 Material（如果已存在）
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null)
            {
                var featureSo = new SerializedObject(inkFeature);
                featureSo.FindProperty("inkMaterial").objectReferenceValue = mat;
                featureSo.ApplyModifiedProperties();
            }

            // 添加到 Renderer Features 列表
            AssetDatabase.AddObjectToAsset(inkFeature, rendererData);

            featuresProp.InsertArrayElementAtIndex(featuresProp.arraySize);
            var newElem = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
            newElem.objectReferenceValue = inkFeature;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);

            Debug.Log("[InkPipelineSetup] InkRenderFeature 已挂载到 Forward Renderer");
        }

        private static void CreateInkMaterial()
        {
            // 检查是否已存在
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                Debug.Log("[InkPipelineSetup] InkFullscreenBlit Material 已存在，跳过");
                return;
            }

            // 查找 Shader
            var shader = Shader.Find("TeaMist/InkRender/InkFullscreenBlit");
            if (shader == null)
            {
                Debug.LogError("[InkPipelineSetup] ❌ 找不到 Shader: TeaMist/InkRender/InkFullscreenBlit\n" +
                    "请确认 Assets/Shaders/InkRender/InkFullscreenBlit.shader 存在且编译通过");
                return;
            }

            var mat = new Material(shader);
            mat.name = "InkFullscreenBlit";
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log("[InkPipelineSetup] 创建 InkFullscreenBlit Material");
        }

        private static void EnsureDirectory(string path)
        {
            var fullPath = System.IO.Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
            }
        }
    }
}
