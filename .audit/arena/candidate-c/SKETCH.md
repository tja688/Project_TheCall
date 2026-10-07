# Candidate C — type and file sketch

Bodies are `throw new NotImplementedException()` or `// TODO` unless noted. Namespaces follow today’s `TheCall` unless stated.

## `src/TheCall.QFramework/`

| File | Types |
|------|--------|
| `IArchitecture.cs` | `IArchitecture`, `ICanGetModel`, `ICanGetSystem`, `ICanGetUtility`, `ICanSendCommand`, `ICanSendQuery` |
| `Architecture.cs` | `Architecture<T>`, static `Interface`, `RegisterModel/System/Utility`, `Deinit`, `Reset` |
| `AbstractCommand.cs` | `AbstractCommand`, `AbstractCommand<TResult>` |
| `AbstractQuery.cs` | `AbstractQuery<TResult>` |
| `AbstractSystem.cs` | `AbstractSystem`, `AbstractModel`, `IUtility`, `ISystem`, `IModel` |
| `TheCall.QFramework.csproj` | `TargetFramework=netstandard2.1`, no Unity refs |

## `src/TheCall.Content/`

| File | Types |
|------|--------|
| `content/thecall-content.json` | Skills[], tools[], levels[], tech[], copy[] |
| `GameContent.cs` | sealed record snapshot; `Load(Stream)`, `LoadEmbedded()`, `Validate()` |
| `GameContentSchema.cs` | DTOs for JSON deserialization only |
| `IContentIndex.cs` | `QuoteOf(string)`, `RarityOf`, `DueAt(int level)`, … read API for mechanics |
| `TheCall.Content.csproj` | references System.Text.Json |

## `src/TheCall.Rules/` (kernel — moved from Assets)

| File | Types / notes |
|------|----------------|
| `TheCallApp.cs` | `TheCallApp : Architecture<TheCallApp>`, `Init()`, `OnRegisterPatch`, `Reset()` — **no Unity attribute** |
| `Models/RunModel.cs` | unchanged shape |
| `Models/LevelModel.cs` | unchanged shape |
| `Models/SkillInstance.cs`, `Monster.cs`, … | unchanged |
| `Utilities/SkillMechanics.cs` | replaces `SkillCatalog` class name internally; public utility registration still `RegisterUtility(new SkillMechanics(content))` — consider type alias `SkillCatalog : SkillMechanics` for minimal diff |
| `Utilities/SkillCopy.cs` | reads strings from `GameContent` |
| `Utilities/LevelCatalog.cs` | wraps `GameContent` level arrays |
| `Utilities/ToolCatalog.cs` | wraps `GameContent` tool defs |
| `Utilities/TechCatalog.cs` | wraps `GameContent` |
| `Utilities/IDraw.cs`, `ILevelCatalog.cs`, `IToolCatalog.cs` | unchanged interfaces |
| `Utilities/ClockIntents.cs` | optional utility |
| `Systems/SettlementSystem.cs` | `Land(...)` adds breakdown; `Settle()`, `Score()` unchanged control flow |
| `Systems/FlowSystem.cs`, `ShopSystem.cs`, `BreedingSystem.cs` | unchanged signatures |
| `Settlement/SettlementEntry.cs` | `SettlementEntry`, `SettlementLanding`, `SettlementPayment`, … |
| `Settlement/MultiplierFactor.cs` | enum or flags: `Isolation`, `EncourageMouth`, `RushDeviceFirstEnergy`, `SoloDeviceAffix` |
| `Settlement/SettlementLandingBreakdown.cs` | `Quote`, `AddedByOthers`, `HostModifier`, `NextBonus`, `SideCount?`, `IReadOnlyList<MultiplierFactor> ActiveFactors` |
| `Settlement/SettlementLanding.cs` | add `Breakdown Breakdown { get; }` populated in `Land` |
| `Commands/*.cs` | all 15 public command types, e.g. `CommitOperationDropCommand : AbstractCommand<bool>` |
| `Queries/*.cs` | all 14 public query types, e.g. `SettlementRecordQuery : AbstractQuery<IReadOnlyList<SettlementEntry>>` |
| `TheCall.Rules.csproj` | ProjectReference QFramework + Content; `RootNamespace=TheCall` |

### `Land` signature (internal, enriched only)

```csharp
void Land(
    LevelModel level,
    RunModel run,
    SkillMechanics catalog,
    IReadOnlyList<string> cells,
    int cell,
    string monsterId,
    string skillName,
    int quote,
    bool doubleFirstExecution,
    int writeback = 0)
{
    // TODO: existing added/baseValue/multiplier/energy math unchanged
    // TODO: build SettlementLandingBreakdown from intermediate terms
    // _entries.Add(new SettlementLanding(..., breakdown));
}
```

## `src/TheCall.Rules.Unity/`

| File | Types |
|------|--------|
| `TheCallApp.Unity.cs` | `partial class TheCallApp` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetStatics()` |
| `TheCall.Rules.Unity.asmdef` | references Rules + Unity engine only |

## `src/TheCall.Rules.Tests/`

| File | Types |
|------|--------|
| `RulesFixture.cs` | same as today, namespace `TheCall.Tests` |
| `SideScoreTests.cs`, … | moved test files |
| `ScriptedDraw.cs`, `ScriptedLevelCatalog.cs`, … | test doubles |
| `TheCall.Rules.Tests.csproj` | NUnit, ProjectReference Rules |

## `src/TheCall.SettlementLab/`

| File | Types |
|------|--------|
| `Scenario.cs` | `Scenario(ExtractionRow, IReadOnlyList<MonsterDef>, ToolFlags, int LevelIndex, ClockIntents? Intents)` |
| `MonsterDef.cs` | `Id`, `IReadOnlyList<SkillDef>` (1–4), affix/modifier fields |
| `SkillDef.cs` | `Name`, runtime quote/add state if needed |
| `ScenarioJson.cs` | `Parse(string json)`, `Serialize(ScenarioResult)` — boundary validation only |
| `SettlementLab.cs` | `static SettlementResult Run(GameContent content, Scenario scenario)` |
| `SettlementResult.cs` | `Landings[]`, `Payment`, `TotalEnergy`, `MeetsDue`, `MeetsExcess` |
| `LandingDto.cs` | mirrors `SettlementLanding` + breakdown for JSON |
| `SettlementLabEntry.cs` | WASM/JSExport `RunScenario(string json)` |
| `Program.cs` | Blazor WASM host |
| `wwwroot/index.html`, `app.js` | DOM builder, fetch content JSON, call dotnet |
| `TheCall.SettlementLab.csproj` | `Microsoft.NET.Sdk.BlazorWebAssembly`, references Rules + Content |

## Build / deploy (sketch only)

| File | Purpose |
|------|--------|
| `.github/workflows/pages.yml` | `dotnet publish src/TheCall.SettlementLab`, copy `thecall-content.json` to output, deploy to GitHub Pages |
| `Assets/StreamingAssets/thecall-content.json` | symlink or CI copy from `src/TheCall.Content/content/` |
| `Packages/com.thecall.rules/package.json` | UPM embed pointing at `../../src/TheCall.Rules` + Unity partial |

## Data flow (settlement lab)

```
thecall-content.json
    → GameContent.Load
    → SkillMechanics(content)
    → TheCallApp (patched catalogs/draw) + Scenario hydrated models
    → ConfirmSettlementCommand / SettlementSystem.Settle
    → SettlementRecordQuery → map to SettlementResult JSON
    → browser renders quote/adds/factors/energy/due/excess
```
