# Candidate B: JSON-rooted UPM kernel + settlement trace sidecar

## Problem

The Call’s rules live in `Assets/Scripts/Core/Rules` as a Unity asmdef that references `QFramework`, which itself references `UnityEngine`, so the scoring kernel cannot today compile or test as a plain .NET library or run in the browser on GitHub Pages. The behavior oracle is the existing NUnit suite (~4400 lines); `SettlementSystem.Land` must remain the single arithmetic path. Unity presentation (`TheCallPresentation`, `MonsterHover`) must keep calling the same public command and query type names through `TheCallApp.Interface` with QFramework-style `SendCommand` / `SendQuery`; `CommitOperationDropCommand` still returns `bool`. The only Unity API inside rules today is `RuntimeInitializeOnLoadMethod` on `TheCallApp` and must leave the package. Content is hardcoded across `SkillCatalog`, `SkillCopy`, `ToolCatalog`, `LevelCatalog`, `TechCatalog`, and shop/wage constants; designers and a later AI editor need one editable source that Unity and the static site both load, while mechanism code (effect interpretation) stays shared so a rule change ships everywhere. The site must list every skill, let a person compose 1–4 skill monsters on the extraction row, toggle three tools, pick levels 1–7, run the same settlement pipeline, and show each landing’s quote, additive terms, multiplier factors, energy, writeback, and due/excess outcome—with C# in the browser via .NET 10 WASM, no server. `ClockIntents` stays optional and null in production; tests keep injecting `IDraw`, `ILevelCatalog`, `IToolCatalog`, and `ClockIntents` through `OnRegisterPatch` before first `Interface` access.

## Usage (caller's view)

### Designer / content editor

Edit one file at repo root:

`src/TheCall.Content/game-content.json`

Skills, quotes, side counts, affix flags, level due/excess tables, tool names, tech flags, shop prices, and UI copy strings live here. CI and local builds copy the same bytes into the UPM package and the GitHub Pages `wwwroot`. Mechanism code reads a validated `GameContent` snapshot at startup; it does not hardcode skill numbers.

### Unity presentation (unchanged call sites)

```csharp
using TheCall.Cqrs; // replaces QFramework for architecture sends only

public sealed class TheCallPresentation : MonoBehaviour, IController
{
    public IArchitecture GetArchitecture() => TheCallApp.Interface;

    void Confirm()
    {
        this.SendCommand(new ConfirmSettlementCommand());
        var landings = this.SendQuery(new SettlementRecordQuery());
        // ...
    }

    bool TryDrop(OperationDrop drop) =>
        this.SendCommand(new CommitOperationDropCommand(drop));
}
```

Bootstrap (Unity-only asmdef):

```csharp
// Assets/Scripts/Core/Rules.UnityBootstrap/TheCallUnityBootstrap.cs
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() => TheCallApp.Reset();
```

Tests (dotnet + Unity Test Runner):

```csharp
TheCallApp.Reset();
TheCallApp.OnRegisterPatch = app =>
{
    app.RegisterUtility<IDraw>(scriptedDraw);
    app.RegisterUtility<ILevelCatalog>(levels);
    app.RegisterUtility(tools);
    app.RegisterUtility(clockIntents);
};
var app = TheCallApp.Interface;
app.SendCommand(new PlaceMonsterCommand(slot, monsterId));
app.SendCommand(new ConfirmSettlementCommand());
var payment = app.SendQuery(new SettlementRecordQuery()).OfType<SettlementPayment>().Single();
```

### GitHub Pages sandbox (Blazor WASM)

```csharp
// Loaded once: fetch /content/game-content.json → GameContent.Load(json)
var content = await ContentLoader.LoadFromUrl("/Project_TheCall/content/game-content.json");
var session = SandboxSession.Create(content); // wires TheCallApp + registers SettlementTraceUtility

session.SendCommand(new BeginLevelCommand(levelIndex: 3));
foreach (var placement in userRow)
    session.EquipAndPlace(placement); // thin helpers → existing commands

session.SendCommand(new ConfirmSettlementCommand());

var ledger = session.SendQuery(new SettlementRecordQuery());
var traces = session.SendQuery(new SettlementTraceQuery()); // quote, adds, multiplier factors per landing
var targets = session.SendQuery(new LevelTargetQuery());
```

The page binds `traces` and `ledger` to rows; it never reimplements `Land`.

## Shape

### Load-bearing layout

```
src/
  TheCall.Content/          # JSON + schema validation + GameContent immutable snapshot
  TheCall.Cqrs/             # Engine-free fork of Architecture/Command/Query/Model/System used by rules
  TheCall.Rules/            # Moved kernel (Systems, Models, Commands, Queries, catalogs-as-facades)
  TheCall.Rules.Tests/      # NUnit; dotnet test entry; same assertions as today
  TheCall.Sandbox/          # SandboxSession, trace types, SettlementTraceQuery (web-facing)
  TheCall.Site/             # Blazor WASM → publish to /docs for GitHub Pages

Packages/
  com.thecall.rules/        # UPM mirror: Runtime + Editor asmdefs, embeds game-content.json
    Runtime/TheCall.Rules.asmdef   (noEngineReferences: true, refs TheCall.Cqrs only)

Assets/Scripts/Core/
  Rules.UnityBootstrap/     # RuntimeInitializeOnLoad only
  (Presentation stays; swaps QFramework CQRS usings to TheCall.Cqrs)
```

**Single content truth:** `src/TheCall.Content/game-content.json` is authoritative. A small MSBuild target (`ContentSync`) copies it to `Packages/com.thecall.rules/Runtime/Content/game-content.json` and `src/TheCall.Site/wwwroot/content/game-content.json`. Unity loads via `GameContent.LoadEmbedded()` from the package TextAsset or raw file; WASM loads the static copy. Build fails if hashes diverge (optional checksum target).

**CQRS without Unity:** `TheCall.Cqrs` contains the subset of QFramework the rules actually use (`Architecture<T>`, `AbstractCommand`, `AbstractQuery`, `AbstractModel`, `AbstractSystem`, `IUtility`, `IController`, `SendCommand` / `SendQuery` extensions, registration). No `UnityEngine`, no scene types. `TheCall.Rules` references only `TheCall.Cqrs` and `TheCall.Content`. Unity presentation references `com.thecall.rules` + `TheCall.Cqrs` for sends; it may still reference full `QFramework` for unrelated toolkit code, but command/query types resolve from the package.

**Catalogs become facades over `GameContent`:** `SkillCatalog`, `ToolCatalog`, `LevelCatalog`, `TechCatalog`, and `SkillCopy` keep their public method names but read from injected `GameContent` (registered in `TheCallApp.Init`). Effect predicates (`IsIsolation`, side-count quote, affix checks) stay C# methods on `SkillMechanism` that take `SkillDefinition` records from content—mechanism logic is shared, numbers are not duplicated in code.

**Settlement trace sidecar (per boundary-discipline):** Existing `SettlementLanding` rows stay identical for oracle tests. `Land` optionally writes a parallel `SettlementLandingTrace` (quote, `AddedByOthers` breakdown, host modifier, next bonus, each multiplier factor with label) when `SettlementTraceUtility` is registered. Production `TheCallApp.Init` does not register it; `SandboxSession.Create` and selected tests do. `SettlementTraceQuery` reads the utility’s buffer cleared each `Score()`. Unity game UI ignores it; the site depends on it. One arithmetic path, two projections.

**TheCallApp split:** Core `TheCallApp` lives in `TheCall.Rules` without `RuntimeInitializeOnLoadMethod`. `OnRegisterPatch`, `Reset`, and `Init` behavior unchanged. Unity bootstrap calls `Reset` on subsystem registration.

**Testing dual entry:** `TheCall.Rules.Tests` runs under `dotnet test` against the class libraries. The same test sources (linked or shared project) compile into Unity’s `TheCall.Rules.Tests` asmdef for Editor Test Runner, referencing the UPM package instead of `Assets/.../Rules`. Fixture continues to patch utilities before `Interface`.

**GitHub Pages:** `TheCall.Site` is Blazor WebAssembly on .NET 10. Publish profile outputs to repository `docs/` (or `root/docs` with base href `/Project_TheCall/`). WASM bundle includes `TheCall.Rules`, `TheCall.Sandbox`, `TheCall.Cqrs`, `TheCall.Content`—not Unity. No server; JSON fetch + in-browser settlement.

**Interface depth:** Callers outside the package see the same command/query names and `TheCallApp.Interface` as today, plus one optional `SettlementTraceQuery` for explain UI. They do not see JSON schema, `GameContent` parsing, clock tick scheduling, or `Land` internals. `SandboxSession` hides command sequencing for the demo (equip/place helpers) behind three methods so the Blazor page does not orchestrate fifteen command types. Complexity concentrates in `SettlementSystem` and content-driven catalogs; the WASM UI stays thin.

**Deliberately not in scope:** ScriptableObjects, a second scoring implementation, server-side API, duplicating skill math in TypeScript, or keeping catalog numbers in C# constants.

## Synthesis decision

*(Arena synthesis fills this section.)*

## Tradeoffs accepted

- We accept vendoring and maintaining a slim `TheCall.Cqrs` fork in exchange for eliminating UnityEngine from the rules assembly without waiting for upstream QFramework to split.
- We accept a JSON content file plus validation code in exchange for one designer-editable source; malformed content fails at load time rather than at first skill use.
- We accept parallel `SettlementLandingTrace` storage in exchange for keeping oracle assertions on unchanged `SettlementLanding` fields while the site shows multiplier/additive decomposition.
- We accept UPM packaging + MSBuild content sync in exchange for Unity consuming the same compiled kernel as `dotnet test` and WASM without manual DLL copying.
- We accept Blazor WASM bundle size and first-load cost in exchange for running real C# settlement on GitHub Pages with no backend.
- We accept presentation `using` migration from `QFramework` to `TheCall.Cqrs` for architecture sends in exchange for type-compatible commands without dual wrappers.

## Alternatives considered

**A. Single flat `TheCall.Rules.csproj` at repo root, catalogs remain C# constants, site reads generated JSON export.** Rejected: export would be derived, not authoritative—designers would still edit C# or a second file; mechanism and content drift returns. Callers also see no clear content boundary; interface depth is shallow for the site (still needs new trace types).

**B. Roslyn source generator emits `SkillCatalog.cs` from JSON at compile time.** Rejected: mechanism and numbers compile into one generated blob; AI/human edits require rebuild to see changes in Unity play mode unless hot reload regens; GitHub Pages would need the generator in CI anyway—same sync problem with worse debuggability. Hides complexity in generated code callers cannot read.

**C. TypeScript settlement port calling WASM exports of only `Land`.** Rejected: violates single-oracle constraint; boundary leaks arithmetic into JS; two places to fix bugs. High caller-visible complexity for the UI layer.

**Chosen: JSON-rooted content assembly + trace sidecar + UPM mirror (this candidate).** Keeps one `Land`, one content file, and separates explain projection from oracle record shape.

## Open questions and risks

- Can `TheCall.Cqrs` stay API-compatible with presentation’s existing `IController` / extension methods without pulling any Unity-adjacent types from full QFramework, and is duplicating those extensions acceptable long-term?
- Should Unity load `game-content.json` from the embedded package only, or also watch the `src/` copy in Editor for faster designer iteration—and how do we prevent accidental edit of the wrong copy?
- Will linking the same test files into both `dotnet` and Unity Test Runner produce divergent defines or flaky double-registration unless `[SetUp]`/`Reset` discipline is enforced in one shared fixture?
- Is Blazor WASM on GitHub Pages acceptable for load time on mobile, or do we need a trimmed AOT profile and what size budget triggers a fallback?
- When `SettlementTraceUtility` is absent, should `SettlementTraceQuery` return empty or throw—what prevents presentation from accidentally depending on traces in production builds?
- Does ADR-0004 need an explicit successor stating rules architecture moves to `TheCall.Cqrs` while presentation-only Unity references remain?

## Next implementation step

Create `src/TheCall.Content/game-content.json` by extracting current catalog numbers, add `GameContent.Load` with schema validation, and scaffold `TheCall.Cqrs` plus an empty `TheCall.Rules` project that compiles `TheCallApp` without Unity references before moving any system code.
