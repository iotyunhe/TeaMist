using UnityEngine;
using System.Collections.Generic;

namespace TeaMist.Core
{
    /// <summary>
    /// NPC 立绘显示/隐藏管理器。
    /// 根据 TeaShopLoop 的访客事件切换 ArtSceneRoot 下的人物 Sprite。
    /// 带淡入淡出动画，替代生硬的 SetActive 闪切。
    /// </summary>
    public class CharacterSpriteManager : MonoBehaviour
    {
        [Header("━━━ Sprite 缓存 ━━━")]
        [Tooltip("人物 Sprite 的父节点（通常是 ArtSceneRoot）")]
        public Transform spriteRoot;

        [Header("━━━ 动画 ━━━")]
        [Range(0.1f, 2f)]
        public float fadeDuration = 0.5f;

        /// <summary>NPC ID → GameObject 名字映射表</summary>
        private static readonly Dictionary<string, string> NpcNameMap = new Dictionary<string, string>
        {
            { "bailu",   "Char_BaiLu" },
            { "zhuqing", "Char_ZhuQing" },
            { "danggui", "Char_DangGui" },
            { "yunhelao","Char_Yunhelao" },
            { "xiaoshan","Char_Xiaoshan" },
            { "qinglan", "Char_QingLan" },
            { "hanlu",   "Char_HanLu" },
            { "qiaoweng","Char_QiaoWeng" },
        };

        private Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        private Dictionary<string, SpriteRenderer> _renderers = new Dictionary<string, SpriteRenderer>();
        private Dictionary<string, Coroutine> _fadeCoroutines = new Dictionary<string, Coroutine>();

        void Start()
        {
            StartCoroutine(InitDeferred());
        }

        private System.Collections.IEnumerator InitDeferred()
        {
            yield return null;

            if (spriteRoot == null)
                spriteRoot = GameObject.Find("ArtSceneRoot")?.transform;

            if (spriteRoot == null)
            {
                Debug.LogWarning("[CharSprite] 找不到 ArtSceneRoot，立绘管理不会生效");
                yield break;
            }

            foreach (var kv in NpcNameMap)
            {
                var child = spriteRoot.Find(kv.Value);
                if (child != null)
                {
                    _cache[kv.Key] = child.gameObject;
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        _renderers[kv.Key] = sr;
                        var c = sr.color;
                        c.a = 0f;
                        sr.color = c;
                    }
                    child.gameObject.SetActive(true);  // 保持 active，用 alpha 控制可见
                }
            }

            var loop = Gameplay.TeaShopLoop.Instance;
            if (loop != null)
            {
                loop.OnCustomerEntered.AddListener(ShowSprite);
                loop.OnCustomerLeft.AddListener(HideSprite);
                Debug.Log("[CharSprite] 已连接 TeaShopLoop 事件（淡入淡出模式）");
            }
        }

        void OnDestroy()
        {
            var loop = Gameplay.TeaShopLoop.Instance;
            if (loop != null)
            {
                loop.OnCustomerEntered.RemoveListener(ShowSprite);
                loop.OnCustomerLeft.RemoveListener(HideSprite);
            }
            // 清理所有协程
            foreach (var coroutine in _fadeCoroutines.Values)
                if (coroutine != null) StopCoroutine(coroutine);
        }

        private void ShowSprite(string npcId)
        {
            if (_renderers.TryGetValue(npcId, out var sr))
            {
                if (_fadeCoroutines.TryGetValue(npcId, out var old) && old != null)
                    StopCoroutine(old);
                _fadeCoroutines[npcId] = StartCoroutine(FadeSprite(sr, 1f, fadeDuration));
                Debug.Log($"[CharSprite] 立绘淡入: {npcId}");
            }
            else
            {
                Debug.LogWarning($"[CharSprite] 未找到 NPC 立绘: {npcId}");
            }
        }

        private void HideSprite(string npcId)
        {
            if (_renderers.TryGetValue(npcId, out var sr))
            {
                if (_fadeCoroutines.TryGetValue(npcId, out var old) && old != null)
                    StopCoroutine(old);
                _fadeCoroutines[npcId] = StartCoroutine(FadeSprite(sr, 0f, fadeDuration * 0.7f));
                Debug.Log($"[CharSprite] 立绘淡出: {npcId}");
            }
        }

        private System.Collections.IEnumerator FadeSprite(SpriteRenderer sr, float targetAlpha, float duration)
        {
            float startAlpha = sr.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // easeOutCubic
                float smooth = 1f - Mathf.Pow(1f - t, 3f);
                var c = sr.color;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, smooth);
                sr.color = c;
                yield return null;
            }
            var final = sr.color;
            final.a = targetAlpha;
            sr.color = final;
        }
    }
}
