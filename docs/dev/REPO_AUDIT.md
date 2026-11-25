# Repository audit (card R1)

Run on 2026-11-25 against `main` at `ca5ad56`, 61 commits deep, working tree clean
apart from this card.

## Headline numbers

| Measure | Value |
|---|---|
| Tracked files | 223 |
| Tracked working-tree bytes | 1,583,733 (~1.5 MB) |
| Tracked files over 1 MB | none |
| Object store | 2.54 MiB total, 975 objects in packs, 193 loose |
| Largest tracked blobs in history | none over 1 MB |
| Tracked file types | 185 `.cs`, 14 `.xaml`, 7 `.png`, 6 `.csproj`, 2 `.md`, 2 `.manifest`, 1 `.sln`, 1 `.json` |

The repository is small and text-heavy: source, XAML, project files, one icon set and
one screenshot set. Nothing needs LFS.

## Forbidden material: status

| Class | Check | Result |
|---|---|---|
| Microsoft PC Manager installer / installed files | `git ls-files` | none |
| REA outputs, screenshots of Microsoft's app, `/rea/` | `git ls-files`, `git log --all -- REA_REFERENCE_AUDIT.md` | file exists locally but is **ignored and never committed** |
| Reference sources (`Win11Debloat`, UAD) | `git log --all -- reference/`, `git ls-files reference` | 0 tracked, never committed; `.gitignore` covers `reference/` |
| Downloaded `platform-tools/` | `git ls-files` | none; pattern added |
| AI-agent files (`AGENT_STATE.md`, `docs/freebuff/`, prompts, `.cursor/`, `.claude/`, `.gemini/`, `.codebuff/`, `.freebuff/`, …) | `git ls-files` | none tracked; patterns added |
| Build/IDE output (`bin/`, `obj/`, `publish/`, `dist/`, `artifacts/`, `TestResults/`, `*.trx`, `*.nupkg`) | `git ls-files` | none tracked |
| Installer output (`installer/Output/`, `*.msi`, `*.exe`, `*.zip`) | `git ls-files` | none tracked |
| Secrets/signing (`.env*`, `*.pfx`, `*.p12`, `*.snk`, `*.key`, `*.pem`, `secrets.json`, `local.properties`) | `git ls-files` | none tracked |
| Local app data (`settings.json`, `cleanup_log.txt`, `debloat_log.txt`, `android_actions.log`, `journal/`, `backups/`, `quarantine/`, `Data/`) | `git ls-files` | none tracked |
| OS junk (`Thumbs.db`, `Desktop.ini`, `.DS_Store`) | `git ls-files` | none tracked |

No `git rm --cached` was needed: nothing forbidden was ever tracked.

## `.gitignore` changes

Replaced the ad-hoc list with a VisualStudio-template-derived policy in labelled
sections: build/IDE output, packages, installer and release artifacts, secrets and
signing material, local app data, third-party reference material, investigation
artifacts, AI-agent files, OS/editor noise, root documentation.

Two corrections worth noting:

- The old rule `/*.md` (with `!/README.md`) would have blocked every future public
  document. It now unignores `README.md`, `CHANGELOG.md`, `CONTRIBUTING.md`,
  `SECURITY.md`, `CODE_OF_CONDUCT.md`, `THIRD_PARTY_NOTICES.md` and all of `docs/**`,
  then re-ignores `docs/freebuff/`.
- `secrets.json`, `.key`, `.pem`, `.p12`, `platform-tools/`, `installer/Output/`,
  `*.zip`, `publish/`, `dist/`, `artifacts/`, `coverage/` and the agent-tool folders
  were missing entirely and are now covered.

The six plan documents were moved to `docs/freebuff/` as the start file asks; they
remain ignored.

## Proof

`git check-ignore -q` on sample paths returns "ignored" for
`app/bin/x.dll`, `installer/Output/FPM-Setup-1.0.exe`,
`reference/win11debloat/Win11Debloat.ps1`, `AGENT_STATE.md`, `FPM_Master_Plan_v7.md`,
`docs/freebuff/FPM_FreeBuff_00_START_HERE.md`, `settings.json`, `secrets.json`, `.env`,
`platform-tools/adb.exe`, `REA_REFERENCE_AUDIT.md`; and "visible" for `README.md`,
`CHANGELOG.md`, `docs/dev/REPO_AUDIT.md`, `.gitattributes`, `installer/FPM.iss`,
`app/src/PCManager/Assets/fpm.ico`. No currently tracked path matches any ignore rule.

`.gitattributes` (committed earlier) already does what R1 asks: `* text=auto eol=crlf`,
per-type text rules, `*.sh` as LF, and binary marks for images, fonts, executables,
installers, archives and signing files.

## Secret scan

`gitleaks` and `trufflehog` are not installed on this machine, so the scan was
grep-based — a real limitation, worth repeating once a scanner is available.

- Working tree, tracked files only, case-insensitive
  `(api[_-]?key|apikey|secret|token|password)\s*[:=]`: one hit,
  `app/tests/PCManager.Tests/SafetyGuardTests.cs:76` — the string is a *file name*
  (`auth-token.json`) in a cleanup test, not a credential.
- History, all 61 commits, `ghp_`, `sk-…`, `BEGIN (RSA|OPENSSH) PRIVATE KEY`,
  `password = "…"`: no hits.
- Absolute machine paths (`C:\Users\<name>`, `C:/Users/`) in tracked files: none.
- History blobs over 1 MB: none.

No secret or proprietary file exists in history, so no history rewrite (which would
need the owner's approval) is required.

## Follow-ups for later cards

- R2 adds `LICENSE` (GPL-3.0-or-later), `THIRD_PARTY_NOTICES.md` and the community
  files; the ignore policy already whitelists them.
- Repeat the secret scan with `gitleaks` once it is installed, and wire it into CI at R4.
