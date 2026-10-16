# 实验室原画接棒笔记

本轮接入的源文件是 `Assets/Arts/Psds/游玩主界面.psd`。正式运行入口仍为 `Assets/Scenes/Main.unity`，开局、商店、科研和结算继续经过原有 `TheCallPresentation`。原 PSD 未改写。

## 画面和玩法的对应

| 画面位置 | 现有玩法 | 场景与代码入口 |
| --- | --- | --- |
| 左上房间 | 第一组培育槽，默认两个亲本 | `OperationScreenView.breedingSlots`，`LabSceneView.Render` |
| 上层中间卷帘房 | 第二组培育槽，研究“槽位扩容”后开放 | `LabRoom2Lock`、`LabArtStage/Room2Interior` |
| 两间房的基因面板 | 培育基因预约和取回 | `breedingSkills`，保留 `SkillChipView` 与 `OperationPayloadDrag` |
| 左上绿色加号 | 研究“基因仓储”，容量由三个变成四个 | 原 `addGeneSlotButton` |
| 下层大房间 | 提取槽，默认五格，员工证工具开放第六格 | `extractionSlots`，仍按格子顺序结算 |
| 右上柜子 | 怪物笼，两列两层，一怪一笼，整排升降 | `LabCageLift`、`LabSceneView.cageScroll` 与 `MonsterInventory` |
| 右侧锯齿嘴 | 废弃怪物并随机取得一个基因 | 原 `OperationZone` 的 `Discard` 落点，成功后调用 `LabRecycler.Consume` |
| 右下柜子 | 三个基因格及第四个扩容格 | `skillChips` |
| 笼柜右上与右下箭头 | 怪物笼向上或向下翻动两排 | `LabPreviousCagePage`、`LabNextCagePage`，`PageCage` |
| 表盘与骷髅罐 | 显示已经演出到的能量 | `currentNumber`、`LabSceneView.EnergyShown` |

辅助图中上层的“生产区”在规则术语中对应“培育槽”。下层“产能区”对应“提取槽”。美术接入沿用这些规则定义。

研究“大乱炖”后，每间已开放房间显示三个亲本。`LabSceneView.Render` 按 `BreedingPlansQuery` 的组和亲本数量安排平铺索引，不能固定用前三格表示第一间房。默认两个亲本时，第二间房从索引 2 开始。

## 素材清单

`Assets/Arts/Lab/manifest.json` 记录 PSD 的 SHA-256、图层编号、原名和左上角像素坐标。PNG 使用双线性过滤、无 mipmap、无压缩，最大尺寸 2048。底图按 1920 × 1080 保持比例，画布缩放使用 Expand，操作和科研界面居中。

| 导出文件 | 来源 |
| --- | --- |
| `LabShell.png` | 保留可见场景，排除老板和本轮独立动画层 |
| `LabFrame.png` | PSD 的“图层 9”，放在光斑之上遮住边缘 |
| `LightBreeding1/2.png` | “图层 31”和“图层 29” |
| `LightProduction1/2.png` | “图层 34”和“图层 35” |
| `CollectorGlow.png` | “图层 36” |
| `CollectorNeedle.png` | “图层 38” |
| `RecyclerGlow.png` | “图层 37” |
| `RecyclerUpperJaw.png`、`RecyclerLowerJaw.png` | 原画嘴部区域，分成上下两半 |
| `Room2Interior.png` | 原画左上房间内壁的局部裁切 |
| `ProductionPlatform.png` | 旧素材 `Assets/Art/Ui/BgProduction.png` 中的传送带局部 |
| `BossReserved.png` | PSD 的“图层 6”，场景中禁用，留给后续教程 |

## 等待补图的项目

| 临时处理 | 正式素材要求 | 替换位置 |
| --- | --- | --- |
| 产能区平台复用旧传送带 | 单格透明底平台，建议约 150 × 38，怪物脚底在平台上沿 | 每个提取槽下的 `LabProductionPlatform` 的 Image Sprite。保留槽根、`MonsterSlotView`、`MonsterPortrait` 和拖放组件 |
| 第二间房内壁复用第一间房 | 打开卷帘后的独立内壁，640 × 278，匹配原画坐标 x836、y200 | `LabArtStage/Room2Interior`，保留 CanvasGroup 和 `room2Interior` 引用 |
| 回收器用一张锯齿嘴拆颚 | 闭嘴和张嘴的透明图或上下颚图。保持约 257 × 74 的口部包围框 | `废弃回收/UpperJaw` 与 `LowerJaw`。整图开闭版可改 `LabRecycler.Paint`，保留 `Consume` 和落点组件 |
| 表盘底画含固定表针，现以 UI 刻度盘覆盖 | 无针表盘底图和独立表针，表针轴心明确 | `LabArtStage/CollectorDialFace`、`CollectorTick0..8`、`CollectorHub` 和 `CollectorNeedle`。替换时沿用 `needle` 引用 |
| 科研卡片和导航复用既有按钮、字体及深色面板 | 科研节点普通、选中、已研究状态，按钮正常、悬停、禁用状态 | 既有 `ResearchScreenView.nodes` 和按钮的 Image，保留 `TechNodeView` 与原监听器 |
| 基因格沿用文字面板，缺少基因图标 | 与内容目录对应的基因图标 | `SkillChipView.icon` 和 `RenderGene` 的图标映射，保留 `SkillName`、`HasPayload` 和预约信息 |
| 老板暂不进入玩法画面 | 后续教程表情、手势和对话框 | 启用 `LabArtStage/BossReserved` 前先给教程建立显隐流程，避免挡住亲本和提取槽 |

直接在 Inspector 替换图片后，不必重跑全量接入工具。`LabArtIntegration.Apply` 会重新应用本轮默认素材和布局，适合从当前源素材重建，后续正式美术坐标应同步修改这个工具。

## 动画和输入

`LabSceneView` 控制光斑轻微呼吸及间歇闪烁。它读取查询结果控制第二房间显隐，按目标能量调整表针。`ScoringShow.AddShown` 把已经显示的能量通知它，触发收集罐闪光和短寿命能量粒子。原有数字飞行动画仍飞向收集罐上的 `currentNumber`。

`LabRecycler` 在真实鼠标进入时张嘴。规则确认废弃成功后，控制器调用一次 `Consume`，嘴部关闭。无效投放不会播放吞食。离开界面会停用能量粒子并复位嘴部。

怪物继续使用已有拼装骨架、悬停说明、抓取摆动和结算反应。笼中画像较小，培育与产能区画像较大。原有 `OperationPointer` 按屏幕中的视口尺寸计算拖拽放大，统一到同一画布后继续复用。

## 收容笼分层与升降（2026-10-16）

柜口固定在原画坐标 `(1538,158)`，尺寸为 `264 × 280`。每格 `132 × 140`，每排两个怪物。库存至少保留两排空笼背景，库存增加时按整排扩展。怪物槽仍保存原有 `monsterId`、`MonsterSlotView`、`OperationZone` 和 `OperationPayloadDrag`。

`Assets/Arts/Lab/Cage/` 保存从现有 PSD 分离的两种笼厢内壁与铁栏前景。怪物画在内壁和铁栏之间。原画下排较宽的浅色笔触留在后方，前景只保留铁栏和门锁的深色部分，避免遮掉整个身体。`CabinetRim`、上下边缘阴影固定在柜口，内壁、怪物、笼门及横梁随库存一起移动。外层 `Mask` 裁切柜口，每个怪物槽另有 `RectMask2D` 防止手脚伸入相邻笼格。前景和阴影不拦截射线。

`LabCageLift` 继承 `ScrollRect`，沿用原有 viewport、content 和库存拖放入口。滚轮一次移动一排，上下箭头一次移动两排，使用不受结算时间缩放影响的缓动并在整排停靠。连续输入以尚未到达的目标排为起点累计，首尾限制在有效库存内。界面停用时清除速度和待完成动画，并回到第一排。垂直滚动条已移除，基因柜原有的左右翻页箭头由同一 PSD 的干净面板覆盖。

这次继续使用覆盖画布中的骨架。当前遮挡和裁切已能在同一坐标空间完成，拖出柜口后，虚影仍由原有 drag layer 绘制。

素材重导出：

```powershell
uv run --with psd-tools python -X utf8 scripts/extract-cage-art.py
```

退出 Play Mode，在 Main 场景中通过 Unity CLI 调用 `TheCall.Editor.LabCageRefinement.Apply()`，可单独重建笼柜，不改动其余实验室布局。完整原画接入工具 `LabArtIntegration.Apply()` 也会执行该步骤。

运行验收使用 `scripts/verify-lab-cages.ps1`，证据保存到 `docs/audit/2026-10-16/cages/`。验收包括实际按钮和滚轮事件、连续帧位移采样、前景后的射线命中、31 只怪物的末尾拖放、库存缩减、空笼和科研界面往返。局部库存由 fixture 注入，不能作为自然七天通关证据。

本轮光效来自原画光斑的透明度变化。Screen Space Overlay UI 不采样 `Light2D`，所以主场景的图层和怪物保持原画配色。场景保留标准 `GlobalLight2D`，供世界精灵使用。

## URP 2D 状态

原场景有两个同名 `WorkspaceWorld`，包含重复世界画布。当前操作槽已经在覆盖画布中，旧世界对象仍使层级和渲染来源难以分辨。两套旧世界对象及旧拖动预览已移除，总控的 `_workspace` 引用已清空。

旧原型装饰集中在 `OperationScreen/PrototypeArchive` 下并禁用，使用中的语义槽位已移出。旧 `PresentationSceneBuilder` 会重建原型界面并覆盖这次布局；重建本轮原画场景时使用 `LabArtIntegration.Apply`。

Graphics 与全部 Quality 档使用 `Assets/Settings/Lab2D_RPAsset.asset`，其唯一渲染器为已有 `Renderer2D.asset`。Main Camera 使用正交投影、纯色清屏，没有天空盒、雾或旧烘焙 LightingData。Scene View 设置为 2D 并关闭天空盒，新建对象默认使用 2D 行为。

Unity 仍以三维 Transform 表示对象。Scene View 里能旋转观察对象，本身不代表游戏在使用 3D 渲染器。这里的运行验证直接检查实际 `Renderer2D` 类型。

配置依据：[Unity 6000.6 Renderer 2D 文档](https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2drenderer-data-overview)。

## 重建和验证

在仓库根目录执行素材导出：

```powershell
uv run --with psd-tools python -X utf8 scripts/extract-lab-art.py
```

保持 Main 场景已打开且退出 Play Mode，再通过 Unity CLI 调用：

```powershell
unity --no-banner --non-interactive command eval --project-path "C:\Users\jinji\Documents\GitHub\Project_TheCall" --caller plugin --skill unity-cli -- "return TheCall.Editor.LabArtIntegration.Apply();" 240000
```

工具首次执行时把原场景及 Graphics、Quality、Editor 设置复制到 `Temp/LabArt/rollback`。当前 Git checkout 也保留改动前的已提交版本。工具通过 Editor API 保存场景，没有手写场景 YAML。

运行验收：

```powershell
pwsh -NoProfile -File scripts/verify-lab-art.ps1
```

`-SkipNaturalRuns` 只跑素材、实际点击、拖放、科研扩建、回收、库存和收集器检查。默认再执行正常、快进、跳过三种七天回放，并比较全部输入后的规则状态。注入怪物和科技点的局部 fixture 仅验证界面，不构成自然通关证据。

验证结果和截图位于 `docs/audit/2026-10-15/lab/`。最终通过项以该目录的 `acceptance.json`、`acceptance-controls.json` 和 `editor-tests.json` 为准。
