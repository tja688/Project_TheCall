# Explorer findings (three slices)

## Host and catalogs

`TheCallApp` is a QFramework `Architecture<TheCallApp>` singleton. `Interface` lazy-inits utilities, models, and systems. The only Unity API in the rules folder is `RuntimeInitializeOnLoadMethod` on `TheCallApp.ResetStatics`. `QFramework.cs` itself references Unity, so referencing QFramework pulls the engine in.

Content numbers are hardcoded. `SkillCatalog` owns mechanism numbers. `SkillCopy` repeats those numbers in Chinese sentences for `MonsterDetailsQuery`. There is no monster catalog. Monsters are runtime ids with 1 to 4 skills. `ClockIntents` is not registered in `Init`. Settlement null-checks it. Production scoring does not use poison, reverse, or a second walk unless something else registers it. Tests inject it through `OnRegisterPatch`.

Fifteen commands each call one system method, except `LeaveShopCommand`, which calls `ShopSystem.Close` then `FlowSystem.LeaveShop`. Fourteen queries read models or one system view.

## Scoring

`ConfirmSettlementCommand` → `FlowSystem.Confirm` → `SettlementSystem.Settle` → `Score` then swaps, end removals, and payment. `Land` computes `added = AddedByOthers + modifier + nextBonus`, base is `quote + added` or `(perMonster + added) * sideCount`, multiplier is the product of isolation, adjacent 鼓励嘴, first-energy 急急装置, and single-affix 独孤装置. Energy is base times multiplier. The record stores base, multiplier, energy, and writeback, not the terms.

## Presentation

`TheCallPresentation` and `MonsterHover` are the only `SendCommand` / `SendQuery` callers. They use QFramework `IController`. Command arguments are strings, ints, and `OperationDrop`. No Unity object enters the kernel. `CommitOperationDropCommand` is the only command whose bool result the UI uses. Reset is `TheCallApp.Reset()`. Tests depend on `OnRegisterPatch` before the first `Interface` access.
