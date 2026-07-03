using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace TeaMist.Dialogue
{
    /// <summary>
    /// 对话系统管理器 —— 封装 Yarn Spinner，提供高层 API。
    /// 
    /// 核心职责：
    /// - 加载 .yarn 剧本
    /// - 驱动对话节点流转
    /// - 管理 Yarn 变量（好感度、选择标记等）
    /// - 桥接泡茶交互 → 对话分支
    /// - 触发碎片掉落事件
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("━━━ 对话 UI ━━━")]
        public DialogueUI dialogueUI;

        [Header("━━━ 事件 ━━━")]
        public UnityEvent<string> OnDialogueStarted;    // 传入剧本名
        public UnityEvent<string> OnDialogueEnded;      // 传入剧本名
        public UnityEvent<int> OnOptionSelected;         // 传入选项序号
        public UnityEvent<string, string> OnFragmentDropped; // (fragmentId, npcName)

        [Header("━━━ 变量存储 ━━━")]
        public DialogueVariableStorage variableStorage = new DialogueVariableStorage();

        // 当前状态
        private string currentScriptName;
        private bool isDialogueActive;
        private Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
        private List<DialogueOption> currentOptions = new List<DialogueOption>();

        // 对话前旁白队列（DailyStoryPool 等系统注入的环境描写）
        private Queue<string> _preDialogueNarrations = new Queue<string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 初始化 UnityEvents（运行时 AddComponent 后可能为 null）
            if (OnDialogueStarted == null) OnDialogueStarted = new UnityEvent<string>();
            if (OnDialogueEnded == null) OnDialogueEnded = new UnityEvent<string>();
            if (OnOptionSelected == null) OnOptionSelected = new UnityEvent<int>();
            if (OnFragmentDropped == null) OnFragmentDropped = new UnityEvent<string, string>();
        }

        // ━━━ 公共 API ━━━

        /// <summary>
        /// 添加对话前旁白（在 StartDialogue 之前调用，旁白会在 NPC 对话前展示）
        /// </summary>
        public void AddPreDialogueNarration(string text)
        {
            if (!string.IsNullOrEmpty(text))
                _preDialogueNarrations.Enqueue(text);
        }

        /// <summary>
        /// 启动一段对话。传入 Yarn 剧本文件名（不含后缀）。
        /// 例如: StartDialogue("bai_lu_first_visit")
        /// </summary>
        public void StartDialogue(string scriptName)
        {
            if (isDialogueActive)
            {
                Debug.LogWarning($"[Dialogue] 已有对话在进行中: {currentScriptName}");
                return;
            }

            currentScriptName = scriptName;
            isDialogueActive = true;
            lineQueue.Clear();
            currentOptions.Clear();

            OnDialogueStarted?.Invoke(scriptName);

            // 插入对话前旁白（DailyStoryPool 环境描写等）
            while (_preDialogueNarrations.Count > 0)
            {
                lineQueue.Enqueue(new DialogueLine
                {
                    type = DialogueLineType.Narrator,
                    text = _preDialogueNarrations.Dequeue()
                });
            }

            // 加载并解析 Yarn 文件
            var lines = LoadAndParseYarnScript(scriptName);
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    lineQueue.Enqueue(line);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning($"[DialogueManager] 未找到或解析失败 Yarn 脚本: {scriptName}");
            }
#endif

            dialogueUI?.Show();
            AdvanceDialogue();
        }

        /// <summary>
        /// 处理玩家选择
        /// </summary>
        public void SelectOption(int optionIndex)
        {
            if (!isDialogueActive || optionIndex < 0 || optionIndex >= currentOptions.Count)
                return;

            var chosen = currentOptions[optionIndex];
            OnOptionSelected?.Invoke(optionIndex);

            // 更新变量
            if (!string.IsNullOrEmpty(chosen.setVariable))
            {
                variableStorage.SetVariable(chosen.setVariable, chosen.setValue);
            }

            // 如果有分支跳转标签，加载后续对话
            if (!string.IsNullOrEmpty(chosen.jumpToLabel))
            {
                var branchLines = LoadBranchLines(currentScriptName, chosen.jumpToLabel);
                lineQueue.Clear();
                foreach (var line in branchLines)
                {
                    lineQueue.Enqueue(line);
                }
            }

            currentOptions.Clear();
            dialogueUI?.HideOptions();
            AdvanceDialogue();
        }

        /// <summary>
        /// 根据泡茶匹配质量推进对话分支
        /// </summary>
        public void AdvanceWithTeaQuality(int qualityScore)
        {
            // qualityScore: 0-100
            // >= 90: 完美 → perfect 分支
            // >= 60: 不错 → good 分支
            // <  60: 勉强 → okay 分支
            string label = qualityScore >= 90 ? "tea_perfect" :
                           qualityScore >= 60 ? "tea_good" : "tea_okay";

            var branchLines = LoadBranchLines(currentScriptName, label);
            lineQueue.Clear();
            foreach (var line in branchLines)
            {
                lineQueue.Enqueue(line);
            }

            // 重新显示对话 UI（之前被 HideImmediate 隐藏了）
            dialogueUI?.HideTeaUI();
            dialogueUI?.Show();
            AdvanceDialogue();
        }

        /// <summary>
        /// 玩家请求泡茶 → 调用此方法，会暂停对话并弹出泡茶 UI
        /// </summary>
        public void RequestTeaBrewing(string npcRequest, string targetTeaId)
        {
            // 通过 TeaShopLoop 统一管理状态
            Gameplay.TeaShopLoop.Instance?.OnTeaBrewingRequested(npcRequest, targetTeaId);
        }

        // ━━━ 内部驱动 ━━━

        private void AdvanceDialogue()
        {
            if (!isDialogueActive) return;

            while (lineQueue.Count > 0)
            {
                var line = lineQueue.Dequeue();

                switch (line.type)
                {
                    case DialogueLineType.NPC:
                        dialogueUI?.ShowNPC(line.speaker, line.text, line.emotion);
                        // 等玩家点击继续
                        return;

                    case DialogueLineType.Narrator:
                        dialogueUI?.ShowNarration(line.text);
                        return;

                    case DialogueLineType.Options:
                        currentOptions = line.options;
                        dialogueUI?.ShowOptions(currentOptions);
                        // 等待玩家选择
                        return;

                    case DialogueLineType.TeaCommand:
                        // 隐藏对话面板，防止阻挡泡茶 UI
                        dialogueUI?.HideImmediate();
                        // 触发泡茶交互
                        RequestTeaBrewing(line.teaRequest, line.targetTeaId);
                        // 等泡茶完成回调
                        return;

                    case DialogueLineType.Command:
                        ExecuteCommand(line.text);
                        break; // 继续下一条

                    case DialogueLineType.End:
                        EndDialogue(line.tag);
                        return;
                }
            }
        }

        /// <summary>玩家点击继续（跳过当前 NPC 对话）</summary>
        public void OnContinueClicked()
        {
            dialogueUI?.HideNPC();
            AdvanceDialogue();
        }

        /// <summary>强制停止当前对话（调试/重置用）</summary>
        public void StopDialogue()
        {
            isDialogueActive = false;
            lineQueue.Clear();
            currentOptions.Clear();
            currentScriptName = null;
        }

        private void ExecuteCommand(string cmd)
        {
            // 支持的指令:
            // set var=value      → 设置变量
            // drop fragment_id   → 掉落碎片
            // jump label         → 跳转到分支
            // wait seconds       → 等待
            var parts = cmd.Trim().Split(' ');
            if (parts.Length == 0) return;

            switch (parts[0])
            {
                case "set" when parts.Length >= 2:
                    var kv = parts[1].Split('=');
                    if (kv.Length == 2)
                        variableStorage.SetVariable(kv[0], kv[1]);
                    break;

                case "drop" when parts.Length >= 2:
                    OnFragmentDropped?.Invoke(parts[1], currentScriptName);
                    break;

                case "jump" when parts.Length >= 2:
                    // 加载分支对话并替换队列
                    var branchLines = LoadBranchLines(currentScriptName, parts[1]);
                    lineQueue.Clear();
                    foreach (var line in branchLines)
                        lineQueue.Enqueue(line);
                    currentOptions.Clear();
                    break;
            }
        }

        private void EndDialogue(string endTag)
        {
            isDialogueActive = false;
            dialogueUI?.Hide();
            OnDialogueEnded?.Invoke(currentScriptName);
            currentScriptName = "";
        }

        // ━━━ Yarn 解析（简化版） ━━━

        private List<DialogueLine> LoadAndParseYarnScript(string scriptName)
        {
            var lines = new List<DialogueLine>();
            var asset = Resources.Load<TextAsset>($"Yarn/Characters/{scriptName}");
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>($"Yarn/{scriptName}");
            }

            if (asset == null)
            {
                Debug.LogError($"[Dialogue] 找不到剧本: {scriptName}");
                lines.Add(new DialogueLine { type = DialogueLineType.End, tag = "not_found" });
                return lines;
            }

            return ParseYarnLines(asset.text, "start");
        }

        private List<DialogueLine> LoadBranchLines(string scriptName, string label)
        {
            var asset = Resources.Load<TextAsset>($"Yarn/Characters/{scriptName}");
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>($"Yarn/{scriptName}");
            }
            if (asset == null) return new List<DialogueLine>();

            return ParseYarnLines(asset.text, label);
        }

        private List<DialogueLine> ParseYarnLines(string yarnText, string targetLabel)
        {
            var lines = new List<DialogueLine>();
            var rawLines = yarnText.Split('\n');
            bool inTarget = string.IsNullOrEmpty(targetLabel) || targetLabel == "start";
            for (int i = 0; i < rawLines.Length; i++)
            {
                var raw = rawLines[i].Trim();

                // 标签跳转
                if (raw.EndsWith(":"))
                {
                    var label = raw.TrimEnd(':').Trim();
                    // 进入目标标签
                    if (label == targetLabel || (string.IsNullOrEmpty(targetLabel) && label == "start"))
                    {
                        inTarget = true;
                    }
                    // 目标段内的 stop 终止
                    else if (inTarget && label == "stop")
                    {
                        break;
                    }
                    // end_xxx 标签：标记性标签，不退出当前段（紧跟着就是 jump 指令）
                    else if (inTarget && label.StartsWith("end_"))
                    {
                        // 保持 inTarget = true，继续处理后续行
                    }
                    // 目标段内遇到其他标签：退出当前段
                    else if (inTarget)
                    {
                        inTarget = false;
                    }
                    continue;
                }

                if (!inTarget || string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                    continue;

                // ── if: 条件块 ──
                if (raw.StartsWith("if:") || raw.StartsWith("if "))
                {
                    string cond = raw.StartsWith("if:") ? raw.Substring(3).Trim() : raw.Substring(3).Trim();
                    // 收集条件块内所有行
                    var blockLines = new List<string>();
                    int bi = i + 1;
                    while (bi < rawLines.Length && !rawLines[bi].Trim().StartsWith("endif"))
                    {
                        blockLines.Add(rawLines[bi]);
                        bi++;
                    }
                    i = bi; // 跳过 endif

                    // 仅当条件满足时将块内行插入解析
                    if (variableStorage.EvaluateCondition(cond))
                    {
                        // 将块内行插入 rawLines 后重新走解析循环
                        // 简化：直接复用 ParseYarnLines 的小段落
                        var blockText = string.Join("\n", blockLines);
                        var blockParsed = ParseYarnLines(blockText, "start");
                        foreach (var bl in blockParsed)
                        {
                            if (bl.type == DialogueLineType.Options && bl.options != null)
                            {
                                // 过滤不符合条件的选项（选项本身也可有 set/jump）
                                lines.Add(bl);
                            }
                            else
                            {
                                lines.Add(bl);
                            }
                        }
                    }
                    continue;
                }

                // 解析行类型
                if (raw.StartsWith("<<") && raw.EndsWith(">>"))
                {
                    var cmd = raw.Substring(2, raw.Length - 4).Trim();
                    if (cmd.StartsWith("tea "))
                    {
                        // << tea "请给我一壶桂花蜜茶" perfect_match:guihuamicha >>
                        var teaCmd = ParseTeaCommand(cmd.Substring(4));
                        lines.Add(new DialogueLine { type = DialogueLineType.TeaCommand, teaRequest = teaCmd.request, targetTeaId = teaCmd.targetId });
                    }
                    else if (cmd.StartsWith("options"))
                    {
                        // << options >>
                        var options = new List<DialogueOption>();
                        int j = i + 1;
                        while (j < rawLines.Length && !rawLines[j].Trim().StartsWith("<<"))
                        {
                            var optLine = rawLines[j].Trim();
                            if (optLine.StartsWith("-> "))
                            {
                                var option = ParseOption(optLine.Substring(3));
                                if (option.HasValue) options.Add(option.Value);
                            }
                            j++;
                        }
                        i = j - 1;
                        if (options.Count > 0)
                            lines.Add(new DialogueLine { type = DialogueLineType.Options, options = options });
                    }
                    else
                    {
                        lines.Add(new DialogueLine { type = DialogueLineType.Command, text = cmd });
                    }
                }
                else if (raw.StartsWith("---"))
                {
                    // 分隔线，跳过
                    continue;
                }
                else if (raw.StartsWith("jump "))
                {
                    // 无条件跳转
                    var label = raw.Substring(5).Trim();
                    lines.Add(new DialogueLine { type = DialogueLineType.Command, text = $"jump {label}" });
                }
                else if (raw.StartsWith("【"))
                {
                    // 叙述
                    var text = raw.Trim('【', '】');
                    lines.Add(new DialogueLine { type = DialogueLineType.Narrator, text = text });
                }
                else if (raw.StartsWith("end:"))
                {
                    // 结束标记: end:first_visit
                    var tag = raw.Substring(4).Trim();
                    lines.Add(new DialogueLine { type = DialogueLineType.End, tag = tag });
                }
                else if (raw.StartsWith("-> "))
                {
                    // 裸选项行（不在 << options >> 块内）—— 收集连续 -> 行
                    var options = new List<DialogueOption>();
                    int k = i;
                    while (k < rawLines.Length && rawLines[k].Trim().StartsWith("-> "))
                    {
                        var optLine = rawLines[k].Trim().Substring(3);
                        var option = ParseOption(optLine);
                        if (option.HasValue) options.Add(option.Value);
                        k++;
                    }
                    i = k - 1;
                    if (options.Count > 0)
                        lines.Add(new DialogueLine { type = DialogueLineType.Options, options = options });
                }
                else if (raw.Contains(":"))
                {
                    // NPC 对话: "白露: 老板老板！"
                    var colonIdx = raw.IndexOf(':');
                    var speaker = raw.Substring(0, colonIdx).Trim();
                    var text = raw.Substring(colonIdx + 1).Trim();

                    // 检查情绪标记
                    string emotion = "neutral";
                    if (text.Contains("（笑）")) { emotion = "happy"; text = text.Replace("（笑）", ""); }
                    if (text.Contains("（惊）")) { emotion = "surprise"; text = text.Replace("（惊）", ""); }
                    if (text.Contains("（忧）")) { emotion = "worried"; text = text.Replace("（忧）", ""); }
                    if (text.Contains("（羞）")) { emotion = "shy"; text = text.Replace("（羞）", ""); }

                    text = text.Trim();

                    // 情绪标记是唯一内容时，用描述性文字代替（否则显示空白框）
                    if (string.IsNullOrEmpty(text))
                    {
                        text = emotion switch
                        {
                            "happy"    => "（笑了笑）",
                            "surprise" => "（吃了一惊）",
                            "worried"  => "（面露忧色）",
                            "shy"      => "（害羞地低下了头）",
                            _          => "……"
                        };
                    }

                    lines.Add(new DialogueLine { type = DialogueLineType.NPC, speaker = speaker, text = text.Trim(), emotion = emotion });
                }
            }

            return lines;
        }

        private (string request, string targetId) ParseTeaCommand(string cmd)
        {
            // "请给我一壶桂花蜜茶" perfect_match:guihuamicha
            var parts = cmd.Split(new[] { ' ' }, 2);
            string request = parts[0].Trim('"');
            string targetId = "";
            if (parts.Length > 1 && parts[1].Contains(":"))
            {
                targetId = parts[1].Split(':')[1].Trim();
            }
            return (request, targetId);
        }

        private DialogueOption? ParseOption(string raw)
        {
            // 格式1（完整）: -> (笑) → 请进吧，外面凉 → set:greeting_style=warm  jump:warm_welcome
            // 格式2（简略）: -> 随时欢迎 → set:affection_bailu=35  jump:ending_warm
            // 格式3（最小）: -> 一言为定 → set:affection_bailu=25  jump:ending_warm
            var parts = raw.Split(new[] { "→" }, System.StringSplitOptions.None);
            if (parts.Length < 2) return null;

            var opt = new DialogueOption();
            string effectsPart = "";

            if (parts.Length >= 3)
            {
                // 三部分：label | display_text | effects
                opt.label = parts[0].Trim();
                opt.text = parts[1].Trim();
                effectsPart = parts[2].Trim();
            }
            else
            {
                // 两部分：label | effects（label 即 text）
                opt.label = parts[0].Trim();
                opt.text = parts[0].Trim();
                effectsPart = parts[1].Trim();
            }

            // 从 effectsPart 解析 set: 和 jump:
            int setPos = effectsPart.IndexOf("set:");
            int jumpPos = effectsPart.IndexOf("jump:");

            if (setPos >= 0)
            {
                int start = setPos + 4; // after "set:"
                int end = jumpPos > setPos ? jumpPos : effectsPart.Length;
                string setStr = effectsPart.Substring(start, end - start).Trim();
                var kv = setStr.Split('=');
                if (kv.Length == 2)
                {
                    opt.setVariable = kv[0].Trim();
                    opt.setValue = kv[1].Trim();
                }
            }

            if (jumpPos >= 0)
            {
                int start = jumpPos + 5; // after "jump:"
                int end = setPos > jumpPos ? setPos : effectsPart.Length;
                string jumpStr = effectsPart.Substring(start, end - start).Trim();
                opt.jumpToLabel = jumpStr;
            }

            return opt;
        }
    }

    // ━━━ 数据结构 ━━━

    public enum DialogueLineType
    {
        NPC,        // NPC 对话
        Narrator,   // 叙述文字
        Options,    // 玩家选择
        TeaCommand, // 触发泡茶
        Command,    // 内部指令
        End         // 对话结束
    }

    [System.Serializable]
    public struct DialogueLine
    {
        public DialogueLineType type;
        public string speaker;
        public string text;
        public string emotion;
        public string teaRequest;
        public string targetTeaId;
        public string tag;
        public List<DialogueOption> options;
    }

    [System.Serializable]
    public struct DialogueOption
    {
        public string label;       // 选项标签（显示在 UI）
        public string text;        // 选项文本
        public string setVariable; // 设置的变量名
        public string setValue;    // 设置的变量值
        public string jumpToLabel; // 跳转标签
    }

    /// <summary>
    /// 对话变量存储 —— 所有跨对话的状态
    /// </summary>
    [System.Serializable]
    public class DialogueVariableStorage
    {
        // NPC 好感度
        public Dictionary<string, int> affection = new Dictionary<string, int>();

        // 剧情标记
        public HashSet<string> flags = new HashSet<string>();

        // 玩家画像
        public int warmth;   // 温暖
        public int wit;      // 机智
        public int calmness; // 沉静
        public int curiosity;// 好奇

        // 通用键值
        public Dictionary<string, string> vars = new Dictionary<string, string>();

        public void SetVariable(string key, string value)
        {
            // 处理 "flag:xxx" 前缀：写入 flags 集合
            if (key.StartsWith("flag:"))
            {
                var flagName = key.Substring(5).Trim();
                if (value == "true") flags.Add(flagName);
                else if (value == "false") flags.Remove(flagName);
                return;
            }

            switch (key)
            {
                case "affection_bailu":
                    affection["bailu"] = int.TryParse(value, out int a) ? a : 0;
                    break;
                case "affection_zhuqing":
                    affection["zhuqing"] = int.TryParse(value, out int z) ? z : 0;
                    break;
                case "affection_danggui":
                    affection["danggui"] = int.TryParse(value, out int dg) ? dg : 0;
                    break;
                case "affection_yunhelao":
                    affection["yunhelao"] = int.TryParse(value, out int yh) ? yh : 0;
                    break;
                case "affection_xiaoshan":
                    affection["xiaoshan"] = int.TryParse(value, out int xs) ? xs : 0;
                    break;
                case "warmth":
                    warmth = int.TryParse(value, out int w) ? w : 0;
                    break;
                case "wit":
                    wit = int.TryParse(value, out int t) ? t : 0;
                    break;
                case "calmness":
                    calmness = int.TryParse(value, out int c) ? c : 0;
                    break;
                case "curiosity":
                    curiosity = int.TryParse(value, out int cu) ? cu : 0;
                    break;
                default:
                    if (value == "true") flags.Add(key);
                    else if (value == "false") flags.Remove(key);
                    else vars[key] = value;
                    break;
            }
        }

        public int GetAffection(string npcId) =>
            affection.TryGetValue(npcId, out int v) ? v : 0;

        public bool HasFlag(string flag) => flags.Contains(flag);

        /// <summary>
        /// 解析运行时变量（天气/季节/时间等，不持久化，从管理器读取）
        /// 支持的键：weather, season, time, day, mood
        /// </summary>
        public string ResolveRuntime(string key)
        {
            switch (key)
            {
                case "weather":
                    return Core.TimeManager.Instance?.CurrentWeather.ToString() ?? "晴";
                case "season":
                    return Core.TimeManager.Instance?.CurrentSeason.ToString() ?? "春";
                case "time":
                    return Core.TimeManager.Instance?.CurrentTimeSlot.ToString() ?? "午后";
                case "day":
                    return Core.TimeManager.Instance?.DayInSeason.ToString() ?? "1";
                case "mood":
                    return vars.TryGetValue("mood", out var m) ? m : "喜悦";
                default:
                    // 尝试从 vars 获取
                    vars.TryGetValue(key, out var val);
                    return val ?? "";
            }
        }

        /// <summary>检查条件表达式: "weather=rain" → true if current weather is Rain</summary>
        public bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            condition = condition.Trim();

            // 支持 != 不等于
            bool negate = false;
            string op = "=";
            if (condition.Contains("!="))
            {
                negate = true;
                op = "!=";
            }

            var parts = condition.Split(new[] { op }, System.StringSplitOptions.None);
            if (parts.Length != 2) return true; // 无法解析，放行

            string key = parts[0].Trim();
            string expected = parts[1].Trim();

            // 检查标记位
            if (key.StartsWith("flag:"))
            {
                bool hasFlag = flags.Contains(key.Substring(5));
                bool flagVal = expected == "true";
                return negate ? (hasFlag != flagVal) : (hasFlag == flagVal);
            }

            // 检查好感度
            if (key.StartsWith("affection_"))
            {
                string npcId = key.Substring(10);
                int current = GetAffection(npcId);
                if (int.TryParse(expected, out int exp))
                {
                    bool result = op == ">=" ? current >= exp :
                                  op == "<=" ? current <= exp :
                                  op == ">" ? current > exp :
                                  op == "<" ? current < exp :
                                  current == exp;
                    return negate ? !result : result;
                }
            }

            // 运行时变量（weather/season/time/day）
            string actual = ResolveRuntime(key);
            if (!string.IsNullOrEmpty(actual))
                return negate ? (actual != expected) : (actual == expected);

            return true; // 未知变量，放行
        }

        // ━━━ 存档桥接 ━━━

        /// <summary>将运行时变量写入 SaveData</summary>
        public void SerializeToSave(Core.SaveData save)
        {
            save.savedAffection.Clear();
            foreach (var kv in affection)
                save.savedAffection.Add(new Core.IntPair { key = kv.Key, value = kv.Value });

            save.savedFlags = new List<string>(flags);

            save.savedVars.Clear();
            foreach (var kv in vars)
                save.savedVars.Add(new Core.StringPair { key = kv.Key, value = kv.Value });

            save.playerWarmth = warmth;
            save.playerWit = wit;
            save.playerCalmness = calmness;
            save.playerCuriosity = curiosity;
        }

        /// <summary>从 SaveData 恢复运行时变量</summary>
        public void DeserializeFromSave(Core.SaveData save)
        {
            affection.Clear();
            foreach (var entry in save.savedAffection)
                affection[entry.key] = entry.value;

            flags = new HashSet<string>(save.savedFlags);

            vars.Clear();
            foreach (var entry in save.savedVars)
                vars[entry.key] = entry.value;

            warmth = save.playerWarmth;
            wit = save.playerWit;
            calmness = save.playerCalmness;
            curiosity = save.playerCuriosity;
        }
    }
}
