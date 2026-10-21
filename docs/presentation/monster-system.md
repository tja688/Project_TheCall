# 完整怪物立绘

2026-10-21 临时修补：所有正式立绘使用纯色填充，取消渐变及白色保护区，解决眼睛、嘴部周围的大块发白。原稿不改写。

怪物来自 `Assets/示意图/怪物实例.jpg` 的 12 个完整造型。正式素材直接提取原 JPG 像素，保留轮廓、眼睛、针脚和四肢。图像生成工具的去背试稿存在细节重绘，未作为正式素材。

## 造型与配色

| 用途 | 完整造型 |
| --- | --- |
| 开局、商店、技能生成 | 脑袋兽、纽扣猫、铆钉兽、独角兽、触角兽、章鱼兽 |
| 培育子代 | 双首爬行者、缝合脑兽、双子星、双首触须、群星、长耳星纹兽 |

六套纯色为苔绿、暮紫、莓红、奶油、雾青、月蓝。颜色沿用参考图中的低饱和色调，导入时使用色卡亮色与暗色的固定中间色。所有填充区域统一着色，取消眼白、嘴部等白色保护范围；保留黑色线条与透明轮廓。

`MonsterAppearance` 只保存完整造型、配色与出生随机变化值。两个抽选池都用独立种子抽取造型与颜色，不改变玩法抽取次数。子代沿用原有培育时点和技能继承流程，外观独立选自培育池。心情、性格和技能不会改变图片、眼型、配色或呼吸强度。

## 更新素材

在退出 Play Mode 的 Unity Editor 中执行菜单 `TheCall/Monster/从参考图提取完整怪物`，或通过 Unity CLI 调用：

```powershell
unity --no-banner --non-interactive command eval --project-path "C:/Users/jinji/Documents/GitHub/Project_TheCall" --caller plugin --skill unity-cli --timeout 600 -- "TheCall.Editor.MonsterArtIntegration.Apply();" 600000
```

工具读取原图的固定裁剪框、移除灰底与封闭空隙、消除灰色边缘，并生成：

- `Assets/Arts/Monsters/Whole/Source-0.png` 至 `Source-11.png`，保留原图配色的透明提取稿。
- 同目录 `Monster-{造型编号}-{色卡编号}.png`，共 72 张纯色版本。
- `Assets/Resources/MonsterArtCatalog.asset`，完整图片、取景与色卡的唯一目录。
- `Assets/Resources/WholeMonstersAtlas.spriteatlas`，供正式玩家使用的图集。

重复导入保留手动调整的色卡。调用 `Apply(true)` 才恢复默认色卡。原 JPG 不改写。旧部件原图继续保留供美术参考，旧部件图集与染色遮罩已从 Resources 移除，运行时没有部件拼装路径。

修改裁剪框、去背规则与纯色算法时编辑 `Assets/Editor/MonsterArtIntegration.cs`。调整默认色卡时编辑 `MonsterTemplateDefaults.cs`。不要编辑场景 YAML。

## 显示与验证

`MonsterPortrait.Show(MonsterView)` 接收值快照，每个画像只有一个 `UnityEngine.UI.Image`。开局、怪物笼、提取槽、培育槽、商店、结算和拖动虚影共用同一画像。呼吸、拖动倾斜和结算弹动作用于整图。

菜单 `TheCall/Monster/打开立绘抽查台` 可逐个检查完整造型与纯色；抽查台中的性格、技能和心情仅用于核对逻辑状态。

运行 `python scripts/audit-whole-monster-art.py` 检查原稿像素、裁剪边缘、透明度与黑线。运行 `scripts/verify-monster-system.ps1` 检查 72 个造型与配色组合、游戏状态不改变图片、600 个基础与培育种子、原始黑线和透明轮廓、实际跨区拖动及拒绝回弹。运行 `scripts/verify-monster-cultivation.ps1` 检查真实安装、培育出生、技能来源和工作休息后的状态。证据目录为 `docs/audit/2026-10-21/whole-monsters/`。

上述局部场景会注入初始资源，不代表自然七天通关。独立的七天流程使用 `scripts/verify-facility-replay.ps1`。
