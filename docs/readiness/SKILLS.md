# Skill inventory and gap analysis

Date: 2026-11-27. Sources: the account skill library, the repository, and `npx skills find`
(the `/find-skills` workflow).

## 1. What is already available (account library)

50 skills are enabled, but none target WPF/.NET desktop verification, Windows system internals, or
this project's safety rules. The ones that genuinely support FPM work:

| Skill | Use in FPM |
|---|---|
| `verification-before-completion` | Forces evidence before claiming a card is green. |
| `systematic-debugging` | Root-cause discipline for build/runtime failures (e.g. `.g.cs`, DLL locks). |
| `planning-with-files` | Multi-step work with on-disk plan/progress files (this folder follows the same idea). |
| `executing-plans` / `writing-plans` | Turning the card backlog into executable steps. |
| `git-commit` | Conventional commit messages, staging discipline. |
| `review` / `requesting-code-review` / `receiving-code-review` | Card reviews along Standards + Spec axes. |
| `test-driven-development` / `tdd` | Unit tests for Core/Services logic before implementation. |
| `using-git-worktrees` | Isolating risky W/D/A work without disturbing `main`. |
| `accessibility` (web-focused) | Partially reusable for contrast/focus/labels; WPF specifics must be adapted. |
| `skill-creator` | Authoring the two FPM-specific skills below. |
| `find-skills` | The discovery workflow used here. |

Everything else in the library is web/cloud/DB/language-specific and not applicable to a WPF
desktop app (`shadcn-ui`, `tailwind-*`, `nextjs-*`, `neon-*`, `supabase-*`, `wrangler`,
`workers-*`, `vercel-*`, `azure-*`, Python/Rust/Go skills, `pdf`, `mcp-builder`, etc.).

## 2. What is installed in the project

The repository has no `AGENTS.md` / `CLAUDE.md` and no local skill folder; the previous protocol
existed only in `AGENT_STATE.md` (ignored) and the plan documents. There are project tools that act
like skills and are inventoried here:

| Project asset | Role |
|---|---|
| `tools/lint-xaml.ps1` + `tools/xaml-lint-baseline.json` | Token ratchet. |
| `tools/contrast-check.ps1` | Light/Dark contrast gate. |
| `tools/IconGen` + `tools/icons/*.svg` | Icon generation and `--check`. |
| `docs/dev/REPO_AUDIT.md` | R1 repo hygiene evidence. |
| `AGENT_STATE.md` (ignored) | Legacy card state; superseded for reporting by `docs/readiness/`. |

## 3. Gap analysis

| Gap | Why it matters | Decision |
|---|---|---|
| No WPF/.NET desktop verification skill | Every card depends on the exact build/lock/gates/screenshot procedure; rediscovering it each session wastes time and invites false “green” claims. | Author `fpm-wpf-verification` (see `skills/`). |
| No encoded safety contract | Cleanup/debloat/ADB work is where a mistake is destructive; the laws are spread across v7 §2/§10–§12 and the cards. | Author `fpm-safety-contract`. |
| No WPF UI-automation skill | Screenshot/scroll/nav verification is hand-written PowerShell each time. | Covered inside `fpm-wpf-verification` §5; a separate skill would duplicate it. |
| No localization workflow | Strings are literals; a future i18n sweep needs a repeatable extraction procedure. | Deferred until Q-03 is answered; document in the U10 card. |
| No release/signing skill | R3/R4 need Inno Setup, SHA256SUMS, optional signing. | Deferred to W6; covered by R3/R4 cards in the plan. |

## 4. Ecosystem search (`/find-skills`)

Ran `npx skills find wpf`, `dotnet`, and `windows desktop` on 2026-11-27:

- WPF results are **Syncfusion vendor component skills** (~140 installs each: grid, pivot, OLAP,
  masked textbox). They document a commercial control suite FPM does not use and would pull in
  vendor-specific advice — **not installed**.
- `dotnet/skills` results are 4K-install skills about templates, xUnit→MSTest migration, data-driven
  ASP.NET Core — none match a WPF desktop card workflow — **not installed**.
- “windows desktop” returned ~30-install, low-reputation skills (iconography, RDP helpers) —
  **not installed**.

Conclusion: no credible external skill covers this project; authoring the two FPM skills is the
correct action.

## 5. Install attempt and blocker

`cloud_manage_skills save` for both skills failed with:

> `Your library is full at 50 skills. Remove one, then upload again.`

Status: **blocked** (R-06). Resolution options for the owner (Q-02):

1. Remove two unused skills from the library (e.g. web-framework-specific ones), then install
   `fpm-wpf-verification` and `fpm-safety-contract` using the descriptions in `skills/`.
2. Accept the repo copies as the working version (the agent reads them directly) and skip the
   account install.

Interim state: both skills exist as repo files (`skills/fpm-wpf-verification.md`,
`skills/fpm-safety-contract.md`) and are referenced from `AGENTS.md`, so they are usable today.

## 6. Sandbox test (2026-11-27)

Procedure: copied both skill files into `.tmp-skill-sandbox/` inside the repo, resolved every
referenced path and command, scanned for destructive verbs, then deleted the sandbox.

Results:

- Files copied: 2; sandbox removed afterwards: yes.
- All referenced paths exist: `tools/lint-xaml.ps1`, `tools/contrast-check.ps1`, `tools/IconGen`,
  `app/PCManager.sln`, the test project, `installer/FPM.iss`, both reference folders.
- Commands resolve: `dotnet`, `powershell`, `git`.
- Destructive-verb scan: none of `rm -rf`, `Remove-Item -Recurse`, `git push`, shutdown, format.
  The only process action is `Stop-Process` scoped to **FPM** (required before builds); the only
  delete instruction is removing `obj`/`bin` for the `.g.cs` failure, which is standard and local.
- Limitation: this is a static/sandbox validation, not a live card run. The first card executed
  under these skills will be the real acceptance test and is recorded in `PROGRESS_LOG.md`.
