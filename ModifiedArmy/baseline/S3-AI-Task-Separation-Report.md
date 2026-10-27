# S3 AI Task Separation Handoff

## Start record

- PHASE: S3
- BASE_SHA: `0fe64f9` (`refactor(army): define campaign recovery ownership`)
- OBJECTIVE: separate runtime task coordination, primitive persistence mapping and settlement-visit execution without changing planning policy.
- IN_SCOPE: `AiForcePlanningBehavior`, task missions/services, existing public compatibility entry points.
- OUT_OF_SCOPE: planning scores, random probability, troop selection, transfer authorization, supply/fief/garrison decomposition.

## Method migration

| Original responsibility | New owner | Compatibility entry |
|---|---|---|
| Encode/decode `_savedTaskRecords` | `PartyForceTaskPersistence` | `AiForcePlanningBehavior.SyncData` retains key and delegates |
| Find/prune runtime tasks | `PartyForceTaskCoordinator` | Behavior uses the same `_tasks` list through a getter |
| Build daily context and apply plan | `PartyForceTaskCoordinator` | `EvaluateParty` remains a public proxy |
| Take/release GoToSettlement ownership | `PartyForceTaskCoordinator` | Daily Behavior event delegates |
| Arrival recruitment/war rest | `PartyVisitExecutionService` | `ExecuteAtSettlement` remains a public proxy |
| Routine garrison donation | `PartyVisitExecutionService` | Called only when no matching force task owns the visit |
| Capture donation | `PartyVisitExecutionService` | `ExecuteCaptureDonation` remains a public proxy |
| Peace demobilization | `PartyVisitExecutionService` | Called only after routine donation is not committed |
| Five-state result transition | `PartyVisitExecutionService.ApplyResult` | S1 committed/partial/recovery semantics retained |

The coordinator receives `Func<List<PartyForceTask>>`, so restoring `_tasks`
replaces the Behavior list without leaving the coordinator with a stale or
second task collection. No new persistent task authority was introduced.

## Preserved contracts

- SyncData key remains `modifiedarmy_force_task_records_v1`.
- Record separator, field order and enum integer representation are unchanged.
- Operation ids remain `force`, `visit` and `capture` with their previous components.
- Daily party+settlement gate remains in `PartyExchangeBehavior`; operation-id idempotency remains in the transaction journal.
- Random admission still calls `MBRandom.RandomFloat` once per managed party in the original daily loop.
- Planner/model inputs, scores, source selection and movement conditions are unchanged.
- `RecoveryRequired` still suspends and freezes; partial commit consumes the current attempt.

## Verification

| Check | Result |
|---|---|
| Release build | PASS: 0 errors, 7 existing/environment warnings |
| Save key/format static comparison | PASS |
| OperationId format static comparison | PASS |
| SaveId 8/9 and protected files | PASS: untouched |
| T01-T07/T14/T24 game behavior | BLOCKED pending Bannerlord run |

## Known cleanup and risk

- The old private helper bodies remain temporarily in `AiForcePlanningBehavior` as unreachable compatibility-era code while the live paths delegate to the new owners. They should be deleted after runtime equivalence is confirmed; they do not execute or own a second state collection.
- T24 must confirm `SettlementEntered` plus `DailyTickPartyEvent` still causes one visit attempt per party/settlement/day.
- A save/load test must confirm the exact primitive task record restores and the coordinator sees the replaced list.

- ROLLBACK: discard the uncommitted S3 files and Behavior edits.
- NEXT_PHASE_ALLOWED: **NO pending runtime equivalence and removal of unreachable duplicate helpers**.
