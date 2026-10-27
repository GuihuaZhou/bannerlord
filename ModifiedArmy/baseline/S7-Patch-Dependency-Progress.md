# S7 Patch and Dependency Progress

- Base commit: `05062ec`
- Scope: residual mercenary/finance Patch business logic and explicit
  `ModifiedPolitics` runtime dependency
- Commit status: not committed; awaiting game verification and explicit approval

## Implemented

- `MercenaryMarketService` owns the existing XML probability, weighted troop
  selection, amount roll, market mutation and logging. Its Harmony Prefix still
  always returns `false` and retains the two independent random draws.
- `ClanFinancialEvaluationService` owns war-party wage-limit calculation,
  assignment and logging. Its Harmony Prefix still always returns `false`.
- `SubModule.xml` now declares `ModifiedPolitics`, matching the existing direct
  compile-time references to `WarDispositionManager` and
  `WarDispositionData`. This integration is therefore explicitly treated as a
  required dependency, not a falsely optional adapter.
- Existing `ForceEntryPatchAudit.LogActivePatches()` startup call and audited
  Harmony targets remain unchanged.

## Preserved boundaries

- Harmony targets, Prefix signatures and interception return values unchanged.
- Mercenary spawn probability, weighted selection, count range and log key
  unchanged.
- Wage-limit calculation, assignment order, visibility filtering and log keys
  unchanged.
- `Main.cs`, SaveDefiner/SyncData, protected architecture and localization
  validation files unchanged.

## Registered gameplay defect

- The caravan-guard branch performs its independent random roll and reads the
  culture's caravan guard, but does not mutate mercenary market state before
  suppressing the native method. This phase deliberately preserves that
  behavior pending a dedicated gameplay reproduction and approval.

## Verification

- Release build: PASS, 0 errors / 7 pre-existing warnings.
- `git diff --check`: PASS for S7 files.
- Static Patch contract comparison: PASS.
- T14/T16/T17 and runtime Patch audit: BLOCKED until Bannerlord is run.
- Required-dependency launcher behavior: BLOCKED until launch-order verification.

## Rollback

Discard only the S7 Patch/service/SubModule/report changes before commit, or
revert the eventual standalone S7 commit after approval. Do not reset the
workspace because unrelated user files are present.

`NEXT_PHASE_ALLOWED=NO` until the runtime smoke test confirms Patch activation,
mercenary refresh, wage-limit evaluation and dependency load order.
