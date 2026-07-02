using UnityEngine;
using System.Collections.Generic;
using TeaMist.Rendering;

namespace TeaMist.Gameplay
{
    /// <summary>
    /// 茶馆场景控制器 —— 管理茶馆背景层、氛围特效、客人入座点。
    /// 场景分为"远山 → 中景竹林 → 近景茶馆"三层，每层独立响应季节和天气。
    /// </summary>
    public class TeaHouseSceneController : MonoBehaviour
    {
        public static TeaHouseSceneController Instance { get; private set; }

        [Header("━━━ 场景层（远→中→近）━━━")]
        [Tooltip("远山：最远的山峦轮廓")]
        public SpriteRenderer farMountainRenderer;
        [Tooltip("中景：竹林/树木/溪流")]
        public SpriteRenderer midForestRenderer;
        [Tooltip("近景：茶馆建筑本体")]
        public SpriteRenderer nearShopRenderer;
        [Tooltip("前景：门框/屋檐/挂帘")]
        public SpriteRenderer foreGroundRenderer;

        [Header("━━━ 氛围组件 ━━━")]
        [Tooltip("竹帘卷帘控制器（运行时由 SceneAutoSetup 赋值）")]
        public Rendering.CurtainController curtainController;

        [Header("━━━ 茶馆内部坐席点 ━━━")]
        [Tooltip("客人入座位置（世界坐标）")]
        public Transform[] seatPositions = new Transform[7];

        [Header("━━━ 水墨特效 ━━━")]
        [Tooltip("茶烟粒子系统")]
        public ParticleSystem teaSmokeParticles;
        [Tooltip("飘落物（花瓣/落叶/雪花）")]
        public ParticleSystem fallingParticles;
        [Tooltip("灯笼/烛光")]
        public SpriteRenderer[] lanterns;

        [Header("━━━ 状态 ━━━")]
        [SerializeField] private bool shopIsOpen = true;
        [SerializeField] private bool[] seatOccupied;

        // -- 各层原始颜色（用于季节着色）--
        private Color[] layerBaseColors;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            seatOccupied = new bool[seatPositions.Length];
            CacheBaseColors();
        }

        void Start()
        {
            if (SeasonManager.Instance != null)
            {
                SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
                SeasonManager.Instance.OnWeatherChanged += OnWeatherChanged;
                SeasonManager.Instance.OnColorsUpdated += OnColorsUpdated;

                // 初始应用当前季节
                ApplySeasonVisuals(SeasonManager.Instance.CurrentSeason);
            }
        }

        void OnDestroy()
        {
            if (SeasonManager.Instance != null)
            {
                SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
                SeasonManager.Instance.OnWeatherChanged -= OnWeatherChanged;
                SeasonManager.Instance.OnColorsUpdated -= OnColorsUpdated;
            }
        }

        // ━━━ 季节视觉 ━━━

        private void CacheBaseColors()
        {
            layerBaseColors = new Color[4];
            if (farMountainRenderer != null) layerBaseColors[0] = farMountainRenderer.color;
            if (midForestRenderer != null) layerBaseColors[1] = midForestRenderer.color;
            if (nearShopRenderer != null) layerBaseColors[2] = nearShopRenderer.color;
            if (foreGroundRenderer != null) layerBaseColors[3] = foreGroundRenderer.color;
        }

        private void OnSeasonChanged(Season season)
        {
            ApplySeasonVisuals(season);
        }

        private void OnWeatherChanged(Weather weather)
        {
            ApplyWeatherVisuals(weather);
        }

        private void OnColorsUpdated(SeasonColorSet colors)
        {
            // 实时混合过渡色到远山（远山受季节叠色影响最大）
            if (farMountainRenderer != null)
            {
                farMountainRenderer.color = Color.Lerp(layerBaseColors[0], colors.seasonTint, 0.4f);
            }
        }

        private void ApplySeasonVisuals(Season season)
        {
            // 控制飘落物类型
            if (fallingParticles != null)
            {
                var main = fallingParticles.main;
                switch (season)
                {
                    case Season.Spring: main.startColor = new Color(0.95f, 0.80f, 0.82f); break; // 花瓣粉
                    case Season.Summer: main.startColor = new Color(0.65f, 0.80f, 0.45f); break; // 绿叶
                    case Season.Autumn: main.startColor = new Color(0.85f, 0.55f, 0.30f); break; // 落叶橙
                    case Season.Winter: main.startColor = new Color(0.92f, 0.94f, 0.97f); break; // 雪花白
                    default:            main.startColor = Color.white;              break;
                }
            }

            // 灯笼色温
            float lanternTemp = season == Season.Winter ? 0.85f :
                                season == Season.Autumn ? 0.90f :
                                season == Season.Summer ? 0.65f : 0.75f;
            if (lanterns != null)
            {
                foreach (var l in lanterns)
                {
                    if (l != null)
                        l.color = new Color(1f, lanternTemp, lanternTemp * 0.5f, 1f);
                }
            }
        }

        private void ApplyWeatherVisuals(Weather weather)
        {
            // 茶烟浓度随天气变化
            if (teaSmokeParticles != null)
            {
                var emission = teaSmokeParticles.emission;
                switch (weather)
                {
                    case Weather.Mist:  emission.rateOverTime = 8f; break;
                    case Weather.Rain:  emission.rateOverTime = 5f; break;
                    case Weather.Snow:  emission.rateOverTime = 6f; break;
                    case Weather.Storm: emission.rateOverTime = 2f; break;
                    default:            emission.rateOverTime = 3f; break;
                }
            }

            // 飘落物在风雪天加强
            if (fallingParticles != null)
            {
                var emission = fallingParticles.emission;
                emission.rateOverTime = weather == Weather.Snow ? 12f :
                                        weather == Weather.Storm ? 15f :
                                        weather == Weather.Mist ? 2f : 4f;
            }
        }

        // ━━━ 茶馆营业 ━━━

        public bool IsOpen() => shopIsOpen;

        public void OpenShop()
        {
            if (shopIsOpen) return;
            shopIsOpen = true;
            StartCoroutine(TransitionOpen());
        }

        private System.Collections.IEnumerator TransitionOpen()
        {
            // 帘幕拉开：场景亮度从暗到明
            float dur = 1.2f;

            // 先触发竹帘卷起
            if (curtainController != null)
            {
                curtainController.RollUp();
                // 等帘子卷到一半再开始亮度渐变
                yield return new WaitForSeconds(0.3f);
            }

            float elapsed = 0f;

            // 收集所有场景 SpriteRenderer
            var renderers = new List<SpriteRenderer>();
            if (farMountainRenderer != null) renderers.Add(farMountainRenderer);
            if (midForestRenderer != null) renderers.Add(midForestRenderer);
            if (nearShopRenderer != null) renderers.Add(nearShopRenderer);
            if (foreGroundRenderer != null) renderers.Add(foreGroundRenderer);

            // 保存原始颜色并设暗
            var originalColors = new Color[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                originalColors[i] = renderers[i].color;
                var dark = renderers[i].color;
                dark.r *= 0.4f; dark.g *= 0.4f; dark.b *= 0.4f;
                renderers[i].color = dark;
            }

            // 逐渐恢复
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float smooth = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic
                for (int i = 0; i < renderers.Count; i++)
                {
                    renderers[i].color = Color.Lerp(
                        new Color(originalColors[i].r * 0.4f, originalColors[i].g * 0.4f, originalColors[i].b * 0.4f, originalColors[i].a),
                        originalColors[i], smooth);
                }
                yield return null;
            }

            // 亮灯
            if (lanterns != null)
            {
                foreach (var l in lanterns) if (l != null) l.enabled = true;
            }

            Debug.Log("[TeaHouse] 茶馆开门 — 帘幕已拉开");
        }

        public void CloseShop()
        {
            if (!shopIsOpen) return;
            shopIsOpen = false;
            StartCoroutine(TransitionClose());
        }

        private System.Collections.IEnumerator TransitionClose()
        {
            // 帘幕落下：场景亮度从明到暗
            float dur = 0.8f;

            // 触发竹帘放下
            if (curtainController != null)
                curtainController.RollDown();

            float elapsed = 0f;

            var renderers = new List<SpriteRenderer>();
            if (farMountainRenderer != null) renderers.Add(farMountainRenderer);
            if (midForestRenderer != null) renderers.Add(midForestRenderer);
            if (nearShopRenderer != null) renderers.Add(nearShopRenderer);
            if (foreGroundRenderer != null) renderers.Add(foreGroundRenderer);

            var originalColors = new Color[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
                originalColors[i] = renderers[i].color;

            // 灭灯
            if (lanterns != null)
            {
                foreach (var l in lanterns) if (l != null) l.enabled = false;
            }

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float smooth = t * t * t; // easeInCubic
                for (int i = 0; i < renderers.Count; i++)
                {
                    var dark = originalColors[i];
                    dark.r *= 0.3f; dark.g *= 0.3f; dark.b *= 0.3f;
                    renderers[i].color = Color.Lerp(originalColors[i], dark, smooth);
                }
                yield return null;
            }

            Debug.Log("[TeaHouse] 茶馆打烊 — 夜深了");
        }

        // ━━━ 坐席管理 ━━━

        /// <summary>
        /// 获取第一个空闲坐席的位置，返回 -1 表示已满
        /// </summary>
        public int GetAvailableSeat()
        {
            for (int i = 0; i < seatOccupied.Length; i++)
            {
                if (!seatOccupied[i]) return i;
            }
            return -1;
        }

        public Vector3 GetSeatPosition(int index)
        {
            if (index < 0 || index >= seatPositions.Length || seatPositions[index] == null)
                return Vector3.zero;
            return seatPositions[index].position;
        }

        public void OccupySeat(int index)
        {
            if (index >= 0 && index < seatOccupied.Length)
                seatOccupied[index] = true;
        }

        public void FreeSeat(int index)
        {
            if (index >= 0 && index < seatOccupied.Length)
                seatOccupied[index] = false;
        }

        public int OccupiedSeatCount
        {
            get
            {
                int count = 0;
                foreach (bool o in seatOccupied) if (o) count++;
                return count;
            }
        }
    }
}
