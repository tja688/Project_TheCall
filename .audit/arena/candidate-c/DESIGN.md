# Candidate C — Headless QFramework core + JSON content snapshot

## Problem

The Call rules live in Unity under `Assets/Scripts/Core/Rules`, compiled with QFramework, which references `UnityEngine`. GitHub Pages must run the same settlement arithmetic (`SettlementSystem.Land`) in the browser via .NET WASM, and `dotnet test` must run the same NUnit oracle without the editor. Presentation (`TheCallPresentation`, `MonsterHover`) must keep calling the same public command and query type names through `TheCallApp.Interface` and `SendCommand` / `SendQuery`; `CommitOperationDropCommand` still returns `bool`. Content is hardcoded across six C# catalog/copy types with no ScriptableObject; designers and a later AI editor need one file both Unity and the static site load, while skill *mechanism* (effect predicates, landing responses, affix behavior) stays shared C# so a rule change ships everywhere. `ClockIntents` stays optional and null in production. Tests inject `IDraw`, `ILevelCatalog`, `IToolCatalog`, and `ClockIntents` through `OnRegisterPatch` before first `Interface` access. The non-obvious part is type identity: systems and commands today inherit QFramework base types that currently ship with UnityEngine; splitting “rules” without forking those bases breaks registration unless one headless QFramework assembly becomes the single inheritance root for both hosts.

## Usage (caller's view)

### Unity presentation (unchanged surface)

```csharp
using QFramework;
using TheCall;

// Still lazy-init on first access; patch hook unchanged for tests.
TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(new ScriptedDraw(...));
var app = TheCallApp.Interface;

app.SendCommand(new PlaceMonsterCommand(slot, monsterId));
app.SendCommand(new ConfirmSettlementCommand());
var landings = app.SendQuery(new SettlementRecordQuery()).OfType<SettlementLanding>();
var ok = app.SendCommand(new CommitOperationDropCommand(drop));
```

Reset for domain reload stays `TheCallApp.Reset()`; the Unity-only partial adds subsystem registration reset.

### dotnet test (oracle, no Unity)

```bash
dotnet test src/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj
```

```csharp
TheCallApp.Reset();
TheCallApp.OnRegisterPatch = app => { /* same registrations as RulesFixture */ };
var landings = TheCallApp.Interface
    .SendQuery(new SettlementRecordQuery())
    .OfType<SettlementLanding>();
Assert.That(landings[0].Energy, Is.EqualTo(expected));
```

### GitHub Pages settlement lab (browser)

Static site loads `content/thecall-content.json` (copied beside WASM) for skill list and copy strings. User builds 1–4-skill monsters, fills the extraction row, toggles three tools, picks level 1–7, clicks run.

```javascript
const input = buildScenarioFromDom(content); // JSON DTO
const resultJson = await dotnetSettlement.runScenario(JSON.stringify(input));
const result = JSON.parse(resultJson);
// result.landings[].quote, .added, .multiplierFactors[], .energy, .writeback
// result.totalEnergy, .meetsDue, .meetsExcess
```

C# entry (Blazor WASM or thin `browser-wasm` host):

```csharp
public static class SettlementLabEntry
{
    public static string RunScenario(string json) =>
        SettlementLab.Run(GameContent.LoadEmbedded(), ScenarioJson.Parse(json));
}
```

Designer / AI content edit: change `content/thecall-content.json` only for numbers and display strings; rebuild Unity and publish web artifact from the same file path in repo.

## Shape

### Module map (repo layout)

| Path | Role |
|------|------|
| `src/TheCall.QFramework/` | netstandard2.1 subset of today’s `QFramework.cs`: `Architecture<T>`, `AbstractCommand`/`Query`/`System`/`Model`, `IUtility`, `SendCommand`/`SendQuery`, registration, deinit. **No UnityEngine.** |
| `src/TheCall.Content/` | `content/thecall-content.json` + `GameContent` immutable snapshot + load/validate at boundary. |
| `src/TheCall.Rules/` | All models, systems, internal catalogs/mechanics, settlement types, public commands/queries. References QFramework + Content. **No Unity.** |
| `src/TheCall.Rules.Tests/` | Moved NUnit tests from `Assets/Scripts/Core/Tests` (rules-only). References Rules only. |
| `src/TheCall.Rules.Unity/` | `TheCallApp` partial: `[RuntimeInitializeOnLoadMethod]` reset only. Optional tiny Unity-specific utilities if any appear later. References Rules; Unity asmdef references this + Rules DLL. |
| `src/TheCall.SettlementLab/` | Blazor WebAssembly (or `browser-wasm` + JS interop) referencing Rules + Content. Publishes to `docs/` or `web/dist/` for GitHub Pages. |
| `Assets/Scripts/Core/Rules/` | Becomes thin re-exports or deleted after move; Unity asmdef points at `Packages/com.thecall.rules` embedding `src/TheCall.Rules` + Unity partial via UPM path or prebuilt DLL copy in CI. |

Single compile truth for rules logic: **`src/TheCall.Rules/**/*.cs`** compiled by `dotnet` and referenced by Unity’s assembly (UPM package with asmdef + same sources, or DLL drop — implementer picks UPM embed; both hosts must load the **same** built Rules assembly in CI).

### Content: one JSON, mechanism in C#

- **`thecall-content.json`** holds skill names, rarity lists, quotes, side counts, affix flags, tool names/flags, level due/excess arrays, tech names, and **`SkillCopy` UI strings** (Chinese sentences keyed by skill + field).
- At load, `GameContent` validates schema (required keys, 15 skills, 7 levels, 3 tools) and exposes read-only indices.
- **`SkillMechanics`** (renamed behavior from today’s `SkillCatalog` predicate methods) takes `GameContent` for numeric lookups and keeps all boolean/effect graph logic in C# — `TryLandingResponse`, `DoublesWhenIsolated`, breeding hooks, etc. **No second formula:** numbers come from JSON; control flow stays code.
- Unity and WASM both call `GameContent.LoadFromFile(path)` or `LoadEmbedded()` reading the **same repo file** copied into StreamingAssets / site root in build scripts.

### Headless QFramework as the shared spine

Move settlement and flow systems **with** their `AbstractSystem` inheritance into `TheCall.Rules`, compiled against `TheCall.QFramework`. Unity stops compiling the monolithic `QFramework.cs` for rules purposes and instead references `TheCall.QFramework.dll` (Unity-specific scene helpers, if still needed, move to a separate `TheCall.QFramework.Unity` optional asmdef that presentation may use). Public command/query types remain in namespace `TheCall` in the Rules assembly so presentation usings stay valid.

`TheCallApp` core (`Init`, `OnRegisterPatch`, `Interface`, `Reset` without Unity attribute) lives in **`TheCall.Rules`**; **`TheCall.Rules.Unity/TheCallApp.Unity.cs`** partial adds `ResetStatics` only.

### Settlement record enriched in `Land` (same arithmetic)

Today `SettlementLanding` stores aggregated `Base` and `Multiplier` but the page must show quote, each add term, and each multiplier factor. **Inside existing `Land`**, after computing `added`, `baseValue`, and `multiplier` exactly as today, construct:

- `SettlementLandingBreakdown` with `Quote`, `AddedByOthers`, `HostModifier`, `NextBonus`, optional `SideMonsterCount`, and `MultiplierFactors` (`Isolation`, `AdjacentEncourage`, `FirstEnergyTool`, `SingleAffixTool` booleans).

Extend `SettlementLanding` to carry optional breakdown (always populated during `Score`; Unity UI can ignore). Web DTO maps breakdown 1:1. NUnit tests keep asserting `Energy`, `Base`, `Multiplier`, `Writeback` — no new oracle unless a dedicated breakdown test is added later.

`SettlementLab` builds a minimal **`Scenario`** (extraction ids → monster defs with skill instances, tool flags, level index, optional `ClockIntents`), hydrates `RunModel`/`LevelModel`, runs `ConfirmSettlementCommand` or `SettlementSystem.Settle()` through the same app host, returns JSON including payment line and due/excess flags from `LevelModel` + `SettlementPayment`.

### Boundary discipline

- JSON validated once in `GameContent.Load*`; mechanics trust `GameContent` indices inside.
- `ScenarioJson.Parse` validates row width, skill count 1–4, level 1–7 at WASM boundary.
- No Unity types in Rules/Content/QFramework core; no QFramework in Content.

### Interface depth

Callers see **unchanged** command/query names and `TheCallApp.Interface`. WASM sees **one** function `RunScenario(json)`. Complexity hidden: clock tick scheduling, swap/removal queues, catalog/mechanics branching, and breakdown assembly all stay inside `SettlementSystem` and `SkillMechanics`. Exposed on purpose: `OnRegisterPatch` for test doubles (same hook name and semantics).

## Synthesis decision

*(Leave empty for arena synthesis.)*

## Tradeoffs accepted

- We accept **maintaining a headless QFramework fork** in exchange for **one inheritance hierarchy** so systems and commands do not split into “pure engine + thin wrapper” duplication across fifty files.
- We accept **JSON for numeric and copy content** in exchange for **a single designer-editable file**; mechanism stays C#, so JSON edits cannot express new effect types without code.
- We accept **extending `SettlementLanding` in the hot path** in exchange for **explain UI without recomputing multiplier terms** (which would drift from `Land`).
- We accept **Blazor WASM bundle size and load time** in exchange for **real C# in browser** without maintaining a TypeScript port of `Land`.
- We accept **UPM + dotnet dual-build wiring complexity** in exchange for **one source tree** under `src/` rather than `#if UNITY` dual compilation.

## Alternatives considered

1. **Pure engines (`SettlementEngine`) + QF skins in Unity-only assembly** — Hides QF from dotnet by moving all logic into stateless engines; Unity `SettlementSystem` delegates. **Rejected:** every system (`Flow`, `Shop`, `Breeding`) duplicates a skin layer; fifty-file move becomes two layers; tests would either bypass commands (oracle drift) or still need headless QF anyway. **Interface depth:** callers still use commands, but implementers maintain parallel types.

2. **Roslyn source generator: JSON → generated `SkillCatalog` C#** — Single content file, mechanism numbers compiled into generated partial class. **Rejected:** effect graph and landing responses stay hand-written; generator only saves quote tables and reintroduces “which file is truth” for AI edits; build step harder for Unity designers without dotnet. **Interface depth:** same public catalogs, but contributors must understand generated output to debug.

3. **TypeScript settlement kernel for web + C# for Unity** — WASM shell calls JS implementation mirrored from tests. **Rejected:** violates “kernel must be C# in browser” and guarantees a second formula over time. **Interface depth:** smallest WASM surface, but pushes all explain logic onto JS callers.

**Chosen:** headless QFramework + JSON snapshot + enriched `Land` record, because it preserves command/query oracle tests verbatim, keeps `Land` as the single arithmetic path, and gives one content file without generated-code indirection.

## Open questions and risks

- Can Unity reference `TheCall.QFramework.dll` while legacy `Assets/.../QFramework.cs` remains for non-rules code without type clashes in the same namespace?
- Is Blazor WASM acceptable for GitHub Pages repo size limits, or should `SettlementLab` publish trimmed `browser-wasm` with manual JS interop only?
- Should `SkillCopy` strings live entirely in JSON, or split JSON (numbers) + `.resx` (copy) while still meeting “one content source” for the page skill list?
- Moving tests off `Assets/Scripts/Core/Tests` may require Unity Edit Mode test runner to delegate to `dotnet test` in CI — is Unity-side duplicate test assembly still required for the done predicate?
- Enriched breakdown fields must not tempt tests to assert decomposed terms before golden tests exist — who owns breakdown golden vectors?

## Next implementation step

Add `src/TheCall.QFramework/TheCall.QFramework.csproj` and extract the non-Unity subset from existing `QFramework.cs`, then add `src/TheCall.Rules/TheCall.Rules.csproj` that compiles a vertical slice (`RunModel`, `LevelModel`, `SettlementSystem.Land` + one test) with `TheCallApp` core and no Unity references, proving `dotnet test` can host `OnRegisterPatch` and `SettlementRecordQuery`.
