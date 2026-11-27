# Skill: fpm-safety-contract

*Intended for the account skill library; kept here because that library is full (see `SKILLS.md`).
Description for installation: “Apply FPM's non-negotiable safety laws to any cleanup, Windows
debloat, Android/ADB or elevated-helper change: scan, review, explicit approval, typed actions
only, verify, log, undo, never fabricate data.”*

## Laws

1. **Read-only first.** Scanning and detection never change state. Nothing destructive runs before
   an explicit user approval of a specific, itemised list.
2. **Typed actions only.** The elevated helper exposes a closed command set (`ApplyTweak(id)`,
   `RevertTweak(id)`, `RemoveAppx(fullName)`, `ProvisionedRemove(name)`…) and validates IDs/paths
   against an embedded allow-listed catalog. Never accept free-form registry paths, shell commands
   or `adb shell` strings from the UI.
3. **No downloaded code execution.** Never run the Win11Debloat PowerShell script, never `irm | iex`,
   never bundle or download Google platform-tools without explicit consent and an Authenticode check
   that it is signed by Google.
4. **Preview → approve → apply → verify → log → undo.** Undo must exist before apply is enabled:
   registry snapshots, package reinstall guidance, Android `install-existing`/`enable`, quarantine
   for deletions.
5. **Failures are isolated.** One failed item never aborts the batch; each result is
   `APPLIED | ALREADY | SKIPPED_* | FAILED:<reason>`, and the log is append-only and rotated.
6. **Real data only.** Never fabricate values, sample numbers or success messages. Unavailable data
   shows “Unavailable” plus the reason.
7. **Protected set.** Never touch Microsoft PC Manager processes/data, Windows critical processes, or
   FPM's own protected process names. Android: the denylist of system/launcher/telephony packages
   cannot be selected without Expert mode plus typed confirmation.
8. **Logging and privacy.** Log what changed (package, action, result, timestamp) and nothing
   personal beyond a hashed serial. No telemetry; no secrets in logs, files or commits.

## Review checklist for a card/PR

- [ ] Scan path provably read-only (no writes, no helper calls).
- [ ] Review UI shows the exact changes (old → new) and risk badges before Apply.
- [ ] Apply requires explicit confirmation; destructive confirmations stay modal.
- [ ] Helper validates every ID/path; unknown IDs are rejected, not silently ignored.
- [ ] Verify step re-reads state after apply.
- [ ] Undo path tested (journal/restore or reinstall guidance).
- [ ] Tests never touch the real registry or a real device; use `IRegistry`/`IAdbRunner` fakes.
- [ ] No fabricated numbers in UI, reports or screenshots.
