# 实验室原画接入验收

Main 场景已保存，编辑器已退出 Play Mode。当前引擎为 Unity 6000.6.0f1，使用 Unity CLI 1.0.0-beta.13 驱动已有 Editor。现有分支为 `dev`。

## 结果

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 最终代码的 EditMode 测试 | 511 项通过，零失败 | [editor-tests.json](editor-tests.json) |
| 素材、输入、扩建、库存、回收及回放检查 | 58 项通过 | [acceptance.json](acceptance.json) |
| 最后排版、运行渲染器和灯光动画检查 | 7 项通过 | [final-visual.json](final-visual.json) |
| 正常、快进、跳过七天回放 | 每种模式 125 次输入后通关 | `natural-0`、`natural-1`、`natural-2` |
| 三种回放之间的规则状态 | 每一步、最终状态及生产记录完全一致 | [acceptance.json](acceptance.json) |
| 与接入前正常回放对照 | 每一步、最终状态及生产记录完全一致 | [baseline-comparison.json](baseline-comparison.json) |
| 重新打开保存的 Main | Renderer2D、Mode2D、单一事件系统、单一全局 2D 光源，场景未修改 | [saved-scene.json](saved-scene.json) |
| 完整验收期间控制台 | 没有新增 Unity 错误 | [console.json](console.json) |

基线是 `docs/audit/2026-10-13/architecture/natural-0/`。对照文本为 `final-state.txt`、`state-trace.txt` 和 `production-log.txt`。

源 PSD 的 SHA-256 为 `3f65ed12595cfc1ddbf0341b978b96e1c3bc8bfd96d1aaa805e26d81b056c543`，与导出清单一致，源文件未改写。

## 运行检查覆盖

- 默认一间培育房开放，另一间显示锁定。实际点击科研节点与研究按钮后，第二间房开放。
- 亲本由两只扩成三只时，四个或六个有效槽位都位于对应的原画房间内，中心能被输入射线命中。
- 小笼子拖至产能区时，虚影的屏幕视口尺寸与目标一致，松手后经过原有规则命令落位。
- 回收器悬停张嘴，实际投放移除怪物、产生基因并只触发一次合嘴。
- 30 只怪物的库存能滚到最后一只，拖至提取槽后再放回。
- 六只怪物触发真实结算，能量到达收集器、驱动表针和粒子。返回标题会清理演出、能量粒子与嘴部状态。
- 六张科研卡片的实际文字网格均没有超出卡片宽度。长说明换行并以省略号收尾，右侧仍显示完整说明。
- 两次读取运行中的光斑透明度，得到 `0.475326657` 和 `0.542114139`，确认动画在推进。

局部 fixture 会注入怪物、工具和科技点，用于检查界面。七天回放另行启动固定种子的既有计划，沿按钮回调、拖放处理与结算链执行，验证规则结果；这不是人工试玩录像。点击遮挡另由局部检查中的 EventSystem 射线和指针点击验证。

## 画面证据

| 截图 | 内容 |
| --- | --- |
| [01-locked-room.png](01-locked-room.png) | 默认房间和怪物笼 |
| [02-enlarged-drag.png](02-enlarged-drag.png) | 放大的拖拽虚影 |
| [03-expanded-lab.png](03-expanded-lab.png) | 两间房、三亲本布局和四格基因仓库 |
| [04-recycler-open.png](04-recycler-open.png) | 回收器张嘴 |
| [05-recycled-gene.png](05-recycled-gene.png) | 回收后合嘴与基因产出 |
| [06-research.png](06-research.png) | 最终科研界面及长说明排版 |
| [07-large-cage.png](07-large-cage.png) | 大库存滚动 |
| [08-energy-collection.png](08-energy-collection.png) | 真实结算中的能量粒子和表针 |

待补图项目及替换方法见[实验室原画接棒笔记](../../../presentation/lab-art-handoff.md)。临时平台、二号房间内壁、回收器嘴型、无针表盘和科研控件均已标注。
