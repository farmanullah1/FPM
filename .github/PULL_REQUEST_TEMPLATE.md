## What changed

<!-- One or two sentences. Link the card or issue. -->

## Safety (required for anything that deletes or changes system state)

- [ ] Scan path is read-only; nothing changes before explicit approval.
- [ ] Actions are typed and validated (no free-form paths, scripts or shell commands).
- [ ] Undo exists and was tested.
- [ ] Failures are isolated and logged; no fabricated results.
- [ ] Microsoft PC Manager and protected processes/packages are untouched.

## Verification (paste real output)

- [ ] `dotnet build app/PCManager.sln -nodeReuse:false -m:1` → 0 warnings, 0 errors
- [ ] `dotnet test` → all green (count: …)
- [ ] `tools/lint-xaml.ps1` → no file above baseline
- [ ] `tools/contrast-check.ps1` → passes in both themes
- [ ] Light + Dark and 360/560/1280 px reviewed where the UI changed
- [ ] Reduced motion and keyboard/automation names verified

## Notes

- [ ] `CHANGELOG.md` updated
- [ ] New dependency licence recorded in `THIRD_PARTY_NOTICES.md` (if any)
- [ ] Anything not verified is listed here explicitly
