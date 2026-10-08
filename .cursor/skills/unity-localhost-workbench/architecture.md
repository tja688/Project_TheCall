# Workbench architecture

## Four layers

```
Browser  →  Transport  →  Authoring  →  Runtime Seam
 (view)     (loopback)    (Editor)      (Play/Dev)
```

| Layer | Owns | Must not |
|-------|------|----------|
| **Browser** | DOM, selection, pins, filters, local UI prefs | Own work-copy or write disk JSON |
| **Transport** | Port, token, CSP static files, revision push, main-thread dispatch | Encode domain rules |
| **Authoring** | Baseline/working JSON, dirty/focus/conflict, save/revert/reload | Let the browser patch files directly |
| **Runtime Seam** | Live snapshot, hot-apply table, preview/stop by id | Silently replace catalog with empty on parse failure; restart already-playing instances unless commanded |

Truth flows Editor → browser via `snapshot`/`delta`. Intent flows browser → Editor via named `command`s. The transport is a dumb pipe.

## Mode branches

Inline what every workbench needs (layers + protocol). Specialize only the domain surface:

### live-capture

- Runtime emits append-only history with a stable monotonic id (Sequence / event id) for pinning
- Pin stores a snapshot of that event client-side so ring-buffer eviction does not erase the sample
- Right pane binds to authoring entry for the event's key; edits hot-apply when Play Mode is on
- Pause-into-view queues DOM rows; server capture continues

### static-author

- Drive from declarations + work-copy rows; same session as live-capture
- Filters for dirty / unbound / broken / draft — domain-defined
- Save/revert/reload; hygiene validation before disk write

### control-panel

- No Session disk authority required
- Commands mutate runtime or Editor utilities only; snapshot reflects results
- Still uses the same envelopes and auth

A single page may host several modes; share dirty/focus/connection chrome; never fork a second authoring owner.

## Hot-apply vs preview vs save

| Verb | Meaning | Use when |
|------|---------|----------|
| **hot-apply** | Atomically replace the table future requests resolve against; bump Revision on success only | Operator must hear/see the next trigger under the work-copy without leaving Play |
| **preview** | One-shot trial of a binding/config; ignore “disabled”; do not touch cooldown/burst/formal history | Audition without waiting for the next natural trigger |
| **save** | Persist work-copy to disk through Authoring; then re-apply if Play is active | Promote temporary experiment to formal data |

If the subsystem cannot safely mutate live state (e.g. generational music tracks), use **preview + save** only — document that hot-apply is out of scope for that slice rather than half-implementing it.

### Temporary vs permanent disable (when applicable)

- Work-copy `enabled=false` + hot-apply → observable suppressed/skipped outcome, still in history (**temporary**)
- Same row after explicit save → formal disabled (**permanent**)
- Revert restores saved state and re-applies

## Domain reload

Persist in `UnityEditor.SessionState` (not browser storage):

- Token + port (so an open tab reconnects)
- Baseline + working JSON, focus, status

On restore: if disk equals stored baselines → `LoadFromSnapshots` and continue. If disk diverged → keep transient payload, load disk into live sessions, **block** save/revert/apply until the operator chooses recover-temporary (overwrite disk path) or discard-temporary. No silent merge.

## Assembly placement

Typical split (rename to project conventions):

- `*.Editor` — server, launcher, authoring state, static web folder
- Runtime / Presentation — seam APIs behind Editor/Dev guards
- Keep third-party backends behind adapters so structure guards stay valid

## Reference implementations

When working inside a repo that already has a workbench, prefer copying its transport patterns over rewriting from scratch. The skeletons under [`templates/`](templates/) are the domain-agnostic floor.
