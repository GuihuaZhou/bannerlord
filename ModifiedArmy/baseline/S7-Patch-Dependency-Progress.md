# S7 Patch and Dependency Progress

- Base commit: `05062ec`
- Scope: residual mercenary/finance Patch business logic and explicit
  `ModifiedPolitics` runtime dependency
- Commit status: committed through `4fdca37`; dependency-cycle and caravan
  follow-ups committed through `0038f93`

## Implemented

- `MercenaryMarketService` owns the existing XML probability, weighted troop
  selection, amount roll, market mutation and logging. Its Harmony Prefix still
  always returns `false` and retains the two independent random draws.
- `ClanFinancialEvaluationService` owns war-party wage-limit calculation,
  assignment and logging. Its Harmony Prefix still always returns `false`.
- The later review follow-up removed Army's compile-time and manifest dependency
  on ModifiedPolitics. `WarDispositionBridge` now uses guarded reflection and
  formal-war fallback, while Politics remains the integration owner.
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

## Resolved gameplay defect pending runtime coverage

- The caravan-guard branch now preserves its independent random roll and uses
  the native weighted troop/count methods before updating the market. Reflection
  incompatibility is logged and safely skipped; game verification remains open.

## Verification

- Release build: PASS, 0 errors / 7 pre-existing warnings.
- `git diff --check`: PASS for S7 files.
- Static Patch contract comparison: PASS.
- T14/T16/T17 and runtime Patch audit: BLOCKED until Bannerlord is run.
- Present/absent optional-integration launcher behavior: BLOCKED until T23.

## Rollback

Discard only the S7 Patch/service/SubModule/report changes before commit, or
revert the eventual standalone S7 commit after approval. Do not reset the
workspace because unrelated user files are present.

`NEXT_PHASE_ALLOWED=NO` until the runtime smoke test confirms Patch activation,
mercenary refresh, wage-limit evaluation and dependency load order.
