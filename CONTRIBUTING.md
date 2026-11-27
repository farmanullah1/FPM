# Contributing to FPM

Thanks for helping. FPM is a safety-first Windows utility; contributions are judged on correctness
and safety before features.

## Ground rules

1. **Safety before features.** Read `docs/readiness/skills/fpm-safety-contract.md`. Anything that
   deletes, uninstalls or changes system state must follow scan → review → explicit approval →
   typed action → verify → log → undo.
2. **No new dependencies without a licence review.** Update `THIRD_PARTY_NOTICES.md` in the same PR.
3. **No stubs, `TODO`s or fake data.** Unavailable data shows “Unavailable” and the reason.
4. **No Microsoft, Google or vendor assets.** Original artwork and text only.
5. **One concern per PR**, with a conventional commit message (`fix(cleanup): …`).

## Build and test

```
cd app
dotnet restore PCManager.sln
dotnet build PCManager.sln -nodeReuse:false -m:1
dotnet test tests/PCManager.Tests/PCManager.Tests.csproj
```

All of these must pass with **0 warnings and 0 errors**. Also run:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/lint-xaml.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/contrast-check.ps1
dotnet run --project tools/IconGen -- --check
```

The XAML lint is a ratchet: never raise `tools/xaml-lint-baseline.json` to make a change pass.

## Code style

- C# 12, nullable enabled, files under ~400 lines; split partials instead of growing one file.
- New source files start with `// SPDX-License-Identifier: GPL-3.0-or-later`.
- UI text goes through the localization resources once the `.resx` foundation lands; until then keep
  strings in the view models/views where they already live.
- UI: tokens only (`Styles/Spacing.xaml`, theme dictionaries), `Icon3D` for icons, motion through
  `Motion`/`IMotionPolicy`, Light and Dark, 360/560/1280 px.
- Tests: no real registry, real device or real user files. Use the existing fakes/abstractions.

## Reporting bugs and security issues

- Functional bugs: GitHub issue with steps, expected/actual, version, Windows build and theme.
- Security issues: **do not open a public issue**; follow `SECURITY.md`.

By contributing you agree your work is licensed under GPL-3.0-or-later.
