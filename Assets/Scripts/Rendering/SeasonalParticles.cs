using UnityEngine;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 四季粒子系统 —— 根据季节切换飘落粒子效果。
    /// 春：樱花瓣 | 夏：萤火虫/流萤 | 秋：落叶 | 冬：雪花
    ///
    /// 挂载到主摄像机或场景根节点，Bootstrap 会自动创建。
    /// </summary>
    public class SeasonalParticles : MonoBehaviour
    {
        [Header("━━━ 粒子预制（不需要 Inspector 设置，代码自动创建）━━━")]
        public ParticleSystem springPetals;
        public ParticleSystem summerFireflies;
        public ParticleSystem autumnLeaves;
        public ParticleSystem winterSnow;

        [Header("━━━ 参数 ━━━")]
        [Range(1, 50)]
        public int maxParticles = 15;
        public float screenCoverage = 1.5f; // 粒子覆盖屏幕倍数

        private ParticleSystem _activeSystem;
        private Gameplay.Season _currentSeason = Gameplay.Season.Spring;

        // 季节颜色
        private static readonly Color SpringPetal = new Color(0.95f, 0.75f, 0.80f, 0.7f);
        private static readonly Color SummerGlow = new Color(0.85f, 0.95f, 0.60f, 0.5f);
        private static readonly Color AutumnLeaf = new Color(0.80f, 0.45f, 0.25f, 0.6f);
        private static readonly Color WinterSnow = new Color(0.90f, 0.92f, 0.95f, 0.55f);

        void Start()
        {
            // 确保四个粒子系统都存在
            EnsureParticleSystems();

            // 从 SeasonManager 读取当前季节
            ApplySeason(Gameplay.SeasonManager.Instance?.CurrentSeason ?? Gameplay.Season.Spring);

            // 监听季节变化
            if (Gameplay.SeasonManager.Instance != null)
            {
                Gameplay.SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
            }
        }

        void OnDestroy()
        {
            if (Gameplay.SeasonManager.Instance != null)
                Gameplay.SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
        }

        private void OnSeasonChanged(Gameplay.Season newSeason)
        {
            ApplySeason(newSeason);
        }

        // ── 公开 API ──

        /// <summary>切换季节粒子</summary>
        public void ApplySeason(Gameplay.Season season)
        {
            _currentSeason = season;
            StopAll();

            switch (season)
            {
                case Gameplay.Season.Spring:
                    _activeSystem = springPetals;
                    break;
                case Gameplay.Season.Summer:
                    _activeSystem = summerFireflies;
                    break;
                case Gameplay.Season.Autumn:
                    _activeSystem = autumnLeaves;
                    break;
                case Gameplay.Season.Winter:
                    _activeSystem = winterSnow;
                    break;
            }

            if (_activeSystem != null)
                _activeSystem.Play();
        }

        public void StopAll()
        {
            springPetals?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            summerFireflies?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            autumnLeaves?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            winterSnow?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ── 内部 ──

        private void EnsureParticleSystems()
        {
            var cam = Camera.main;
            float worldHeight = cam != null ? cam.orthographicSize * 2f : 10f;
            float worldWidth = cam != null ? cam.orthographicSize * 2f * cam.aspect : 16f;

            springPetals = springPetals ?? CreateSystem("SpringPetals",
                SpringPetal, worldWidth, worldHeight, 0.8f, 1.5f, 4f, true);
            summerFireflies = summerFireflies ?? CreateSystem("SummerFireflies",
                SummerGlow, worldWidth, worldHeight, 0.3f, 0.6f, 6f, false);
            autumnLeaves = autumnLeaves ?? CreateSystem("AutumnLeaves",
                AutumnLeaf, worldWidth, worldHeight, 1.2f, 2.5f, 5f, true);
            winterSnow = winterSnow ?? CreateSystem("WinterSnow",
                WinterSnow, worldWidth, worldHeight, 0.5f, 1.0f, 7f, true);
        }

        private ParticleSystem CreateSystem(string name,
            Color color, float worldW, float worldH,
            float minLife, float maxLife, float speed, bool fallDownward)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(2f, 4f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(worldW * screenCoverage, 1f, 1f);
            shape.position = new Vector3(0, worldH * 0.6f, 0);

            // 速度与生命周期变化
            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            if (fallDownward)
            {
                velOverLifetime.y = new ParticleSystem.MinMaxCurve(-speed * 0.3f, -speed * 0.1f);
                velOverLifetime.x = new ParticleSystem.MinMaxCurve(-speed * 0.15f, speed * 0.15f);
                velOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            else
            {
                // 萤火虫：随机飘动
                velOverLifetime.x = new ParticleSystem.MinMaxCurve(-speed * 0.2f, speed * 0.2f);
                velOverLifetime.y = new ParticleSystem.MinMaxCurve(-speed * 0.2f, speed * 0.2f);
                velOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            // 大小随生命周期变化（渐隐）
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // 颜色随生命周期变化（Alpha 渐隐）
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaCurve = new Gradient();
            alphaCurve.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.15f),
                    new GradientAlphaKey(0.7f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaCurve);

            // 不使用纹理，用默认圆形粒子
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // 使用内置粒子材质
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = color;

            return ps;
        }
    }
}
