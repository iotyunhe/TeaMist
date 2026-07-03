using UnityEngine;
using System.Collections.Generic;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 茶壶口袅袅热气效果。
    /// 挂载到茶壶 Sprite GameObject 上，TeaBrewingManager
    /// 在开始泡茶时 Play()，泡茶完成时 Stop()。
    /// </summary>
    public class TeaSteamEffect : MonoBehaviour
    {
        [Header("━━━ 蒸汽粒子 ━━━")]
        public ParticleSystem steamParticles;

        [Header("━━━ 参数 ━━━")]
        [Range(1, 30)]
        public int maxSteam = 8;
        [Range(0.1f, 3f)]
        public float riseSpeed = 0.5f;
        [Range(0.5f, 4f)]
        public float steamLifetime = 2.2f;

        [Header("━━━ 水墨感 ━━━")]
        [Tooltip("蒸汽底色（会被茶色调制）")]
        public Color steamBaseColor = new Color(0.95f, 0.94f, 0.92f, 0.65f);
        [Tooltip("蒸汽随茶色偏移强度 0=不变 1=完全跟随茶色")]
        [Range(0f, 1f)]
        public float teaColorInfluence = 0.12f;

        // 运行时
        private Material _steamMaterial;
        private Texture2D _steamTexture;
        private Color _currentTeaColor = Color.white;

        void Awake()
        {
            if (steamParticles == null)
                EnsureSteamSystem();
        }

        /// <summary>开始冒热气</summary>
        public void Play()
        {
            if (steamParticles != null && !steamParticles.isPlaying)
                steamParticles.Play();
        }

        /// <summary>停止冒热气</summary>
        public void Stop()
        {
            if (steamParticles != null && steamParticles.isPlaying)
                steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>立即清除</summary>
        public void StopImmediate()
        {
            if (steamParticles != null)
                steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// 设置茶色，让蒸汽带一点茶汤颜色
        /// </summary>
        public void SetTeaColor(Color teaColor)
        {
            _currentTeaColor = teaColor;
            if (steamParticles == null) return;

            var main = steamParticles.main;
            Color c = Color.Lerp(steamBaseColor, teaColor, teaColorInfluence);
            c.a = steamBaseColor.a;
            main.startColor = c;
        }

        // ── 内部 ──

        private void EnsureSteamSystem()
        {
            var go = new GameObject("TeaSteam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.3f, 0); // 壶口上方

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            SetupSteamParticle(ps);

            steamParticles = ps;
        }

        private void SetupSteamParticle(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(steamLifetime * 0.5f, steamLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(riseSpeed * 0.2f, riseSpeed * 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = steamBaseColor;
            main.maxParticles = maxSteam;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f, 0.01f); // 轻微向上

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            emission.enabled = true;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.06f;
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(-8f, 0, 0); // 略微向上倾斜

            // 速度随生命周期：先慢后快再慢（蒸汽自然漂浮感）
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, -0.04f),
                    new Keyframe(0.3f, 0.05f),
                    new Keyframe(0.7f, -0.03f),
                    new Keyframe(1f, 0.01f)));
            vel.y = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, riseSpeed * 0.3f),
                    new Keyframe(0.4f, riseSpeed * 0.9f),
                    new Keyframe(0.8f, riseSpeed * 0.5f),
                    new Keyframe(1f, riseSpeed * 0.2f)));
            // z 轴必须与 x/y 同模式，否则报 "Particle Velocity curves must all be in the same mode"
            vel.z = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 0f)));
            vel.space = ParticleSystemSimulationSpace.Local;

            // 大小：从小到大再消散（蒸汽扩散）
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.25f, 0.75f),
                    new Keyframe(0.55f, 1.0f),
                    new Keyframe(0.80f, 1.0f),
                    new Keyframe(1f, 0.4f)));

            // Alpha：柔入柔出（水墨晕染感）
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.08f, 0.15f),
                    new GradientAlphaKey(0.12f, 0.40f),
                    new GradientAlphaKey(0.10f, 0.70f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            // 噪声：让蒸汽有自然飘动感（更强）
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            noise.frequency = 0.4f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            noise.damping = true;
            noise.octaveCount = 2;
            noise.octaveMultiplier = 0.5f;
            noise.octaveScale = 2f;

            // 旋转：让粒子有轻微旋转（更自然）
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-25f, 25f);

            // 外部力场：模拟空气流动（influenceFilter/influenceMask 需 Unity 2023.1+，2022 仅启用基础力场）
            var external = ps.externalForces;
            external.enabled = true;

            // ── 渲染 ──
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            // 尝试加载水墨柔光材质，fallback 到普通透明材质
            FindOrCreateSteamMaterial();
            renderer.material = _steamMaterial;
            renderer.sortingLayerName = SortingLayers.Props;
            renderer.sortingOrder = 8;
            renderer.enableGPUInstancing = true;
        }

        private void FindOrCreateSteamMaterial()
        {
            // 生成柔和圆形粒子贴图，避免用默认白图渲染成实心方块
            _steamTexture = CreateSoftCircleTexture(64, 0.45f);

            // 尝试几个可能的 URP particle shader 路径
            string[] shaderNames =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default"
            };

            foreach (var name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader != null && shader.isSupported)
                {
                    _steamMaterial = new Material(shader);
                    _steamMaterial.SetTexture("_MainTex", _steamTexture);
                    _steamMaterial.SetColor("_BaseColor", Color.white);
                    _steamMaterial.SetColor("_Color", Color.white);
                    // 透明 Alpha 混合
                    _steamMaterial.SetFloat("_Surface", 1f);       // 1 = Transparent
                    _steamMaterial.SetFloat("_Blend", 0f);         // 0 = Alpha
                    _steamMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _steamMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                    _steamMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _steamMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _steamMaterial.SetInt("_ZWrite", 0);
                    _steamMaterial.renderQueue = 3000;
                    return;
                }
            }

            // 最终 fallback：纯透明材质
            _steamMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            _steamMaterial.SetTexture("_MainTex", _steamTexture);
        }

        /// <summary>
        /// 生成边缘柔和的圆形贴图，用于水墨蒸汽粒子
        /// </summary>
        private static Texture2D CreateSoftCircleTexture(int size, float softness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = 1f - Mathf.Clamp01(dist / maxDist);
                    alpha = Mathf.Pow(alpha, Mathf.Lerp(0.6f, 2.5f, softness));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 依据茶名设置蒸汽色调（由 TeaBrewingManager 调用）
        /// </summary>
        public void SetTeaName(string teaName)
        {
            Color c = teaName switch
            {
                "桂花蜜茶" => new Color(0.92f, 0.82f, 0.55f),
                "清心茶" => new Color(0.60f, 0.78f, 0.65f),
                "红茶" => new Color(0.75f, 0.35f, 0.20f),
                "绿茶" => new Color(0.55f, 0.72f, 0.45f),
                "乌龙茶" => new Color(0.80f, 0.65f, 0.40f),
                _ => steamBaseColor
            };
            SetTeaColor(c);
        }

        void OnDestroy()
        {
            if (steamParticles != null)
                Destroy(steamParticles.gameObject);
            if (_steamMaterial != null)
                Destroy(_steamMaterial);
            if (_steamTexture != null)
                Destroy(_steamTexture);
        }
    }
}
