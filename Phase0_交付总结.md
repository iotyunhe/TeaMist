# Phase 0 交付总结

## 完成时间
2026-06-24

## 产出清单

### 0.1 工程目录结构
完整的 Unity 2022.3 URP 项目骨架，包含：
- `Assets/Scripts/Core/` — 核心管理器
- `Assets/Scripts/Data/` — ScriptableObject 数据定义
- `Assets/Scripts/Gameplay/`, `UI/`, `Utils/` — 待填充
- `Assets/Shaders/InkRender/` — 6 套水墨 Shader
- `Assets/Shaders/PostProcess/` — 1 套全屏后处理整合
- `Assets/Yarn/` — 对话脚本目录 (Characters/DailyStories/MainStory)
- `Assets/Spine/`, `Audio/`, `Textures/`, `ScriptableObjects/` — 资源目录
- `Packages/manifest.json` — URP + Yarn Spinner + Addressables + Localization

### 0.2 水墨 Shader 体系 (7 个 .shader)
| Shader | 功能 | 技术点 |
|--------|------|--------|
| `InkTone` | 单层墨色五阶染色 | 灰度→五色阶梯映射 + 笔触噪点 + 基础光照 |
| `InkWash` | 多层水墨晕染混合 | 3层遮罩融合 + 枯笔飞白 + 湿笔渗透 + 宣纸纹理 |
| `SeasonTint` | 四季全局调色 | RGB↔HSV 色相偏移 + 暖色叠加 + 灰度保留 |
| `InkEdge` | 水墨边缘描边 | Roberts Cross 深度法线双检测 + 笔触粗细变化 + 飞白 |
| `InkVignette` | 画面暗角聚气 | 圆度自适应 + 纸质泛黄 + 墨色渗透边缘 |
| `InkSmoke` | 茶烟/云雾粒子 | 3层噪声叠加 + 湍流偏移 + 升腾高度衰减 + 渐变映射 |
| `InkMasterCombine` | 全屏后处理整合 | 5Pass合一：SeasonTint→InkTone→InkEdge→InkVignette→PaperAge |

全部使用 URP 14.x 的 Core.hlsl + Lighting.hlsl，兼容 Unity 2022.3 LTS。

### 0.3 核心数据模型 (6 个 ScriptableObject)
| SO 类 | 文件 | 关键字段 |
|-------|------|---------|
| `TeaRecipeSO` | `TeaRecipeSO.cs` | 茶名/六维属性/泡制参数/解锁条件/情绪加成 |
| `NPCProfileSO` | `NPCProfileSO.cs` | 身份/日程偏好/六维口味/关系网/故事线/碎片产出 |
| `FragmentSO` | `FragmentSO.cs` | 碎片类型/章节/获取条件/连锁解锁/经营关联 |
| `ShopPropertySO` | `ShopPropertySO.cs` | 四业共享经营属性/气质标签/名声/升级/装饰 |
| `SeasonConfigSO` | `SeasonConfigSO.cs` | 四季节气/Shader参数/天气概率/可采资源/限定内容 |
| `DialogueConfigSO` | `DialogueConfigSO.cs` | Yarn节点映射/触发条件/优先级/回报/冷却 |

所有 SO 支持 `[CreateAssetMenu]`，策划可在 Unity Inspector 中直接创建和编辑。

### 0.4 核心管理器 (5 个 C# 脚本)
| 管理器 | 文件 | 职责 |
|--------|------|------|
| `GameManager` | `GameManager.cs` | 游戏生命周期/新游戏/读档/存档/暂停/场景切换 |
| `DataManager` | `DataManager.cs` | SO加载+运行时数据+内容解锁+存档桥接 |
| `TimeManager` | `TimeManager.cs` | 真实时间流/四季昼夜判定/离线天数处理/每日随机种子 |
| `SaveManager` | `SaveManager.cs` | 5槽位JSON存档+PlayerPrefs设置+版本迁移 |
| `SaveData` | `SaveData.cs` | 完整存档结构（玩家/店铺/NPC/碎片/世界状态） |
| `Bootstrap` | `Bootstrap.cs` | 启动引导/单例确保/开发模式 |
| `InkRenderFeature` | `InkRenderFeature.cs` | URP Renderer Feature/全屏后处理注入/季节参数绑定 |

## 项目路径
```
C:\Users\admin\WorkBuddy\2026-06-23-17-23-39\TeaMist\
```

## 下一步（Phase 1）
Phase 1 将实现核心循环跑通：
- 茶馆场景 + 四季切换
- Yarn Spinner 对话系统 + 白露剧本
- 5 步泡茶交互 + 匹配算法
- 完整闭环验证
