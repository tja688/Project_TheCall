# Candidate B — type and file sketch

Bodies are `throw new NotImplementedException()` unless noted as pseudocode. Namespace `TheCall` unless stated.

## Module map

| Project / folder | Responsibility |
|------------------|----------------|
| `src/TheCall.Content/` | JSON schema, load/validate, immutable definitions |
| `src/TheCall.Cqrs/` | Engine-free architecture + command/query base types |
| `src/TheCall.Rules/` | Full game kernel (moved from Assets) |
| `src/TheCall.Rules.Tests/` | NUnit oracle tests |
| `src/TheCall.Sandbox/` | Web session + settlement trace query |
| `src/TheCall.Site/` | Blazor WASM UI → `docs/` publish |
| `Packages/com.thecall.rules/` | UPM embed of Rules + Content JSON |
| `Assets/.../Rules.UnityBootstrap/` | `RuntimeInitializeOnLoadMethod` only |

## TheCall.Content

```
src/TheCall.Content/
  game-content.json                 # authoritative data
  GameContentSchema.cs              # validation rules
  GameContent.cs
  SkillDefinition.cs
  ToolDefinition.cs
  LevelDefinition.cs
  TechDefinition.cs
  ShopConstants.cs                  # wage 20/40, prices from JSON
  ContentLoadException.cs
  TheCall.Content.csproj
```

```csharp
public sealed class GameContent
{
    public IReadOnlyList<SkillDefinition> Skills { get; }
    public IReadOnlyList<ToolDefinition> Tools { get; }
    public IReadOnlyList<LevelDefinition> Levels { get; }
    public IReadOnlyList<TechDefinition> Techs { get; }
    public ShopConstants Shop { get; }

    public static GameContent Load(ReadOnlySpan<byte> utf8Json);
    public static GameContent LoadEmbedded(); // reads manifest resource
}

public sealed class SkillDefinition
{
    public string Id { get; }           // stable key, e.g. "encourage_mouth"
    public string DisplayName { get; }  // 鼓励嘴
    public int Quote { get; }
    public int PerMonster { get; }       // side-count skills
    public int SideCount { get; }
    public Rarity Rarity { get; }
    public SkillAffix Affixes { get; }   // flags enum
    public string[] DetailLines { get; } // SkillCopy source
}
```

## TheCall.Cqrs

```
src/TheCall.Cqrs/
  IArchitecture.cs
  Architecture.cs                   # Architecture<T>, Register*, Get*
  ICommand.cs / IQuery.cs
  AbstractCommand.cs
  AbstractCommand{T}.cs
  AbstractQuery.cs
  AbstractQuery{T}.cs
  AbstractModel.cs
  AbstractSystem.cs
  IUtility.cs
  IController.cs
  CanSendCommandExtensions.cs       # SendCommand, SendQuery
  TheCall.Cqrs.csproj
```

```csharp
namespace TheCall.Cqrs
{
    public interface IArchitecture { /* RegisterUtility, RegisterModel, RegisterSystem, Deinit */ }
    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new() { }
    public abstract class AbstractCommand : ICommand { protected abstract void OnExecute(); }
    public abstract class AbstractCommand<TResult> : ICommand<TResult> { protected abstract TResult OnExecute(); }
    public abstract class AbstractQuery<TResult> : IQuery<TResult> { protected abstract TResult OnDo(); }
}
```

## TheCall.Rules (kernel — selected types)

```
src/TheCall.Rules/
  TheCallApp.cs                     # no Unity; OnRegisterPatch; Init registers GameContent-backed catalogs
  SettlementSystem.cs               # Land unchanged; calls trace utility when present
  FlowSystem.cs
  ShopSystem.cs
  BreedingSystem.cs
  RunModel.cs
  LevelModel.cs
  SkillCatalog.cs                   # facade over GameContent + SkillMechanism
  SkillMechanism.cs                 # bool predicates + effect scheduling helpers
  SkillCopy.cs                      # builds MonsterDetails from SkillDefinition.DetailLines
  ToolCatalog.cs
  LevelCatalog.cs
  TechCatalog.cs
  IDraw.cs / SystemDraw.cs
  ClockIntents.cs
  OperationDrop.cs
  CommitOperationDropCommand.cs     # AbstractCommand<bool>
  ConfirmSettlementCommand.cs
  PlaceMonsterCommand.cs
  ... (all existing command/query files, same public names)
  SettlementRecordQuery.cs
  SettlementEntry.cs                # SettlementLanding, SettlementPayment, ...
  TheCall.Rules.csproj              # refs Content, Cqrs; no Unity
```

```csharp
public sealed class TheCallApp : Architecture<TheCallApp>
{
    public static Action<TheCallApp> OnRegisterPatch { get; set; }
    public static TheCallApp Interface => /* lazy singleton */;
    public static void Reset();       // Deinit + clear patch

    protected override void Init()
    {
        var content = GameContent.LoadEmbedded();
        RegisterUtility(content);
        RegisterUtility(new SkillCatalog(content));
        RegisterUtility(new SkillCopy(content));
        // IDraw, ILevelCatalog, IToolCatalog, ClockIntents via patch in tests
        RegisterModel(new RunModel());
        // ...
        OnRegisterPatch?.Invoke(this);
    }
}

internal sealed class SettlementSystem : AbstractSystem
{
    void Land(/* same signature as today */)
    {
        // ... existing arithmetic ...
        _entries.Add(new SettlementLanding(...));
        GetUtility<SettlementTraceUtility>()?.Record(new SettlementLandingTrace(...));
    }
}
```

## TheCall.Sandbox

```
src/TheCall.Sandbox/
  SandboxSession.cs
  SettlementTraceUtility.cs         # IUtility, buffer cleared in Score
  SettlementLandingTrace.cs
  SettlementTraceQuery.cs
  SandboxPlacement.cs               # DTO for UI row
  TheCall.Sandbox.csproj            # refs Rules, Content, Cqrs
```

```csharp
public sealed class SandboxSession
{
    public static SandboxSession Create(GameContent content);
    public IArchitecture App { get; }

    public void SendCommand<T>(T cmd) where T : ICommand;
    public TResult SendQuery<TResult>(IQuery<TResult> query);

    public void EquipAndPlace(SandboxPlacement placement);
    // pseudocode: create monster id, EquipSkillCommand x N, PlaceMonsterCommand, toggle tools via BuyToolCommand or direct model helper
}

public sealed class SettlementLandingTrace
{
    public string MonsterId { get; }
    public string SkillName { get; }
    public int Quote { get; }
    public int AddedByOthers { get; }
    public int HostModifier { get; }
    public int NextMonsterBonus { get; }
    public IReadOnlyList<MultiplierFactor> MultiplierFactors { get; } // label + factor (2)
    public int Base { get; }
    public int Multiplier { get; }
    public int Energy { get; }
    public int Writeback { get; }
}

public sealed class SettlementTraceQuery : AbstractQuery<IReadOnlyList<SettlementLandingTrace>> { }
```

## TheCall.Rules.Tests

```
src/TheCall.Rules.Tests/
  RulesFixture.cs                   # same OnRegisterPatch pattern
  SideScoreTests.cs                 # linked or copied from Assets/Scripts/Core/Tests
  ... (all existing test files)
  TheCall.Rules.Tests.csproj        # NUnit, ProjectReference Rules
```

## TheCall.Site (Blazor WASM)

```
src/TheCall.Site/
  Program.cs
  App.razor
  Pages/SettlementLab.razor         # skill list, row builder, tools, level, run, results table
  Components/LandingTraceRow.razor
  Services/WasmContentLoader.cs
  wwwroot/content/game-content.json # synced copy
  TheCall.Site.csproj               # WASM, refs Sandbox
```

```csharp
// Program.cs
builder.RootComponents.Add<App>("#app");
builder.Services.AddScoped(sp => SandboxSession.Create(/* content from loader */));
```

## Unity packaging

```
Packages/com.thecall.rules/
  package.json
  Runtime/
    TheCall.Rules.asmdef            # references: TheCall.Cqrs; noEngineReferences: true
    Content/game-content.json       # synced
    (compiled DLLs via asmdef source inclusion OR embed prebuilt from CI — pick one at implement)

Assets/Scripts/Core/Rules.UnityBootstrap/
  TheCallUnityBootstrap.cs
  TheCall.Rules.UnityBootstrap.asmdef  # references com.thecall.rules, UnityEngine
```

```csharp
static class TheCallUnityBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => TheCallApp.Reset();
}
```

## Content sync (build)

```
build/ContentSync.targets
  Copy src/TheCall.Content/game-content.json → Packages/.../Runtime/Content/
  Copy → src/TheCall.Site/wwwroot/content/
  Fail if destination hash != source hash after copy (idempotent)
```

## Data flow (settlement lab)

```
game-content.json
    → GameContent.Load
    → TheCallApp.Init (catalogs)
    → user commands (place, confirm)
    → SettlementSystem.Score → Land (single path)
    → SettlementRecordQuery (oracle shape)
    → SettlementTraceQuery (explain shape)
    → Blazor table
```
