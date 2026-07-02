using UnityEngine;
using System.Collections.Generic;
using TeaMist.Gameplay;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 茶馆桌面小物件 —— 香炉 + 烟、花瓶 + 时令花、茶宠。
    /// 挂在 Props 层，与泡茶茶具配合。
    /// 挂在 ArtSceneRoot 下，单例职责。
    /// </summary>
    public class TeaHouseDecoration : MonoBehaviour
    {
        [Header("━━━ 物件参数 ━━━")]
        [SerializeField] private float decorationGroupX = -2.5f;
        [SerializeField] private float decorationGroupY = -3.0f;
        [SerializeField] private float itemSpacing = 0.55f;

        [Header("━━━ 香炉烟 ━━━")]
        [SerializeField] private Color smokeTint = new Color(0.82f, 0.80f, 0.76f);
        [SerializeField] private float smokeParticleSize = 0.08f;

        [Header("━━━ 季节性粒子 ━━━")]
        [SerializeField] private int maxSeasonalParticles = 12;

        // 子对象
        private GameObject _incenseBurner;
        private ParticleSystem _incenseSmoke;
        private ParticleSystem _seasonalParticles; // 落叶/落花/雪
        private GameObject _flowerVase;
        private SpriteRenderer _flowerRenderer;
        private GameObject _teaPet;

        // 运行时
        private Season _currentSeason;

        void Awake()
        {
            BuildDecorations();
        }

        void Start()
        {
            _currentSeason = SeasonManager.Instance != null
                ? SeasonManager.Instance.CurrentSeason
                : Season.Spring;
            ApplySeason(_currentSeason);

            if (SeasonManager.Instance != null)
                SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
        }

        void OnDestroy()
        {
            if (SeasonManager.Instance != null)
                SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
        }

        // ━━ 构建 ━━

        private void BuildDecorations()
        {
            float x0 = decorationGroupX;
            float y0 = decorationGroupY;
            float g = itemSpacing;

            // ── 香炉（位置 0）──
            _incenseBurner = new GameObject("Deco_IncenseBurner");
            _incenseBurner.transform.SetParent(transform, false);
            _incenseBurner.transform.localPosition = new Vector3(x0, y0, -0.1f);
            _incenseBurner.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            // 香炉主体
            CreateInkCircle("Incense_Body", _incenseBurner.transform, 0.95f,
                new Color(0.42f, 0.32f, 0.20f), SortingLayers.Props, 2);
            CreateInkCircle("Incense_Rim", _incenseBurner.transform, 1.05f,
                new Color(0.35f, 0.25f, 0.14f), SortingLayers.Props, 1);
            CreateInkCircle("Incense_Lid", _incenseBurner.transform, 0.50f,
                new Color(0.28f, 0.18f, 0.10f), SortingLayers.Props, 3);

            // 烟粒子（双粒子系统：主烟 + 飘散 wisps）
            var smokeGo = new GameObject("Incense_Smoke");
            smokeGo.transform.SetParent(_incenseBurner.transform, false);
            smokeGo.transform.localPosition = new Vector3(0, 0.18f, 0);
            _incenseSmoke = smokeGo.AddComponent<ParticleSystem>();
            SetupIncenseSmoke(_incenseSmoke);

            // 飘散 wisps（第二粒子系统，更慢更散）
            var wispGo = new GameObject("Incense_Wisps");
            wispGo.transform.SetParent(_incenseBurner.transform, false);
            wispGo.transform.localPosition = new Vector3(0, 0.35f, 0);
            var wispPs = wispGo.AddComponent<ParticleSystem>();
            SetupIncenseWisps(wispPs);

            // ── 花瓶 + 花（位置 1）──
            _flowerVase = new GameObject("Deco_FlowerVase");
            _flowerVase.transform.SetParent(transform, false);
            _flowerVase.transform.localPosition = new Vector3(x0 + g, y0 + 0.05f, -0.1f);

            CreateInkVase("Vase_Body", _flowerVase.transform,
                new Color(0.60f, 0.73f, 0.68f), SortingLayers.Props, 2);
            CreateInkCircle("Vase_Rim", _flowerVase.transform, 0.42f,
                new Color(0.48f, 0.62f, 0.57f), SortingLayers.Props, 1);

            // 花
            var flowerGo = new GameObject("Flower");
            flowerGo.transform.SetParent(_flowerVase.transform, false);
            flowerGo.transform.localPosition = new Vector3(0, 0.30f, -0.1f);
            _flowerRenderer = flowerGo.AddComponent<SpriteRenderer>();
            _flowerRenderer.sprite = CreateFlowerSprite();
            _flowerRenderer.sortingLayerName = SortingLayers.Props;
            _flowerRenderer.sortingOrder = 4;
            _flowerRenderer.transform.localScale = new Vector3(0.20f * 100f, 0.20f * 100f, 1f);

            // ── 茶宠（位置 2）──
            _teaPet = new GameObject("Deco_TeaPet");
            _teaPet.transform.SetParent(transform, false);
            _teaPet.transform.localPosition = new Vector3(x0 + g * 2f, y0 + 0.08f, -0.1f);
            CreateInkCircle("TeaPet_Body", _teaPet.transform, 0.22f,
                new Color(0.22f, 0.18f, 0.13f), SortingLayers.Props, 2);
            CreateInkCircle("TeaPet_Head", _teaPet.transform, 0.13f,
                new Color(0.20f, 0.16f, 0.11f), SortingLayers.Props, 3);
            var petHead = _teaPet.transform.Find("TeaPet_Head");
            if (petHead != null) petHead.localPosition = new Vector3(0, 0.15f, 0);

            // ── 季节性粒子（落叶/落花/雪）──
            var seaGo = new GameObject("Deco_SeasonalParticles");
            seaGo.transform.SetParent(transform, false);
            seaGo.transform.localPosition = new Vector3(0, 3.5f, -0.2f); // 从画面上方落下
            _seasonalParticles = seaGo.AddComponent<ParticleSystem>();
            _seasonalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetupSeasonalParticles(_seasonalParticles);
        }

        void Update()
        {
            // 香炉微微浮动
            if (_incenseBurner != null)
            {
                float bob = Mathf.Sin(Time.time * 2.1f) * 0.015f;
                float drift = Mathf.Sin(Time.time * 0.6f) * 0.02f;
                _incenseBurner.transform.localPosition = new Vector3(
                    decorationGroupX + drift,
                    decorationGroupY + bob,
                    -0.1f);
            }

            // 花轻轻摇曳
            if (_flowerVase != null && _flowerRenderer != null)
            {
                float sway = Mathf.Sin(Time.time * 1.4f + 0.5f) * 0.012f;
                _flowerRenderer.transform.localRotation = Quaternion.Euler(0, 0, sway * 10f);
                var p = _flowerRenderer.transform.localPosition;
                _flowerRenderer.transform.localPosition = new Vector3(sway, p.y, p.z);
            }

            // 茶宠呼吸感（极微缩放）
            if (_teaPet != null)
            {
                float breath = 1f + Mathf.Sin(Time.time * 0.8f) * 0.003f;
                _teaPet.transform.localScale = new Vector3(breath, breath, 1f);
            }
        }

        // ━━ 季节反应 ━━

        private void OnSeasonChanged(Season season) => ApplySeason(season);

        private void ApplySeason(Season season)
        {
            _currentSeason = season;

            // 花的颜色
            if (_flowerRenderer != null)
            {
                _flowerRenderer.color = season switch
                {
                    Season.Spring => new Color(0.95f, 0.72f, 0.76f), // 桃花粉
                    Season.Summer => new Color(0.91f, 0.84f, 0.52f), // 夏菊黄
                    Season.Autumn => new Color(0.82f, 0.42f, 0.28f), // 秋叶橙
                    Season.Winter => new Color(0.90f, 0.88f, 0.82f), // 冬梅（近白）
                    _ => new Color(0.95f, 0.72f, 0.76f)
                };
            }

            // 烟的颜色随季节微调
            if (_incenseSmoke != null)
            {
                var main = _incenseSmoke.main;
                Color c = smokeTint;
                // 冬天烟更白（冷），秋天烟偏暖
                if (season == Season.Winter) c = new Color(0.90f, 0.91f, 0.93f);
                if (season == Season.Autumn) c = new Color(0.85f, 0.78f, 0.68f);
                main.startColor = new Color(c.r, c.g, c.b, 0.35f);
            }

            // 季节性粒子
            if (_seasonalParticles != null)
                ApplySeasonalParticles(season);
        }

        // ━━ 粒子设置 ━━

        private void SetupIncenseSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                smokeParticleSize * 0.5f, smokeParticleSize * 1.4f);
            main.startColor = new Color(smokeTint.r, smokeTint.g, smokeTint.b, 0.35f);
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.01f, 0.03f);
            main.playOnAwake = true;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.025f;
            shape.length = 0.15f;

            // 速度：缓慢上升 + 横向飘
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);

            // 大小：扩散感
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.3f, 0.9f),
                new Keyframe(0.6f, 1.3f),
                new Keyframe(1f, 0.7f)));

            // Alpha：水墨晕染
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.30f, 0.18f),
                    new GradientAlphaKey(0.32f, 0.45f),
                    new GradientAlphaKey(0.18f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            // 噪声：自然飘动
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            noise.frequency = 0.35f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            noise.octaveCount = 2;
            noise.damping = true;

            // 渲染
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = SortingLayers.Props;
            renderer.sortingOrder = 6;
            renderer.material = FindOrCreateSoftParticleMaterial();
        }

        private void SetupIncenseWisps(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.startColor = new Color(0.88f, 0.86f, 0.82f, 0.22f);
            main.maxParticles = 6;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.playOnAwake = true;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 0.8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.20f, 0.25f),
                    new GradientAlphaKey(0.15f, 0.70f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = SortingLayers.Props;
            renderer.sortingOrder = 7;
            renderer.material = FindOrCreateSoftParticleMaterial();
        }

        private void SetupSeasonalParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.maxParticles = maxSeasonalParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.playOnAwake = false;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f; // 由季节控制
            emission.enabled = true;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 0.1f, 0.1f);
            shape.position = new Vector3(0, 0, 0);

            // 旋转（落叶翻转感）
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-60f, 60f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = SortingLayers.Props;
            renderer.sortingOrder = 1;
            renderer.material = FindOrCreateSoftParticleMaterial();
        }

        private void ApplySeasonalParticles(Season season)
        {
            if (_seasonalParticles == null) return;

            var main = _seasonalParticles.main;
            var emission = _seasonalParticles.emission;

            switch (season)
            {
                case Season.Spring:
                    // 落花
                    main.startColor = new Color(0.95f, 0.72f, 0.78f, 0.7f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                    emission.rateOverTime = 1.8f;
                    _seasonalParticles.Play();
                    break;
                case Season.Summer:
                    // 零星绿叶
                    main.startColor = new Color(0.55f, 0.72f, 0.45f, 0.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                    emission.rateOverTime = 0.8f;
                    _seasonalParticles.Play();
                    break;
                case Season.Autumn:
                    // 红叶
                    main.startColor = new Color(0.82f, 0.45f, 0.28f, 0.75f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
                    emission.rateOverTime = 2.5f;
                    _seasonalParticles.Play();
                    break;
                case Season.Winter:
                    // 雪花
                    main.startColor = new Color(0.95f, 0.96f, 0.98f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                    emission.rateOverTime = 3.5f;
                    _seasonalParticles.Play();
                    break;
            }
        }

        // ━━ 工具 ━━

        private static Material FindOrCreateSoftParticleMaterial()
        {
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
                    var mat = new Material(shader);
                    mat.SetFloat("_Surface", 0); // alpha blend
                    return mat;
                }
            }
            return new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        }

        private static GameObject CreateInkCircle(string name, Transform parent, float diameter,
            Color color, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateInkCircleSprite(diameter);
            sr.color = color;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            sr.transform.localScale = new Vector3(diameter, diameter, 1f);
            return go;
        }

        private static GameObject CreateInkVase(string name, Transform parent,
            Color color, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateInkVaseSprite();
            sr.color = color;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            sr.transform.localScale = new Vector3(0.28f, 0.48f, 1f);
            sr.transform.localPosition = new Vector3(0, 0.12f, -0.05f);
            return go;
        }

        private static Sprite CreateInkCircleSprite(float diameter)
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = 1f - Mathf.Clamp01((dist - radius + 1.5f) / 2f);
                    // 水墨边缘晕染
                    float edge = Mathf.Clamp01((radius - dist + 0.5f) / 1.5f);
                    float ink = edge * 0.3f;
                    tex.SetPixel(x, y, new Color(ink, ink, ink, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateInkVaseSprite()
        {
            int w = 16, h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                float bodyWidth = Mathf.Sin(v * Mathf.PI) * 0.8f + 0.2f;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float dist = Mathf.Abs(u - 0.5f) * 2f;
                    float alpha = Mathf.Clamp01(1f - dist / bodyWidth);
                    // 水墨笔触：中间深边缘浅
                    float ink = Mathf.Lerp(0.1f, 0.4f, alpha);
                    tex.SetPixel(x, y, new Color(ink, ink, ink, alpha * 0.9f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.3f), 32f);
        }

        private static Sprite CreateFlowerSprite()
        {
            int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var center = new Vector2(size / 2f, size / 2f);
            // 五瓣花
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    // 花瓣形状
                    float petal = Mathf.Abs(Mathf.Sin(angle * 2.5f + Mathf.PI));
                    float shape = Mathf.Clamp01(1f - dist / (size * 0.38f)) * petal;
                    float alpha = Mathf.Clamp01(shape * 2f);
                    float core = Mathf.Clamp01(1f - dist / (size * 0.12f));
                    if (core > 0.1f) alpha = core * 0.8f; // 花心稍深
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
