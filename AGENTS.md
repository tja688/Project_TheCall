# Repository working agreements

## Unity

- This is a Unity project. Required engine version: **6000.6.0f1**.
- Drive the project through the **Unity CLI**. Do not use Unity MCP or other MCP tools to control Unity, Unity Hub GUI, or `-batchmode`.
- First inspect editor instances:
  ```bash
  unity --no-banner --non-interactive status --format json
  ```
- If this project already has a `ready` instance, reuse it and execute commands directly; do not launch another editor.
- If this project has no instance, open it using its absolute project path:
  ```bash
  unity --no-banner --non-interactive open "<项目路径>" --args "-automated"
  ```
- If an instance exists but is not ready, check its status until ready or report the blocker; do not open a duplicate editor.
- Control the ready instance with:
  ```bash
  unity --no-banner --non-interactive command <命令> --format json
  ```
- Make scene changes through Unity editor operations driven by the CLI. Never manually edit scene YAML.

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

Use a single-context layout: root `CONTEXT.md` and `docs/adr/`. Before exploring the codebase or proposing domain or architecture changes, read `docs/agents/domain.md` and follow its consumer rules.
