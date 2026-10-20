# 成品模板怪物的实现与维护

正式怪物使用 `Assets/示意图/怪物实例.jpg` 中的 12 个完整造型。每个模板固定身体、肢体、嘴型、花纹和眼位。出生时确定模板、整套色卡及头饰、背饰；技能增减不改变这些内容。性格决定眼型，真实心情控制呼吸、姿态与动作强度。

## 数据与调用

```csharp
portrait.Show(monsterView);
var ghost = MonsterPortrait.SpawnGhost(dragLayer, portrait);
```

`RunModel` 中的怪物拥有唯一的 `MonsterAppearance`，保存 `Recipe`、`Palette`、`HeadAccessory`、`BackAccessory`、`Variation`。`Recipe` 对应稳定的 `MonsterTemplate` 标识，`Palette` 对应稳定的 `MonsterPalette` 标识，均不依赖 Inspector 数组排序。画像、商店、结果、拖动虚影与结算都消费同一快照，不自行抽取或查询下一天状态。

出生均匀选择 12 个模板、6 套色卡与模板允许的饰品组合。两亲、三亲培育有 80% 概率继承一名亲本的完整模板，20% 从亲本未出现的其他模板中选择。整套色卡沿用现有次亲本规则。饰品候选来自亲本，筛选兼容组合；没有适配项时使用无附加饰品。外观哈希不调用 `IDraw`，性格、技能、种族、经济与亲本来源仍由原规则处理。

饰品兼容规则只在无 Unity 依赖的 `MonsterAppearance.Compatible` 中维护。资源目录必须覆盖该清单。头饰编号为无、短角、纹角、蝴蝶结；背饰编号为无、蝙蝠翼、羽翼。成对翅膀算一个背饰。复杂模板可以只开放蝴蝶结，或关闭背饰。

## 作者化资源

`Assets/Resources/MonsterArtCatalog.asset` 是当前美术调参入口，schemaVersion 为 2。

- `bodies`：按稳定 ID 查找的 12 套固定分层、表达眼位、饰品挂点、动作分组与取景框。
- `palettes`：身体、花纹、眼部强调色、嘴部四个颜色角色。完整色卡共同遗传。
- `parts`：70 张原始透明图片及有效 alpha 范围，部分含派生着色遮罩。
- `viewportFill` 与 `idleMotion`：外框留白与动作幅度。

`MonsterComposition` 按数据绘制，不包含随机头型、手脚、口型或技能改造。各层按颜色角色取色。眼睛只在标记为 expression 的固定位置替换；纽扣外壳等装饰眼不改变数量与位置。眼睛、嘴和身体通过动作分组共同运动，着色遮罩与原图使用相同变换。

菜单 `TheCall/Monster/更新部件目录与图集` 更新原图引用、遮罩与图集，同时保留现有模板和色卡。首次由旧目录迁移时填入 12 套成品默认值。开发调用 `MonsterArtIntegration.Apply(true)` 会明确重置为 `MonsterTemplateDefaults` 中的布局及色卡，日常换图不要传 true。

原始 PNG 不修改。`MonsterTintMasks` 生成 `Assets/Resources/MonsterTintMasks/` 下的着色遮罩，保留黑线、瞳孔、白色高光、爪尖和吸盘。需要改变语义分区时调整生成器，并检查原图与遮罩叠加后的实际画像。

## 抽查与验证

编辑器菜单 `TheCall/Monster/打开立绘抽查台` 使用现有 localhost 控制面板。可选择固定模板、完整色卡、兼容饰品、性格与心情，对照原始参考图。预览只改变抽查记录，不修改当前对局。旧版九结构槽缓存已用 v2 会话键隔离。

```powershell
python scripts/audit-monster-art.py --check
pwsh -NoProfile -File scripts/verify-monster-system.ps1
pwsh -NoProfile -File scripts/verify-monster-cultivation.ps1
pwsh -NoProfile -File scripts/verify-facility-replay.ps1 -EvidenceDirectory docs/audit/2026-10-20/templates/replay
python scripts/monster-template-evidence.py
```

导出真实画像使用 Unity CLI 调用 `MonsterArtAcceptance.ExportTemplates()`，生成 72 个真实 Canvas 色卡画像与模板几何数据。`monster-template-evidence.py` 需要 Pillow，将这些截图整理为对照图、色卡图与动作 GIF，同时检查 OKLab 色差、全部合法饰品的原始 alpha 结构连通性及饰品对表情眼位的遮挡，不绘制替代怪物。

证据目录为 `docs/audit/2026-10-20/templates/`。画像矩阵、局部资源注入、真实指针交互与七天玩家输入回放分别记录，不能相互代替。审美判断仍需查看实际图片与动作，不能仅凭测试或几何连通性通过。
