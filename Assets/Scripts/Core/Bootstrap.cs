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
        public SaveLoadUI saveLoadUI;

        // 调试面板状态
        private GUIStyle _debugGuiStyle;
        private GUIStyle _debugBoxStyle;
        private GUIStyle _debugButtonStyle;
        private int _weatherCycleIndex;

        private void Awake()
        {
            EnsureSingleton<GameManager>("GameManager");
            EnsureSingleton<DataManager>("DataManager");
            EnsureSingleton<TimeManager>("TimeManager");
            EnsureSingleton<SeasonManager>("SeasonManager");
            EnsureSingleton<NPCScheduleManager>("NPCScheduleManager");
            EnsureSingleton<TeaHouseManager>("TeaHouseManager");
            EnsureSingleton<ShopSynergyManager>("ShopSynergyManager");
            EnsureSingleton<InkRenderSettings>("InkRenderSettings");
            EnsureSingleton<InkSpriteMaterial>("InkSpriteMaterial");
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

            EnsureDebugStyles();

            float btnW = 200, btnH = 30, gap = 5;
            float x = Screen.width - btnW - 12;
            float y = 12;

            var weatherName = WeatherManager.Instance != null
                ? WeatherManager.WeatherName(WeatherManager.Instance.CurrentWeather) : "-";
            bool isIdle = TeaShopLoop.Instance?.GetCurrentState() == TeaShopLoop.ShopState.Idle;

            GUI.Box(new Rect(x - 4, y - 4, btnW + 8, 70 + 8), "", _debugBoxStyle);
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
                    isIdle ? $"> {DataManager.GetNpcDisplayName(npcId)}来访" : $"  {DataManager.GetNpcDisplayName(npcId)}（等待中）",
                    _debugButtonStyle))
                    TryForceVisit(npcId);
                GUI.enabled = enabled;
                y += btnH + gap;
            }

            y += gap;
            DrawButton("跳过一天",    x, ref y, btnW, btnH, () => TimeManager.Instance?.AdvanceOneDay());
            DrawButton("茶谱图鉴",    x, ref y, btnW, btnH, () => collectionUI?.Toggle());
            DrawButton("\ud83d\udcc1 存档", x, ref y, btnW, btnH, () => saveLoadUI?.Toggle());
            DrawButton("\u2699 设置", x, ref y, btnW, btnH, () => settingsUI?.Toggle());
            DrawButton("切换天气",    x, ref y, btnW, btnH, CycleWeather);
            DrawButton("重置状态",    x, ref y, btnW, btnH,
                () => TeaShopLoop.Instance?.EndCustomerVisitPublic());

            // 快捷存档提示（由 SaveLoadUI.F5 实现）
        }

        private void EnsureDebugStyles()
        {
            if (_debugGuiStyle != null) return;

            _debugGuiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.94f, 0.90f, 0.82f) }
            };

            _debugBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeInkTexture(32, 32, new Color(0.12f, 0.10f, 0.08f, 0.82f), 0.08f) }
            };

            _debugButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeInkTexture(32, 32, new Color(0.22f, 0.18f, 0.13f, 0.85f), 0.06f), textColor = new Color(0.94f, 0.90f, 0.82f) },
                hover = { background = MakeInkTexture(32, 32, new Color(0.30f, 0.24f, 0.17f, 0.90f), 0.06f), textColor = new Color(1f, 0.96f, 0.88f) },
                active = { background = MakeInkTexture(32, 32, new Color(0.15f, 0.12f, 0.09f, 0.95f), 0.06f), textColor = new Color(0.85f, 0.82f, 0.74f) },
                onNormal = { background = MakeInkTexture(32, 32, new Color(0.22f, 0.18f, 0.13f, 0.85f), 0.06f), textColor = new Color(0.94f, 0.90f, 0.82f) }
            };
        }

        private Texture2D MakeInkTexture(int width, int height, Color baseColor, float noiseStrength)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.15f + 0.1f, y * 0.15f + 0.1f) - 0.5f;
                    Color c = baseColor;
                    c.r += n * noiseStrength;
                    c.g += n * noiseStrength;
                    c.b += n * noiseStrength;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private void DrawButton(string label, float x, ref float y,
            float w, float h, System.Action action)
        {
            if (GUI.Button(new Rect(x, y, w, h), label, _debugButtonStyle))
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
