# 界面分部转场

入口仍是 `Assets/Scenes/Main.unity`。三选一后进入第一天生产区、结算后进入商店、科研开合，以及关卡提示和结果页，都通过总控的同一个界面交接入口播放。生产区与宿舍区继续使用 `FacilityPanorama` 横移，不触发这套演出。

## 节奏与画面

| 时段 | 动作 |
| --- | --- |
| 离场 0.20–0.22 秒 | 内容按反向次序淡出，轻微撤回原来的入场方向 |
| 入场前 0.14 秒 | 恢复画面亮度，露出第一批结构 |
| 入场 0–0.75 秒 | 底图沿墙体、上下层与柜体边界分块归位 |
| 入场 0.16–0.80 秒 | 培育舱、生产席、收容柜、货架卡片与操作栏错峰出现 |
| 科研入场约 0.57 秒 | 标题先出现，六个节点按行展开，详情从右侧就位 |

位移采用四次减速，末端不弹跳。节点只做 2% 的轻缩放。离场加进场约 0.8–1.0 秒。重复渲染、研究节点选择、购买、商店分类与刷新货架都不会重播整页入场。

设计比较过两个编排草图：整页淡出淡入能遮住切换，但现有拆层没有表演；按屏幕中心散开的碎块会割裂机器结构。当前使用沿已有墙体、层板、设备轮廓的短位移，各区块仍保持自身朝向。

## 所有权与调用

`DrawOperation` 等界面入口调用 `Show(screen, render)`。相同界面立即 `Render`；换页时先离场，再激活和绘制目标页，最后播放目标入场。音乐仍跟随真正可见的界面。结算完成回调仍是商店交接的唯一入口，快进和跳过不会提前展示商店。

| 文件 | 职责 |
| --- | --- |
| `TheCallPresentation.Transitions.cs` | 界面交接、输入遮挡、刷新合并、中断清理 |
| `ScreenAssembly.cs` | 保存并恢复局部姿态，按 `parts` 的时间和方向临时移动、淡入淡出 |
| `AssemblySurface.cs` | 演出期间按 `regions` 裁切已有底图，结束后恢复完整原图 |
| `ScreenTransitionIntegration.cs` | 为已有场景写入默认编排，不重建场景对象或玩法引用 |

规则仍由原 Model/System 持有。动画不发送玩法命令、不抽取随机数、不推进天数。切换期间挡住重复操作，`Refresh` 合并到结束后执行一次。返回标题、停用总控和退出界面会恢复位置、缩放、透明度及交互，不留旧协程继续换页。

## 换主界面或科研素材

1. 保留原 `OpeningScreenView` / `ResearchScreenView`、怪物槽、`TechNodeView` 和按钮 adapter。图片可在它们下面替换。
2. 在该页 `ScreenAssembly.parts` 把 `target` 改为新图片或图片组的 `RectTransform`。整组只绑定父级，避免父子同时移动。`offset` 是入场前相对原位的偏移，`delay` 是晚多少秒，`seconds` 是该区块的入场时长。
3. 零 `offset`、`scale = 1` 表示只淡入淡出。给已有待机动作的对象使用这个配置。当前博士、收集罐液面和卷帘门的动态属性仍归原控制器。
4. 如果新图已经拆成独立对象，直接配置 `parts`，不再需要 `AssemblySurface`。如果还是一张合并图，可调整 `AssemblySurface.regions`。矩形使用 1920×1080 左上原点坐标，必须完整覆盖图片且互不重叠。
5. `AssemblySurface` 当前支持原始 Single Sprite。不要把配置的合并底图放进会旋转/紧密打包的 SpriteAtlas。分块只在运行时创建，源 PNG、PSD 和导入设置保持原样。
6. 只调整 Inspector 即可保存新的编排。`TheCall/Presentation/配置分部转场` 会重新应用默认编排；已有定制后不要用它覆盖自己的节奏。若更改默认结构，同步修改 `ScreenTransitionIntegration` 中对应页的短注释和配置。

底图在动画收尾换回原始单张图片，静止画面没有裁切接缝。分块对象和 Sprite 是临时资源，不保存到场景。`FacilitySprite` 继续使用现有光照材质和 CanvasGroup 透明度，实际灯光与画面分块坐标保持原接法。

## 重跑验收

退出 Play Mode，使用项目已就绪的 Unity 6000.6.0f1：

```powershell
pwsh -NoProfile -File scripts/verify-screen-transitions.ps1
```

脚本先检查六页配置、裁块覆盖和引用，再通过真实射线和按钮录制逐帧演出，检查暂停、连续刷新、商店原操作、同场景横移、切换中重置及停用恢复。商店局部检查明确使用资源 fixture。随后单独执行正常、快进、跳过各 125 次玩家输入的七天自然回放，比较三种模式的最终规则状态、全部状态轨迹及生产记录。

较短的演出检查：

```powershell
pwsh -NoProfile -File scripts/verify-screen-transitions.ps1 -SkipNaturalRuns
```

证据写入 `docs/audit/2026-10-20/transitions/`。`acceptance.json` 包含每个过渡的真实帧号、透明度及区块坐标；PNG 是对应 Game View 的多帧画面。自然回放与局部 fixture 分开保存。脚本最后检查 Console 并确认 Play Mode 没有改写已保存的 Main 场景。
