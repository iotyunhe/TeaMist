using UnityEngine;
using TeaMist.Gameplay;
using TeaMist.Dialogue;
using TeaMist.NPC;
using TeaMist.Rendering;
using TeaMist.UI;
using TeaMist.Data;

namespace TeaMist.Core
{
    /// <summary>
    /// 游戏启动引导器 —— 挂在首批加载的场景中，按顺序初始化所有核心管理器。
    /// 
    /// 初始化顺序：
    ///   SaveManager → DataManager → TimeManager → SeasonManager
    ///   → 场景对象（TeaHouseScene/TeaShopLoop/DialogueUI/BrewingUI）
    ///   → 初始化完成 → 开始日循环
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Bootstrap : MonoBehaviour
    {
        [Header("━━━ 启动配置 ━━━")]
        [Tooltip("开发模式：跳过主菜单，直入茶馆")]
        public bool devMode = true;

        [Tooltip("跳过首日引导，直接开始自由游玩")]
        public bool skipIntro = false;

        [Header("━━━ 引用 ━━━")]
        public TeaHouseSceneController teaHouseScene;
        public DialogueUI dialogueUI;
        public TeaBrewingUI teaBrewingUI;
        public TeaShopLoop teaShopLoop;
        public TeaRecipeCollectionUI collectionUI;
        public InkBlotTransition inkBlotTransition;
        public SettingsUI settingsUI;

        // 调试面板状态
        private GUIStyle _debugGuiStyle;
        private float _saveToastTime = -99f;
        private int _weatherCycleIndex;

        private void Awake()
        {
            EnsureSingleton<GameManager>("GameManager");
            EnsureSingleton<DataManager>("DataManager");
            EnsureSingleton<TimeManager>("TimeManager");
            EnsureSingleton<SeasonManager>("SeasonManager");
            EnsureSingleton<NPCScheduleManager>("NPCScheduleManager");
            EnsureSingleton<TeaHouseManager>("TeaHouseManager");
            EnsureSingleton<InkRenderSettings>("InkRenderSettings");
            EnsureSingleton<DialogueManager>("DialogueManager");
            EnsureSingleton<TeaBrewingManager>("TeaBrewingManager");
            EnsureSingleton<WeatherManager>("WeatherManager");
            EnsureSingleton<NarrativeStateManager>("NarrativeStateManager");
            EnsureSingleton<AudioManager>("AudioManager");

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            FontManager.Initialize();

            // 立即设置相机背景，避免 SceneAutoSetup 协程未执行时出现洋红背景
            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.93f, 0.91f, 0.87f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            Debug.Log("[Bootstrap] 核心管理器初始化完成");
        }

        private void Start()
        {
            StartCoroutine(DeferredInit());
        }

        private System.Collections.IEnumerator DeferredInit()
        {
            yield return null; // 等一帧确保 Awake 完成

            WireUpReferences();
            SceneAutoSetup.Setup(this);
            WireUpReferences(); // 二次连接：场景创建后重新绑定

            if (devMode)
            {
                Debug.Log("[Bootstrap] 开发模式：进入茶馆");
                if (TimeManager.Instance != null)
                    TimeManager.Instance.timeScale = 10f;

                if (SaveManager.HasAnySave())
                {
                    var save = SaveManager.Load(SaveManager.GetLastUsedSlot());
                    if (save != null) GameManager.Instance?.RestoreFromSave(save);
                }
                else
                {
                    GameManager.Instance?.InitializeNewGameState();
                }

                TeaHouseSceneController.Instance?.OpenShop();
            }

            Debug.Log("[Bootstrap] 启动完成。茶烟袅袅升起。");
        }

        // ── 引用连接 ────────────────────────────────────────────────

        private void WireUpReferences()
        {
            // devMode 下 SceneAutoSetup 已创建所有对象，此处仅做二次确认
            // 非 devMode 下 Inspector 应已配置所有引用
            if (teaHouseScene == null)
                Debug.LogWarning("[Bootstrap] teaHouseScene 未配置，NPC 场景控制不可用");
            if (teaShopLoop == null)
                Debug.LogWarning("[Bootstrap] teaShopLoop 未配置，茶馆循环不可用");
            if (collectionUI == null)
                Debug.LogWarning("[Bootstrap] collectionUI 未配置，茶谱收集不可用");

            if (DialogueManager.Instance != null && dialogueUI != null)
                DialogueManager.Instance.dialogueUI = dialogueUI;

            Debug.Log($"[Bootstrap] 场景引用: Scene={teaHouseScene!=null} " +
                $"DlgUI={dialogueUI!=null} BrewUI={teaBrewingUI!=null} Shop={teaShopLoop!=null}");
        }

        private static void EnsureSingleton<T>(string objName) where T : Component
        {
            if (Object.FindObjectOfType<T>() == null)
            {
                var go = new GameObject(objName);
                go.AddComponent<T>();
                Object.DontDestroyOnLoad(go);
                Debug.Log($"[Bootstrap] 创建单例: {objName}");
            }
        }

        // ── 开发调试面板 ────────────────────────────────────────────

        void OnGUI()
        {
            if (!devMode) return;

            if (_debugGuiStyle == null)
            {
                _debugGuiStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }

            float btnW = 200, btnH = 28, gap = 4;
            float x = Screen.width - btnW - 10;
            float y = 10;

            var weatherName = WeatherManager.Instance != null
                ? WeatherManager.WeatherName(WeatherManager.Instance.CurrentWeather) : "-";
            bool isIdle = TeaShopLoop.Instance?.GetCurrentState() == TeaShopLoop.ShopState.Idle;

            GUI.Label(new Rect(x, y, btnW, 70),
                $"茶烟起处 · 调试面板\n" +
                $"天气:{weatherName} 状态:{(isIdle ? "空闲" : "忙")}\n" +
                $"客人:{TeaShopLoop.Instance?.GetCurrentNPC() ?? "无"}\n" +
                $"碎片:{NarrativeStateManager.Instance?.CollectedFragmentCount ?? 0}",
                _debugGuiStyle);
            y += 75;

            // NPC 来访按钮
            var npcIds = new[] { "bailu", "zhuqing", "danggui", "yunhelao", "xiaoshan", "qinglan", "hanlu", "qiaoweng" };
            foreach (var npcId in npcIds)
            {
                bool enabled = GUI.enabled;
                GUI.enabled = isIdle;
                if (GUI.Button(new Rect(x, y, btnW, btnH),
                    isIdle ? $"> {DataManager.GetNpcDisplayName(npcId)}来访" : $"  {DataManager.GetNpcDisplayName(npcId)}（等待中）"))
                    TryForceVisit(npcId);
                GUI.enabled = enabled;
                y += btnH + gap;
            }

            y += gap;
            DrawButton("跳过一天",    x, ref y, btnW, btnH, () => TimeManager.Instance?.AdvanceOneDay());
            DrawButton("茶谱图鉴",    x, ref y, btnW, btnH, () => collectionUI?.Toggle());
            DrawButton("\u2699 设置", x, ref y, btnW, btnH, () => settingsUI?.Toggle());
            DrawButton("切换天气",    x, ref y, btnW, btnH, CycleWeather);
            DrawButton("重置状态",    x, ref y, btnW, btnH,
                () => TeaShopLoop.Instance?.EndCustomerVisitPublic());

            GUI.color = new Color(0.7f, 1f, 0.7f);
            DrawButton("\ud83d\udcbe 保存进度", x, ref y, btnW, btnH, () =>
            {
                GameManager.Instance?.SaveGame();
                _saveToastTime = Time.time;
            });
            GUI.color = Color.white;

            GUI.color = new Color(0.7f, 0.85f, 1f);
            DrawButton("\ud83d\udcc2 从存档继续", x, ref y, btnW, btnH, () =>
            {
                if (SaveManager.HasAnySave())
                    GameManager.Instance?.RestoreFromSave(
                        SaveManager.Load(SaveManager.GetLastUsedSlot()));
            });
            GUI.color = Color.white;

            if (Time.time - _saveToastTime < 1.5f)
                GUI.Label(new Rect(x, y, btnW, 20), "\u2713 已保存", _debugGuiStyle);
        }

        private void DrawButton(string label, float x, ref float y,
            float w, float h, System.Action action)
        {
            if (GUI.Button(new Rect(x, y, w, h), label))
                action();
            y += h + 4;
        }

        private void TryForceVisit(string npcId)
        {
            var ts = TeaShopLoop.Instance;
            if (ts != null && ts.GetCurrentState() == TeaShopLoop.ShopState.Idle)
                ts.ForceCustomerVisit(npcId);
        }

        private void CycleWeather()
        {
            var wm = WeatherManager.Instance;
            if (wm == null) return;
            var weathers = new[] { WeatherType.晴, WeatherType.多云, WeatherType.雨,
                                    WeatherType.雾, WeatherType.风, WeatherType.雪 };
            _weatherCycleIndex = (_weatherCycleIndex + 1) % weathers.Length;
            wm.ForceWeather(weathers[_weatherCycleIndex]);
        }
    }
}
