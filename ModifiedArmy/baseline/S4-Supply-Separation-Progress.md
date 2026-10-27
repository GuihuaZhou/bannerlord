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

## Final S4 implementation state

- `SupplyDispatchService` now owns daily eligibility sequencing and the
  existing persisted cooldown dictionary; candidate scoring remains unchanged
  in the Behavior during the transition.
- `SupplyRecoveryService` now owns load-time component ownership restoration,
  native-avoidance protection and mission-route selection.
- Successful local-mission return now commits component completion, escort and
  inventory restoration, capacity logging and party destruction through
  `SupplyRecoveryService.CompleteReturn`.
- Move outbound provisioning, purchases and full compensation into cargo service.
- Failed-departure compensation now runs through `SupplyCargoService` and
  restores escort troops, removes purchased animals before returning cargo,
  refunds gold and destroys the incomplete party in the original order.
- Destroyed-party cooldown/notification and successful local return completion
  now run through `SupplyRecoveryService`.
- Town and village food purchase loops now execute through
  `SupplyCargoService.PurchaseFood`; the original town price versus village
  base-value rule is supplied as the unchanged price callback.
- Aid-return recovery, partial-capacity handling and party destruction now run
  through `SupplyRecoveryService.CompleteAidReturn`.
- The partial files remain as compatibility call surfaces for formulas and
  diagnostics. Food, riding-mount and pack-animal purchase/transfer,
  compensation and recovery completion are owned by the extracted services.
- The final static resource-write scan reports no `GiveGoldAction`, direct
  `ItemRoster.AddToCounts`, or `DestroyPartyAction` in the three partial files.

- ROLLBACK: discard the uncommitted S4 working-tree files.
- NEXT_PHASE_ALLOWED: **NO pending T08-T12 runtime evidence**. Static/build
  implementation is complete; proceed to S5 only after the user validates the
  supply-party paths or explicitly accepts the remaining runtime risk.
