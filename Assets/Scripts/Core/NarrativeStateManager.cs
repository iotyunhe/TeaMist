using System;
using System.Collections.Generic;
using UnityEngine;
using TeaMist.Dialogue;

namespace TeaMist.Core
{
    /// <summary>
    /// 叙事状态管理器 — 三层叙事体系的数据中枢。
    ///
    /// 管理的变量域（对应 Phase 4 设计文档的状态变量表）：
    ///   npc_{id}_affection      好感度（0-5，对应人物线章节触发阈值）
    ///   npc_{id}_chapter        当前章节进度（0=序章, 1-5=第一章→终章）
    ///   npc_{id}_last_visit     上次来访日期（yyyy-MM-dd）
    ///   npc_{id}_memory_{key}    NPC 记忆事件（多组，布尔标记）
    ///   player_choice_{event_id} 玩家选择记录（影响结局走向）
    ///   fragment_collected_{id}  碎片收集状态
    ///
    /// 与现有系统的关系：
    ///   - DialogueManager.variableStorage.affection 是旧好感度存储，本管理器通过
    ///     SyncToDialogueStorage / SyncFromDialogueStorage 与之保持双向同步。
    ///   - SaveData.NPCSaveData 是存档中的 NPC 数据，SerializeToSave
    ///     / DeserializeFromSave 负责持久化。
    /// </summary>
    public class NarrativeStateManager : MonoBehaviour
    {
        public static NarrativeStateManager Instance { get; private set; }

        // ━━━ NPC 状态 ━━━

        /// <summary>NPC ID → NPC 叙事状态</summary>
        private Dictionary<string, NPCNarrativeState> _npcStates
            = new Dictionary<string, NPCNarrativeState>();

        // ━━━ 全局状态 ━━━

        /// <summary>玩家选择记录（eventId → choiceIndex）</summary>
        private Dictionary<string, int> _playerChoices = new Dictionary<string, int>();

        /// <summary>已收集的碎片 ID 列表（与 SaveData.collectedFragments 对应）</summary>
        private List<string> _collectedFragments = new List<string>();

        /// <summary>已完成的对话节点（防重复触发）</summary>
        private HashSet<string> _completedDialogues = new HashSet<string>();

        /// <summary>对话冷却计时（nodeId → lastTriggeredDate）</summary>
        private Dictionary<string, string> _dialogueCooldowns = new Dictionary<string, string>();

        /// <summary>世界状态标志（可任意扩展的键值对）</summary>
        private Dictionary<string, string> _worldFlags = new Dictionary<string, string>();

        // ━━━ 事件 ━━━

        public event Action<string, int> OnAffectionChanged;       // npcId, newValue
        public event Action<string, int> OnChapterAdvanced;         // npcId, newChapter
        public event Action<string> OnFragmentCollected;           // fragmentId
        public event Action<string> OnMemoryUnlocked;               // memoryKey

        // ━━━ 生命周期 ━━━

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ═══════════════════════════════════════════
        //  NPC 好感度
        // ═══════════════════════════════════════════

        public int GetAffection(string npcId)
        {
            if (_npcStates.TryGetValue(npcId, out var state))
                return state.affection;
            return 0;
        }

        public void SetAffection(string npcId, int value)
        {
            value = Mathf.Clamp(value, 0, 5);
            EnsureNPCState(npcId).affection = value;

            // 同步到旧的 DialogueVariableStorage（DialogueManager 中使用）
            var dm = DialogueManager.Instance;
            if (dm != null && dm.variableStorage != null)
                dm.variableStorage.affection[npcId] = value;

            OnAffectionChanged?.Invoke(npcId, value);
            Debug.Log($"[Narrative] {npcId} 好感度 → {value}");
        }

        public void ChangeAffection(string npcId, int delta)
        {
            int current = GetAffection(npcId);
            SetAffection(npcId, current + delta);
        }

        // ═══════════════════════════════════════════
        //  NPC 章节
        // ═══════════════════════════════════════════

        public int GetChapter(string npcId)
        {
            if (_npcStates.TryGetValue(npcId, out var state))
                return state.chapter;
            return 0;
        }

        /// <summary>推进 NPC 人物线到下一章</summary>
        public void AdvanceChapter(string npcId)
        {
            var state = EnsureNPCState(npcId);
            int next = Mathf.Min(state.chapter + 1, 5); // 0→1→...→5
            if (next != state.chapter)
            {
                state.chapter = next;
                OnChapterAdvanced?.Invoke(npcId, next);
                Debug.Log($"[Narrative] {npcId} 章节推进 → 第{next}章");
            }
        }

        // ═══════════════════════════════════════════
        //  NPC 来访记录
        // ═══════════════════════════════════════════

        public string GetLastVisitDate(string npcId)
        {
            if (_npcStates.TryGetValue(npcId, out var state))
                return state.lastVisitDate;
            return "";
        }

        public int GetVisitCount(string npcId)
        {
            if (_npcStates.TryGetValue(npcId, out var state))
                return state.visitCount;
            return 0;
        }

        /// <summary>记录一次 NPC 来访</summary>
        public void RecordVisit(string npcId)
        {
            var state = EnsureNPCState(npcId);
            state.visitCount++;
            state.lastVisitDate = DateTime.Now.ToString("yyyy-MM-dd");
            
            // 成就系统：检查来访成就
            Core.AchievementManager.Instance?.CheckVisitAchievements(npcId, state.visitCount);
            Core.AchievementManager.Instance?.CheckNPCDepthAchievement(npcId, state.visitCount);
        }

        // ═══════════════════════════════════════════
        //  NPC 记忆
        // ═══════════════════════════════════════════

        public bool HasMemory(string npcId, string memoryKey)
        {
            if (_npcStates.TryGetValue(npcId, out var state))
                return state.memories.Contains(memoryKey);
            return false;
        }

        public void UnlockMemory(string npcId, string memoryKey)
        {
            var state = EnsureNPCState(npcId);
            if (!state.memories.Contains(memoryKey))
            {
                state.memories.Add(memoryKey);
                OnMemoryUnlocked?.Invoke($"{npcId}.{memoryKey}");
                Debug.Log($"[Narrative] {npcId} 记忆解锁: {memoryKey}");
            }
        }

        // ═══════════════════════════════════════════
        //  玩家选择
        // ═══════════════════════════════════════════

        public void RecordChoice(string eventId, int choiceIndex)
        {
            _playerChoices[eventId] = choiceIndex;
            Debug.Log($"[Narrative] 选择记录: {eventId} → 选项{choiceIndex}");
        }

        public int GetChoice(string eventId, int defaultIndex = -1)
        {
            if (_playerChoices.TryGetValue(eventId, out int idx))
                return idx;
            return defaultIndex;
        }

        public bool HasChoice(string eventId)
        {
            return _playerChoices.ContainsKey(eventId);
        }

        // ═══════════════════════════════════════════
        //  碎片收集
        // ═══════════════════════════════════════════

        public bool IsFragmentCollected(string fragmentId)
        {
            return _collectedFragments.Contains(fragmentId);
        }

        public void CollectFragment(string fragmentId)
        {
            if (_collectedFragments.Contains(fragmentId)) return;
            _collectedFragments.Add(fragmentId);
            OnFragmentCollected?.Invoke(fragmentId);
            Debug.Log($"[Narrative] 碎片收集: {fragmentId}");
        }

        public int CollectedFragmentCount => _collectedFragments.Count;
        public List<string> GetCollectedFragments() => new List<string>(_collectedFragments);

        // ═══════════════════════════════════════════
        //  对话节点 & 冷却
        // ═══════════════════════════════════════════

        public bool IsDialogueCompleted(string nodeId)
        {
            return _completedDialogues.Contains(nodeId);
        }

        public void MarkDialogueCompleted(string nodeId)
        {
            _completedDialogues.Add(nodeId);
        }

        public string GetDialogueCooldown(string nodeId)
        {
            _dialogueCooldowns.TryGetValue(nodeId, out var date);
            return date;
        }

        public void SetDialogueCooldown(string nodeId, string date)
        {
            _dialogueCooldowns[nodeId] = date;
        }

        /// <summary>检查对话是否在冷却中（返回 true 表示已冷却可触发）</summary>
        public bool IsDialogueOffCooldown(string nodeId, int cooldownDays = 0)
        {
            if (!_dialogueCooldowns.TryGetValue(nodeId, out var lastDate))
                return true; // 从未触发过，可触发

            if (cooldownDays <= 0) return false; // 永久冷却（一次性对话）

            if (DateTime.TryParse(lastDate, out var last))
            {
                int daysPassed = (int)(DateTime.Now - last).TotalDays;
                return daysPassed >= cooldownDays;
            }
            return true;
        }

        // ═══════════════════════════════════════════
        //  世界状态标志
        // ═══════════════════════════════════════════

        public void SetWorldFlag(string key, string value)
        {
            _worldFlags[key] = value;
        }

        public string GetWorldFlag(string key, string defaultValue = "")
        {
            _worldFlags.TryGetValue(key, out var value);
            return value ?? defaultValue;
        }

        public bool HasWorldFlag(string key) => _worldFlags.ContainsKey(key);

        // ═══════════════════════════════════════════
        //  存档序列化
        // ═══════════════════════════════════════════

        /// <summary>将当前叙事状态写入 SaveData</summary>
        public void SerializeToSave(SaveData save)
        {
            if (save == null) return;

            // NPC 状态
            save.npcs.Clear();
            foreach (var kv in _npcStates)
            {
                save.npcs.Add(new NPCSaveData
                {
                    npcId = kv.Key,
                    affection = kv.Value.affection,
                    chapter = kv.Value.chapter,
                    visitCount = kv.Value.visitCount,
                    lastVisitDate = kv.Value.lastVisitDate,
                    memories = new List<string>(kv.Value.memories),
                    completedChapters = new List<string>(kv.Value.completedChapters)
                });
            }

            // 碎片
            save.collectedFragments = new List<string>(_collectedFragments);

            // 对话历史
            save.completedDialogues = new List<string>(_completedDialogues);
            save.dialogueCooldowns.Clear();
            foreach (var kv in _dialogueCooldowns)
                save.dialogueCooldowns.Add(new StringPair { key = kv.Key, value = kv.Value });

            // 玩家选择
            save.worldState.Clear();
            foreach (var kv in _playerChoices)
                save.worldState.Add(new StringPair { key = $"choice.{kv.Key}", value = kv.Value.ToString() });

            // 世界标志
            foreach (var kv in _worldFlags)
                save.worldState.Add(new StringPair { key = $"flag.{kv.Key}", value = kv.Value });
        }

        /// <summary>从 SaveData 恢复叙事状态</summary>
        public void DeserializeFromSave(SaveData save)
        {
            if (save == null) return;

            // NPC 状态
            _npcStates.Clear();
            foreach (var npc in save.npcs)
            {
                _npcStates[npc.npcId] = new NPCNarrativeState
                {
                    affection = Mathf.RoundToInt(npc.affection),
                    chapter = npc.chapter,
                    visitCount = npc.visitCount,
                    lastVisitDate = npc.lastVisitDate,
                    memories = new List<string>(npc.memories),
                    completedChapters = new List<string>(npc.completedChapters)
                };
            }

            // 碎片
            _collectedFragments = new List<string>(save.collectedFragments);

            // 对话历史
            _completedDialogues = new HashSet<string>(save.completedDialogues);
            _dialogueCooldowns.Clear();
            foreach (var kv in save.dialogueCooldowns)
                _dialogueCooldowns[kv.key] = kv.value;

            // 玩家选择 & 世界标志
            _playerChoices.Clear();
            _worldFlags.Clear();
            foreach (var kv in save.worldState)
            {
                if (kv.key.StartsWith("choice."))
                {
                    string eventId = kv.key.Substring(7);
                    if (int.TryParse(kv.value, out int idx))
                        _playerChoices[eventId] = idx;
                }
                else if (kv.key.StartsWith("flag."))
                {
                    string flagKey = kv.key.Substring(5);
                    _worldFlags[flagKey] = kv.value;
                }
            }
        }

        // ═══════════════════════════════════════════
        //  同步桥接（与旧的 DialogueVariableStorage 双向同步）
        // ═══════════════════════════════════════════

        /// <summary>将 NarrativeState 中的好感度同步到 DialogueManager.variableStorage</summary>
        public void SyncToDialogueStorage()
        {
            var dm = DialogueManager.Instance;
            if (dm == null || dm.variableStorage == null) return;

            foreach (var kv in _npcStates)
                dm.variableStorage.affection[kv.Key] = kv.Value.affection;

            foreach (var kv in _worldFlags)
                dm.variableStorage.flags.Add(kv.Key);
        }

        /// <summary>从 DialogueManager.variableStorage 拉取数据</summary>
        public void SyncFromDialogueStorage()
        {
            var dm = DialogueManager.Instance;
            if (dm == null || dm.variableStorage == null) return;

            foreach (var kv in dm.variableStorage.affection)
                EnsureNPCState(kv.Key).affection = kv.Value;

            foreach (var flag in dm.variableStorage.flags)
                if (!_worldFlags.ContainsKey(flag))
                    _worldFlags[flag] = "true";
        }

        // ═══════════════════════════════════════════
        //  Debug
        // ═══════════════════════════════════════════

        public string GetStatusReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("══ 叙事状态 ══");
            foreach (var kv in _npcStates)
            {
                sb.AppendLine($"  {kv.Key}: 好感={kv.Value.affection} 章节={kv.Value.chapter}" +
                              $" 来访={kv.Value.visitCount}次");
            }
            sb.AppendLine($"  碎片: {_collectedFragments.Count} 个");
            sb.AppendLine($"  选择: {_playerChoices.Count} 个");
            sb.AppendLine($"  世界标志: {_worldFlags.Count} 个");
            return sb.ToString();
        }

        // ═══════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════

        private NPCNarrativeState EnsureNPCState(string npcId)
        {
            if (!_npcStates.TryGetValue(npcId, out var state))
            {
                state = new NPCNarrativeState();
                _npcStates[npcId] = state;
            }
            return state;
        }
    }

    /// <summary>单个 NPC 的叙事状态</summary>
    [System.Serializable]
    public class NPCNarrativeState
    {
        public int affection;
        public int chapter;
        public int visitCount;
        public string lastVisitDate = "";
        public List<string> memories = new List<string>();
        public List<string> completedChapters = new List<string>();
    }
}
