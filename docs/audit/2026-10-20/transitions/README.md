# 分部转场验收

Unity 6000.6.0f1，实际 Main 场景。当前配置已保存，退出 Play Mode 后场景不脏。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 六个界面配置、引用、底图裁切覆盖和边界 | PASS | `ScreenTransitionAcceptance.Saved()` |
| 九段真实界面交接，连续刷新、暂停、输入阻挡、商店原操作、同场景横移及中断恢复 | 15 项 PASS，1316 帧采样 | `acceptance.json` |
| 各段确实有位移和同帧内不同透明度，证明错峰入场 | PASS，约 0.73–0.97 秒 | `motion-summary.json`、逐帧 PNG |
| 科研反复开合后，生产区/商店区块的位置与缩放 | 与进入科研前的实际姿态相差小于 0.001 | `ScreenTransitionAcceptance.CheckPoses` |
| 正常、快进、跳过三种自然七天回放 | 各 125 次真实玩家输入，全部 Victory | `natural-0/`、`natural-1/`、`natural-2/` |
| 最终状态、完整状态轨迹、生产记录跨三种模式比较 | 六项逐字比较全部一致 | `replay-comparison.json` |
| 原始场景布局 | 1135 个原有 Transform 的布局参数均未改变 | `saved-layout.json` |
| 重复执行配置工具 | 连续执行输出一致 | `idempotency.json` |
| 退出后的临时资源 | 拼块 0、临时 Sprite 0，场景不脏 | `cleanup.json` |
| 编译与运行异常 | 编译通过，运行错误 0 | `console.json` |

Console 中已有 Demigiant/DOTween 旧版 `PluginImporter` 元数据警告，保存在 `plugin-import-warnings.json`，本次未修改插件。没有转场相关运行异常。

商店局部验收在 `fixtures/` 使用注入资源；自然七天回放独立运行，不注入金币、科技、工具或解锁。`preview.gif` 是实际 Game View 多帧截图的演出预览，`contact-sheet.jpg` 方便对照切换前、中、后的画面。

重跑入口：`pwsh -NoProfile -File scripts/verify-screen-transitions.ps1`。换图与编排维护见 `docs/presentation/screen-transitions.md`。
