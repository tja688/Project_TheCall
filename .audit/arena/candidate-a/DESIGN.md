## Problem

要把现有约 3700 行规则收成一个闭合的 C# 包：`dotnet test` 不引用 Unity，浏览器里的 GitHub Pages 跑同一份 `SettlementSystem.Land`，Unity 里的 `TheCallPresentation` 和 `MonsterHover` 继续 `GetArchitecture() => TheCallApp.Interface`，并继续 `SendCommand` / `SendQuery`。公开命令和查询的类型名保持不变，`CommitOperationDropCommand` 仍返回 `bool`。

难点是这三件事互相咬住。`QFramework.cs` 引用 `UnityEngine`，所以规则项目不能引用 QFramework。表现层的 `this.SendCommand` 又要求命令类型实现 QFramework 的 `ICommand`。现有 NUnit 测试是行为神谕，它们在第一次访问 `TheCallApp.Interface` 之前用 `OnRegisterPatch` 替换 `IDraw`、`ILevelCatalog`、`IToolCatalog`，并可选注入 `ClockIntents`。内容今天写死在 `SkillCatalog`、`SkillCopy`、`ToolCatalog`、`LevelCatalog`、`TechCatalog` 和商店标价、工资里，没有 ScriptableObject。设计师和以后的 AI 只能改一份文件，Unity 和网页都加载这一份。效果机制仍是共享 C#，改效果两边一起走。`Land` 仍是唯一的加减乘。网页要看到每一笔加成和每一个乘数因子，而今天的 `SettlementLanding` 只存合计后的 base、multiplier、energy、writeback。ADR 0004 里「规则留在同一个 QFramework 架构里」这一句不能再成立；其余约束（规则不碰场景、时间、Unity 随机；表现层只走命令、查询和结算记录）继续成立。

## Usage (caller's view)

Unity 表现层不改调用。结算按钮仍是：

```csharp
public IArchitecture GetArchitecture() => TheCallApp.Interface;

void OnSettle()
{
    this.SendCommand(new ConfirmSettlementCommand());
    var record = this.SendQuery(new SettlementRecordQuery());
}

internal bool CommitDrop(OperationDrop drop)
{
    var applied = this.SendCommand(new CommitOperationDropCommand(drop));
    return applied;
}
```

`CommitOperationDropCommand` 的结果仍是 `bool`。悬停仍是 `this.SendQuery(new MonsterDetailsQuery(id))`。

测试夹具仍在第一次 `Interface` 之前打补丁。多出来的只有一句装书，并且必须在架构立起来之前：

```csharp
[SetUp]
public void SetUp()
{
    TheCallApp.Reset();
    ContentGate.Use(ContentBook.Parse(File.ReadAllText(BookFile())));
    TheCallApp.OnRegisterPatch = app =>
        app.RegisterUtility<IDraw>(new ScriptedDraw("能量吐息", "左能量体"));
    App = TheCallApp.Interface;
}
```

`UseRules` 继续按今天的类型键注册 `IDraw`、`ILevelCatalog`、`IToolCatalog` 和可选的 `ClockIntents`。生产路径不注册 `ClockIntents`，`GetUtility<ClockIntents>()` 仍是 null。

Unity 进程的入口只有宿主这一处碰引擎。它不参与计分：

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void Boot()
{
    TheCallApp.Reset();
    var path = Path.Combine(Application.streamingAssetsPath, "call-book.json");
    ContentGate.Use(ContentBook.Parse(File.ReadAllText(path)));
}
```

网页不发送命令，也不自己加能量。`wwwroot/bench.js` 用相对路径取回和 Unity 同一份 `call-book.json`，交给浏览器里的 C#：

```javascript
const book = await fetch(new URL("call-book.json", import.meta.url)).then(r => r.text());
const api = await boot();
api.Boot(book);
const catalog = JSON.parse(api.Catalog());
const cells = api.Cells(JSON.stringify(["上级员工证"]));
const report = JSON.parse(api.Score(JSON.stringify({
  level: 3,
  tools: ["上级员工证"],
  row: [{ skills: ["能量吐息"] }, null, null, null, null, { skills: ["鼓励嘴"] }]
})));
```

`report.landings[i]` 上有 `quote`、`sideCount`、`adds`、`factors`、`energy`、`writeback`。`report.meetsDue` 和 `report.meetsExcess` 来自 `SettlementSystem.Settle` 写下的 `SettlementPayment`，不是 JavaScript 用 due 再比一次。

## Shape

同一批 `Assets/Scripts/Core/Rules` 源文件编译两次。Unity 的 `TheCall.Rules` asmdef 只引用真正的 QFramework，命令类仍继承 QFramework 的 `AbstractCommand` / `AbstractQuery`。`kernel/TheCall.Rules.csproj` 引用 `kernel/TheCall.QfSubset`，那个项目在命名空间 `QFramework` 里只放规则实际用到的架构切片，不引用 `UnityEngine`。`dotnet test` 和浏览器引用这一次编译的结果。Unity 不引用这个 dll，dll 的输出放在 `kernel/artifacts`，不进入 `Assets`。

这不是两套规则。命令方法体仍是今天的 `OnExecute`，没有一层「命令转发给 Session」的壳。QFramework 全文件不能进包，因为文件里有场景、向量和 `RuntimeInitializeOnLoadMethod`。切片只保留 `Architecture<T>` 的 Init 顺序：`Init()`，然后 `OnRegisterPatch`，然后尚未初始化的 Model，然后尚未初始化的 System；`Register<T>` 以 `typeof(T)` 为键、后写覆盖；`Get<T>` 缺席时返回 null。`TheCallApp` 上的 `RuntimeInitializeOnLoadMethod` 删掉，改到 `Assets/Scripts/UnityHost/TheCallBookHost.cs`。规则 asmdef 设 `noEngineReferences: true`。共享规则文件禁止 `UnityEngine`，规则 csproj 把 `LangVersion` 固定为 9.0，避免一边用了 Unity 编译器吃不下的语法。

内容只有 `Assets/StreamingAssets/call-book.json`。规则包不嵌入它，只提供 `ContentBook.Parse(string)`。Unity 宿主读 StreamingAssets，测试夹具读拷到输出目录的同一文件，网页 `fetch` 工作流拷进去的同一文件。三处都把字符串交给 `Parse`。`ContentGate.Use` 是唯一的书槽。架构已经立起时 `Use` 抛异常，所以补丁不能把书换成另一本而目录还握着旧引用。`TheCallApp.Init` 用 `ContentGate.Current` 构造 `SkillCatalog`、`SkillCopy`、`LevelCatalog`、`ToolCatalog`、`TechCatalog`。不把 `ContentBook` 注册成 Utility，避免 `OnRegisterPatch` 再换一本。

`SkillCatalog` 的公开查询方法签名保持不变，`Land` 继续调用 `TryEnergyQuote`、`TrySideCount`、`AddedToOthers`、`DoublesWhenIsolated`、`DoublesAdjacentEnergy`。方法内部改成查表，不再按中文名写分支。效果种类是内部枚举 `EffectKind`。解析在边界做完：未知种类、重复技能名、关卡不是 7 档、句子模板没有覆盖的效果组合，`Parse` 直接抛 `ContentBookException`。过了边界，`Land` 信任目录。

句子不进 JSON。`SkillCopy` 仍是测试里的那个 Utility，但 `TryDescribe` 改调 `SkillSentences.Format`。数字只有效果参数上那一份，句子由共享模板拼出来，所以现有断言「产生5点能量」仍指向机制而不是另一份文案。`Parse` 对每个技能调用 `Format`，拼不出来就拒绝整本书。商店成交价仍是标价除以 2，这个除法留在 `ShopSystem`，不写入 JSON。

`SettlementLanding` 增加 `Quote`、`SideCount`、`Adds`、`Factors`。普通落地的 `Quote` 是传入 `Land` 的 quote；侧向技能的 `Quote` 是 `perMonster`，`SideCount` 是那一侧的怪物数，因为 `BaseAfterAdds` 用的是 `(perMonster + added) * sideCount`，不是外面那份已经乘过侧向人数的 quote。`SideCount == 0` 表示不是侧向技能。`Land` 在现有算术的同一段里记下每一笔非零加成和每一个真正乘上去的因子，然后用私有断言核对：侧向为 0 时 `Base == Quote + sum(Adds)`，否则 `Base == (Quote + sum(Adds)) * SideCount`；`Multiplier` 等于因子之积，没有因子时为 1；`Energy == Base * Multiplier`。算术仍是原来的那几行。网页和断言读记录，不再推一遍公式。毒跳伤走原来的六元构造，`Quote` 等于伤害，加项和因子为空。

网页工作台不是第二套计分，也不是 Unity 的公开 API。`ExtractionBench` 标成 `internal`，只对程序集 `TheCall.Page` 可见。`Run` 先确认书已装上，再 `TheCallApp.Reset()`，装上一个「永远取候选第一项」的 `IDraw`，不注册 `ClockIntents`，然后调用 `FlowSystem.Lay` 和 `SettlementSystem.Settle`。`Lay` 是 `FlowSystem` 上的内部方法：`EnterOperation`、写入工具、`BeginLevel`、`FitExtraction`、`CreateMonster`、`Put`。`BeginLevel` 会把提取格重置为 5，所以 `FitExtraction` 必须在它之后。`Lay` 不计算能量。校验发生在 `Run` 里，并且格数用活的 `IToolCatalog.ExtractionCells`，不在 JavaScript 里写「上级员工证加一格」。

浏览器项目是 `net10.0`、`RuntimeIdentifier=browser-wasm` 的 Exe，不用 Blazor 组件。`PageExports` 上四个 `[JSExport]`：`Boot`、`Catalog`、`Cells`、`Score`。请求和响应的 JSON 只存在于 `TheCall.Page`。规则包的对外入口仍是命令、查询、`ContentBook` / `ContentGate`，以及结算记录上新的项。`Catalog()` 不把工具效果种类发给页面，避免页面自己推导格数。

接口深度：Unity 调用方看到的仍是原来的命令和查询。网页调用方看到四个函数。QfSubset、效果表、句子允许表、`Lay`、书的解析器都在被调用方里面。页面 JSON 不是规则包的类型。

一次页面结算穿过 `bench.js`、`PageExports`、`ExtractionBench`。再往下是已有的 `FlowSystem` 和 `SettlementSystem`，页面作者不必打开它们才能接上按钮。计分作者只打开 `Land`。

静态架构一个进程一份。Unity 播放和 GitHub Pages 不是同一个进程。`ExtractionBench.Run` 每次先 `Reset`，同一块板子跑两次结果相同；中途失败后下一次 `Run` 会先清掉半初始化的架构。

ADR 0004 要改成：Unity 进程里规则类型仍挂在真正的 QFramework 架构上；离开 Unity 的那一次编译挂在 QfSubset 上。规则源文件不调用场景、时间或 `UnityEngine.Random`。`SystemDraw` 继续使用 `System.Random`。

## Synthesis decision

## Tradeoffs accepted

- 我们接受同一份规则源码编译两次，以换取命令类不再包一层转发。Unity 引用真正的 QFramework，`dotnet test` 和浏览器引用 QfSubset。两个宿主的架构行为靠同一批测试文件对齐，而不是靠把 `QFramework.cs` 整文件搬进包。
- 我们接受 `kernel/artifacts` 里有一份绝对不能放进 `Assets` 的 dll。放进去会出现两套 `QFramework` 类型。输出路径固定在 `kernel/artifacts`。
- 我们接受句子措辞写在 C# 模板里，不写在 JSON 里。改「能量吐息是 5 还是 6」只改书；改一句中文要改 `SkillSentences`，这份 C# 两边一起编译。书里因此不能出现模板覆盖不了的效果组合。
- 我们接受手写一个只懂这本书的 JSON 读取器。Unity 这次编译没有 `System.Text.Json`，网页和测试又不能引用 `UnityEngine.JsonUtility`。尾随逗号和注释一律拒绝。
- 我们接受工作台会 `Reset` 当前进程里的 `TheCallApp`。网页是单独的 wasm 进程。不在规则包里再做一套不碰单例的平行 Model。
- 我们接受 `IToolCatalog` 的默认方法改成按 `ToolDefinition` 上的效果字段扫描，不再按「上级员工证」这三个字分支。`FixedTools` 用旧的三元构造时，该工具没有效果。真正的目录从书里带上效果。若某个商店测试曾经靠名字默认值得到 6 格或翻倍，就在那个 `ToolDefinition` 上补效果参数，不把名字开关加回去。
- 我们接受网页不走开局、商店和培育。它只铺提取行并调用 `Settle`。战役仍走原来的命令。两条路的计分都在 `Land`。
- 我们接受结算记录变长。旧的六元构造留给毒跳伤。表现层原来读到的 `Base`、`Multiplier`、`Energy`、`Writeback` 还在。

## Alternatives considered

会话门面加 Unity 薄壳。规则包公开 `CallSession.Place` / `Confirm`，每个现有命令的 `OnExecute` 转发给它。调用方要同时认识会话和命令。15 个命令都是同形转发，代理改壳或改会话都会漏掉另一边。现有测试要么继续打在命令上（会话就不是真正的接口），要么改写成另一套调用（神谕被重写）。QfSubset 的体积换来的是命令体保持原样，这比多一层会话深。

把整个 `QFramework.cs` 对着一个 `UnityEngine` 桩编译，让浏览器和 `dotnet test` 加载「真正的」QFramework。桩必须cover场景、向量、`Object`、`RuntimeInitializeOnLoadMethod`，体积比规则用到的架构切片大，而且会把 BindableProperty 和事件系统带进 wasm。Unity 游戏不能改用这个桩，否则引擎 API 是假的，于是仍然是两次编译，只是第二次编译的依赖更宽。切片把依赖收成 Init、补丁、容器和命令查询。

## Open questions and risks

- 近期要不要打 Android？宿主现在用 `File.ReadAllText` 读 StreamingAssets，这在编辑器和独立播放器上成立，在 Android 包里不成立。规则签名可以保持「只收字符串」。若要上手机，只改 `TheCallBookHost`。
- QfSubset 的 `Dictionary` 遍历顺序必须和 Unity 里 QFramework 的容器一致，Model / System 的 `OnInit` 才同序。两边都跑现有测试是检查办法。若 Unity 运行时的字典顺序不同，测试会先红在开局抽牌或注册时机，而不是红在一个含糊的积分差异上。
- 工作台独占进程、并且会重置单例。这是否就是网页的使用方式？若有人希望 Play 模式里的编辑器窗口调用同一块板子而不清掉当局，当前形状接不住，需要另一次设计，而不是在 `Lay` 旁边再加一个入口。

## Next implementation step

先提交 `kernel/TheCall.QfSubset` 和链接现有测试文件的 `kernel/TheCall.Rules.Tests`，在不改断言、不搬任何数字的前提下让 `dotnet test` 和 Unity 测试宿主都绿。
