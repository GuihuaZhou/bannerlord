# Release GO / NO-GO

## Decision: NO-GO for full release

- Base: `72ef039`
- Candidate: `4fdca37`
- Release build: PASS (0 errors, 7 existing warnings)
- Protected-file and static compatibility review: PASS
- Short user runtime smoke after each stage: PASS
- T01-T24 scenario matrix: BLOCKED

The implementation is suitable for continued controlled game testing, but not
for a claim of full acceptance or general release. Critical missing evidence
includes partial commit, rollback failure/recovery, resource compensation,
72-hour routing, in-transit save/load, A-B-A campaign switching, new-game
reward frequency, legacy/new SaveId compatibility and a 30-day campaign run.

## Release gate

GO requires every critical T01-T20 case to be PASS with a named test save,
tester, scenario and log/screenshot/resource snapshot. T21-T24 must also be
resolved or explicitly waived with recorded rationale. Any conservation,
save-load, or duplicate-Harmony failure is an automatic NO-GO.

## Rollback

Each remediation stage is an independent commit from `6824929` through
`4fdca37`. Revert in reverse dependency order. Do not use a hard reset or erase
the user's unrelated workspace files. Restore test saves from copies if a
resource-conservation or serialization problem appears.

`NEXT_PHASE_ALLOWED=NO`

## Automated static gate

Run `tools/Test-ArchitectureReleaseGate.ps1` from the ModifiedArmy module or
repository workspace after architecture changes. It builds both projects and
checks dependency direction, SaveId 8/9, protected files, retired references,
and critical Behavior ownership boundaries. A PASS does not replace T01-T24
runtime evidence.

## Runtime evidence gate

Run `tools/Test-RuntimeAcceptanceEvidence.ps1` while filling in
`baseline/T01_T24_ResultMatrix.md`. It rejects incomplete or duplicate rows and
prevents a PASS without concrete evidence, tester and commit. Use `-RequireGo`
only for a release decision: T01-T20 must all be PASS, while T21-T24 must be
PASS or carry an explicit WAIVED rationale.
