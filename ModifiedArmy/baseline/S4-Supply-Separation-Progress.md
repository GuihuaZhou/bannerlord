# S4 Supply Separation Progress

## Start record

- PHASE: S4 (in progress)
- BASE_SHA: `0c1e8b1` (`refactor(army): extract force task coordination`)
- OBJECTIVE: separate dispatch, cargo, routing and recovery ownership while preserving every supply rule and component identity.

## Implemented in this slice

### SupplyRouteController

- Owns threat-recovery deadlines, route-command grace timestamps and daily mismatch suppression.
- Native avoidance and superior-enemy checks are unchanged.
- A threatened party still yields immediately to native flee/avoidance behavior.
- The six-hour recovery delay and two-hour route-command grace window are unchanged.
- Runtime route state is now attached to the Behavior's controller instance and therefore to one Campaign.
- Destroyed party cleanup now delegates to `ForgetParty`.

### SupplyCargoService

- Owns the active low-level food, item and escort restoration mutations.
- Existing `SupplyPartyBehavior` helpers are compatibility delegates, so all current call sites retain their exact order and return values.
- No amount, reserve, capacity, purchase, wounded or compensation formula changed.
- Outbound food-capacity calculation and the weight-aware committed transfer
  now execute inside the cargo service; `SupplyProvisionModel.Plan` remains the
  pure calculation authority.

## Preserved boundaries

- Event subscriptions and `_modifiedArmySupplyNextDispatchTimes` are unchanged.
- `SupplyPartyComponent` and its Saveable properties are unchanged.
- Local-foraging/cross-city-aid eligibility is unchanged.
- Five-day provision, safety days, final-day granary reserve, animal purchases and recovery rules are unchanged.
- No Harmony, fief, reward or dependency files changed.

## Verification

| Check | Result |
|---|---|
| Release build | PASS: 0 errors, 7 existing/environment warnings |
| Route constants and native avoidance order | PASS (static) |
| Cargo write order and amounts | PASS (static) |
| T08-T12 / 72-hour game run | BLOCKED pending runtime |

## Remaining S4 work

- `SupplyDispatchService` now owns daily eligibility sequencing and the
  existing persisted cooldown dictionary; candidate scoring remains unchanged
  in the Behavior during the transition.
- `SupplyRecoveryService` now owns load-time component ownership restoration,
  native-avoidance protection and mission-route selection.
- Move outbound provisioning, purchases and full compensation into cargo service.
- Move destroy/return recovery completion behind `SupplyRecoveryService`.
- Remove the partial-class business implementations after every caller migrates.

- ROLLBACK: discard the uncommitted S4 working-tree files.
- NEXT_PHASE_ALLOWED: **NO**. S4 is intentionally incomplete and must not be committed or advanced yet.
