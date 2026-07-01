# Phase 1 交付总结 · 核心循环

## 文件清单

| 类别 | 文件数 | 关键产出 |
|------|--------|----------|
| **Core** | 8 个 .cs | Bootstrap/GameManager/DataManager/TimeManager/SaveManager/InkRenderFeature/InkRenderPass/SaveData |
| **Gameplay** | 5 个 .cs | SeasonManager/TeaHouseSceneController/TeaBrewingManager/TeaMatchAlgorithm/TeaShopLoop |
| **Dialogue** | 1 个 .cs | DialogueManager（Yarn 剧本解析+变量存储+分支驱动） |
| **UI** | 2 个 .cs | DialogueUI（卷轴对话框+打字机效果）/ TeaBrewingUI（5步泡茶UI） |
| **Shader** | 7 个 .shader | InkTone/InkWash/SeasonTint/InkEdge/InkVignette/InkSmoke/InkMasterCombine |
| **Yarn** | 1 个 .yarn | 白露首次来访完整剧本（3种迎客+泡茶分支+隐藏路径） |
| **Data** | 1 个 .cs (已更新) | TeaRecipeSO 新增匹配方法+FlavorType/TeaSeason枚举 |

**总计：22 个 C# · 7 个 Shader · 1 个 Yarn 剧本**

## 核心系统架构

```
Bootstrap (启动引导)
    ├─ GameManager (游戏状态)
    ├─ TimeManager (时间流)
    ├─ SeasonManager (季节/天气)
    ├─ DataManager (数据仓库)
    ├─ SaveManager (存档)
    ├─ InkRenderFeature (水墨渲染管线)
    ├─ TeaHouseSceneController (茶馆场景)
    ├─ DialogueManager (对话系统)
    │   ├─ DialogueUI (卷轴对话视图)
    │   └─ DialogueVariableStorage (变量存储)
    ├─ TeaBrewingManager (泡茶交互)
    │   ├─ TeaBrewingUI (5步 UI)
    │   └─ TeaMatchAlgorithm (匹配算法)
    └─ TeaShopLoop (核心循环)
        客人进店 → 对话 → 泡茶 → 分支 → 碎片 → 离店
```

## 白露首次来访流程

1. 茶馆开门（TimeManager 推进到上午）
2. 白露推门（TeaShopLoop 触发）
3. 3 种迎客方式 → DialogueManager 解析 .yarn
4. << tea >> 指令 → TeaBrewingManager 接管
5. 5 步泡茶 → TeaMatchAlgorithm 打分
6. 完美/不错/勉强 分支 → 对话继续
7. << drop fragment >> → 碎片掉落
8. 温暖/平静/沉默 结局 → 白露离店

## 下一步 Phase 2

NPC 生态：日程系统 + 竹青剧本 + 多条人物线 + 八卦消息池
