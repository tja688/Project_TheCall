---
name: unity-localhost-workbench
description: >-
  Scaffold a Unity localhost HTML workbench: authenticated loopback HTTP+WS,
  snapshot/delta protocol, Editor authoring authority, optional runtime hot-apply
  seam. Use when building a browser-based Unity editor, live debug panel,
  hot-config UI, or replacing UI Toolkit with a system-browser workbench.
---

# Unity Localhost Workbench

A **workbench** is a system-browser page that drives Unity over an authenticated
loopback protocol. Unity owns truth; the browser only renders and sends commands.

Default stack: plain `index.html` + `app.js` + `styles.css` (no npm), Editor-hosted
`HttpListener`, Bearer token, WebSocket with long-poll fallback. No embedded WebView/CEF.

Read this skill when scaffolding or extending such a workbench. Copy skeletons from
[`templates/`](templates/) and fill domain seams — do not invent a second transport.

## Steps

### 1. Name the workbench modes

Pick 1–3 modes and the default-mode rule (e.g. Play Mode → live-capture, else static-author):

| Mode | Job |
|------|-----|
| `live-capture` | Stream runtime events; pin a row; inspect/tune from the right |
| `static-author` | Table/list of declarations; patch work-copy; save/revert |
| `control-panel` | Snapshot + commands only; no disk authoring |

**Done when:** mode names and default rule are written down for this feature.

### 2. Design the runtime seam

Under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, expose only what the workbench needs:

- `GetWorkbenchSnapshot()` — immutable copy + monotonic `Revision`
- Optional `Apply…` — **hot-apply**: replace what **future** requests use; reject bad input without clearing the prior table
- Optional `Preview…` — audition/trial without polluting cooldown / formal history
- Optional `Stop…` — act on one live instance by stable id

**Done when:** snapshot fields, command list, and which ops are hot-apply / preview / save-only are listed. See [architecture.md](architecture.md).

### 3. Design authoring authority

One Editor owner holds baseline + working copies. The browser never edits JSON text — only named commands that patch the session.

- Explicit save is the only disk write
- Domain reload: restore from `SessionState` when disk matches baseline; if disk changed, block writes and offer recover-or-discard (no silent merge)
- Shared focus/dirty/error across modes; never clone a second session owner

**Done when:** session owner type, command names, and conflict policy are named.

### 4. Scaffold transport

Implement loopback server + launcher from [protocol.md](protocol.md) and
[`templates/server-skeleton.cs.txt`](templates/server-skeleton.cs.txt):

- Menu / entrypoint → `EnsureStarted()` → `Application.OpenURL(LaunchUrl)` with `#token=`
- Static files from a fixed Editor asset folder; CSP + `no-store`
- `/api/*` Bearer; `/stream` hello-with-token; main-thread job queue on `EditorApplication.update`
- Reject non-loopback, bad Host/Origin, path `..`, payloads over the size cap

**Done when:** authenticated LaunchUrl opens and an unauthenticated `/api/snapshot` is rejected.

### 5. Scaffold the browser client

Start from [`templates/client-connect.js.txt`](templates/client-connect.js.txt) and
[`templates/index.minimal.html.txt`](templates/index.minimal.html.txt)
(plus empty [`templates/styles.minimal.css.txt`](templates/styles.minimal.css.txt) so the static trio exists):

- Token from URL hash; `api()` / `command()` wrappers
- On connect: full `snapshot`; then WS deltas (or long-poll); out-of-order revision → request snapshot again
- UI prefs (pins, filters, widths) may use `localStorage`; work-copies never do

**Done when:** client shows connection state, applies a snapshot, and round-trips one command with `commandResult`.

### 6. Verify the shell

- [ ] Non-loopback / wrong Origin / traversal / oversize / missing token → rejected
- [ ] Reconnect after domain reload recovers token+port and replaces state from snapshot
- [ ] No Unity API calls off the main thread
- [ ] Illegal hot-apply leaves prior runtime table intact
- [ ] Save path (if any) still goes only through the session owner

**Done when:** the checklist above is exercised (tests or manual) for this workbench.

## Reference

- Layers, hot-apply vs preview-save, mode branches → [architecture.md](architecture.md)
- Routes, envelopes, security, reconnect → [protocol.md](protocol.md)
- Copy-paste skeletons → [templates/](templates/)
