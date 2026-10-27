# S0 Baseline and Boundary Audit

## Start record

- BASE_SHA: `a3d46c90c9328bc776a5d480b7997d6d7c09613d`
- `origin/main`: `a3d46c90c9328bc776a5d480b7997d6d7c09613d` after fetch on 2026-10-27.
- Branch: `main`.
- Anchor diff under `ModifiedArmy`: none.
- Pre-existing dirty state: `ModifiedArmy.code-workspace` modified; the implementation documents under `file/` are untracked. They were not changed by S0.
- OBJECTIVE: freeze state, persistence, resource-write and Harmony boundaries before S1.
- IN_SCOPE: read-only source audit and files under `ModifiedArmy/baseline/`.
- OUT_OF_SCOPE: runtime source, game rules, save ids, SyncData keys, Harmony behavior, protected files.

## State and call boundaries

The method-level flow used for the next phases is:

1. Campaign events enter `AiForcePlanningBehavior`, `PartyExchangeBehavior`, `SupplyPartyBehavior`, `FiefPartyManager`, `GarrisonRecruitFromPrisonersBehavior`, and transport/recovery behaviors.
2. Planning models produce approvals or plans; they do not own roster, gold, item, hearth or service-batch state.
3. `PartyExchangeService` is the common roster/fief execution path. `UnifiedRecruitmentExecutionService.ExecuteMercenary` is a separate four-resource purchase path. Supply, reinforcement and fief services remain specialist authorities.
4. Fief state is authoritative in `FiefPartyManager`/`FiefPartyData`; AI task serialization remains authoritative in `AiForcePlanningBehavior`; transport identity remains in its party components.
5. Behaviors consume execution results and serialize existing keys. S1 must close the result-state and idempotency gap before S2 changes lifecycle ordering.

See `StateOwnership.csv`, `ResourceWrites.csv`, and `PatchContracts.csv` for the required audit tables.

## Persistence signature freeze

- `FiefSaveDefiner` continues to register legacy `ReinforcementMission` as class id 8 and `ReinforcementPartyComponent` as class id 9.
- `ReinforcementMission` properties 1-6 and `ReinforcementPartyComponent` properties 1-8 are frozen.
- `FiefServiceBatch`, `FiefReturnBatch`, `FiefTroopDetachment`, `FiefPartyData`, `FiefWageExemptionManager`, `SupplyPartyComponent`, and manpower-production saveable ids are frozen.
- Existing keys including `_mobilizedHearth`, `_mobilizationOwners`, `_mobilizationMigrationComplete`, `_legacyUntrackedMobilization`, `_exemptionMap`, and `modifiedarmy_force_task_records_v1` are frozen.

## Existing work not to repeat

- W1: obsolete active reinforcement services are absent; `ReinforcementMissionBehavior` is compatibility-only and cancels loaded legacy missions; ids 8/9 remain.
- W4: donation, disband, prisoner and VM patches already delegate substantial business logic; do not re-extract them merely by line count.
- W5: finance code already resides under `PartyManagement/Finance`; do not move it again.

## Test assets and status

No Bannerlord save samples were found in the repository for S1 normal, S2 supply-in-transit, S3 reinforcement-in-transit or S4 legacy reinforcement. Their backup and game-level baselines are `BLOCKED` pending user-provided local saves. No Bannerlord process was run.

| Check | Result | Evidence |
|---|---|---|
| Remote/base SHA comparison | PASS | both resolve to `a3d46c90` |
| Runtime source unchanged by S0 | PASS | only `ModifiedArmy/baseline/` added |
| Three required audit tables | PASS | CSV files beside this report |
| Save id/key inventory | PASS (static) | source scan recorded above |
| Game save backups | BLOCKED | no save samples in repository |
| Game runtime baseline | BLOCKED | Bannerlord not launched |

## Handoff

- RISK: S1 currently has runtime-only operation ids; load can permit replay.
- RISK: mercenary purchase ignores its operation id and spans gold, market, roster and budget writes without a durable recovery record.
- RISK: `RecoveryRequired` is not yet preserved as a frozen task state by all consumers.
- ROLLBACK: remove only the four files under `ModifiedArmy/baseline/`; no runtime rollback is needed.
- NEXT_PHASE_ALLOWED: **YES for S1 source implementation**, because the static baseline and protected boundaries are frozen. Game-level S1/T21-T23 acceptance remains blocked until appropriate saves/runtime are available.
