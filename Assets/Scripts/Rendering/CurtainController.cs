using UnityEngine;
using System.Collections.Generic;
using TeaMist.Core;

namespace TeaMist.Rendering
{
    /// <summary>
    /// 竹帘控制器 —— 前景层竹帘/卷帘。
    /// 茶馆开门时卷起，打烊时放下，配平滑动画。
    /// 挂在 ArtSceneRoot 下，单例职责。
    /// </summary>
    public class CurtainController : MonoBehaviour
    {
        [Header("━━━ 帘子参数 ━━━")]
        [SerializeField] private int slatCount = 16;
        [SerializeField] private float slatWidth = 0.28f;
        [SerializeField] private float slatHeight = 0.06f;
        [SerializeField] private float slatSpacing = 0.16f;
        [SerializeField] private float curtainWidth = 12f;
        [SerializeField] private float rollUpDuration = 1.4f;
        [SerializeField] private float rollDownDuration = 0.9f;

        [Header("━━━ 水墨感 ━━━")]
        [SerializeField] private Color bambooDark = new Color(0.32f, 0.26f, 0.14f);
        [SerializeField] private Color bambooLight = new Color(0.48f, 0.40f, 0.22f);
        [SerializeField] private Color stringColor = new Color(0.25f, 0.20f, 0.12f, 0.7f);

        [Header("━━━ 摇晃 ━━━")]
        [SerializeField] private float swaySpeed = 0.4f;
        [SerializeField] private float swayAmount = 0.025f;
        [SerializeField] private float swayVariation = 0.015f;

        // 内部
        private Transform[] _slatTransforms;
        private SpriteRenderer[] _slatFront;
        private SpriteRenderer[] _slatBack; // 背面微暗层
        private float[] _slatTargetY;
        private float _rollPosition = 0f; // 0=放下, 1=完全卷起
        private float _rollTarget = 0f;
        private float _rollVelocity = 0f;

        public bool IsOpen => _rollPosition > 0.95f;
        public bool IsClosed => _rollPosition < 0.05f;
        public float RollPosition => _rollPosition;

        void Awake()
        {
            BuildCurtain();
        }

        void Update()
        {
            // 平滑过渡到目标 roll 位置 (spring-damper)
            float dur = _rollTarget > _rollPosition ? rollUpDuration : rollDownDuration;
            _rollPosition = Mathf.SmoothDamp(_rollPosition, _rollTarget,
                ref _rollVelocity, dur * 0.35f);

            ApplyRollPosition();
            ApplySway();
        }

        // ━━ 公共 API ━━

        /// <summary>卷起帘子（开门）</summary>
        public void RollUp()
        {
            _rollTarget = 1f;
        }

        /// <summary>放下帘子（关门）</summary>
        public void RollDown()
        {
            _rollTarget = 0f;
        }

        /// <summary>立即设置帘子位置（无动画）</summary>
        public void SetOpen(bool open)
        {
            _rollTarget = open ? 1f : 0f;
            _rollPosition = _rollTarget;
            _rollVelocity = 0f;
            ApplyRollPosition();
        }

        // ━━ 构建 ━━

        private void BuildCurtain()
        {
            _slatTransforms = new Transform[slatCount];
            _slatFront = new SpriteRenderer[slatCount];
            _slatBack = new SpriteRenderer[slatCount];
            _slatTargetY = new float[slatCount];

            float totalHeight = (slatCount - 1) * slatSpacing + slatHeight;
            float startY = totalHeight * 0.5f;

            // 生成竹片 Sprite（带竖向渐变）
            var bambooSprite = CreateBambooSlatSprite();

            for (int i = 0; i < slatCount; i++)
            {
                // 每根竹片是一个 GO + 2 个 SpriteRenderer（正面+背面阴影）
                var slatGO = new GameObject($"Curtain_Slat_{i:D2}");
                slatGO.transform.SetParent(transform, false);
                slatGO.transform.localPosition = new Vector3(0, startY - i * slatSpacing, -0.05f * i);

                // 正面
                _slatFront[i] = AddSlatRenderer(slatGO, $"Front", bambooSprite,
                    SortingLayers.Foreground, 10 + i, bambooLight);
                _slatFront[i].transform.localPosition = new Vector3(0, 0, -0.01f);

                // 背面（微暗，制造厚度感）
                _slatBack[i] = AddSlatRenderer(slatGO, $"Back", bambooSprite,
                    SortingLayers.Foreground, 9 + i, bambooDark);
                _slatBack[i].transform.localPosition = new Vector3(0, 0, 0.01f);
                var c = _slatBack[i].color;
                c.a = 0.45f;
                _slatBack[i].color = c;

                _slatTransforms[i] = slatGO.transform;
                _slatTargetY[i] = startY - i * slatSpacing;
            }

            // 垂绳（左、中、右）
            AddString(-curtainWidth * 0.48f, totalHeight, "Left");
            AddString(0f, totalHeight, "Mid");
            AddString(curtainWidth * 0.48f, totalHeight, "Right");
        }

        private SpriteRenderer AddSlatRenderer(GameObject parent, string name, Sprite sprite,
            string layer, int order, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            sr.color = col;
            return sr;
        }

        private void AddString(float x, float totalHeight, string label)
        {
            var go = new GameObject($"Curtain_String_{label}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateStringSprite();
            sr.sortingLayerName = SortingLayers.Foreground;
            sr.sortingOrder = 8;
            sr.color = stringColor;
            float ppu = 100f;
            sr.transform.localScale = new Vector3(0.03f * ppu, totalHeight * ppu, 1f);
            sr.transform.localPosition = new Vector3(x, 0, 0.02f);
        }

        // ━━ 应用卷起位置 ━━

        private void ApplyRollPosition()
        {
            if (_slatTransforms == null) return;

            float gatherYBase = _slatTargetY[_slatTransforms.Length - 1];

            for (int i = 0; i < _slatTransforms.Length; i++)
            {
                var t = _slatTransforms[i];
                if (t == null) continue;

                // 卷起时：竹片向顶部聚集 + 轻微旋转
                float gatherY = _slatTargetY[i] * (1f - _rollPosition);
                float rollUpY = Mathf.Lerp(gatherYBase, gatherYBase + 5f, _rollPosition);
                float newY = gatherYBase + _rollPosition * 5f + gatherY;

                t.localPosition = new Vector3(
                    t.localPosition.x,
                    newY,
                    t.localPosition.z);

                // 卷起时旋转（竹片卷在杆上）
                float rollRot = _rollPosition * 180f * (1f - (float)i / _slatTransforms.Length * 0.6f);
                t.localRotation = Quaternion.Euler(0, 0, rollRot);

                // Alpha 递减
                float alpha = Mathf.Lerp(1f, 0f,
                    _rollPosition * (1f - (float)i / _slatTransforms.Length * 0.4f));
                SetAlpha(_slatFront[i], alpha);
                SetAlpha(_slatBack[i], alpha * 0.45f);
            }
        }

        private void ApplySway()
        {
            if (_slatTransforms == null) return;

            float t = Time.time * swaySpeed;
            float visibleFactor = 1f - _rollPosition; // 卷起时不晃

            for (int i = 0; i < _slatTransforms.Length; i++)
            {
                var tf = _slatTransforms[i];
                if (tf == null) continue;

                // 每根竹片有独立相位
                float phase = i * 0.7f;
                float xSway = Mathf.Sin(t + phase) * swayAmount * visibleFactor;
                float rotSway = Mathf.Sin(t * 0.8f + phase * 1.3f) * 1.2f * visibleFactor;

                // 应用位移（保留 roll 动画的 Y）
                var pos = tf.localPosition;
                tf.localPosition = new Vector3(xSway, pos.y, pos.z);

                // 叠加摇晃旋转（保留卷起旋转）
                float rollRot = _rollPosition * 180f * (1f - (float)i / _slatTransforms.Length * 0.6f);
                tf.localRotation = Quaternion.Euler(0, 0, rollRot + rotSway);
            }
        }

        private static void SetAlpha(SpriteRenderer sr, float a)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = Mathf.Clamp01(a);
            sr.color = c;
        }

        // ━━ 程序化 Sprite 生成 ━━

        /// <summary>
        /// 生成带竖向渐变的竹片 Sprite（水墨笔触感）
        /// </summary>
        private static Sprite CreateBambooSlatSprite()
        {
            int w = 8;
            int h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1); // 0→1 从下到上
                // 中间亮、边缘暗（圆柱感）
                float center = 1f - Mathf.Abs(v - 0.5f) * 2f;
                float brightness = Mathf.Lerp(0.85f, 1.0f, center);
                // 加一点随机墨点（水墨感）
                float noise = (Mathf.Sin(y * 7.1f + w * 3.3f) * 0.5f + 0.5f) * 0.08f;
                brightness -= noise;

                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    // 横向也加一点微妙变化
                    float uBright = 1f - Mathf.Abs(u - 0.5f) * 0.3f;
                    tex.SetPixel(x, y, new Color(uBright * brightness, uBright * brightness, uBright * brightness, 1f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateStringSprite()
        {
            int w = 2;
            int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                float alpha = Mathf.Lerp(0.9f, 0.4f, Mathf.Sin(v * Mathf.PI));
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
