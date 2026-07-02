using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TeaMist.Gameplay;
using TeaMist.Dialogue;
using TeaMist.Rendering;
using TeaMist.UI;

namespace TeaMist.Core
{
    /// <summary>
    /// 场景自动搭建器 —— 当 Inspector 引用为空时，在代码中创建必要的 GameObject。
    /// 由 Bootstrap 在 devMode 下调用，让开发者无需手动拖拽即可看到运行效果。
    /// </summary>
    public static class SceneAutoSetup
    {
        public static void Setup(Bootstrap bootstrap)
        {
            if (!bootstrap.devMode) return;

            // 1. 茶馆场景控制器
            if (bootstrap.teaHouseScene == null)
            {
                var go = new GameObject("TeaHouseScene");
                bootstrap.teaHouseScene = go.AddComponent<TeaHouseSceneController>();
                Debug.Log("[SceneAutoSetup] 创建: TeaHouseSceneController");
            }

            // 2. UI Canvas（强制重建）
            CreateCanvasUI(bootstrap);

            // 3. 茶馆循环控制器
            if (bootstrap.teaShopLoop == null)
            {
                var go = new GameObject("TeaShopLoop");
                bootstrap.teaShopLoop = go.AddComponent<TeaShopLoop>();
                Debug.Log("[SceneAutoSetup] 创建: TeaShopLoop");
            }

            // 4. 测试场景
            CreateArtScene(bootstrap);
        }

        // ── Canvas & UI ────────────────────────────────────────────────

        private static void CreateCanvasUI(Bootstrap bootstrap)
        {
            // 强制清除场景中残留的旧 Canvas
            var existingCanvas = GameObject.Find("Canvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
                Debug.Log("[SceneAutoSetup] 已清除旧 Canvas");
            }
            bootstrap.dialogueUI = null;
            bootstrap.teaBrewingUI = null;

            // 创建 Canvas
            var canvasGo = new GameObject("Canvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // 调试文本
            CreateDebugLabel(canvasGo.transform);

            // 对话 UI
            if (bootstrap.dialogueUI == null)
            {
                var diagGo = CreateUIPanel("DialoguePanel", canvasGo.transform);
                bootstrap.dialogueUI = diagGo.AddComponent<DialogueUI>();
                bootstrap.dialogueUI.panelGroup = diagGo.GetComponent<CanvasGroup>();
                Debug.Log("[SceneAutoSetup] 创建: DialogueUI");
            }

            // 泡茶 UI
            if (bootstrap.teaBrewingUI == null)
            {
                var brewGo = CreateUIPanel("TeaBrewingPanel", canvasGo.transform);
                bootstrap.teaBrewingUI = brewGo.AddComponent<TeaBrewingUI>();
                bootstrap.teaBrewingUI.panelGroup = brewGo.GetComponent<CanvasGroup>();
                bootstrap.teaBrewingUI.panelGroup.alpha = 0f;
                bootstrap.teaBrewingUI.panelGroup.interactable = false;
                bootstrap.teaBrewingUI.panelGroup.blocksRaycasts = false;
                Debug.Log("[SceneAutoSetup] 创建: TeaBrewingUI");
            }

            // 茶谱收集 UI
            if (bootstrap.collectionUI == null)
            {
                var colGo = CreateUIPanel("CollectionPanel", canvasGo.transform);
                bootstrap.collectionUI = colGo.AddComponent<TeaRecipeCollectionUI>();
                bootstrap.collectionUI.panelGroup = colGo.GetComponent<CanvasGroup>();
                bootstrap.collectionUI.panelGroup.alpha = 0f;
                bootstrap.collectionUI.panelGroup.interactable = false;
                bootstrap.collectionUI.panelGroup.blocksRaycasts = false;
                Debug.Log("[SceneAutoSetup] 创建: TeaRecipeCollectionUI");
            }

            // 碎片通知
            var notifGo = new GameObject("FragmentNotification", typeof(RectTransform));
            notifGo.transform.SetParent(canvasGo.transform, false);
            var notifRt = notifGo.GetComponent<RectTransform>();
            notifRt.anchorMin = notifRt.anchorMax = new Vector2(0.5f, 0.5f);
            notifRt.pivot = new Vector2(0.5f, 0.5f);
            notifRt.anchoredPosition = new Vector2(0, 100f);
            notifRt.sizeDelta = new Vector2(700, 180);
            notifGo.AddComponent<CanvasGroup>();
            notifGo.AddComponent<FragmentNotification>();

            // 墨滴转场覆盖层
            var inkGo = new GameObject("InkBlotOverlay", typeof(RectTransform));
            inkGo.transform.SetParent(canvasGo.transform, false);
            var inkRt = inkGo.GetComponent<RectTransform>();
            inkRt.anchorMin = Vector2.zero;
            inkRt.anchorMax = Vector2.one;
            inkRt.offsetMin = Vector2.zero;
            inkRt.offsetMax = Vector2.zero;
            var inkImage = inkGo.AddComponent<Image>();
            inkImage.color = Color.white;
            inkImage.raycastTarget = false;
            var inkShader = Shader.Find("TeaMist/UI/InkBlotTransition");
            if (inkShader != null && inkShader.isSupported)
            {
                inkImage.material = new Material(inkShader);
                Debug.Log("[SceneAutoSetup] InkBlotTransition material 创建成功");
            }
            else
            {
                Debug.LogError($"[SceneAutoSetup] InkBlotTransition shader 不可用! found={inkShader!=null} supported={inkShader?.isSupported}");
                // 不设 material，Image 用默认 UI 材质（白色），不会洋红
            }
            bootstrap.inkBlotTransition = inkGo.AddComponent<InkBlotTransition>();

            // 设置面板
            if (bootstrap.settingsUI == null)
            {
                var settingsGo = CreateUIPanel("SettingsPanel", canvasGo.transform);
                bootstrap.settingsUI = settingsGo.AddComponent<SettingsUI>();
                bootstrap.settingsUI.panelGroup = settingsGo.GetComponent<CanvasGroup>();
            }

            Debug.Log("[SceneAutoSetup] Canvas UI 创建完成");
        }

        private static void CreateDebugLabel(Transform parent)
        {
            var go = new GameObject("DebugText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(600, 200);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = new Color(0.82f, 0.80f, 0.76f);
            text.text = "《茶烟起处》\n茶馆已开张 · 等待客人到来...";
        }

        private static GameObject CreateUIPanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            return go;
        }

        // ── 美术场景 ──────────────────────────────────────────────────

        private static void CreateArtScene(Bootstrap bootstrap)
        {
            Debug.Log("[SceneAutoSetup] CreateArtScene 开始");
            if (GameObject.Find("ArtSceneRoot") != null)
            {
                Debug.Log("[SceneAutoSetup] ArtSceneRoot 已存在，跳过场景创建");
                return;
            }

            ArtLoader.LoadAll();
            if (ArtLoader.LoadedCount == 0)
            {
                Debug.LogWarning("[SceneAutoSetup] ArtLoader 无图片，使用降级场景");
                CreateFallbackScene();
                return;
            }

            var root = new GameObject("ArtSceneRoot");
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5.4f;
                cam.transform.position = new Vector3(0, -0.3f, -10f);
                cam.transform.rotation = Quaternion.identity;
                cam.backgroundColor = new Color(0.93f, 0.91f, 0.87f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                Debug.Log($"[SceneAutoSetup] 相机已设置: bg={cam.backgroundColor}, clear={cam.clearFlags}");
            }
            else
            {
                Debug.LogError("[SceneAutoSetup] Camera.main 为 null!");
            }

            float camWidth = cam.orthographicSize * cam.aspect * 2f;
            float camHeight = cam.orthographicSize * 2f;

            // ── Layer BG_FAR: 远山 ──
            var hillsSprite = ArtLoader.Find("远山");
            if (hillsSprite != null)
            {
                var go = CreateSpriteGO("BG_Hills", hillsSprite, root.transform,
                    SortingLayers.Background, SortingLayers.OrderInLayer.BG_Far);
                go.AddComponent<ParallaxLayer>().parallaxFactor = 0.15f;
                float scale = camWidth / hillsSprite.bounds.size.x;
                go.transform.localScale = new Vector3(scale, scale * 0.45f, 1f);
                go.transform.position = new Vector3(0, 1.5f, 5f);
                go.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.75f);
            }

            // ── Layer BG_MID: 竹林 ──
            var zhuSprite = ArtLoader.Find("竹青");
            if (zhuSprite != null)
            {
                var go = CreateSpriteGO("BG_Bamboo_L", zhuSprite, root.transform,
                    SortingLayers.Background, SortingLayers.OrderInLayer.BG_Mid);
                go.AddComponent<ParallaxLayer>().parallaxFactor = 0.3f;
                go.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
                go.transform.position = new Vector3(-5.5f, -1f, 3f);
                go.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.5f);

                var go2 = CreateSpriteGO("BG_Bamboo_R", zhuSprite, root.transform,
                    SortingLayers.Background, SortingLayers.OrderInLayer.BG_Mid);
                go2.AddComponent<ParallaxLayer>().parallaxFactor = 0.3f;
                go2.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                go2.transform.position = new Vector3(6f, -0.5f, 3f);
                go2.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.4f);
            }

            // ── Layer MAIN: 茶馆内景 ──
            var teaHouseSprite = ArtLoader.Find("茶馆内景");
            if (teaHouseSprite != null)
            {
                var go = CreateSpriteGO("BG_TeaHouse", teaHouseSprite, root.transform, SortingLayers.MainScene);
                go.AddComponent<ParallaxLayer>().parallaxFactor = 0.6f;
                float scale = camHeight / teaHouseSprite.bounds.size.y;
                go.transform.localScale = new Vector3(scale * 1.1f, scale, 1f);
                go.transform.position = new Vector3(0, 0, 1f);
            }

            // ── Layer PROPS: 茶壶 + 蒸汽 ──
            var teawareSprite = ArtLoader.Find("茶壶");
            if (teawareSprite != null)
            {
                var go = CreateSpriteGO("Prop_Teaware", teawareSprite, root.transform, SortingLayers.Props);
                go.AddComponent<ParallaxLayer>().parallaxFactor = 0.75f;
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
                go.transform.position = new Vector3(0.2f, -3.2f, 0.2f);
                go.AddComponent<TeaSteamEffect>();
            }

            // ── C1: 窗外纸窗景色 ──
            var windowGo = new GameObject("WindowView");
            windowGo.transform.SetParent(root.transform, false);
            windowGo.transform.localPosition = new Vector3(0, 2.5f, 4f); // 位于远山和茶馆之间
            windowGo.AddComponent<WindowView>();
            Debug.Log("[SceneAutoSetup] 创建: WindowView（窗外纸窗景色）");

            // ── C3: 桌面小物件 ──
            var decoGo = new GameObject("TeaHouseDecoration");
            decoGo.transform.SetParent(root.transform, false);
            decoGo.transform.position = new Vector3(0, 0, 0.3f);
            decoGo.AddComponent<TeaHouseDecoration>();
            Debug.Log("[SceneAutoSetup] 创建: TeaHouseDecoration（香炉/花瓶/茶宠）");

            // ── C2: 竹帘卷帘（前景层）──
            var curtainGo = new GameObject("CurtainController");
            curtainGo.transform.SetParent(root.transform, false);
            curtainGo.transform.position = new Vector3(0, 0, -2f); // 前景，离相机最近
            var curtain = curtainGo.AddComponent<CurtainController>();
            Debug.Log("[SceneAutoSetup] 创建: CurtainController（竹帘）");

            // 将帘子引用传给茶馆场景控制器
            if (bootstrap.teaHouseScene != null)
            {
                bootstrap.teaHouseScene.curtainController = curtain;
            }

            // ── Layer CHARACTERS: 8 位 NPC（初始隐藏）──
            CreateNpcSprite("白露", "Char_BaiLu", root, SortingLayers.OrderInLayer.Char_Default,
                new Vector3(-4f, -2.5f, -0.3f), 0.85f);
            CreateNpcSprite("竹青", "Char_ZhuQing", root, SortingLayers.OrderInLayer.Char_Default,
                new Vector3(4f, -2.5f, -0.3f), 0.85f);
            CreateNpcSprite("当归", "Char_DangGui", root, SortingLayers.OrderInLayer.Char_Default,
                new Vector3(3.5f, -2.5f, -0.3f), 0.85f);
            CreateNpcSprite("云鹤老", "Char_Yunhelao", root, SortingLayers.OrderInLayer.Char_Behind,
                new Vector3(-5.5f, -2.2f, -0.4f), 0.82f, 3.0f);
            CreateNpcSprite("小山", "Char_Xiaoshan", root, SortingLayers.OrderInLayer.Char_Behind,
                new Vector3(1.5f, -3.8f, 0.1f), 1.0f, 3.0f);
            CreateNpcSprite("青岚", "Char_QingLan", root, SortingLayers.OrderInLayer.Char_Default,
                new Vector3(-3f, -2.5f, -0.3f), 0.85f);
            CreateNpcSprite("寒露", "Char_HanLu", root, SortingLayers.OrderInLayer.Char_Default,
                new Vector3(0f, -2.3f, -0.3f), 0.88f);
            CreateNpcSprite("樵翁", "Char_QiaoWeng", root, SortingLayers.OrderInLayer.Char_Behind,
                new Vector3(-5f, -2.8f, -0.4f), 0.82f, 3.0f);

            // 相机特效
            if (cam != null)
            {
                var breath = cam.gameObject.AddComponent<CameraBreath>();
                breath.amplitude = 0.15f;
                breath.period = 8f;
                cam.gameObject.AddComponent<SeasonalParticles>();
            }

            Debug.Log($"[SceneAutoSetup] 水墨场景完成 ({ArtLoader.LoadedCount} 精灵)");

            // 立绘管理器
            var charMgr = root.AddComponent<CharacterSpriteManager>();
            charMgr.spriteRoot = root.transform;
        }

        private static void CreateNpcSprite(string artName, string goName, GameObject root,
            int orderInLayer, Vector3 position, float parallaxFactor, float targetHeight = 3.5f)
        {
            var sprite = ArtLoader.Find(artName);
            if (sprite == null)
            {
                Debug.LogWarning($"[SceneAutoSetup] 未找到 '{artName}' 立绘");
                return;
            }
            var go = CreateSpriteGO(goName, sprite, root.transform,
                SortingLayers.Characters, orderInLayer);
            go.AddComponent<ParallaxLayer>().parallaxFactor = parallaxFactor;
            float scale = targetHeight / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = position;
            go.SetActive(false);
        }

        private static GameObject CreateSpriteGO(string name, Sprite sprite, Transform parent,
            string sortingLayer, int orderInLayer = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = orderInLayer;
            return go;
        }

        private static void CreateFallbackScene()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            var root = new GameObject("ArtSceneRoot");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "FallbackGround";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(3, 1, 3);
            ground.GetComponent<MeshRenderer>().material = new Material(urpLit)
                { color = new Color(0.62f, 0.58f, 0.52f) };

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 5f, -7f);
                cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                cam.backgroundColor = new Color(0.92f, 0.90f, 0.85f);
            }
        }
    }
}
