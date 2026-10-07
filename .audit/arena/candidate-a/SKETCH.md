# Sketch

规则源码仍在 `Assets/Scripts/Core/Rules`。下面新文件里的方法体都是 `throw new NotImplementedException();`，除了标明「原样搬移」的 QfSubset 成员。不要另起模块名。

## 文件

```
Assets/StreamingAssets/call-book.json          唯一内容文件
Assets/Scripts/Core/Rules/TheCallApp.cs        删掉 UnityEngine 与特性
Assets/Scripts/Core/Rules/AssemblyInfo.cs      补 InternalsVisibleTo("TheCall.Page")
Assets/Scripts/Core/Rules/ContentBook.cs       新
Assets/Scripts/Core/Rules/ContentGate.cs       新
Assets/Scripts/Core/Rules/BookJson.cs          新，internal
Assets/Scripts/Core/Rules/EffectKind.cs        新，internal
Assets/Scripts/Core/Rules/SkillDef.cs          新，internal
Assets/Scripts/Core/Rules/SkillSentences.cs    新，internal
Assets/Scripts/Core/Rules/ToolRules.cs         新，internal
Assets/Scripts/Core/Rules/ExtractionBench.cs   新，internal
Assets/Scripts/Core/Rules/SkillCatalog.cs      改为读 SkillDef
Assets/Scripts/Core/Rules/SkillCopy.cs         改为调 SkillSentences
Assets/Scripts/Core/Rules/LevelCatalog.cs      改为读 book.Levels
Assets/Scripts/Core/Rules/ToolCatalog.cs       从书构造 ToolDefinition
Assets/Scripts/Core/Rules/TechCatalog.cs       改为读 flag 与 economy
Assets/Scripts/Core/Rules/ShopSystem.cs        标价与权重改读 ContentGate.Current
Assets/Scripts/Core/Rules/BreedingSystem.cs    Percent / ModifierAmount 改读书
Assets/Scripts/Core/Rules/SettlementSystem.cs  工资改读书；Land 写下项
Assets/Scripts/Core/Rules/SettlementRecordQuery.cs
Assets/Scripts/Core/Rules/FlowSystem.cs        增加 Lay
Assets/Scripts/Core/Rules/ToolDefinition 仍在 ToolCatalog.cs
Assets/Scripts/UnityHost/TheCallBookHost.cs    新，唯一的 UnityEngine 入口
Assets/Scripts/UnityHost/TheCall.UnityHost.asmdef
Assets/Scripts/Core/Rules/TheCall.Rules.asmdef  noEngineReferences true，仍引用 QFramework
Assets/Scripts/Core/Tests/RulesFixture.cs      SetUp 里 Use 书
kernel/TheCall.QfSubset/TheCall.QfSubset.csproj
kernel/TheCall.QfSubset/Architecture.cs
kernel/TheCall.QfSubset/Container.cs
kernel/TheCall.Rules/TheCall.Rules.csproj
kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj
kernel/TheCall.Page/TheCall.Page.csproj
kernel/TheCall.Page/Program.cs
kernel/TheCall.Page/PageExports.cs
kernel/TheCall.Page/wwwroot/index.html
kernel/TheCall.Page/wwwroot/bench.js
kernel/TheCall.Page/wwwroot/bench.css
.github/workflows/pages.yml
```

不创建 `CallSession`、ScriptableObject、Blazor 组件、第二份计分、嵌入资源版的书。

`TheCall.Rules.asmdef` 继续编译 `Assets/Scripts/Core/Rules` 下的全部 `.cs`。宿主 asmdef 引用 `TheCall.Rules`，不引用 QfSubset。

## 项目

`kernel/TheCall.QfSubset/TheCall.QfSubset.csproj`

- `Microsoft.NET.Sdk`，`TargetFramework` `net10.0`，`RootNamespace` `QFramework`，`ImplicitUsings` disable，`Nullable` disable
- 无 `UnityEngine` 包引用
- `OutputPath` 落在 `kernel/artifacts`

`kernel/TheCall.Rules/TheCall.Rules.csproj`

- `TargetFramework` `net10.0`，`AssemblyName` `TheCall.Rules`，`LangVersion` `9.0`，`Nullable` disable，`ImplicitUsings` disable
- `ProjectReference` 只指向 QfSubset
- `Compile Include`：`../../Assets/Scripts/Core/Rules/**/*.cs`
- 不嵌入 json，不引用 Unity

`kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj`

- `AssemblyName` 必须是 `TheCall.Rules.Tests`，这样已有的 `InternalsVisibleTo` 同时覆盖 Unity 测试程序集和这个程序集
- `Compile Include`：`../../Assets/Scripts/Core/Tests/**/*.cs`
- 包：`NUnit` 3.14.0，`NUnit3TestAdapter` 4.5.0，`Microsoft.NET.Test.Sdk`
- `Content Include` `call-book.json`，`CopyToOutputDirectory` PreserveNewest，链接到 `Assets/StreamingAssets/call-book.json`
- `ProjectReference`：规则项目和 QfSubset
- 不定义 `UNITY_5_3_OR_NEWER`

`kernel/TheCall.Page/TheCall.Page.csproj`

- `Microsoft.NET.Sdk`，`OutputType` Exe，`TargetFramework` `net10.0`，`RuntimeIdentifier` `browser-wasm`
- `AllowUnsafeBlocks` true，`InvariantGlobalization` true，`PublishTrimmed` true，`TrimMode` partial
- `CompressionEnabled` false。GitHub Pages 不会为 `.br` 送 `Content-Encoding`
- 发布产物里的脚本用相对 URL，禁止以 `/_framework/` 开头。站点挂在 `https://tja688.github.io/Project_TheCall/`
- `ProjectReference` 规则项目
- 若本机 `wasm-tools` 工作负载缺模板，以上面的属性为准，不改成 Blazor WebAssembly SDK

`Assets/Scripts/UnityHost/TheCall.UnityHost.asmdef`：`name` `TheCall.UnityHost`，`references` 含 `TheCall.Rules`，`noEngineReferences` false。

## 书

`ContentBook.Parse` 只接受对象、数组、字符串、整数、`true`、`false`、`null`。拒绝小数、注释、尾随逗号。字符串用文件里的 UTF-8 中文，不必写成 `\u`。错误信息带路径，例如 `$.skills[3].effects[0].kind`。

```json
{
  "economy": {
    "wage": 40,
    "overtimeWage": 20,
    "price": { "White": 10, "Blue": 20, "Gold": 30 },
    "weight": { "White": 7, "Blue": 2, "Gold": 1 },
    "techPercent": 10,
    "modifierAmount": 2,
    "baseExtractionCells": 5,
    "baseBreedingSlots": 1,
    "expandedBreedingSlots": 2,
    "baseParents": 2,
    "expandedParents": 3
  },
  "levels": [
    { "due": 50, "excess": 60 },
    { "due": 75, "excess": 90 },
    { "due": 100, "excess": 120 },
    { "due": 150, "excess": 175 },
    { "due": 200, "excess": 230 },
    { "due": 250, "excess": 285 },
    { "due": 300, "excess": 350 }
  ],
  "tools": [
    { "name": "急急装置", "price": 30, "rarity": "White", "effect": "DoubleFirstEnergy" },
    { "name": "上级员工证", "price": 40, "rarity": "Blue", "effect": "ExtractionCells", "cells": 6 },
    { "name": "独孤装置", "price": 50, "rarity": "Gold", "effect": "DoubleSingleAffix" }
  ],
  "techs": [
    { "name": "基因实验", "flag": "BreedingSkill" },
    { "name": "槽位扩容", "flag": "ExtraBreedingSlot" },
    { "name": "变异学说", "flag": "ExtraSkill" },
    { "name": "大乱炖", "flag": "ExtraParent" },
    { "name": "科学培育", "flag": "Modifier" }
  ],
  "skills": []
}
```

`levels` 长度必须是 7。`tools` 三个效果各种类至多一件。`ExtractionCells` 必须带 `cells`。成交价不是字段。

技能数组顺序就是 `SkillCatalog.Names` 的顺序。稀有度池的顺序用每条技能上的 `poolIndex`，不要再写第二份名字列表。`NamesOf` 是「筛出该稀有度，按 poolIndex 排序」。

| name | rarity | poolIndex | use | affix | effects |
| --- | --- | --- | --- | --- | --- |
| 能量吐息 | White | 0 | Active | None | EnergyQuote 5 |
| 左能量体 | White | 1 | Active | None | SideCount side=Right perMonster=2 |
| 右能量体 | White | 2 | Active | None | SideCount side=Left perMonster=2 |
| 增量小手 | White | 3 | Passive | None | AddToOthers 1 |
| 增量大手 | Blue | 0 | Passive | None | AddToOthers 2 |
| 残留提取腺体 | Blue | 1 | Passive | None | LandingResponse 1 |
| 孤独心 | Blue | 2 | Active | None | EnergyQuote 4, DoubleWhenIsolated |
| 吞噬大嘴 | White | 4 | Active | Destroy, Permanent | EnergyQuote 4, Devour writeback=2 permanent=true |
| 双重吐息 | White | 5 | Active | None | EnergyQuote 2, RepeatQuote 2 |
| 时间操控器官 | Blue | 3 | Passive | None | CapacityExtraForLeftNeighbor 1 |
| 再回首头 | Gold | 0 | Passive | None | ExtraWalksForRightNeighbor 1 |
| 分享之手 | White | 6 | Active | None | EnergyQuote 2, NextEnergyBonus 3 |
| 太阳能头 | White | 7 | Active | Capacity | GainCapacity 1 |
| 换位手 | Blue | 4 | Active | Immovable | SwapWithLeft |
| 鼓励嘴 | Gold | 1 | Passive | None | DoubleAdjacentEnergy |

左能量体数的是右侧，右能量体数的是左侧。这个方向写在 `side` 里，不要从名字推断。

`Parse` 额外拒绝：

- `SwapWithLeft` 但词缀不含 `Immovable`
- `Devour` 但词缀不是 `Destroy | Permanent`
- `GainCapacity` 但词缀不含 `Capacity`
- 其余技能的词缀不是 `None`
- 技能名重复，或效果组合不在 `SkillSentences` 的允许表里

`FlowSystem` 里技能槽上限 3 仍是代码，不进书。

## 句子允许表

`SkillSentences.Format(SkillDef) : string`。键是效果种类的集合。下面右端是必须逐字匹配现有测试和 `SkillCopy` 的句子。

| 种类集合 | 句子 |
| --- | --- |
| EnergyQuote | 产生{quote}点能量 |
| SideCount | {右侧或左侧}每有一个怪物产生{perMonster}点能量 |
| AddToOthers | 其他怪物产生的能量数值+{amount} |
| LandingResponse | 其他怪物产生能量时，本怪物产生{quote}点能量（不会因为同名技能效果产生能量） |
| EnergyQuote + DoubleWhenIsolated | 产生{quote}点能量，当相邻没有其他怪物时，本怪物产生的能量翻倍 |
| EnergyQuote + Devour | 产生{quote}点能量，消灭随机一只相邻怪物，本技能产生的能量数值永久+{writeback} |
| EnergyQuote + RepeatQuote | 产生{quote}点能量两次（times 必须是 2） |
| CapacityExtraForLeftNeighbor | 左侧相邻怪物，产生能量的技能次数+{layers} |
| ExtraWalksForRightNeighbor | 右侧相邻怪物的所有技能效果都触发{walks + 1}次 |
| EnergyQuote + NextEnergyBonus | 产生{quote}点能量，下一个怪物产生的能量数值+{amount} |
| GainCapacity | 获得{layers}点产能 |
| SwapWithLeft | 与左侧相邻怪物交换位置，之后获得不动 |
| DoubleAdjacentEnergy | 相邻怪物产生的能量翻倍 |

`StartingQuote(name)` 改为：有 `EnergyQuote` 就返回它的 quote，否则 0。侧向技能因此仍是 0，和现在 `SkillInstance` 的初值一致。`ProducesEnergy` 改为这些种类的或：`EnergyQuote`、`SideCount`、`Devour`、`RepeatQuote`、`NextEnergyBonus`。

## 类型

```csharp
namespace TheCall
{
    public sealed class ContentBookException : System.Exception
    {
        public ContentBookException(string message);
    }

    public sealed class ContentBook
    {
        public static ContentBook Parse(string json); // 失败抛 ContentBookException

        internal Economy Economy { get; }
        internal IReadOnlyList<LevelBand> Levels { get; } // 长度 7，下标 0 是第 1 关
        internal IReadOnlyList<ToolDefinition> Tools { get; }
        internal IReadOnlyList<TechDef> Techs { get; }
        internal IReadOnlyList<SkillDef> Skills { get; }

        internal int Wage(bool overtime);
        internal int Price(Rarity rarity);
        internal int Weight(Rarity rarity);
    }

    public static class ContentGate
    {
        public static ContentBook Current { get; }

        // book 为 null，或 TheCallApp.IsRunning，则抛 InvalidOperationException。
        // 不在 Reset 时清空 Current。
        public static void Use(ContentBook book);
    }

    internal enum EffectKind
    {
        EnergyQuote,
        SideCount,
        AddToOthers,
        LandingResponse,
        DoubleWhenIsolated,
        Devour,
        RepeatQuote,
        CapacityExtraForLeftNeighbor,
        ExtraWalksForRightNeighbor,
        NextEnergyBonus,
        GainCapacity,
        SwapWithLeft,
        DoubleAdjacentEnergy,
    }

    internal readonly struct Effect
    {
        public EffectKind Kind { get; }
        public int A { get; }          // quote、amount、perMonster、writeback、times、layers、walks
        public int B { get; }          // SideCount 时是 (int)CountedSide；Devour 时 1 表示 permanent
    }

    internal sealed class SkillDef
    {
        public string Name { get; }
        public Rarity Rarity { get; }
        public int PoolIndex { get; }
        public SkillUse Use { get; }
        public SkillAffix Affix { get; }
        public IReadOnlyList<Effect> Effects { get; }
        public bool Has(EffectKind kind);
        public Effect Get(EffectKind kind); // 缺席抛 InvalidOperationException
    }

    internal static class SkillSentences
    {
        public static string Format(SkillDef skill);
    }

    internal static class ToolRules
    {
        public static int ExtractionCells(int baseCells, IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held);
        public static bool DoublesFirstEnergy(IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held);
        public static bool DoublesSingleAffix(IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held);
    }

    public enum ToolEffect
    {
        None = 0,
        DoubleFirstEnergy = 1,
        ExtractionCells = 2,
        DoubleSingleAffix = 3,
    }

    // 旧构造转成 effect = None, extractionCells = 0。
    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, int price, Rarity rarity);
        public ToolDefinition(string name, int price, Rarity rarity, ToolEffect effect, int extractionCells);
        public string Name { get; }
        public int Price { get; }
        public Rarity Rarity { get; }
        public ToolEffect Effect { get; }
        public int ExtractionCells { get; }
    }

    public readonly struct LandingAdd
    {
        public LandingAdd(string label, int amount);
        public string Label { get; }
        public int Amount { get; }
    }

    public readonly struct LandingFactor
    {
        public LandingFactor(string label, int factor);
        public string Label { get; }
        public int Factor { get; }
    }

    internal readonly struct BenchMonster
    {
        public BenchMonster(IReadOnlyList<string> skillNames); // 此处不校验
        public IReadOnlyList<string> SkillNames { get; }
    }

    internal sealed class BenchBoard
    {
        // row 中的 null 是空格。
        public BenchBoard(int levelNumber, IReadOnlyList<string> tools, BenchMonster?[] row);
        public int LevelNumber { get; }
        public IReadOnlyList<string> Tools { get; }
        public BenchMonster?[] Row { get; }
    }

    internal sealed class BenchReport
    {
        public int Produced { get; }       // 各 SettlementLanding.Energy 之和
        public int Due { get; }
        public int ExcessAt { get; }
        public bool MeetsDue { get; }      // 抄 SettlementPayment，不重新比较
        public bool MeetsExcess { get; }
        public IReadOnlyList<SettlementEntry> Entries { get; }
    }

    internal static class ExtractionBench
    {
        // 先要求 ContentGate.Current 非空，然后 Reset、打固定 IDraw、Interface、校验、Lay、Settle。
        public static BenchReport Run(BenchBoard board);
    }
}
```

`IToolCatalog` 删掉三个方法体里的中文名分支，改成默认实现，各一行，转给 `ToolRules`。`BaseExtractionCells` 默认返回 5。`ToolCatalog` 只重写 `BaseExtractionCells`，返回书里的 `baseExtractionCells`。`Tools` 返回书里的定义。`FixedTools` 不重写扫描逻辑。

`ILevelCatalog` 不改。`LevelCatalog.EnergyDue` / `ExcessEnergy` 读 `book.Levels`，越界仍返回 0。`Parse` 已保证长度为 7。

`TechCatalog` 去掉 `const Percent` 和 `const ModifierAmount`。`BreedingSystem` 改读 `ContentGate.Current.Economy`。`Contains`、`SlotCount`、`ParentCapacity`、`AllowsBreedingSkill`、`GrantsExtraSkill`、`GrantsModifier` 按 `flag` 判断，数字用 economy 里的 base / expanded。

`ShopSystem.PriceOf` 与 `Weight` 改读 `ContentGate.Current`。`ProceedsOf` 仍是 `PriceOf / 2`。

## 结算记录

`SettlementLanding` 保留六元构造，供毒跳伤使用：`Quote = baseValue`，`SideCount = 0`，`Adds` 与 `Factors` 为空列表。

另增：

```csharp
public SettlementLanding(
    string monsterId,
    string skillName,
    int baseValue,
    int multiplier,
    int energy,
    int writeback,
    int quote,
    int sideCount,
    IReadOnlyList<LandingAdd> adds,
    IReadOnlyList<LandingFactor> factors);

public int Quote { get; }
public int SideCount { get; }
public IReadOnlyList<LandingAdd> Adds { get; }
public IReadOnlyList<LandingFactor> Factors { get; }
```

`Land` 在现有 `added` / `baseValue` / `multiplier` 计算处同时填写列表。不要改 `BaseAfterAdds` 的公式。

加成标签只允许三种来源：

- 其他怪物身上 `AddedToOthers != 0` 的技能名，一笔一条，金额是该技能的加数
- 宿主 `Modifier != 0` 时，标签 `宿主修正`
- `_nextBonus` 非 0 时，标签 `下家`

因子标签只在乘数真正乘上去时追加，因子值是 2：

- 孤立且 `DoublesWhenIsolated`：`孤独心`。同一邻居上多张鼓励嘴只产生一个因子，与今天 `NeighborEnergyDouble` 遇到第一张就返回的行为一致
- 每个满足条件的相邻格：`鼓励嘴`
- `doubleFirstExecution`：`急急装置`
- 单词缀且持有独孤：`独孤装置`

侧向技能记录的 `quote` 参数仍传给 `Land`（今天是 `perMonster * sideCount`），但写入 `SettlementLanding.Quote` 的是目录里的 `perMonster`，`SideCount` 是 `CountSide` 的结果。然后断言记录与 `baseValue`、`multiplier`、`energy` 一致，不一致抛 `InvalidOperationException`。

工资：

```csharp
var wage = ContentGate.Current.Wage(wasOvertime);
```

## TheCallApp

```csharp
public sealed class TheCallApp : Architecture<TheCallApp>
{
    public static bool IsRunning => mArchitecture != null;

    public static void Reset()
    {
        if (mArchitecture != null)
            mArchitecture.Deinit();
        OnRegisterPatch = _ => { };
    }

    protected override void Init()
    {
        var book = ContentGate.Current;
        if (book == null)
            throw new InvalidOperationException("先 ContentGate.Use，再访问 Interface。");

        RegisterUtility(new SkillCatalog(book));
        RegisterUtility(new SkillCopy(book));
        RegisterUtility<IDraw>(new SystemDraw());
        RegisterUtility<ILevelCatalog>(new LevelCatalog(book));
        RegisterUtility<IToolCatalog>(new ToolCatalog(book));
        RegisterUtility(new TechCatalog(book));
        RegisterModel(new RunModel());
        RegisterModel(new LevelModel());
        RegisterSystem(new FlowSystem());
        RegisterSystem(new SettlementSystem());
        RegisterSystem(new ShopSystem());
        RegisterSystem(new BreedingSystem());
    }
}
```

不注册 `ClockIntents`。

`RulesFixture.BookFile`：

```csharp
static string BookFile()
{
#if UNITY_5_3_OR_NEWER
    return System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "call-book.json");
#else
    return System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "call-book.json");
#endif
}
```

这是测试里唯一允许的 `#if`。规则生产文件里没有 `#if`。`RulesFixture` 加 `[NonParallelizable]`。`SetUp` 开头调用 `TheCallApp.Reset()`，然后 `Use`，然后补丁，然后 `Interface`。`UseDraw` / `UseRules` 同样在 `Reset` 之后 `Use` 同一本书，再打补丁。

## Lay 与工作台

```csharp
internal void Lay(BenchBoard board)
{
    // EnterOperation(board.LevelNumber)
    // 每个工具 run.AddTool
    // BeginLevel(levels.EnergyDue, levels.ExcessEnergy)   // 这会把提取格打回长度 5
    // FitExtraction(run)                                  // 必须在 BeginLevel 之后
    // 对每个非空格：run.CreateMonster(skills) 后 level.Put(Extraction, index, id)
}
```

`ExtractionBench.Run` 的校验，失败抛 `BenchRejectedException`：

- 关卡在 1 到 7
- 工具名都在目录里，且不重复
- `board.Row.Length == IToolCatalog.ExtractionCells(board.Tools)`
- 每只怪物 1 到 4 个技能，技能名都在 `SkillCatalog.Names`，同一只怪物上不重复

固定抽牌：

```csharp
sealed class FirstDraw : IDraw
{
    public T Choose<T>(IReadOnlyList<T> options) => options[0];
    public bool Chance(int percent) => false;
}
```

`FlowSystem.OnInit` 仍会抽三名开局候选。他们不在提取格上，不进入 `AddedByOthers`。工作台不调用 `Keep`。

## QfSubset

`Architecture.cs` 从 `Assets/QFramework/Framework/Scripts/QFramework.cs` 原样搬这些成员，删掉 `UnityEngine`、场景、`BindableProperty`、`TypeEventSystem`、`SendEvent`：

- `IArchitecture` 上的 Register / Get / SendCommand 两个重载 / SendQuery / Deinit
- `Architecture<T>`：`OnRegisterPatch`、`mArchitecture`、`Interface`、`InitArchitecture` 的五步顺序、`Deinit`、`RegisterSystem`、`RegisterModel`、`RegisterUtility`、三个 Get、两个 `ExecuteCommand`、`DoQuery`
- `ISystem`、`AbstractSystem`、`IModel`、`AbstractModel`、`IUtility`、`ICanInit`
- `ICommand`、`ICommand<TResult>`、`AbstractCommand`、`AbstractCommand<TResult>`
- `IQuery<T>`、`AbstractQuery<T>`（`GetArchitecture` / `SetArchitecture` 保持 public，和现文件一致）
- `IBelongToArchitecture`、`ICanSetArchitecture`、`ICanGetModel`、`ICanGetSystem`、`ICanGetUtility` 及三个 Get 扩展

`Container.cs` 搬 `IOCContainer` 的 `Register`、`Get`、`GetInstancesByType`、`Clear`。键是 `typeof(T)`。用 `Dictionary<Type, object>`，依赖插入顺序，不要改成排序字典。

规则代码不调用 `SendEvent`。若以后调用，先补切片再编译，不要在规则项目里引用 Unity 那份 QFramework。

## 页面

`PageExports` 是 `public static partial class`。程序集名 `TheCall.Page`。

```csharp
[JSExport]
public static void Boot(string bookJson);     // Reset，Parse，Use

[JSExport]
public static string Catalog();               // 技能名、稀有度、主动或被动、句子；工具只给名字；关卡给 level、due、excess

[JSExport]
public static int Cells(string toolsJson);    // 工具名数组。内部走 IToolCatalog.ExtractionCells

[JSExport]
public static string Score(string boardJson); // 解析成 BenchBoard，ExtractionBench.Run，再写成下面的 JSON
```

`Boot` 失败或 `Score` 校验失败时，导出函数把异常消息放进 `{"error":"..."}` 返回，不在 JavaScript 里补计分。

`Score` 请求：

```json
{ "level": 1, "tools": ["急急装置"], "row": [ { "skills": ["能量吐息"] }, null ] }
```

`Score` 响应：

```json
{
  "produced": 0,
  "due": 50,
  "excessAt": 60,
  "meetsDue": false,
  "meetsExcess": false,
  "landings": [
    {
      "monsterId": "m1",
      "skillName": "能量吐息",
      "quote": 5,
      "sideCount": 0,
      "adds": [{ "label": "增量小手", "amount": 1 }],
      "factors": [{ "label": "鼓励嘴", "factor": 2 }],
      "base": 6,
      "multiplier": 2,
      "energy": 12,
      "writeback": 0
    }
  ]
}
```

换位和移除若出现在 `Entries` 里，响应里加 `swaps` 与 `removals`，字段沿用 `SettlementSwap` / `SettlementRemoval`。页面要显示它们，避免吞噬和换位看起来没跑到 `Settle`。

`index.html` 引入 `./_framework/dotnet.js` 和 `./bench.js`。DOM：

- `#skill-list`：`Catalog().skills` 的名字、稀有度、主动或被动、句子
- `#monster-bin` 与 `#add-monster`：每只怪物 1 到 4 个技能下拉框，选项只来自 `Catalog`
- `#tool-row`：三个复选框，标签是 `Catalog().tools[].name`
- `#level`：1 到 7，选项文字可以带 `Catalog().levels` 的 due
- `#cells`：格数等于 `Cells(当前工具)`，工具变化时重画。每格一个下拉框，选怪物或空
- `#settle`：调用 `Score`
- `#report`：只渲染 `Score` 的 JSON。due、excess、是否达到，用 `meetsDue`、`meetsExcess`、`produced`、`due`、`excessAt`。不要在 JS 里写 `produced >= due`

`bench.css` 只排这一列。不引入 npm。

`Program.Main` 返回 0。驱动在 JS。

## 工作流

`.github/workflows/pages.yml`：push 到 `dev` 和 `main` 时，`actions/setup-dotnet` 用 SDK 10，`dotnet workload install wasm-tools`，`dotnet test kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj`，再 `dotnet publish` 页面项目。把 `Assets/StreamingAssets/call-book.json` 复制到发布目录的 `wwwroot/call-book.json`。用 `actions/upload-pages-artifact` 和 `actions/deploy-pages` 发布该 `wwwroot`。不要把 wasm 提交进 git。仓库的 Pages 来源设为 GitHub Actions。

完成后用浏览器打开 `https://tja688.github.io/Project_TheCall/`，确认技能列表来自这份 json，搭 1 到 4 技能的怪物，放上提取行，切换三件工具，选择 1 到 7 关，结算结果与 Unity 里同布局的 `SettlementLanding` 一致。
