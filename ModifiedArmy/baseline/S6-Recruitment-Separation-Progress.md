# S6 Recruitment Responsibility Separation Progress

- Base commit: `f1ecd23`
- Scope: professional-offer commit, prisoner retention/conformity/transfer,
  and weekly garrison normalization
- Commit status: committed through `05062ec`, with follow-up cleanup and
  compensation fixes through `d670ceb`; full T01/T15/T17/T19 evidence remains
  BLOCKED

## Implemented

- `ProfessionalRecruitmentService` is the single professional-offer commit
  path used by daily garrison recruitment. It consumes the existing daily offer
  before adding the approved troop count to the garrison.
- `PrisonerGarrisonRecruitmentService` owns prisoner retention, AI-only sale,
  conformity progress, wounded-preserving roster transfer, and weekly removal
  of invalid garrison characters.
- `GarrisonRecruitFromPrisonersBehavior` retains campaign events, planning,
  shared daily quota, finance commit and existing result logging.
- `SettlementManpowerProductionBehavior` retains its original SyncData key and
  production state. `TryConsumeProfessionalOffer` remains the authoritative
  public offer-consumption API.

## Preserved boundaries

- No Saveable type/field, SaveDefiner ID, or SyncData key changed.
- Culture matching, training capacity, barracks level, production rates,
  prisoner tier threshold, conformity rate, recruitment costs, daily quota,
  capacity reservations and logging keys are unchanged.
- `GetPredictedDailyRecruitmentCount` and
  `GetPredictedProfessionalOffers` remain calculation-only paths; resource
  writes are confined to production/commit services and weekly normalization.
- Protected architecture/localization files and `Main.cs` were not modified.

## Verification

- Release build: PASS, 0 errors / 7 pre-existing warnings.
- `git diff --check`: PASS for the S6 source scope.
- Static prediction write scan: PASS.
- T01/T15/T17 game verification: BLOCKED until Bannerlord is run. Compare
  professional production, prisoner conformity/transfer, troop culture,
  garrison capacity and recruitment expense for at least one daily tick.
- T19 save/load verification: BLOCKED until a save/load cycle is run.

## Rollback

Discard only the S6 behavior/service/report files before commit, or revert the
eventual standalone S6 commit after approval. Do not reset the workspace because
unrelated user files are present.

`NEXT_PHASE_ALLOWED=NO` until T01/T15/T17/T19 smoke verification is reported.
