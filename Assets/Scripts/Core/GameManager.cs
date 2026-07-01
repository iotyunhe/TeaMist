using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TeaMist.Gameplay;

namespace TeaMist.Core
{
    /// <summary>
    /// 游戏主管理器 — 游戏生命周期控制
    /// - 启动流程：新游戏 / 读档
    /// - 场景切换 + 水墨过渡
    /// - 全局事件总线
    /// - 暂停/恢复
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("场景")]
        public string mainMenuScene = "MainMenu";
        public string teaHouseScene = "TeaHouse";
        public string loadingScene  = "Loading";

        [Header("游戏设置")]
        public int targetFrameRate = 60;
        public bool vSync = true;

        // ── 游戏状态 ──
        public GameState CurrentState { get; private set; } = GameState.Booting;
        public bool IsNewGame { get; private set; }
        public int CurrentSaveSlot { get; private set; } = 1;

        // ── 全局事件 ──
        public event Action<GameState> OnStateChanged;
        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action<int> OnDayAdvanced; // dayIndex

        private bool _isPaused = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
        }

        private void Start()
        {
            SetState(GameState.MainMenu);
        }

        // ── 游戏流程控制 ──

        /// <summary>开始新游戏（完整流程含场景加载）</summary>
        public void NewGame()
        {
            IsNewGame = true;
            CurrentSaveSlot = 1;
            InitializeNewGameState();
            LoadTeaHouseScene();
        }

        /// <summary>
        /// 初始化新游戏状态（不加载场景）。
        /// 清空所有运行时数据，设置初始茶谱。
        /// </summary>
        public void InitializeNewGameState()
        {
            IsNewGame = true;

            // 初始化时间
            if (TimeManager.Instance != null)
                TimeManager.Instance.Initialize(null);

            // 清空并解锁初始茶谱
            if (DataManager.Instance != null)
            {
                DataManager.Instance.UnlockedTeaRecipes.Clear();
                DataManager.Instance.CollectedFragments.Clear();
                DataManager.Instance.CompletedDialogues.Clear();
                DataManager.Instance.UnlockTeaRecipe("清心茶");
                DataManager.Instance.UnlockTeaRecipe("桂花蜜茶");
            }

            // 清空叙事状态
            NarrativeStateManager.Instance?.DeserializeFromSave(new SaveData());

            TeaHouseSceneController.Instance?.OpenShop();

            Debug.Log("[GameManager] 新游戏状态已初始化");
        }

        /// <summary>继续游戏（读档，完整流程含场景加载）</summary>
        public void ContinueGame(int slot = -1)
        {
            if (slot < 0) slot = SaveManager.GetLastUsedSlot();

            var save = SaveManager.Load(slot);
            if (save == null)
            {
                Debug.LogWarning("[GameManager] 存档不存在，开始新游戏");
                NewGame();
                return;
            }

            IsNewGame = false;
            CurrentSaveSlot = slot;
            RestoreFromSave(save);
            LoadTeaHouseScene();
        }

        /// <summary>
        /// 从存档恢复所有状态（不加载场景，适用于当前已在茶馆场景中的读档）。
        /// 恢复：TimeManager / DataManager / DialogueManager / NarrativeStateManager
        /// </summary>
        public void RestoreFromSave(SaveData save)
        {
            IsNewGame = false;

            if (TimeManager.Instance != null)
                TimeManager.Instance.Initialize(save);

            if (DataManager.Instance != null)
                DataManager.Instance.LoadFromSave(save);

            Dialogue.DialogueManager.Instance?.variableStorage.DeserializeFromSave(save);

            NarrativeStateManager.Instance?.DeserializeFromSave(save);
            NarrativeStateManager.Instance?.SyncToDialogueStorage();

            TeaHouseSceneController.Instance?.OpenShop();

            Debug.Log($"[GameManager] 状态已从存档恢复 — 第 {save.totalDaysPlayed} 天, {save.currentSeason}");
        }

        /// <summary>保存游戏</summary>
        public void SaveGame(int slot = -1)
        {
            if (slot < 0) slot = CurrentSaveSlot;

            var save = new SaveData
            {
                saveName = IsNewGame ? "新旅程" : "继续前行"
            };

            // 收集时间数据
            if (TimeManager.Instance != null)
            {
                save.lastPlayDate = DateTime.Now.ToString("yyyy-MM-dd");
                save.totalDaysPlayed = TimeManager.Instance.TotalDaysPlayed;
                save.currentSeason = TimeManager.Instance.CurrentSeason;
                save.dayInSeason = TimeManager.Instance.DayInSeason;
                save.currentWeather = TimeManager.Instance.CurrentWeather;
                save.currentTimeSlot = TimeManager.Instance.CurrentTimeSlot;
            }

            // 收集数据管理器状态
            DataManager.Instance?.SaveToSave(save);

            // 收集对话变量存储（好感度/flag/玩家特质）
            Dialogue.DialogueManager.Instance?.variableStorage.SerializeToSave(save);

            // 收集叙事状态
            NarrativeStateManager.Instance?.SerializeToSave(save);

            SaveManager.Save(save, slot);
            CurrentSaveSlot = slot;
        }

        /// <summary>快速存档到上一槽位</summary>
        public void QuickSave() => SaveGame(CurrentSaveSlot);

        /// <summary>返回主菜单</summary>
        public void ReturnToMainMenu()
        {
            SaveGame();
            SetState(GameState.MainMenu);
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>推进一天</summary>
        public void AdvanceDay()
        {
            TimeManager.Instance?.AdvanceOneDay();
            SaveGame();
            OnDayAdvanced?.Invoke(TimeManager.Instance?.TotalDaysPlayed ?? 0);
        }

        // ── 暂停/恢复 ──

        public void PauseGame()
        {
            if (_isPaused) return;
            _isPaused = true;
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
        }

        public void ResumeGame()
        {
            if (!_isPaused) return;
            _isPaused = false;
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }

        public bool IsPaused() => _isPaused;

        // ── 场景 ──

        private void LoadTeaHouseScene()
        {
            SetState(GameState.Loading);
            SceneManager.LoadScene(teaHouseScene);
            SetState(GameState.Playing);
        }

        // ── 状态 ──

        private void SetState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 状态: {newState}");
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && CurrentState == GameState.Playing)
            {
                QuickSave();
            }
        }

        private void OnApplicationQuit()
        {
            if (CurrentState == GameState.Playing)
            {
                QuickSave();
            }
        }
    }

    public enum GameState
    {
        Booting,
        MainMenu,
        Loading,
        Playing,
        Paused,
        Dialoguing // 对话中（防止意外操作）
    }
}
