using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TeaMist.Core
{
    /// <summary>
    /// 运行时美术加载器 — 从 Assets/Art/Generated/ 加载 PNG，创建 Sprite
    /// 通过文件名关键词匹配（茶馆/远山/茶壶/白露/竹青/宣纸）
    /// </summary>
    public static class ArtLoader
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static bool _loaded;
        private static string _basePath;

        /// <summary>已加载的 Sprite 数量</summary>
        public static int LoadedCount => _cache.Count;

        /// <summary>
        /// 一次性加载所有 Generated 目录下的 PNG
        /// </summary>
        public static void LoadAll()
        {
            if (_loaded) return;

            _basePath = Path.Combine(Application.dataPath, "Art", "Generated");
            if (!Directory.Exists(_basePath))
            {
                Debug.LogWarning($"[ArtLoader] 目录不存在: {_basePath}");
                _loaded = true;
                return;
            }

            var files = Directory.GetFiles(_basePath, "*.png");
            foreach (var file in files)
            {
                var bytes = File.ReadAllBytes(file);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning($"[ArtLoader] 无法解码: {Path.GetFileName(file)}");
                    continue;
                }

                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                // 根据文件名推断 pivot
                var fileName = Path.GetFileNameWithoutExtension(file);
                var pivot = GetPivot(fileName);

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    pivot,
                    100f  // pixels per unit — 大图用大值避免 scale 1 时占满全屏
                );

                // 以文件名作为 key
                _cache[fileName] = sprite;
                Debug.Log($"[ArtLoader] 加载: {fileName} ({tex.width}x{tex.height}) pivot={pivot}");
            }

            _loaded = true;
            Debug.Log($"[ArtLoader] 全部加载完成，共 {_cache.Count} 张精灵");
        }

        /// <summary>按文件名关键词查找，返回第一个匹配的 Sprite</summary>
        public static Sprite Find(string keyword)
        {
            if (!_loaded) LoadAll();

            foreach (var kv in _cache)
            {
                if (kv.Key.Contains(keyword))
                    return kv.Value;
            }
            return null;
        }

        /// <summary>查找所有匹配的 Sprite</summary>
        public static List<Sprite> FindAll(string keyword)
        {
            var result = new List<Sprite>();
            if (!_loaded) LoadAll();

            foreach (var kv in _cache)
            {
                if (kv.Key.Contains(keyword))
                    result.Add(kv.Value);
            }
            return result;
        }

        /// <summary>清空缓存（切换场景时可选）</summary>
        public static void Clear()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value != null)
                {
                    if (kv.Value.texture != null)
                        UnityEngine.Object.Destroy(kv.Value.texture);
                    UnityEngine.Object.Destroy(kv.Value);
                }
            }
            _cache.Clear();
            _loaded = false;
        }

        // ── 私有 ──

        private static Vector2 GetPivot(string fileName)
        {
            // 角色立绘：底部居中，让角色"站"在地面上
            if (fileName.Contains("白露") || fileName.Contains("竹青") || fileName.Contains("当归")
                || fileName.Contains("云鹤老") || fileName.Contains("小山")
                || fileName.Contains("青岚") || fileName.Contains("寒露") || fileName.Contains("樵翁"))
                return new Vector2(0.5f, 0f);

            // 其他：居中
            return new Vector2(0.5f, 0.5f);
        }
    }
}
