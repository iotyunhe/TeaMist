using UnityEngine;

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
        public float riseSpeed = 0.6f;
        [Range(0.1f, 3f)]
        public float steamLifetime = 1.8f;

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

        // ── 内部 ──

        private void EnsureSteamSystem()
        {
            var go = new GameObject("TeaSteam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.3f, 0); // 壶口上方

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(steamLifetime * 0.7f, steamLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(riseSpeed * 0.5f, riseSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new Color(0.92f, 0.90f, 0.86f, 0.25f);
            main.maxParticles = maxSteam;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(3f, 6f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.08f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // 上升速度
            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.8f, riseSpeed);
            velOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 大小：从小到大再变小（蒸汽扩散消散）
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.3f, 1f),
                new Keyframe(0.7f, 1.2f),
                new Keyframe(1f, 0.6f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Alpha：渐入渐出
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.35f, 0.2f),
                    new GradientAlphaKey(0.35f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);

            // 噪声模块：让蒸汽有自然飘动感
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            steamParticles = ps;
        }

        void OnDestroy()
        {
            if (steamParticles != null)
                Destroy(steamParticles.gameObject);
        }
    }
}
