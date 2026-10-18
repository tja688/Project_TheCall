# 怪物结算演出与布局验收

Main 已保存并重新打开检查。五格和六格根据实际开放数量等距排列，紧凑表盘给怪物头顶的演出留出空间。辅助飘字避让主飘字、表盘和画面边缘。

主飘字保留技能名与能量数字。辅助效果拆成真实字形碎片，汇入数字后单独膨胀改值，再把本次产出注入本格分柱。结算轴前往下一格时，前一格分柱回缩；主柱、指针和累计数字随接收过程上涨。零产出上浮淡出。原有自然加速、点击快进和再次点击跳过保留。

## 实机画面

| 时刻 | 截图 |
| --- | --- |
| 蜜能量体保留 +3，奇异香说明 +1 | [主效果与辅助效果](normal/02-support.png) |
| 字形破碎后飞向当前数字 | [辅助碎字汇入](normal/02-merge.png) |
| 数字膨胀为 +4 | [数字变化](normal/02-number.png) |
| 本格分柱蓄能发亮 | [分柱蓄能](normal/02-charged.png) |
| 下一只开始，前一柱同步传能 | [分柱回缩与主柱接收](normal/03-transfer.png) |
| 工具提示位于工具栏下方 | [工具加成](tools/05-tool.png) |

## 验证结果

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| Unity Editor 测试 | 519 通过，0 失败 | [测试结果](editor-tests.json) |
| 独立规则测试 | 418 通过，0 失败 | [TRX](rule-tests/settlement-visual-rules.trx) |
| 七类实机 fixture | 6345 个采样帧；数字、清理、避让和同步检查通过 | [逐帧检查](acceptance.json) |
| 正常、快进、跳过的七天玩家回放 | 各 125 次实际输入，均通关；9/9 份状态与生产记录等于原基线 | [回放对照](replay/replay-comparison.json) |
| 首段结算的实际指针角度和主柱液面 | 8903 帧；最大角度误差 0.00001717 度，液面误差 0.0 | [Transform 检查](replay/instrument-transforms.json) |
| 分柱真实灯光 | 固定液面，仅关闭 Light2D；局部平均像素差 17.8953 | [开灯](column-light-on.png)、[关灯](column-light-off.png) |
| 画幅与实际 UI 射线 | 1280×720、1920×1200、2560×1080 均对齐 | [画幅结果](aspects/aspects.json)、[边界像素](aspects/borders.json) |
| 源素材 | PSD 未改动；35 张 PNG 与独立重建逐字节一致 | [素材重建](art-provenance.json) |
| 结束状态 | 编辑器退出播放、无结算和碎屑残留；六根分柱与受光材质绑定完整 | [保存场景](saved-scene.json)、[运行控制台](runtime-console.json) |

fixture 使用注入的起始怪物与工具，通过真实结算按钮执行规则与演出。七天回放使用原有玩家操作计划，独立记录，未注入结算结果。

| 实机案例 | 规则确认能量 | 演出最终能量 | 结果 |
| --- | --- | --- | --- |
| 正常 | 25 | 25 | 通过 |
| 快进 | 25 | 25 | 通过 |
| 跳过 | 25 | 25 | 通过 |
| 六格、多技能、负加成和倍率 | 50 | 50 | 通过 |
| 带入能量 | 60 | 60 | 通过 |
| 全零产出 | 0 | 0 | 通过 |
| 工具加成 | 28 | 28 | 通过 |

Main SHA-256：`b4c19887cd94e3d1e0932f4c44b4b023d1eefbba06f7ebc0f2d53fa397847629`。

## 复跑

在 Unity 6000.6.0f1 的 Main 场景中退出 Play Mode，使用 PowerShell 7 执行：

```powershell
pwsh -NoProfile -File scripts/verify-settlement-visuals.ps1
pwsh -NoProfile -File scripts/verify-facility-replay.ps1 -EvidenceDirectory docs/audit/2026-10-18/settlement/replay
pwsh -NoProfile -File scripts/verify-facility-aspects.ps1 -EvidenceDirectory docs/audit/2026-10-18/settlement/aspects
```

实时回放记录写入忽略目录 Temp，完成后归档，避免 Windows 文件读取者占用正在反复重写的证据文件。调参入口见[横向设施接入说明](../../../presentation/horizontal-facility-handoff.md)。
