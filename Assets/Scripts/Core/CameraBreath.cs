using UnityEngine;

namespace TeaMist.Core
{
    /// <summary>
    /// 相机呼吸动画 —— 极其缓慢的平移，模拟旁观者的目光游移。
    /// 配合 ParallaxLayer 产生自然的景深感。
    /// </summary>
    public class CameraBreath : MonoBehaviour
    {
        [Tooltip("呼吸幅度（世界单位）")]
        public float amplitude = 0.1f;

        [Tooltip("完整呼吸周期（秒）")]
        public float period = 10f;

        [Tooltip("呼吸方向偏好")]
        public Vector2 direction = new Vector2(0.6f, 0.4f);

        private Vector3 _startPos;
        private Vector3 _offset = Vector3.zero;  // 复用避免 GC 分配

        void Start()
        {
            _startPos = transform.position;
        }

        void Update()
        {
            float t = Mathf.Sin(Time.time * 2f * Mathf.PI / period);
            _offset.x = direction.x * amplitude * t;
            _offset.y = direction.y * amplitude * t * 0.5f;
            _offset.z = 0f;
            transform.position = _startPos + _offset;
        }
    }
}
