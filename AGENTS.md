# Bannerlord repository — Codex permanent instructions

> Scope: the entire `bannerlord` repository. Codex should load this file when a session starts with this repository as the working project. Keep it short: detailed module rules live in module-specific files.

## 1. Repository and authority

- Repository: `GuihuaZhou/bannerlord`. Main modules: `ModifiedArmy`, `ModifiedPolitics`, `ModifiedDiplomacy`, `ModifiedFamily`, `ModifiedTroops`.
- **Current source code, current Git state, and explicit user requirements are the implementation authority.** Old design documents and previous reports are reference material, not proof of what is currently implemented. Never infer completion from a commit title or a Worker report alone.
- Read the `AGENTS.md` applicable to a target directory before changing its code. For `ModifiedArmy`, also read `ModifiedArmy/CODE_ORGANIZATION_RULES.md` before planning or editing any runtime code.
- Never assume another module's requirements from `ModifiedArmy` conventions. Trace any cross-module call and inspect both modules' actual dependency declarations.

## 2. Mandatory session preflight

Before any repository edit or code audit:

1. Identify repository root, current branch and HEAD SHA, and check `git status --short`. Do not overwrite unrelated or uncommitted changes.
2. Identify the requested module and relevant directories; read applicable `AGENTS.md` files and the detailed module rules they reference.
3. Inspect the actual call chain, original/native game API behavior, resource-write sites, persistence, Harmony entry points and relevant tests **for the affected feature**. Do not read every source file indiscriminately.
4. Summarize the scope, untouched boundaries, risks and verification plan. For small safe edits, keep this concise; for large changes, work in independently verifiable phases.
5. If the user requests analysis only, do not modify source or push commits.

## 3. Non-negotiable code organization

- Organize by **business domain first**, then by responsibility (`Models`, `Behaviors`, `Services`, `Patches`, `Contracts`, `Presentation`, etc.). Follow actual existing directories; do not move code purely for naming aesthetics.
- `Model`/policy computes decisions, preferably without game-state writes. `Behavior` owns Campaign events and established save entry points. `Service` performs business execution or mutations. `Coordinator` orchestrates a workflow. `Harmony Patch` is a minimal adapter. `Presentation` is read-only unless explicitly a user action.
- **One authoritative source of truth per state/resource.** Do not create duplicate troop, fief, manpower, gold, food, or mission ledgers when extracting a service.
- Do not claim architecture separation merely because code was split into `partial` files or a forwarding Service was added. Trace active methods and actual write ownership.
- Do not add process-global mutable Campaign/Party/Clan/Settlement references. Runtime caches must be Campaign-scoped, reset at a proven lifecycle boundary, and restored in a defined order where needed.
- Preserve original game AI ownership; do not continuously overwrite native movement/avoidance/army decisions with custom route commands unless explicitly required and verified.
- Avoid circular dependencies between modules in both `SubModule.xml` and compile-time DLL references. Verify dependency direction across *all* affected modules before changing a declaration.

## 4. Safety and compatibility invariants

- Preserve existing `SaveableTypeDefiner` IDs, registered class IDs, field/property IDs, `SyncData` keys, record formats and serialized type identities unless an explicit, tested migration is approved.
- Preserve troop counts (including wounded), source ownership, fief service batches, Hearth, goods, money, recruitment budgets and cooldowns across operations, failures and reloads. No silent duplication/loss.
- Persistent transaction identity and failure recovery must survive save/load. `RecoveryRequired` freezes the affected domain until explicitly reconciled; merely cancelling or rescheduling a task is not recovery.
- Harmony target, Prefix/Postfix return semantics and subscription uniqueness are compatibility contracts. Verify native execution is neither accidentally skipped nor doubled.
- Do not silently change military doctrine, AI planning scores, recruitment odds, economic formulae, XML data, localization keys, logging levels or user-facing gameplay during an architecture-only change.
- Never casually change `Main.cs` startup/new-game rewards, historical SaveId 8/9 compatibility or retired reinforcement loading behavior. Inspect these boundaries before touching them.

## 5. Implementation and verification contract

- Use narrow changes. For architecture work, separate mechanical extraction from behavior/rule changes; fix unrelated defects in a separately described task.
- Before editing a resource-mutating path, record its preconditions, authoritative owner, mutation sequence, rollback/compensation sequence and downstream events. Treat callbacks/events as potentially throwing.
- After editing, inspect `git diff`, run applicable builds/static checks and report **observed** results: `PASS`, `FAIL`, or `BLOCKED` (with reason). A compile pass is not a runtime or save-compatibility pass.
- For ModifiedArmy, prefer existing `ModifiedArmy/tools/Invoke-ReleaseValidation.ps1`, `Test-ArchitectureReleaseGate.ps1`, and `Test-RuntimeAcceptanceEvidence.ps1` when available and applicable. Inspect script parameters before running; the full T01–T24 runtime matrix still requires game/save evidence.
- Record relevant file paths, tests, risks, any unverified constraints, and the final HEAD SHA in the handoff. Do not announce complete acceptance if required critical tests remain `BLOCKED`.
- A blocked mandatory gate prevents advancing to a dependent phase unless the user explicitly accepts the *identified* risk. Do not demand repeated approval for routine edits inside an already authorized phase.
- Do not commit, push, open PRs, or destructively rewrite history without an explicit user request. Never use `git reset --hard` to clean unrelated working-tree changes.

## 6. For ModifiedArmy work

Read `ModifiedArmy/AGENTS.md` and `ModifiedArmy/CODE_ORGANIZATION_RULES.md` before implementing or reviewing its architecture. The existing `ModifiedArmy/ARCHITECTURE.md` is historical documentation (last updated 2026-08-29); verify claims against current code, and do not silently rewrite it.

## 7. Diagnostic logging and localization

- Add enough structured diagnostics to reconstruct important decisions, state transitions, transaction boundaries, rejection reasons and recovery outcomes. Log identifiers and before/after values that help isolate faults, but never let logging perform or repeat business mutations.
- Choose severity deliberately: `Debug` for high-frequency probes and detailed scoring, `Info` for successful routine lifecycle transitions, `Notice` for important player-relevant outcomes, `Warn` for rejected operations, conflicts and recoverable degradation, and `Error` for exceptions or states that require intervention. Do not promote routine events merely to make them visible.
- Prevent log flooding. High-frequency or per-party diagnostics should normally be restricted to the player's current kingdom by the module's shared log filter. Global summaries are acceptable when emitted once per lifecycle boundary and when they do not enumerate the whole map.
- Author base log text and technical reason codes in English. Every player-visible or screen-eligible message must use the localization system and provide the required Chinese translation; do not hardcode Chinese in C# or concatenate unlocalizable player-facing sentences.
- Keep log construction in the owning domain's diagnostics helper where practical, use stable structured fields, and preserve placeholder parity between base and translated text. File-only debug traces still follow the same language and privacy/noise rules.
