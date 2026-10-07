# Rules kernel grounding

Read from the current checkout on branch `dev` at `d7e0931`, plus uncommitted edits in `SettlementSystem.cs`, tests, and presentation.

## What exists

`Assets/Scripts/Core/Rules` is about 3700 lines across 50 C# files. `Assets/Scripts/Core/Tests` is about 4400 lines of NUnit tests. The assembly `TheCall.Rules` references `QFramework` and does not set `noEngineReferences`. The only `UnityEngine` use inside the rules folder is `TheCallApp.ResetStatics`, which calls `RuntimeInitializeOnLoadMethod`.

`QFramework.cs` uses `UnityEngine` (scene, vectors, runtime init). The rules code uses the architecture subset: `Architecture<T>`, `AbstractCommand`, `AbstractQuery`, `AbstractModel`, `AbstractSystem`, `IUtility`, `SendCommand`, `SendQuery`, `GetModel`, `GetSystem`, `GetUtility`. Rules code does not call `SendEvent` or `BindableProperty`.

Presentation reaches the kernel only through `TheCallApp.Interface` plus `SendCommand` / `SendQuery` in `TheCallPresentation.cs` and `MonsterHover.cs`. Both files `using QFramework` for that.

## Content

There is no ScriptableObject and no JSON catalog. Numbers live in C#.

- `SkillCatalog` owns 15 skill names, rarity lists, quotes, side counts, affixes, and boolean effect queries.
- `ToolCatalog` owns 急急装置, 上级员工证, 独孤装置.
- `LevelCatalog` owns seven due values `50, 75, 100, 150, 200, 250, 300` and excess `60, 90, 120, 175, 230, 285, 350`.
- `TechCatalog` owns five tech names and their flags.

Monsters are not a fixed library. A monster is an id plus 1 to 4 skill instances. Opening and the shop create them by drawing skill names.

## Scoring

`ConfirmSettlementCommand` calls `FlowSystem.Confirm`, which calls `SettlementSystem.Settle`. `Score` clears energy, reads tool flags, schedules a left-to-right pass, an optional reverse pass, an optional second pass, and clock intents. Each occupied cell runs `ScoreStay`.

`Land` is the arithmetic step.

- Added amount is other monsters' `AddedToOthers`, plus the host modifier, plus a next-monster bonus.
- Base is `quote + added`, except side-count skills, where base is `(perMonster + added) * monsters on that side`.
- Multiplier starts at 1. Isolation doubles it. Each adjacent 鼓励嘴 doubles it. The first energy execution doubles it once when 急急装置 is held. A single-affix monster doubles it when 独孤装置 is held.
- Energy is `base * multiplier`. The record stores monster id, skill name, base, multiplier, energy, writeback. It does not store the terms that produced base and multiplier.

## ADR conflict

`docs/adr/0004-rules-without-unity.md` says rules stay registered in the same QFramework architecture and must not call Unity scene, time, or random. The new requirement is a package with no Unity runtime, because QFramework itself references Unity. That part of 0004 cannot stay. The rest of 0004 (no scene, time, or random inside rules, presentation only through commands, queries, and the settlement record) still holds.

## Done predicate

All four must be true on the real artifacts.

1. `dotnet test` passes on a rules test project whose rules project file does not reference Unity.
2. Existing settlement assertions still match. The kernel does not get a second scoring formula.
3. `https://tja688.github.io/Project_TheCall/` loads a page that lists every skill from the shared content file, lets a person build a row of monsters (each 1 to 4 skills), toggle tools, pick a level, run the same `SettlementSystem`, and show each landing's quote, adds, multiplier factors, energy, writeback, plus whether the total meets due and excess.
4. The Unity game still sends the same commands. A Unity test run of the rules tests passes, and the content file the page copies is the file Unity loads.
