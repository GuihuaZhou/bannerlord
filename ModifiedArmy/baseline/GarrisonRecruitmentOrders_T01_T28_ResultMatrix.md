# Garrison recruitment orders T01-T28 result matrix

Static compilation and pure planning-math tests are not substitutes for the
campaign/save evidence required below. All scenarios remain `BLOCKED` until a
tester records a save identifier, log excerpt or roster/resource snapshot, and
the tested commit.

| ID | Scenario | Status | Evidence | Tester | Commit |
|---|---|---|---|---|---|
| T01 | Wealthy clan below s creates an order without a military event | BLOCKED | Game/save evidence required | User environment required | working tree |
| T02 | Stock between s and S creates no order | BLOCKED | Game/save evidence required | User environment required | working tree |
| T03 | No order with professional offers adds no AI garrison troops | BLOCKED | Game/save evidence required | User environment required | working tree |
| T04 | No order prepares/sells prisoners but does not convert them | BLOCKED | Game/save evidence required | User environment required | working tree |
| T05 | Two professional and three prisoner commits reduce ten to five | BLOCKED | Game/save evidence required | User environment required | working tree |
| T06 | Offers present but plan approves zero | BLOCKED | Game/save evidence required | User environment required | working tree |
| T07 | Remaining one never commits more than one | BLOCKED | Game/save evidence required | User environment required | working tree |
| T08 | Garrison transfer out does not expand the active order | BLOCKED | Pure math PASS; campaign evidence required | User environment required | working tree |
| T09 | Cooldown prevents immediate replacement order | BLOCKED | Game/save evidence required | User environment required | working tree |
| T10 | New order ID is allowed after cooldown | BLOCKED | Game/save evidence required | User environment required | working tree |
| T11 | Zero daily budget pauses without troop or order debit | BLOCKED | Game/save evidence required | User environment required | working tree |
| T12 | Improved finances expand only through explicit target revision | BLOCKED | Pure math PASS; campaign evidence required | User environment required | working tree |
| T13 | Two garrisons share one clan daily budget in stable order | BLOCKED | Game/save evidence required | User environment required | working tree |
| T14 | Source-less priority settlement does not lock budget | BLOCKED | Game/save evidence required | User environment required | working tree |
| T15 | Old save without order key creates no duplicate troops | BLOCKED | Old-save cycle required | User environment required | working tree |
| T16 | Active order survives save/load accurately | BLOCKED | Save/load cycle required | User environment required | working tree |
| T17 | Ownership change invalidates the previous clan order | BLOCKED | Game/save evidence required | User environment required | working tree |
| T18 | Resident 74 plus 50 escorts permits no transfer out | BLOCKED | Game/save evidence required | User environment required | working tree |
| T19 | Resident 76 plus 50 escorts permits at most one transfer out | BLOCKED | Game/save evidence required | User environment required | working tree |
| T20 | Capacity or wage contraction never exceeds limits | BLOCKED | Game/save evidence required | User environment required | working tree |
| T21 | Player garrison auto recruitment remains unchanged | BLOCKED | Player campaign evidence required | User environment required | working tree |
| T22 | Bound party withdrawal ignores order count but honors defense | BLOCKED | Game/save evidence required | User environment required | working tree |
| T23 | Party can still seek mercenaries in another settlement | BLOCKED | Game/save evidence required | User environment required | working tree |
| T24 | Partial failure compensates or freezes RecoveryRequired | BLOCKED | Fault-injection evidence required | User environment required | working tree |
| T25 | Reload and repeated same-day tick never double commits | BLOCKED | Save/load replay evidence required | User environment required | working tree |
| T26 | Long multi-clan run does not flood logs or churn zero orders | BLOCKED | Long-run evidence required | User environment required | working tree |
| T27 | Existing return/transport paths retain their capacity policies | BLOCKED | Regression evidence required | User environment required | working tree |
| T28 | Disabled switch restores the previous AI recruitment path | BLOCKED | Enabled/disabled comparison required | User environment required | working tree |
