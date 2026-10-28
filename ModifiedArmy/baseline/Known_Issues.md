# Known Issues

## KI-01 Caravan guard refresh is a no-op

`MercenaryMarketService` preserves the previous behavior: it performs the
independent native caravan-guard probability roll and reads the culture's
caravan guard, but does not change the market before the Prefix suppresses the
native method. This needs a dedicated gameplay reproduction and an explicit
rule-change decision; it was not altered during architecture remediation.

## KI-02 Full integration evidence is incomplete

Short runtime smoke tests found no immediate problem, but no named save IDs,
screenshots, resource snapshots, controlled exception injection, 72-hour route
run, A-B-A load cycle, or 30-day campaign run were supplied. T01-T24 therefore
remain BLOCKED rather than PASS.

## KI-03 Build paths remain machine-specific

The project still contains local Bannerlord DLL hint paths. The current machine
builds successfully, but a portable build-path redesign was intentionally kept
out of the architecture commits.

## KI-04 Optional external integration requires runtime coverage

The Army-side compile and manifest dependency was removed to break the module
cycle. War disposition is now read through a guarded reflection adapter and
falls back to formal-war state when Politics is absent or incompatible. Both
with-Politics and without-Politics launcher scenarios still require runtime
coverage.

