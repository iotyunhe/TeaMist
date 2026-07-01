/// <summary>
/// 正式 SortingLayer 层级定义。替代所有 sortingOrder 硬编码。
/// 通过 SortingLayer 名称映射到 TagManager.asset 中定义的层。
/// </summary>
namespace TeaMist.Core
{
    public static class SortingLayers
    {
        // ── SpriteRenderer 层级（从后到前） ──
        public const string Background  = "Background";   // 远山、中景、天空
        public const string MainScene   = "MainScene";    // 茶馆主体、建筑
        public const string Props       = "Props";        // 道具、茶具、桌椅
        public const string Characters  = "Characters";   // NPC 角色立绘
        public const string Foreground  = "Foreground";   // 前景装饰
        public const string Overlay     = "Overlay";      // 天气覆盖层、屏幕特效

        // ── 层内 order（微调，值越小越靠后） ──
        public static class OrderInLayer
        {
            // Background 层内
            public const int BG_Far  = 0;   // 远山
            public const int BG_Mid  = 10;  // 中景

            // Characters 层内
            public const int Char_Default = 0;    // 普通 NPC
            public const int Char_Behind  = -10;  // 在正常 NPC 之后（如云鹤老、小山，视觉上被遮挡）

            // Overlay 层内
            public const int Overlay_Weather = 0;
            public const int Overlay_Effect  = 10;
        }
    }
}
