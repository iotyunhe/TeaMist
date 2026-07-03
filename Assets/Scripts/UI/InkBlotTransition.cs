using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

namespace TeaMist.UI
{
    /// <summary>
    /// 墨滴扩散转场控制器。
    /// 挂载在 Canvas 下的全屏 Image 上，通过 Material 的 _Radius 参数驱动墨滴扩散动画。
    /// 
    /// 自动监听 TeaShopLoop.OnCustomerEntered/OnCustomerLeft 事件播放转场。
    /// </summary>
    public class InkBlotTransition : MonoBehaviour
    {
        [Header("━━━ 材质 ━━━")]
        [SerializeField] private Material _transitionMaterial;
        [SerializeField] private Image _overlayImage;

        [Header("━━━ 参数 ━━━")]
        [SerializeField] private float _forwardDuration = 1.2f;
        [SerializeField] private float _reverseDuration = 1.0f;
        [SerializeField] private float _holdDuration   = 0.4f;
        [SerializeField] private AnimationCurve _forwardCurve;
        [SerializeField] private AnimationCurve _reverseCurve;

        [Header("━━━ 墨滴落点 ━━━")]
        [SerializeField] private Vector2 _defaultCenter = new Vector2(0.5f, 0.5f);

        [Header("━━━ 事件 ━━━")]
        public UnityEvent OnForwardComplete;
        public UnityEvent OnReverseComplete;
        public UnityEvent OnFullCycleMidpoint;   // 全屏覆盖时触发（换景时机）

        private Material _runtimeMat;
        private Coroutine _activeRoutine;

        private static readonly int PropRadius    = Shader.PropertyToID("_Radius");
        private static readonly int PropCenter    = Shader.PropertyToID("_Center");
        private static readonly int PropOpacity   = Shader.PropertyToID("_Opacity");
        private static readonly int PropTimeParam = Shader.PropertyToID("_TimeParam");
        private static readonly int PropInkDeep   = Shader.PropertyToID("_InkDeep");
        private static readonly int PropInkWash   = Shader.PropertyToID("_InkWash");
        private static readonly int PropTendril   = Shader.PropertyToID("_Tendril");

        void Start()
        {
            if (_overlayImage == null)
                _overlayImage = GetComponent<Image>();

            if (_overlayImage != null && _overlayImage.material != null)
            {
                _runtimeMat = _overlayImage.material; // Bootstrap 已创建实例
            }
            else if (_overlayImage != null && _transitionMaterial != null)
            {
                _runtimeMat = new Material(_transitionMaterial);
                _overlayImage.material = _runtimeMat;
            }

            // shader 编译检查
            if (_runtimeMat != null && !_runtimeMat.shader.isSupported)
            {
                Debug.LogError("[InkBlotTransition] Shader 编译失败！回退到默认 UI 材质");
                _runtimeMat = null;
                if (_overlayImage != null)
                    _overlayImage.material = null; // 用默认 UI material
            }

            if (_overlayImage != null)
                _overlayImage.raycastTarget = false;

            // 缓动曲线默认值
            if (_forwardCurve == null || _forwardCurve.length == 0)
                _forwardCurve = CreateSpreadCurve();
            if (_reverseCurve == null || _reverseCurve.length == 0)
                _reverseCurve = CreateRecedeCurve();

            // 墨色默认值（仅在材质有效时设置）
            if (_runtimeMat != null)
            {
                _runtimeMat.SetColor(PropInkDeep, new Color(0.05f, 0.04f, 0.03f, 1f));
                _runtimeMat.SetColor(PropInkWash, new Color(0.18f, 0.16f, 0.13f, 1f));
                _runtimeMat.SetFloat(PropTendril, 0.18f);
            }

            // 初始状态
            SetRadius(0);
            SetOpacity(0);
            if (_overlayImage != null)
                _overlayImage.enabled = false;

            // 延迟连接 TeaShopLoop 事件
            StartCoroutine(InitDeferred());
        }

        private IEnumerator InitDeferred()
        {
            yield return null;

            var loop = Gameplay.TeaShopLoop.Instance;
            if (loop != null)
            {
                loop.OnCustomerEntered.AddListener(OnCustomerArrive);
                loop.OnCustomerLeft.AddListener(OnCustomerDepart);
                Debug.Log("[InkBlotTransition] 已连接 TeaShopLoop 事件");
            }
        }

        private void OnDestroy()
        {
            var loop = Gameplay.TeaShopLoop.Instance;
            if (loop != null)
            {
                loop.OnCustomerEntered.RemoveListener(OnCustomerArrive);
                loop.OnCustomerLeft.RemoveListener(OnCustomerDepart);
            }

            if (_runtimeMat != null)
                Destroy(_runtimeMat);
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);
        }

        // ━━━ 公开 API ━━━

        /// <summary>NPC 到访：播放墨滴扩散→揭示转场</summary>
        public void OnCustomerArrive(string npcId)
        {
            // 覆盖 → 揭示（NPC 立绘在揭开时淡入，由 CharacterSpriteManager 处理）
            PlayFullCycle(midpointCallback: null,
                          forwardDur: _forwardDuration * 0.7f,
                          reverseDur: _reverseDuration,
                          center: GetNPCCenter(npcId));
        }

        /// <summary>NPC 离开：播放覆盖→收敛转场</summary>
        public void OnCustomerDepart(string npcId)
        {
            PlayFullCycle(midpointCallback: null, center: GetNPCCenter(npcId));
        }

        /// <summary>墨滴从中心扩散覆盖全屏</summary>
        public void PlayForward(float? duration = null, Vector2? center = null)
        {
            StopActive();
            _activeRoutine = StartCoroutine(AnimateForward(duration ?? _forwardDuration, center ?? _defaultCenter));
        }

        /// <summary>墨滴从全屏收敛回中心</summary>
        public void PlayReverse(float? duration = null, Vector2? center = null)
        {
            StopActive();
            _activeRoutine = StartCoroutine(AnimateReverse(duration ?? _reverseDuration, center ?? _defaultCenter));
        }

        /// <summary>
        /// 完整过渡周期：覆盖 → [中点回调] → 揭开。
        /// 在 midpointCallback 中切换场景内容（例如 NPC 立绘显隐）。
        /// </summary>
        public void PlayFullCycle(System.Action midpointCallback = null,
                                   float? forwardDur = null, float? reverseDur = null,
                                   Vector2? center = null)
        {
            StopActive();
            _activeRoutine = StartCoroutine(AnimateFullCycle(
                forwardDur ?? _forwardDuration,
                reverseDur ?? _reverseDuration,
                center ?? _defaultCenter,
                midpointCallback));
        }

        /// <summary>立即跳到指定半径（无动画）</summary>
        public void SetRadius(float r)
        {
            if (_runtimeMat != null)
                _runtimeMat.SetFloat(PropRadius, r);
        }

        public void SetOpacity(float alpha)
        {
            if (_runtimeMat != null)
                _runtimeMat.SetFloat(PropOpacity, alpha);
        }

        // ━━━ 动画协程 ━━━

        private IEnumerator AnimateForward(float duration, Vector2 center)
        {
            if (_runtimeMat == null) yield break;
            PrepareOverlay(center);

            float t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float curve = _forwardCurve.Evaluate(Mathf.Clamp01(t));
                _runtimeMat.SetFloat(PropRadius, Mathf.Lerp(0, 1.5f, curve));
                _runtimeMat.SetFloat(PropTimeParam, Time.unscaledTime);
                yield return null;
            }

            _runtimeMat.SetFloat(PropRadius, 1.5f);
            OnForwardComplete?.Invoke();
        }

        private IEnumerator AnimateReverse(float duration, Vector2 center)
        {
            if (_runtimeMat == null) yield break;
            _runtimeMat.SetVector(PropCenter, new Vector4(center.x, center.y, 0, 0));
            EnsureOverlayVisible();

            float t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float curve = _reverseCurve.Evaluate(Mathf.Clamp01(t));
                _runtimeMat.SetFloat(PropRadius, Mathf.Lerp(1.5f, 0, curve));
                _runtimeMat.SetFloat(PropTimeParam, Time.unscaledTime);
                yield return null;
            }

            SetRadius(0);
            HideOverlay();
            OnReverseComplete?.Invoke();
        }

        private IEnumerator AnimateFullCycle(float fwdDur, float revDur, Vector2 center,
                                              System.Action midpointCb)
        {
            if (_runtimeMat == null) yield break;
            // Forward
            PrepareOverlay(center);

            float t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / fwdDur;
                float curve = _forwardCurve.Evaluate(Mathf.Clamp01(t));
                _runtimeMat.SetFloat(PropRadius, Mathf.Lerp(0, 1.5f, curve));
                _runtimeMat.SetFloat(PropTimeParam, Time.unscaledTime);
                yield return null;
            }

            _runtimeMat.SetFloat(PropRadius, 1.5f);
            OnForwardComplete?.Invoke();
            OnFullCycleMidpoint?.Invoke();

            // 中点：执行换景回调
            midpointCb?.Invoke();

            // 短暂停留
            if (_holdDuration > 0)
                yield return new WaitForSecondsRealtime(_holdDuration);

            // Reverse
            t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / revDur;
                float curve = _reverseCurve.Evaluate(Mathf.Clamp01(t));
                _runtimeMat.SetFloat(PropRadius, Mathf.Lerp(1.5f, 0, curve));
                _runtimeMat.SetFloat(PropTimeParam, Time.unscaledTime);
                yield return null;
            }

            SetRadius(0);
            HideOverlay();
            OnReverseComplete?.Invoke();
        }

        // ━━━ 内部工具 ━━━

        private void PrepareOverlay(Vector2 center)
        {
            if (_runtimeMat == null) return;
            _runtimeMat.SetVector(PropCenter, new Vector4(center.x, center.y, 0, 0));
            _runtimeMat.SetFloat(PropRadius, 0);
            _runtimeMat.SetFloat(PropOpacity, 1);
            if (_overlayImage != null)
                _overlayImage.enabled = true;
        }

        private void EnsureOverlayVisible()
        {
            if (_overlayImage != null)
                _overlayImage.enabled = true;
            SetOpacity(1);
        }

        private void HideOverlay()
        {
            if (_overlayImage != null)
                _overlayImage.enabled = false;
            SetRadius(0);
            SetOpacity(0);
        }

        // ━━━ 缓动曲线工厂 ━━━

        /// <summary>
        /// 墨滴扩散曲线：墨笔点下→墨珠形成→缓慢洇开→加速渗透→铺满
        /// 从一开始就可见，避免前半段"什么都不发生"的突变感
        /// </summary>
        private static AnimationCurve CreateSpreadCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f,    0f,    0f,   0.5f));   // 墨笔点下
            curve.AddKey(new Keyframe(0.15f, 0.04f, 0.5f, 0.8f));   // 墨珠形成
            curve.AddKey(new Keyframe(0.4f,  0.15f, 0.8f, 1.5f));   // 开始洇开
            curve.AddKey(new Keyframe(0.75f, 0.55f, 1.5f, 2.5f));   // 加速渗透
            curve.AddKey(new Keyframe(1f,    1f,    2.5f, 0f));     // 铺满
            return curve;
        }

        /// <summary>
        /// 墨滴收敛曲线：边缘先退→主体收回→接近中心→最后一丝消散
        /// 先慢后快再慢，像墨被纸吸回笔尖
        /// </summary>
        private static AnimationCurve CreateRecedeCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f,    0f,    0f,   1.0f));   // 边缘开始退
            curve.AddKey(new Keyframe(0.2f,  0.15f, 1.0f, 1.8f));   // 加速退散
            curve.AddKey(new Keyframe(0.6f,  0.75f, 1.8f, 0.8f));   // 主体收回
            curve.AddKey(new Keyframe(0.9f,  0.95f, 0.8f, 0.2f));   // 接近干净
            curve.AddKey(new Keyframe(1f,    1f,    0.2f, 0f));     // 完全消散
            return curve;
        }

        private void StopActive()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
        }

        /// <summary>根据 NPC 决定墨滴滴落位置</summary>
        private static Vector2 GetNPCCenter(string npcId)
        {
            switch (npcId)
            {
                case "bailu":     return new Vector2(0.55f, 0.40f); // 白露 — 偏右（她从右方来）
                case "zhuqing":   return new Vector2(0.45f, 0.42f); // 竹青 — 偏左
                case "danggui":   return new Vector2(0.65f, 0.45f); // 当归 — 右侧
                case "yunhelao":  return new Vector2(0.30f, 0.35f); // 云鹤老 — 远处左侧
                case "xiaoshan":  return new Vector2(0.55f, 0.75f); // 小山 — 地面位
                case "qinglan":   return new Vector2(0.40f, 0.45f); // 青岚 — 偏左
                case "hanlu":     return new Vector2(0.50f, 0.40f); // 寒露 — 中央
                case "qiaoweng":  return new Vector2(0.35f, 0.50f); // 樵翁 — 左侧远处
                case "moyan":     return new Vector2(0.50f, 0.45f); // 墨砚
                case "shuangjiang": return new Vector2(0.50f, 0.42f); // 霜降
                case "qichi":     return new Vector2(0.50f, 0.42f); // 栖迟
                default:          return new Vector2(0.5f, 0.45f);
            }
        }

        // ━━━ Editor 预览 ━━━

#if UNITY_EDITOR
        [ContextMenu("预览：Forward")]
        private void PreviewForward() => PlayForward();

        [ContextMenu("预览：Reverse")]
        private void PreviewReverse() => PlayReverse();

        [ContextMenu("预览：Full Cycle")]
        private void PreviewFullCycle() => PlayFullCycle();
#endif
    }
}
