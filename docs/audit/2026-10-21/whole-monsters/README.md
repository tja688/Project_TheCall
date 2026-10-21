# 完整怪物立绘验收

2026-10-21，Unity 6000.6.0f1。素材从原 JPG 提取，玩家中的每个画像只显示一张完整图片。

- 12 个原稿造型，6 套渐变，72 张正式立绘。原 JPG 保持原样。
- 485 项项目 EditMode 测试通过，0 失败、0 跳过。
- 972 个实际画像样本通过，覆盖全部造型与配色、玩法状态独立性、600 个出生种子与培育池检查。
- 36 项 Canvas、配色、真实跨区拖动、虚影与拒绝回弹检查通过。
- 基因安装、真实培育出生、技能继承来源、两天工作与两天休息检查通过。心情按规则变化，出生图片与配色保持不变。
- 72 张配色保留透明轮廓与原稿黑线，12 张提取稿通过原稿像素与边缘检查。
- 重复导入后 170 个素材及配置文件内容不变，GUID 与引用保留。
- 两轮运行验收都确认 Main 场景文件未改变，运行中没有新增 Unity 错误；最终已退出 Play Mode。

这些运行验收使用明确记录的局部初始资源，未运行自然七天通关。本页不声称自然七天流程已通过。

[最终造型总览](gallery-4.png) · [游戏中的实际画像](real-production-monster.png) · [实际培育子代](cultivation/05-inherited-child-next-day.png)

[编辑器测试](editmode-tests.json) · [画面与拖动验收](acceptance.json) · [培育与状态验收](cultivation/acceptance.json) · [原稿检查](source-audit.json) · [重复导入](reimport-idempotence.json) · [素材来源](provenance.md)

维护说明见 [完整怪物立绘](../../../presentation/monster-system.md)。正式 PNG 位于 `Assets/Arts/Monsters/Whole/`。
