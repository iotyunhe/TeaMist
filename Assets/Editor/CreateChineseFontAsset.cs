using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using System.Text;
using UnityEngine.TextCore.LowLevel;

namespace TeaMist.Editor
{
    public static class CreateChineseFontAsset
    {
        [MenuItem("TeaMist/Create Chinese Font SDF", false, 10)]
        public static void Create()
        {
            try { DoCreate(); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FM] 创建失败: {ex}");
            }
        }

        static void DoCreate()
        {
            string dstDir = "Assets/Resources/Fonts";
            if (!Directory.Exists(dstDir))
                Directory.CreateDirectory(dstDir);

            string dstPath = Path.Combine(dstDir, "SimHei.ttf");
            string sdfPath = Path.Combine(dstDir, "SimHei SDF.asset");
            string pngPath = Path.Combine(dstDir, "SimHei SDF Atlas.png");

            // ── 1. 准备源字体 ──
            string fontsDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            string srcPath = Path.Combine(fontsDir, "simhei.ttf");
            if (!File.Exists(srcPath))
            {
                Debug.LogError("[FM] 未找到 simhei.ttf。请在系统字体目录中确认黑体已安装。");
                return;
            }

            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath, false);
                Debug.Log("[FM] 字体文件已复制");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceUpdate);

            Font font = AssetDatabase.LoadAssetAtPath<Font>(dstPath);
            if (font == null)
            {
                Debug.LogError("[FM] 无法加载 SimHei.ttf。");
                return;
            }
            Debug.Log($"[FM] 源字体就绪: {font.name}");

            // ── 2. 删除旧资产 ──
            if (File.Exists(sdfPath))
            {
                AssetDatabase.DeleteAsset(sdfPath);
                Debug.Log("[FM] 旧 SDF 已删除");
            }
            if (File.Exists(pngPath))
            {
                AssetDatabase.DeleteAsset(pngPath);
                Debug.Log("[FM] 旧 Atlas PNG 已删除");
            }
            AssetDatabase.Refresh();

            // ── 3. 创建 TMP_FontAsset ──
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 64, 8, GlyphRenderMode.SDF, 2048, 2048);

            if (fontAsset == null)
            {
                Debug.LogError("[FM] CreateFontAsset 返回 null");
                return;
            }

            // ── 4. 烘焙字符 ──
            string allChars = GetAllChars();
            Debug.Log($"[FM] 烘焙 {allChars.Length} 字符...");
            fontAsset.TryAddCharacters(allChars, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[FM] {missing.Length} 字符未烘焙（超出容量）");

            fontAsset.ReadFontAssetDefinition();

            // ── 5. 导出 atlas 为 PNG（核心修复）──
            //     运行时 Texture2D 无法通过 AddObjectToAsset 可靠序列化。
            //     改为导出 PNG → 重新导入为磁盘资产 → 回赋给 FontAsset。
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0
                || fontAsset.atlasTextures[0] == null)
            {
                Debug.LogError("[FM] atlas 纹理为空，无法导出");
                return;
            }

            var runtimeTex = fontAsset.atlasTextures[0];
            runtimeTex.Apply(false, false);

            byte[] pngBytes = runtimeTex.EncodeToPNG();
            File.WriteAllBytes(pngPath, pngBytes);
            Debug.Log($"[FM] Atlas 已导出 PNG: {pngPath} ({pngBytes.Length} bytes)");

            // ── 6. 导入 PNG 为纹理资产 ──
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var texImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (texImporter != null)
            {
                texImporter.textureType = TextureImporterType.SingleChannel;
                texImporter.sRGBTexture = false;
                texImporter.mipmapEnabled = false;
                texImporter.wrapMode = TextureWrapMode.Clamp;
                texImporter.filterMode = FilterMode.Bilinear;
                texImporter.isReadable = true;
                texImporter.textureCompression = TextureImporterCompression.Uncompressed;
                texImporter.SaveAndReimport();
            }

            var diskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (diskTex == null)
            {
                Debug.LogError("[FM] 导入 PNG 失败");
                return;
            }
            Debug.Log($"[FM] Atlas 纹理已导入: {diskTex.width}x{diskTex.height}");

            // ── 7. 回赋磁盘纹理 + 保存 FontAsset ──
            fontAsset.atlasTextures[0] = diskTex;
            if (fontAsset.material != null)
            {
                fontAsset.material.mainTexture = diskTex;
                fontAsset.material.hideFlags = HideFlags.HideInHierarchy;
            }

            AssetDatabase.CreateAsset(fontAsset, sdfPath);
            EditorUtility.SetDirty(fontAsset);

            // 材质作为子资产保存
            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 8. 验证 ──
            var verify = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
            if (verify == null)
            {
                Debug.LogError("[FM] 验证失败：无法重新加载");
                return;
            }

            int cjkChars = 0;
            if (verify.characterTable != null)
            {
                foreach (var c in verify.characterTable)
                    if (c.unicode >= 0x4E00 && c.unicode <= 0x9FFF)
                        cjkChars++;
            }

            string texStatus = "null";
            try
            {
                if (verify.atlasTextures != null && verify.atlasTextures.Length > 0
                    && verify.atlasTextures[0] != null)
                    texStatus = $"{verify.atlasTextures[0].width}x{verify.atlasTextures[0].height} OK";
            }
            catch { texStatus = "访问异常"; }

            if (cjkChars > 0)
            {
                Debug.Log(
                    "[FM] ======== 中文字体资产已生成 ========\n" +
                    $"  路径:  {sdfPath}\n" +
                    $"  CJK:   {cjkChars}  总字符: {verify.characterTable?.Count ?? 0}\n" +
                    $"  Atlas: {verify.atlasWidth}x{verify.atlasHeight}  纹理: {texStatus}\n" +
                    $"  现在进入 Play 模式即可显示中文。");
            }
            else
            {
                Debug.LogError($"[FM] 字符表无 CJK！请使用 Window → TextMeshPro → Font Asset Creator 手动生成");
            }
        }

        static string GetAllChars()
        {
            var sb = new StringBuilder(4096);

            for (char c = (char)32;  c <= 126;  c++) sb.Append(c);
            for (char c = (char)160; c <= 255;  c++) sb.Append(c);

            // TMP 特殊字符（ellipsis, NBSP, 等）—— 必须预烘焙，否则 Dynamic 模式
            // 下 set_font 时 TMP 尝试动态添加时遇到已销毁的纹理引用
            sb.Append('\u2026');  // …
            sb.Append('\u00A0');  // non-breaking space
            sb.Append('\u00AD');  // soft hyphen
            sb.Append('\u200B');  // zero-width space

            sb.Append(
                "茶烟起处泡白露竹青当归桂花蜜清心碧螺龙井普洱铁观音大红袍武夷岩茉莉菊花薰衣玫瑰" +
                "陈金骏眉牡丹王云雾山谷雨露春雪霜泉溪涧潭湖海江河岸石岩壁草木花叶枝根藤蔓" +
                "兰芷若芬芳馥馨香醇甘苦涩酸甜咸淡浓清浊温凉寒热冷暖暑冬夏秋季节气" +
                "阴阳虚实补泻表里风暑湿燥火痰瘀郁陷脱崩漏闭厥昏乱狂痴呆疯癫" +
                "气血津液精神魂魄意志壶杯盏炉火炭薪勺匙箸碾研筛箩罐瓮缸瓶坛釜灶鼎" +
                "翎羽毫锥锋刃吴王越官汝哥定制钧建窑景德镇紫砂宜兴陶土青瓷白瓷黑釉" +
                "一二三四五六七八九十百千万亿零个只次件品样类种份斤两钱文贯锭枚张本首篇册" +
                "卷第段行句章节回目卷序跋引注疏笺诠按语案评价论说记叙描写抒情议论" +
                "选择返回确定取消继续跳过开始暂停结束退出保存读取设置帮助关于关闭是否" +
                "状态时间客人进度收集解锁好感等级经验属性品质稀有传说史诗普通" +
                "今日明日昨天前天上午中午下午晚上午夜清晨傍晚黄昏拂晓白昼黑夜" +
                "庭院竹篱茅舍松柏杨柳梅兰菊莲池塘荷菱藕萍藻苔藓蕨藤蔓茵席" +
                "笔墨纸砚琴棋书画诗酒花月风烟云雾雨露霜雪山水林泉石径斜阳落霞" +
                "晨曦暮霭流萤蛙蝉鸟雀鹰隼鱼虾蟹鳖鹤鹭燕雁鸳鸯凤凰龙凤麒麟");

            sb.Append(
                "的了一是在我不们他有上来到下说看去想会做能可知道过人个这" +
                "就以还着都也为然要把你之里得那没而小所己如与年出后没被它怎吗吧呢啊" +
                "爱安保八把爸白百班板半办帮包报抱杯北备被本比笔必边变便标表别" +
                "冰饼并病播补不步部才采彩菜参餐草测层查差产长常场唱超朝车晨称成" +
                "城吃持冲重出初除处础穿传船窗床创春词此次从村存错答打代带单但当" +
                "导到道得灯等低底地点弟第典电店调掉定冬东懂动都读度短队对多饿儿" +
                "而二发法反饭方房放飞非分份丰风封否夫服福父付复富该改感干刚高告" +
                "歌格个各给跟更工公共狗姑古故顾瓜关观馆管光广逛鬼贵国果过孩海害" +
                "喊汉好号喝河和合何黑很红后候忽湖互护花化画话坏欢还环换慌黄灰回" +
                "会婚活火伙或机鸡积基及急集几己计记纪技际既继寄加家假间检简建健" +
                "将讲交脚叫教较街接节姐解介界借今斤金紧进近京经睛精景静久酒旧就" +
                "居局举句具据觉决军开看康考科可渴克刻客课空口苦裤块快筷况困拉啦" +
                "来蓝老乐累冷离礼李里力历立丽利例连联脸练凉粮两亮谅了料林零领另" +
                "留流六龙楼路旅绿乱论落妈马吗买卖满慢忙毛么没美每门们梦米面民明名" +
                "末母目拿哪那男南难脑呢内能你年念鸟您牛农努女暖爬怕排旁胖跑朋皮片" +
                "偏漂票平瓶苹七期齐其奇骑起气汽千前钱强墙切亲青轻清晴情请秋求球区" +
                "取去全缺却确群然让热人认任仍日容肉如入三散色杀山商上少绍舌设社谁" +
                "身深神生声省剩师十时识实食使始世事是适室收手首受书熟数术树双谁水" +
                "睡顺说思死四送诉素算虽随岁所他她它台太态谈汤堂糖躺讨特疼提题体天" +
                "条跳听停通同统头图土推外完玩晚碗万王网忘望为位文问我屋无五午武舞" +
                "物务西希息习洗喜系细下夏先险现线相香想向像消小校笑些鞋写谢心新星" +
                "行醒性姓幸休需许续选学雪血寻压牙言眼演验羊阳样药要爷也夜一医衣宜" +
                "已以义艺议因音应影硬用由邮游友有又右鱼语育预元园员原远愿月越运杂" +
                "在再咱早造增展站张找照者真整正证政知支之直职止纸指志制治致中钟终" +
                "种众重周住注祝转装准子自字总走租族组嘴最昨左作坐座做");

            return sb.ToString();
        }
    }
}
