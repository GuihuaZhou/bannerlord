# S1 Transfer Transaction Handoff

## Start record

- PHASE: S1
- BASE_SHA: `a3d46c90c9328bc776a5d480b7997d6d7c09613d`
- OBJECTIVE: close result-state consumption, operation idempotency and recovery freezing for roster/fief/mercenary force operations.
- IN_SCOPE: unified transfer contracts/executor, mercenary execution, AI result consumption, a primitive persistence behavior, budget compensation.
- OUT_OF_SCOPE: S2 lifecycle ordering, S3 behavior extraction, supply/fief/garrison decomposition, patch semantics, dependency policy.
- STATE_CONTRACTS: all existing keys and Saveable ids remain unchanged. One additive key is introduced: `modifiedarmy_transfer_operation_journal_v1`, containing `Dictionary<string,string>` primitive projections only.

## Implemented

1. `PartyExchangeExecutionResult` now carries source/destination observations and a freeze domain without claiming an exact count when recovery is indeterminate.
2. `TransferOperationJournalBehavior` persists terminal committed/partial/recovery operation ids. Loading only rebuilds a lookup; it never replays a resource write. Recovery records are never trimmed; completed history is bounded to 2048 entries.
3. `PartyExchangeService` rejects ids found in either its runtime set or the persisted journal, blocks a party domain with unresolved recovery, and records results after executor metadata is complete.
4. `AiForcePlanningBehavior.ExecuteAtSettlement` classifies all five statuses: committed/partial complete the consumed attempt, rejected/rolled-back cancel it, recovery suspends it.
5. `UnifiedRecruitmentExecutionService.ExecuteMercenary` now consumes `operationId`, checks the journal/domain freeze, snapshots gold/market/roster/budget, compensates in reverse order, records committed operations, and records an unrecoverable partial state as `RecoveryRequired`.
6. `ClanRecruitmentBudgetManager.RestoreRemainingBudget` restores only a captured pre-transaction snapshot and clamps it to that day's original totals.

## Static checks

- `ModifiedArmy/ARCHITECTURE.md`: unchanged.
- `ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`: unchanged.
- `FiefSaveDefiner`, legacy id 8 and transport id 9: unchanged.
- Existing SyncData keys: unchanged.
- `Main.OnAfterSessionLaunched` 99000 gold/equipment block: unchanged; only behavior registration was added elsewhere.
- Harmony targets and Prefix/Postfix behavior: unchanged.
- Authorization policy: unchanged.
- Consumer scan: the AI task path now switches explicitly on `TransferCommitStatus`; the remaining `.Executed` use is routine donation, whose roster executor is all-or-rollback and cannot legitimately return partial success.

## Build and tests

| Check | Result | Evidence |
|---|---|---|
| Release build | PASS | `dotnet build .\\ModifiedArmy\\ModifiedArmy.sln -c Release`; 0 errors, 7 pre-existing/environment warnings |
| Protected files / SaveIds | PASS (static) | zero diff in protected files and save-definer files |
| Complete/partial/rejected/rolled-back status routing | PASS (static) | explicit switch in `ExecuteAtSettlement` |
| Duplicate id after load | BLOCKED | requires Bannerlord save/load runtime |
| Roster exception and rollback injection | BLOCKED | no test harness/fault injector in repository |
| Mercenary gold/market/roster/budget exception injection | BLOCKED | requires controlled game runtime |
| Recovery freeze after save/load | BLOCKED | requires Bannerlord save/load runtime |
| T02-T07 / T21-T23 game acceptance | BLOCKED | no supplied baseline saves; Bannerlord not launched |

## Required manual evidence

1. Copy a normal save before testing. Trigger one fief/garrison recruitment operation, save immediately, reload, and invoke the same operation id; verify roster/hearth/batch counts do not change on the second call.
2. Inject a failure after mercenary gold, market and roster steps separately. For compensated failures verify all four values equal the before snapshot. For an uncompensated failure verify the journal survives reload and the same party receives `TransferDomainFrozenForRecovery` without another purchase.
3. Force a partial fief authoritative result; verify the current attempt becomes terminal and a later planner creates a different version/id for the remaining gap.
4. Force a roster rollback failure; verify the task is `Suspended`, observed fingerprints are logged/persisted, and neither daily visit nor reload writes the roster again.

## Risks and gate

- HIGH: `Dictionary<string,string>` is a standard primitive SaveSystem shape, but successful Bannerlord serialization is not proven until a real save/load is performed.
- HIGH: event dispatch occurs after the mercenary resource commit. An exception from the external event cannot safely be compensated because listeners may already have observed it; this remains a runtime validation point.
- MEDIUM: journal history is bounded; recovery records are retained indefinitely by design pending a later explicit reconciliation/unfreeze workflow.
- ROLLBACK: revert the S1 source commit; the additive journal key is ignored by the previous build and no legacy ids/keys were changed.
- NEXT_PHASE_ALLOWED: **NO**. Source and build work is complete, but the plan's S1 hard acceptance requires real save/load, duplicate-id and recovery-fault evidence. Do not begin S2 until these checks pass or the user explicitly accepts the isolated risk.
