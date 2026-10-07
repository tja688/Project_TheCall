# The Call 表现层 Demo 交接

## 交付范围

本次把 `Assets/Scenes/Main.unity` 做成了当前内核代码对应的可运行 Demo。场景仍然是单场景结构，所有表现层由一个宿主组件在运行时生成：

- 场景入口：`Assets/Scenes/Main.unity`
- 表现层脚本：`Assets/Scripts/Presentation/TheCallPresentation.cs`
- 核心玩法：`Assets/Scripts/Core/Rules`
- 运行时美术资源：`Assets/Resources/MonsterParts`
- 运行时字体：`Assets/Resources/SmileySans-Oblique-3 SDF.asset`

`Main.unity` 只保留相机、灯光和 `TheCallPresentation` 宿主对象。Canvas、EventSystem、按钮、卡片、文字和怪物插画均在运行时创建，便于后续替换布局和美术而不修改场景 YAML。

## 已打通的游戏流程

### 开局

1. `TheCallApp` 初始化核心架构。
2. `OpeningCandidatesQuery` 读取三只候选怪物。
3. 玩家点击“留下它”。
4. `KeepOpeningMonsterCommand` 进入核心 `FlowSystem.Keep`。
5. 被选怪物加入怪物笼，核心再生成六只单技能怪物。
6. 画面进入第 1 关开场。

### 单关卡

1. 第 1 关开场显示交款目标和超额科技目标。
2. 点击“进入操作台”发送 `BeginLevelCommand`。
3. 操作阶段可以：
   - 从怪物笼选择怪物。
   - 将怪物放入五格提取轨。
   - 将怪物放入培育槽。
   - 点击已占用格子把怪物送回怪物笼。
   - 选择技能槽中的技能。
   - 将技能装入选中的怪物。
   - 将技能放入培育方案。
   - 废弃怪物并把随机技能放入技能槽。
   - 打开科技研究档案并解锁科技。
4. 点击“开始结算”发送 `ConfirmSettlementCommand`。
5. 核心 `SettlementSystem` 进行真实的结算行列、报价、倍率、写回、换位、消灭、毒跳伤和付款逻辑。
6. 结算成功进入商店；第一次不足进入加班，第二次仍不足进入失败画面。
7. 商店可购买四只怪物、最多两件工具，也可以打开怪物笼出售未锁定亲本。
8. 点击离开货架发送 `LeaveShopCommand`，进入下一关或第七关胜利画面。

## 内核接入点

表现层没有复制玩法规则。所有状态变化都经过 QFramework 架构：

- 读取：`RunPhaseQuery`、`RunLedgerQuery`、`LevelTargetQuery`、`LevelEnergyQuery`、`LevelShortfallQuery`
- 怪物读取：`OpeningCandidatesQuery`、`MonsterCageQuery`、`MonsterQuery`
- 操作读取：`ExtractionSlotsQuery`、`BreedingSlotsQuery`、`BreedingPlansQuery`
- 商店读取：`ShelfQuery`
- 结算读取：`SettlementRecordQuery`
- 状态写入：`KeepOpeningMonsterCommand`、`BeginLevelCommand`、`PlaceMonsterCommand`、`ReturnMonsterCommand`、`DiscardMonsterCommand`、`EquipSkillCommand`、`PlaceBreedingSkillCommand`、`ConfirmSettlementCommand`、`UnlockTechCommand`、`BuyMonsterCommand`、`BuyToolCommand`、`SellMonsterCommand`、`LeaveShopCommand`

后续修改玩法时，优先修改 `Assets/Scripts/Core/Rules`，不要在 `TheCallPresentation` 中重新实现规则。

## 视觉设计落地

### 核心气质

采用“奇异生物研究档案 + 午夜剧场工作台”的方向：

- 深墨蓝舞台背景，带轻微纸张颗粒和不规则墨点。
- 中心纸张使用暖象牙色，模拟手绘研究档案。
- 主色为墨蓝、珊瑚红、氧化薄荷、纸张黄和科技金。
- 所有主要窗口都有细珊瑚色顶线、金色图钉和轻微圆角。
- 字体使用项目已有的 `SmileySans-Oblique-3 SDF`，保持独立游戏标题和说明文字的手绘感。
- 文案加入少量奇异幽默，但不影响玩法信息扫描。

### 怪物组合

怪物插画由透明部件按层生成：

1. 身体
2. 尾巴
3. 头
4. 眼睛
5. 嘴
6. 头饰
7. 光晕

部件来自原始目录，复制到以下运行时目录后统一加载：

- `Assets/Resources/MonsterParts/Body`
- `Assets/Resources/MonsterParts/Head`
- `Assets/Resources/MonsterParts/Eye`
- `Assets/Resources/MonsterParts/Mouth`
- `Assets/Resources/MonsterParts/Hand`
- `Assets/Resources/MonsterParts/Foot`
- `Assets/Resources/MonsterParts/Tail`
- `Assets/Resources/MonsterParts/Hat`
- `Assets/Resources/MonsterParts/Accessory`

核心怪物 ID 用稳定哈希选择部件组合。因此同一只怪物在怪物笼、提取轨、培育台、商店和胜负画面中会保持一致的视觉身份。部件全部叠加在一个插画容器内，避免出现漂浮碎片。

当前技能与部件不是一一绑定关系。怪物的技能由核心随机生成，部件组合用于表达其遗传和培育身份；后续可以在 `StableSeed` 或技能到部件的映射处增加更强的语义绑定。

## 动效与交互质感

当前已实现：

- 光晕以非常小的幅度呼吸缩放，不改变布局。
- 按钮拥有高亮、按下和禁用色彩过渡。
- 选中的怪物和技能会使用珊瑚、金色高亮。
- 每次核心操作后刷新当前视图并显示短时提示条。
- 结算不足时明确显示欠额和“这是加班，不是重开”。
- 提取轨、培育槽、技能槽、商店货架均使用稳定尺寸，避免动态内容导致布局跳动。
- UI 使用 `CanvasScaler.ScaleWithScreenSize`，参考分辨率为 1600 x 900。

## 科技与出售面板

操作阶段顶部的“科技”按钮打开研究档案：

- 五项科技均显示名称、用途和当前是否解锁。
- 解锁通过 `UnlockTechCommand` 发送给核心。
- 科技点不足时按钮仍显示状态，但核心会拒绝实际解锁。

商店阶段的“打开怪物笼”按钮打开出售台：

- 从 `MonsterCageQuery` 读取当前怪物。
- 每只怪物显示组合插画、技能列表和出售按钮。
- 出售通过 `SellMonsterCommand` 进入核心。
- 核心负责阻止出售已锁定培育方案中的亲本。

## 资源与字体注意事项

- `Resources.LoadAll<Sprite>` 优先读取 Sprite；如果导入结果是 Texture2D，脚本会创建运行时 Sprite 作为回退。
- 字体优先加载 `SmileySans-Oblique-3 SDF`，找不到时回退到 `LiberationSans SDF`。
- 当前部件 PNG 的导入设置来自项目已有素材，透明区域不会显示白色背景。
- 不要手动编辑场景 YAML。场景对象应通过 Unity Editor CLI 或 Unity 编辑器保存。
- 如果增加新的部件目录，需要同时在 `LoadArt()` 中增加加载路径，并在 `DrawCreature()` 中增加层级。

## 验证记录

已使用 Unity CLI 和 Unity 6000.6.0f1 验证：

- Unity Editor 实例：ready，项目路径为当前仓库。
- 表现层正常编译：`recompile_status` 返回 `completed`，`failed=false`。
- Play Mode 正常进入 `Main`。
- 合成屏幕截图确认 Screen Space Overlay UI 正常可见。
- 运行时确认使用 `UnityEngine.InputSystem.UI.InputSystemUIInputModule`。
- 运行时按钮回调验证：
  - 开局候选按钮进入 `LevelStart`。
  - 开始按钮进入 `Operation`。
  - 核心查询返回 `phase=Operation, cage=7, slots=5`。
  - 选择怪物、放入提取槽、点击结算按钮成功执行核心命令。
  - 结算不足时仍留在 `Operation`，能量清零并记录欠额，符合加班规则。
- 最终控制台无编译错误、无运行时异常。

## 后续推荐工作

1. 将目前的程序化纸张背景替换成正式的全幅手绘背景纹理或分层舞台插画。
2. 给技能增加更明确的部件语义，例如“吞噬大嘴”固定使用嘴部变体，“换位手”增加手部变体。
3. 为结算记录增加逐条播放面板，将 `SettlementRecordQuery` 的每一条落地、换位、消灭和付款结果做成时间轴动画。
4. 增加音频：按钮纸张摩擦声、怪物短叫声、结算落地音、商店铃声和失败/胜利主题。
5. 如果需要移动端，进一步为操作阶段加入横屏安全区和触控拖拽；当前交互是“选择后点击槽位”，优先保证桌面和键鼠可玩。
6. 目前科技系统使用已有核心定义，变异概率和后代视觉继承仍由核心规则后续确定，表现层已经预留了下一关出生与怪物组合展示的位置。

## 重要边界

本 Demo 的表现层已经完整接通当前核心，但核心设计案中仍有一些长期规则尚未确定，尤其是培育变异的具体概率和后代技能变化。表现层没有擅自固化这些规则，只负责展示核心当前给出的怪物和技能状态。
