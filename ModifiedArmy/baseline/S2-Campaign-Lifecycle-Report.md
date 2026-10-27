# S2 Campaign Lifecycle Handoff

## Start record

- PHASE: S2
- BASE_SHA: `f178c87c` (`fix(army): preserve loaded basic troop configuration`)
- OBJECTIVE: isolate runtime state by Campaign and prevent force-resource writes until persistent state and dependent runtime indexes are restored.
- IN_SCOPE: lifecycle coordination, AI task post-load validation ordering, transaction readiness gates, cross-load new-game flag reset.
- OUT_OF_SCOPE: S3 task-coordinator extraction, supply/fief/garrison service decomposition, Patch and dependency work.

## Recovery sequence

```text
Campaign construction
  -> each Behavior.SyncData loads its existing authoritative key
  -> specialist OnGameLoadFinished handlers restore supply/transport routes
  -> CampaignRecoveryCoordinator rebuilds fief service index
  -> coordinator revalidates AI tasks without replaying resources
  -> earlier OnSessionLaunched handlers initialize fief/supply session state
  -> coordinator (registered last) marks Ready
  -> PartyExchangeService / recruitment execution may mutate resources
```

New games skip the load stage and become Ready only after the final
`OnSessionLaunched` barrier.

## Implemented

1. Added `CampaignRecoveryCoordinator`, registered after every specialist Behavior. It owns an explicit `Constructing -> LoadingPersistentState -> RebuildingRuntimeIndexes -> Ready/Failed` state.
2. Existing SyncData keys and Saveable identities remain in their original Behaviors. The coordinator stores no persistent business data.
3. The final recovery pass rebuilds `FiefPartyManager`'s serving-detachment index before revalidating restored AI tasks.
4. `PartyExchangeService` and `UnifiedRecruitmentExecutionService` reject new writes with `CampaignRecoveryNotReady` until the barrier is Ready. Recovery handlers themselves remain able to restore saved transport identities.
5. AI load validation is now coordinated after fief indexes instead of running early in its own `OnGameLoadFinished` callback. The compatibility fallback remains for environments that do not install the coordinator.
6. `CampaignState.IsNewGame` is reset on `OnGameLoaded`. This prevents a new-game flag from surviving an A -> B load in the same process and incorrectly satisfying the unchanged reward guard.

## Static and build checks

| Check | Result |
|---|---|
| Release build | PASS: 0 errors, 7 existing/environment warnings |
| Existing SyncData keys | PASS: unchanged |
| Saveable ids including legacy 8 / transport 9 | PASS: unchanged |
| 99000 gold/equipment reward block | PASS: byte-for-byte source block unchanged |
| Harmony targets/returns | PASS: untouched |
| Basic troop XML registry | PASS: no longer cleared during Campaign scope replacement |

## Runtime acceptance still required

- T12: load a save with a supply party in transit and verify the tactical route/recovery state.
- T13: create Campaign A, load B, then A again in one process; verify no shared warnings, budgets, task ids or object references.
- T18: confirm a new game receives the existing reward once and loading another save afterward does not receive it.
- T19: load reinforcement/service-batch saves and verify party/component identity and fief batch reconciliation.

BUILD is complete, but these checks are `BLOCKED` until run in Bannerlord. No claim of runtime PASS is made.

## Delivery state

- CHANGED_FILES: coordinator, `Main.cs`, AI task behavior, exchange service and recruitment execution service, plus this report.
- RISK (high): actual Bannerlord ordering is inferred from event registration order and requires A -> B -> A observation.
- RISK (medium): transaction attempts occurring before session launch are intentionally rejected and retried by later planning rather than mutating partially restored state.
- ROLLBACK: discard the uncommitted S2 working-tree changes listed above.
- NEXT_PHASE_ALLOWED: **NO pending T12/T13/T18/T19 runtime evidence**, unless the user explicitly accepts the isolated risk and directs S3 work.
