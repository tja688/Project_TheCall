# 收容笼精修验收

> 2026-10-20 起，怪物拼接、骨架、部件目录和拼装工作区已从工程删除。下文若提到它们，只作当时记录。新系统从 `Assets/Notes/新怪物系统交接.md` 开始。


Main 已保存，Unity 6000.6.0f1 已退出 Play Mode。笼柜使用两列两层的原画比例，每只怪物占一个完整笼格。内壁、怪物、铁栏和横梁一起升降，固定柜口负责裁切和上下阴影。上下箭头移至笼柜旁，滚动条已移除。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 编辑器测试 | 512 项通过，零失败 | [editor-tests.json](editor-tests.json) |
| 笼柜运行验收 | 44 项通过 | [acceptance.json](acceptance.json) |
| 按钮翻动两排、滚轮翻动一排 | 连续帧采样确认无瞬移、无越过目标，最终整排停靠 | [motion](motion/) |
| 一怪一笼及真实立绘 | 8、31、30、1、0 只库存分别核对怪物 ID、画像和笼格 | [acceptance.json](acceptance.json) |
| 前景遮挡与输入 | 四个可见笼格的射线命中正确，柜口外的笼格不能点击 | [acceptance.json](acceptance.json) |
| 拖放 | 小笼中的怪物拖出后放大，落入提取槽再放回；第 31 只怪物也能完成 | [fixtures](fixtures/) |
| 库存缩减及空笼 | 31 只缩到 1 只时范围正确，最后一只拖走后保留空笼背景 | [06-single-occupant.png](06-single-occupant.png)、[07-empty-cabinet.png](07-empty-cabinet.png) |
| 界面生命周期 | 科研往返后回到首排，没有残留升降动画 | [acceptance.json](acceptance.json) |
| 保存后的场景 | LabCageLift、264×280 柜口、140 行距、无滚动条，场景无未保存修改 | [saved-scene.json](saved-scene.json) |
| 控制台 | 本轮运行无新增错误 | [console.json](console.json) |

运行时另发现一格有怪物 ID 却没有画像：立绘缓存把“没有骨架、也没有任何可见回退部件”的状态判作有效。同外观刷新因此没有重建画像。`MonsterPortrait.RigVisualIntact` 现会验证回退图片，并有对应恢复测试。最终验收逐只检查了立绘，截图的第二页四只怪物均可见。

库存数量由既有局部 fixture 注入。按钮检查通过 EventSystem 射线和指针点击，滚轮通过事件层级传入实际 `ScrollRect`，拖放经过原有 `OperationPointer` 与规则命令。本轮没有重跑自然七天通关，也不把局部 fixture 作为通关证据。

验证命令为 `pwsh -NoProfile -File scripts/verify-lab-cages.ps1`。重导出与单独重建步骤见[接棒记录](../../../presentation/lab-art-handoff.md)。源 PSD 的 SHA-256 保持 `3f65ed12595cfc1ddbf0341b978b96e1c3bc8bfd96d1aaa805e26d81b056c543`。

![最终收容笼局部](cage-detail.png)

下图为实际升降过程的五张画面摘录，不是生成的插值图：

![升降连续画面摘录](transition-frames.png)
