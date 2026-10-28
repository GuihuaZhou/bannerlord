# Architecture Remediation Acceptance

- Remediation base: `72ef039`
- Remediation implementation: commits through `0038f93`
- Validation tooling: commits through `95891fc`, plus the current S8 closeout
- Reference upstream anchor: `a3d46c90c9328bc776a5d480b7997d6d7c09613d`

## Resulting call chain

```text
Main / Campaign events
  -> CampaignRecoveryCoordinator -> saved behaviors become ready
  -> AiForcePlanningBehavior -> PartyForceTaskCoordinator
       -> PartyVisitExecutionService -> PartyExchangeService
  -> SupplyPartyBehavior
       -> SupplyDispatchService / SupplyRouteController
       -> SupplyCargoService / SupplyRecoveryService
  -> FiefPartyManager (save authority)
       -> FiefManpowerLedger / FiefServiceLedger
       -> FiefServiceReconciler / FiefReturnService
  -> GarrisonRecruitFromPrisonersBehavior
       -> ProfessionalRecruitmentService
       -> PrisonerGarrisonRecruitmentService
  -> Harmony Prefix/Postfix -> thin domain-service adapters
```

## Ownership and lifecycle

| Domain | Persistent authority | Runtime/service responsibility | Recovery |
|---|---|---|---|
| Force operations | original task records plus transfer journal projection | transaction status, idempotency, partial/failed outcome coordination | campaign recovery barrier restores frozen operations before planning |
| AI tasks | `AiForcePlanningBehavior` original save key | coordinator owns evaluation/execution; persistence adapter maps records | rebuilt for the current Campaign only |
| Supply | original behavior keys and `SupplyPartyComponent` | dispatch, routing, cargo mutation, compensation and return settlement | load recovery validates existing components; never invents a new saved identity |
| Fief service | `_fiefDataMap`, three original mobilization dictionaries, `ServiceBatches` | ledgers operate those exact objects; reconciler matches real roster; return service commits actual counts | runtime serving index rebuilt from saved batches and real parties |
| Recruitment | `_settlementRecruitmentPools` and production data | prediction is calculation-only; services own professional/prisoner roster commits | daily offers are runtime state regenerated from the saved production ledger |
| Finance | PartyManagement finance models | service evaluates and logs wage limits | no additional persisted state |
| War disposition | `ModifiedPolitics` behavior when installed | reflection bridge reads the external behavior without a compile/load dependency | absent or incompatible integration falls back to formal-war state |

## Compatibility boundaries

- `FiefSaveDefiner` retains ID `20251116`; reinforcement IDs 8 and 9 remain assigned.
- Existing SyncData keys and Saveable field/property numbers remain authoritative.
- Harmony targets and interception return semantics were preserved.
- `Main.cs` new-game 99000-gold/equipment path was not altered by the remediation commits.
- No separate DLL, dependency-injection framework, or parallel fief/manpower store was added.

## Evidence status

- Static source/diff review: PASS.
- Release build on the user's configured workspace: PASS, 0 errors.
- User-reported short runtime smoke after each stage: PASS for startup/basic running only.
- Full T01-T24 acceptance: BLOCKED; see the result matrix.
- Unified static/evidence validation: `tools/Invoke-ReleaseValidation.ps1`.

