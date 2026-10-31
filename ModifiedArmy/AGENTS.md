# ModifiedArmy module instructions

This file supplements the repository-root `AGENTS.md`; all root instructions remain applicable.

- Read `CODE_ORGANIZATION_RULES.md` before planning, editing, or reviewing runtime code.
- Treat current code, Git state, native game behavior, and explicit user requirements as authoritative.
- Preserve save identifiers, `SyncData` keys, transaction ownership, troop counts, Hearth, goods, money, budgets, and Harmony semantics unless an explicit migration is approved.
- Keep changes within the requested business domain. Do not mix unrelated cleanup or another module's conventions into ModifiedArmy work.
- For resource mutations, identify the authoritative owner, mutation order, rollback/compensation path, callbacks, and recovery behavior before editing.
- Run the applicable build and release gates, and report runtime/save scenarios as `BLOCKED` until supported by game evidence.
