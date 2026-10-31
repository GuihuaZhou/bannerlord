# ModifiedArmy code organization rules

## Domain ownership

- Organize code by business domain, then responsibility: `Models`, `Behaviors`, `Services`, `Patches`, `Contracts`, `Presentation`, and `Diagnostics`.
- Models and policies compute decisions without mutating campaign resources.
- Behaviors own campaign events and established persistence entry points.
- Services own business execution and mutations. Coordinators schedule and order workflows but do not create a second ledger or budget.
- Harmony patches are minimal adapters. Presentation reads state and writes only through an explicit user-action service or behavior API.

## State and transactions

- Maintain one authoritative owner for every roster, manpower pool, Hearth balance, item inventory, gold balance, budget, task, and mission state.
- Campaign runtime caches must be campaign-scoped and rebuilt or cleared at a proven lifecycle boundary.
- Resource-changing workflows must define validation, mutation order, compensation, downstream event handling, and save/load recovery.
- `RecoveryRequired` state freezes the affected workflow until reconciliation; cancellation or rescheduling is not recovery.

## Compatibility and integration

- Preserve save type IDs, property IDs, keys, record formats, Harmony targets, event subscription uniqueness, and native AI ownership unless explicitly authorized and verified.
- Verify dependency direction in source references and `SubModule.xml`; do not introduce circular module dependencies.
- Architecture extraction must move real ownership, not merely add forwarding services or `partial` files.

## Diagnostics and presentation

- Important decisions and state transitions use stable reason codes and structured identifiers/before-after values.
- Avoid per-party/global-map log flooding; use the shared player-kingdom filter for frequent diagnostics.
- Player-visible text is localized with English base text and Chinese translation. Do not hardcode Chinese in C#.
