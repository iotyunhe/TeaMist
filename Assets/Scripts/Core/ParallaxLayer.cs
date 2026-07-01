using UnityEngine;

namespace TeaMist.Core
{
    /// <summary>
    /// 视差滚动层 —— 根据相机位移，以不同速率移动，产生景深效果。
    /// 
    /// 用法：挂在场景 Sprite GameObject 上，设置 parallaxFactor：
    ///   0.0 = 固定背景（不动）
    ///   0.5 = 中景（半速跟随）
    ///   1.0 = 前景（完全跟随相机）
    ///   1.2 = 极近景（比相机还快，产生强烈纵深）
    /// </summary>
    [ExecuteAlways]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("视差系数。0=固定不动，1=完全跟随相机，>1=比相机更快")]
        [Range(0f, 2f)]
        public float parallaxFactor = 0.5f;

        [Tooltip("是否限制 X 轴视差")]
        public bool lockX;

        [Tooltip("是否限制 Y 轴视差")]
        public bool lockY;

        private Vector3 _lastCamPos;
        private Transform _camTransform;
        private Vector3 _initialOffset;
        private Vector3 _offsetVec = Vector3.zero;  // 复用避免 GC 分配

        void Start()
        {
            CacheCamera();
        }

        void LateUpdate()
        {
            if (_camTransform == null) return;

            Vector3 delta = _camTransform.position - _lastCamPos;
            _offsetVec.x = lockX ? 0f : delta.x * parallaxFactor;
            _offsetVec.y = lockY ? 0f : delta.y * parallaxFactor;
            _offsetVec.z = 0f;

            transform.position += _offsetVec;
            _lastCamPos = _camTransform.position;
        }

        /// <summary>重置初始偏移（场景重设后调用）</summary>
        public void ResetOffset()
        {
            CacheCamera();
        }

        private void CacheCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                _camTransform = cam.transform;
                _lastCamPos = _camTransform.position;
                _initialOffset = transform.position - _camTransform.position;
            }
        }
    }
}
