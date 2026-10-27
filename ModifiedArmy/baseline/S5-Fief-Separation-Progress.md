# S5 Fief Responsibility Separation Progress

- Base commit: `ddc0b11`
- Scope: fief manpower, active service-batch indexing, reconciliation and return dispatch
- Commit status: not committed; awaiting game verification and explicit approval

## Implemented

- `FiefManpowerLedger` now performs mobilized-hearth reads, reservations,
  releases, exact return releases and rollback against the manager's original
  `_mobilizedHearth`, `_mobilizationOwners` and
  `_legacyUntrackedMobilization` dictionaries.
- `FiefServiceLedger` rebuilds and maintains only the runtime index over the
  original `FiefPartyData.ServiceBatches`; it has no persistent collection.
- Existing `FiefServiceReconciler` remains the roster-authoritative upgrade and
  loss matcher. It never creates troops.
- `FiefReturnService` owns request validation, reconciliation, batch lookup,
  tracked-hearth gating and return result logging. The existing transactional
  commit in `FiefPartyData.TryCommitFiefReturn` continues to preserve wounded
  counts, cooldown batches and rollback behavior.
- `FiefPartyManager` remains the public compatibility and save/event entry.

## Preserved boundaries

- `FiefSaveDefiner` ID `20251116`, class IDs and container definitions unchanged.
- Saveable fields 1-5 and all original `SyncData` keys unchanged.
- `FiefPartyData.ServiceBatches` and `FiefServiceBatch.BatchId` remain the only
  persistent service-batch authority.
- No new Saveable type, field, SyncData key or persistent dictionary was added.
- Protected architecture/localization files and `Main.cs` were not modified.

## Verification

- Release build: PASS, 0 errors / 7 pre-existing warnings.
- `git diff --check`: PASS (workspace file outside phase still reports its
  existing line-ending notice).
- Static save-boundary scan: PASS.
- T07 game verification: BLOCKED until Bannerlord is run. Verify same-source
  batch return, wounded soldiers, free-wage days, service duration, exact return
  count, and mobilized hearth restoration.
- T19 load verification: BLOCKED until a save/load cycle is run.

## Rollback

Discard only the S5 files listed in this report before they are committed, or
revert the eventual standalone S5 commit after approval. Do not reset the
workspace because unrelated user files are present.

`NEXT_PHASE_ALLOWED=NO` until T07/T19 smoke verification is reported.
