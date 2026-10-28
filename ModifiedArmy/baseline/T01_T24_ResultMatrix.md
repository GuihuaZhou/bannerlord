# T01-T24 Result Matrix

The user reported that each staged build ran for several minutes without an
observed problem. That evidence is recorded as a general startup/basic-running
smoke test and is not promoted into scenario-specific PASS results below.

Execution steps and evidence requirements are defined in
`S8-Runtime-Acceptance-Runbook.md`.

| ID | Scenario / invariant | Status | Save ID / evidence | Tester | Relevant commit |
|---|---|---|---|---|---|
| T01 | Same actor/settlement garrison reinforcement, approved equals actual | BLOCKED | No roster before/after snapshot | User environment required | `05062ec` |
| T02 | Invalid clan/fake garrison/siege/foreign ownership rejected | BLOCKED | No authorization matrix runtime log | User environment required | `6824929` |
| T03 | Approved 30, actual 20, no replay | BLOCKED | No forced partial-transfer fixture | User environment required | `6824929` |
| T04 | Roster changes before commit | BLOCKED | No stale-snapshot fixture | User environment required | `6824929` |
| T05 | Exception with full rollback | BLOCKED | No injected exception/resource snapshot | User environment required | `6824929` |
| T06 | Rollback failure freezes recovery and survives load | BLOCKED | No rollback-failure save | User environment required | `6824929`, `0fe64f9` |
| T07 | Fief recruit/upgrade/loss/return/service expiry conservation | BLOCKED | Short smoke only; no batch/hearth snapshot | User | `f1ecd23` |
| T08 | Supply dispatch with sufficient granary | BLOCKED | Short smoke only; no cargo ledger | User | `ddc0b11` |
| T09 | Insufficient supply refuses and compensates all resources | BLOCKED | No forced-insufficient snapshot | User environment required | `ddc0b11` |
| T10 | Supply party travels 72 hours without false return | BLOCKED | Run duration below 72 campaign hours/not recorded | User environment required | `db666b7` |
| T11 | Native enemy avoidance remains effective | BLOCKED | No threat encounter evidence | User environment required | `db666b7` |
| T12 | Reinforcement transport save/load/deliver/return | BLOCKED | No in-transit save ID | User environment required | `0fe64f9` |
| T13 | A -> B -> A in one process does not leak state | BLOCKED | No three-load sequence | User environment required | `0fe64f9` |
| T14 | Runtime Harmony audit: complete, single interception | BLOCKED | Static entry present; runtime audit log not supplied | User environment required | `4fdca37` |
| T15 | Prediction has no writes; production/culture matches | BLOCKED | Static read-path scan PASS; no daily ledger comparison | User environment required | `05062ec` |
| T16 | Wage/budget result unchanged | BLOCKED | Build PASS; no pre/post wage snapshot | User environment required | `4fdca37` |
| T17 | AI/player prisoner recruit/sale and disband merge | BLOCKED | Short smoke only; no gold/roster comparison | User | `05062ec` |
| T18 | New-game 99000 gold/equipment occurs original number of times | BLOCKED | Static protected-path review PASS; no new-game capture | User environment required | remediation range |
| T19 | Save IDs, keys and old/new saves load without conflict | BLOCKED | Static ID/key review PASS; no identified old/new save cycle | User environment required | remediation range |
| T20 | New game runs 30 campaign days | BLOCKED | Several-minute smoke is insufficient | User environment required | `4fdca37` |
| T21 | Replaying a committed operation ID causes no second resource write | BLOCKED | No replay fixture | User environment required | `6824929` |
| T22 | Campaign recovery barrier blocks writes until dependencies are ready | BLOCKED | No controlled load-order trace | User environment required | `0fe64f9` |
| T23 | Optional ModifiedPolitics integration works both present and absent | BLOCKED | Dependency cycle removed and both projects build; launcher logs not supplied | User environment required | post-review hotfix |
| T24 | Mercenary and wage Prefixes suppress native execution exactly once | BLOCKED | Static Prefix contract PASS; no runtime counter/log | User environment required | `4fdca37` |

## General smoke evidence

- Tester: user.
- Save identifier: not supplied.
- Evidence: repeated report “运行了几分钟，没问题” through the S8 evidence-gate stage.
- Result: PASS for launch/basic short-duration running only.

