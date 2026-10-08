# Repository working agreements

## Unity

- This is a Unity project. Required engine version: **6000.6.0f1**.
- Drive the project through the **Unity CLI**. Do not use Unity MCP or other MCP tools to control Unity, Unity Hub GUI, or `-batchmode`.
- The project uses **`com.unity.pipeline` 0.8+**. Keep the CLI current (`unity --version`, then `unity upgrade -y` when behind). Pipeline 0.8 listens on **`127.0.0.1` only**; an outdated CLI that probes `localhost` looks like a dead Pipeline even when the Editor is open.
- First inspect editor instances (pin the project when the shell cwd might not be the repo root):
  ```bash
  unity --no-banner --non-interactive status --format json --project-path "<项目路径>"
  ```
- If this project already has a `ready` instance, reuse it and execute commands directly; do not launch another editor.
- If this project has no instance, open it using its absolute project path:
  ```bash
  unity --no-banner --non-interactive open "<项目路径>" --args "-automated"
  ```
- After `open`, wait for Pipeline in one step (do not hand-roll sleep loops):
  ```bash
  unity --no-banner --non-interactive status --until-ready --project-path "<项目路径>" --timeout 300 --format json
  ```
- If an instance exists but is not `ready`, use the same `--until-ready` command or report the blocker; do not open a duplicate editor.
- Discover Editor tools with `unity list` (not `unity command list` — that tries to run a Pipeline command named `list`).
- Control the ready instance with:
  ```bash
  unity --no-banner --non-interactive command <命令> --project-path "<项目路径>" --caller plugin --skill <技能名> --format json
  ```
  Use the skill name that produced the call (e.g. `unity-cli` when following that skill).
- Make scene changes through Unity editor operations driven by the CLI. Never manually edit scene YAML.

### When CLI cannot connect

Work through this order before reinstalling packages or opening another Editor:

1. **`unity --version`** — upgrade with `unity upgrade -y` if the CLI predates Pipeline 0.8 compatibility (e.g. stuck on `1.0.0-beta.1`). Remove stale rollback files under `%LOCALAPPDATA%\Unity\bin\` (`unity.exe.previous*`) only after a successful upgrade.
2. **`unity pipeline list --format json`** — for this project, confirm `pipelineServer.isReachable` and `apiUrl` uses `http://127.0.0.1:…` (not `localhost`).
3. **`unity status --format json --project-path "<项目路径>"`** — read `state`, and `blockedBy` if a modal dialog is holding the Editor (`STATUS_BLOCKED_BY_DIALOG`).
4. **Safe Mode** — if `pipeline list` reports Safe Mode, fix compile errors and restart the Editor; Pipeline does not load in Safe Mode.
5. **`unity pipeline install`** — only when the project lacks `com.unity.pipeline` or Hub/registry install is actually missing.

## Git

- Work in the existing checkout and current branch.
- Do not create or use Git worktrees.
- Do not create or switch branches unless the user explicitly requests that operation. Task implementation or isolation is not implicit authorization.

## Architecture

Gameplay code uses QFramework. One `Architecture<T>` registers every Model, System, and Utility. `SendCommand`, `SendQuery`, and `SendEvent` on that Architecture are the write path, the read path, and the notification path.

- A Controller changes Model or System state by sending a Command. A read that leaves state unchanged is a Query. The Model or System that owns the change notifies Controllers through an Event or a `BindableProperty`.
- Commands and Queries hold no fields. Each piece of mutable state has one Model owner.
- Controllers, Systems, Commands, and Queries call downward with `GetSystem`, `GetModel`, and `GetUtility`.
- A Command or Query that exists to keep this routing stays. Fold a further one-caller wrapper that adds no rule into the type that owns the rule.

When calling or implementing a QFramework type, read `Assets/Notes/QFramework API.md`.

## Agent skills

### Issue tracker

Issues and specs are tracked in GitHub Issues for `tja688/Project_TheCall`. Before creating, fetching, updating, or triaging tickets, read `docs/agents/issue-tracker.md`.

### Triage labels

Use the five default canonical triage labels. Before applying triage labels, read `docs/agents/triage-labels.md`.

### Domain docs

Use a single-context layout: root `GLOSSARY.md` and `docs/adr/`. Before exploring the codebase or proposing domain or architecture changes, read `docs/agents/domain.md` and follow its consumer rules.
