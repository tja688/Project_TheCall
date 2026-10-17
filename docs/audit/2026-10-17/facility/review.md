# 横向设施正式美术接入验收

交付场景为 `Assets/Scenes/Main.unity`，通过 Unity 6000.6.0f1 的就绪 Editor 和 Unity CLI 保存。场景已退出 Play Mode，未保存验收注入的怪物、资源或研究状态。最终场景 SHA-256 为 `C59F0D88F505D135F2A0CFA54C4E3CCE8BA938719CC4148C80AFE65027EF9E63`。

## 已接通的功能

- 宿舍显示休息怪物，可分页、悬停和拖动调度；左侧独立监控汇总持有怪物的心情、位置和工作/休息记录。
- 两区可拖动切换并自动停靠；拖住怪物在边缘停留可跨区投放，无效投放返回切区后的实际来源位置。
- 生产、培育、科研扩容、收容柜升降、基因回收与安装继续使用原规则和命令。回收口、卷帘、能量表针、管线和收集罐随实际操作演出。
- 商店使用正式背景与怪物/工具分类，保留购买、出售、刷新和下一天；摇杆随成功刷新动作。
- 真实 URP 2D 光照用于暖红宿舍、冷蓝生产区和绿色商店。全场景 66 个怪物头像入口使用五种整体剪影，旧拼装和布娃娃路径保留。

## 验证结果与边界

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| EditMode 测试 | 516/516 通过，无失败或跳过 | [editor-tests.json](editor-tests.json) |
| 局部界面验收 | 52 项通过；覆盖实际射线、指针事件、跨区拖放、无效返回、31 只怪物的列表末端、科研、回收、买卖 | [acceptance.json](acceptance.json) |
| 七天操作回放 | 三轮各 125 次实际输入；每轮最终状态、生产日志和逐步状态记录均与接入前基线一致，共九项对照 | [replay-comparison.json](replay-comparison.json) |
| 画幅适配 | 1280×720、1920×1200、2560×1080 的渲染和射线对齐通过；宽高画幅遮边通过 | [aspects.json](aspects.json)、[art-integrity.json](art-integrity.json) |
| 源图与导出 | PSD 未改动；33 张 PNG 哈希一致；五张剪影透明轮廓逐像素一致 | [art-integrity.json](art-integrity.json) |
| 引擎光照 | 关闭真实 Light2D 后，空白培养室采样区约 79.45% 像素发生显著变化 | [lighting-off.png](lighting-off.png)、[art-integrity.json](art-integrity.json) |
| 运行日志 | 最终界面验收 0 错误、0 警告；回放无 Unity 错误 | [console.json](console.json)、[replay-console.json](replay-console.json) |
| 保存结果 | Main 无未保存修改，66/66 头像使用剪影，10 盏灯、4 个改造显现接口，测试单例已清理 | [saved-scene.json](saved-scene.json) |

局部界面验收使用明确注入的 fixture，以覆盖大库存、研究和边界条件；这与七天玩家操作回放分开记录。第二张宿舍图仅是改造美术预览，默认游戏中这些摆件保持隐藏。

早期回放记录器曾把一次中途失败的记录追加在成功会话之前。拆分前已确认最后一个完整会话逐字节等于原基线，原始合并文件与偏移证据保存在 [trace-session-splits.json](trace-session-splits.json) 和 `replay-history/`。记录器现已改为每次开始新计划时清空当前会话文件。

## 待设计接入

四种宿舍改造的美术、待机动画及工具/科技显现接口已完成，默认隐藏。价格、效果和正式内容名称尚待确定。博士目前使用通讯屏和情境文字，缺少的立绘待补；基因继续使用可读名称。

重建方式、字段和区域对应关系见 [接入说明](../../../presentation/horizontal-facility-handoff.md)。

## 截图

- [宿舍与全员监控](01-dorm-monitor.png)
- [宿舍改造美术预览](02-dorm-upgrade-art-preview.png)
- [生产培育区](03-production-expanded.png)
- [回收口张开](04-recycler-open.png)
- [回收后基因](05-recycled-gene.png)
- [商店怪物](06-shop-monsters.png)、[工具](07-shop-tools.png)、[出售](08-shop-selling.png)
- [收容柜末端](cage-tail.png)
- [超宽画幅](aspect-2560-1080.png)、[高画幅](aspect-1920-1200.png)
