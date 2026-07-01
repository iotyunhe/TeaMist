using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace TeaMist.Core
{
    /// <summary>
    /// 字体管理器（Legacy UI.Text 版本）。
    /// 优先级：OS 系统字体 → 兜底 LegacyRuntime。
    /// </summary>
    public static class FontManager
    {
        private static bool _initialized;
        private static Font _chineseFont;

        /// <summary>测试用预渲染文本：覆盖游戏中所有会出现的汉字</summary>
        private const string TestChinese =
            "的一是不了在有人我他这中大为上个国到说们和就出会可也你对生能而子那得于着下自之年" +
            "过发后作里用道行所然家种事成方多经么去法学如都同现当没动面起看定天分还进好小部其些" +
            "主样理心她本前开但因只从想实日军者意无力它与长把机十民第公此已工使情明性知全三又关" +
            "点正业外将两高间由问很最重并物手应战向头文体政美相见被利什二等产或新己制身果加西斯" +
            "月话合回特代内信表化老给世位次度门任常先海通教儿原东声提立及比员解水名真论处走义各" +
            "入几口认条平系气题活尔更别打女变四神总何电数安少报才结反受目太量再感建务做接必场件" +
            "计管期市直德资命山金指克许统区保至队形社便空决治展马科司五基眼书非则听白却界达光放" +
            "强即像难且权思王象完设式色路记南品住告类求据程北边死张该交规万取拉格望觉术领共确传" +
            "师观清今切院让识候带导争运笑飞风步改收根干造言联持组每济车亲极林服快办议往元英士证" +
            "近失转夫令准布始怎呢存未远叫台单影具罗字爱击流备兵连调深商算质团集百需价花党华城石" +
            "级整府离况亚续近请技际约示复病息究线似官火断精满支视消越器容照须九增研写称企八功吗" +
            "包片史委乎查轻易早曾除农找装广显吧阿李标谈吃图念六引历首医局突专费号尽另周较注语仅" +
            "考落青随选列武红响虽说决半愿站降谢编限妹环排叶趣米角述岁乎杀续劳苏密谓齐境降演析秘" +
            "河终刘录构展钢曾升乡湖翻坦娘静弹街萨露佛室春冬雷秋夏雪雨风霜雾云山茶烟泡壶桂花蜜香" +
            "清心甜苦淡浓温暖凉冷热新旧来往去留坐站走跑停说笑笑哭想忘记看听闻触感喜忧怒惊羞怕白" +
            "竹青当归云鹤老小山小狐狸妖人客店馆茶老板先生小姐姑娘桂花树药草石溪鸟窗门帘桌凳杯碗" +
            "好喝泡煮冲泡注汤沸凉温热闷煎焙炒揉捻晒阴晴多云确认择叶选壶控温手法出汤评分温度品质" +
            "完美不错还行勉强等待空闲忙重置状态切换跳过天气欢迎进来到离开阖休息问候请问抱歉谢谢" +
            "第天日月时分享节季节气雾风雨霜雪花瓣萤火落叶飘落窗棂竹帘剪影耕远山近景中景前景道具" +
            "一言为定很期待随时欢迎点头微笑请进外面凉有什么需要语气平和不说话微笑不语空座位故事里" +
            "味道回事次再给准备告诉懂得愿答应约信由借放收买卖送退换赚赔赢输胜败成得失存亡生死" +
            "今昨明后现在曾经永远总是偶尔常常从未已经快要马上立刻赶紧稍微更加非常特别极其多么" +
            "一二三四五六七八九十百千万亿个只条张把杯壶碗瓶件双对群些点些许都全";

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Step 1: 列出已安装的 OS 字体
            var installedFonts = Font.GetOSInstalledFontNames();
            Debug.Log($"[FontManager] 系统安装 {installedFonts.Length} 款字体");
            foreach (var f in installedFonts.Take(15))
                Debug.Log($"[FontManager]   - {f}");

            // Step 2: 按优先级尝试加载 OS 中文字体
            string[] candidates = {
                "SimHei", "Microsoft YaHei", "SimSun",
                "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
                "WenQuanYi Micro Hei", "WenQuanYi Zen Hei",
                "FangSong", "KaiTi", "NSimSun", "DengXian",
                "Microsoft JhengHei", "DFKai-SB", "MingLiU", "PMingLiU"
            };

            foreach (var name in candidates)
            {
                _chineseFont = Font.CreateDynamicFontFromOSFont(name, 32);
                if (_chineseFont != null)
                {
                    Debug.Log($"[FontManager] ✓ 找到字体: {name}");
                    break;
                }
            }

            // Step 3: 验证字体能渲染中文
            if (_chineseFont != null)
            {
                try
                {
                    _chineseFont.RequestCharactersInTexture(TestChinese);
                    Debug.Log($"[FontManager] ✓ 中文渲染验证通过 ({TestChinese.Length} 字符)");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[FontManager] ✗ 中文渲染验证失败: {e.Message}");
                    _chineseFont = null;
                }
            }

            // Step 4: 兜底
            if (_chineseFont == null)
            {
                Debug.LogError("[FontManager] ✗ 未找到可用中文字体，回退到 LegacyRuntime (中文将显示为方块)");
                _chineseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            Debug.Log($"[FontManager] 最终字体: {(_chineseFont != null ? _chineseFont.name : "NULL")}");
        }

        public static Font ChineseFont
        {
            get
            {
                if (!_initialized) Initialize();
                return _chineseFont;
            }
        }
    }
}
