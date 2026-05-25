---
name: rackpeek-upstream-impact
description: |
  Decide whether it is safe to pull/merge incoming base-branch changes
  (default upstream/main) into the current RackPeek branch, BEFORE doing it.
  Read-only git analysis that flags whether incoming changes would conflict
  with, behaviorally break, or make moot (redundant) the work on the current
  branch, then gives a concise GO / REVIEW / NO-GO verdict. Use whenever the
  user asks "is it safe to pull main", "will upstream/main break my branch",
  "what's pending on main", "should I rebase onto main", "check incoming
  changes before I merge", "did main change anything that affects my work",
  or is about to integrate upstream/origin changes into a feature branch.
  RackPeek-specific: knows which incoming changes (schema/migrations, the
  Resource model, IResourceCollection, DI/CLI wiring, analyzers, the Playwright
  pin) tend to break feature branches. NEVER pulls, merges, or rebases — it
  only analyzes and reports.
targets: [claude]
tags: [git, code-review, rackpeek]
license: MIT
metadata:
  author: Claude Code
  version: 1.0.0
---

# RackPeek Upstream Impact Check

Determine, **before** integrating, whether pending base-branch changes will hurt the current branch. This repo is a **fork**: `upstream/main` is the canonical RackPeek, `origin` is your fork. "Pending changes on main" almost always means `upstream/main`.

## Important — read-only, never integrate

This skill performs analysis ONLY. The single ref-touching command is `git fetch` (updates remote-tracking refs like `upstream/main`; it does not change local branches or the working tree). **Never** run `git pull`, `merge`, `rebase`, `reset`, `checkout`, or `stash` from this skill. End by reporting a verdict; the user decides whether to integrate. If the user wants zero ref writes, run the script with `--no-fetch`.

## Step 1: Run the analysis

```bash
bash .claude/skills/rackpeek-upstream-impact/scripts/analyze-incoming.sh [base-ref] [--no-fetch]
```

- `base-ref` defaults to `upstream/main`. Override for other targets: `upstream/staging`, `origin/main`, a release branch, etc.
- The script prints labeled sections: `incoming-commits`, `incoming-files`, `branch-files` (committed **and** uncommitted — work is often not committed yet), `overlap-files`, and `textual-merge-preview`.

If `base-ahead-by: 0`, there is nothing pending → verdict **GO** immediately, stop.

## Step 2: Classify the impact

Work through three questions in order. The first that fires sets the floor for the verdict.

### A. Textual conflict?
If `textual-merge-preview` reports `TEXTUAL-CONFLICTS`, the listed files will not auto-merge → at least **REVIEW**, usually **NO-GO** until the user resolves them. Name the conflicting files.

### B. Does an incoming change hit a RackPeek breakage hotspot the branch depends on?
Cross-reference `incoming-files` (and `overlap-files`) against the table below. `overlap-files` (touched by both sides) is the highest-risk set — inspect each with `git diff <merge-base>..<base> -- <file>` to see *what* changed.

| Incoming change touches… | Why it can break a feature branch | Severity if branch depends on it |
|---|---|---|
| `schemas/vN/`, `RackPeek.Domain/Persistence/Yaml/*Migration*`, `ResourceYamlMigrationService` | Persisted YAML shape / schema version changed. A new version may demand your branch add a forward migration; a shape change can invalidate how your branch reads or writes resources. | NO-GO until reconciled |
| `RackPeek.Domain/Resources/Resource.cs` (kind maps, `CanRunOn`, `IsHardware`) | Resource-kind semantics and run-on rules. Affects anything that adds a kind, checks relationships, or iterates resource types. | NO-GO / REVIEW |
| `RackPeek.Domain/Persistence/IResourceCollection.cs`, `Yaml/YamlResourceCollection.cs` | Repository contract. If `GetByNameAsync` / `GetConnectionsAsync` / `GetAllOfTypeAsync` signatures or semantics change, every caller in your branch breaks at compile or runtime. | NO-GO |
| `RackPeek.Domain/ServiceCollectionExtensions.cs`, `Shared.Rcl/CliBootstrap.cs` | DI registration and CLI command tree. New required services, renamed registrations, or removed commands break startup or `rpk`. | REVIEW / NO-GO |
| Resource models / `RackPeek.Domain/Resources/SubResources/*` (e.g. `Port.cs`, `PortReference`, `Connection`) | The data your branch renders/computes over. A field rename, type change, or new required member can break helpers and render sites. | NO-GO / REVIEW |
| `Directory.Build.props`, `.editorconfig` | `TreatWarningsAsErrors` is repo-wide. A new analyzer rule can fail your branch's build; a style-rule change can fail `dotnet format --verify-no-changes`. | REVIEW |
| `Tests.E2e/Tests.E2e.csproj` `Microsoft.Playwright` version bump | Each Playwright version pins a Chromium build; a bump invalidates the browser cache and every E2E test fails until `just e2e-setup` re-runs. | REVIEW (re-run e2e-setup) |
| `RackPeek.Web/Dockerfile`, `docker-compose.yml`, `justfile` | Build/run/test pipeline. Image build steps or command names may change; your branch's run/test commands may need updating. | REVIEW |
| The exact `.razor` / `.cs` files in `overlap-files` | Direct edit collision with your branch's behavioral changes, even when text auto-merges (semantic conflict). | REVIEW minimum |

A hotspot appears in `incoming-files` but the branch does **not** touch or depend on it → note it, but it does not raise the floor.

### C. Does an incoming change make the branch's work MOOT?
Read `incoming-commits` messages and the diffs of `incoming-files`. If the base **independently implements the same behavior the branch adds**, the branch may be redundant. Signals: an incoming commit message or diff describing the same feature/fix your branch delivers; a new field/method on the same model your branch added logic for; a refactor that already solves the problem your branch solves. If found → **REVIEW** with an explicit "possibly MOOT" note so the user can decide to drop or rebase the branch.

## Step 3: Emit the verdict (concise — this is the whole output)

Keep it to a verdict line plus at most three reasons and one next step. Do not dump the raw sections.

```
Verdict: GO | REVIEW | NO-GO   (base: <base-ref>, <N> incoming commits)
Reasons:
  1. <top reason, with file/commit>
  2. <next reason, if any>
  3. <next reason, if any>
Next: <one concrete read-only step the user can take, e.g. "inspect git diff <mb>..upstream/main -- <file>", or "safe to pull">
```

Verdict rubric:
- **GO** — no incoming commits, OR incoming changes share no files and hit no depended-on hotspot, no textual conflict, no moot signal.
- **REVIEW** — overlap with no textual conflict but semantic risk; a hotspot adjacent to the branch's work; a possible-MOOT signal; an analyzer/style/Playwright/pipeline change.
- **NO-GO** — textual conflict in a file the branch needs, an incompatible change to a depended-on hotspot (repository contract, resource model, schema/migration), or a schema-version bump the branch's persisted shape does not account for.

If `uncommitted-change-count` is high, mention that the branch surface includes uncommitted work (so the overlap reflects work-in-progress, which is intended).

## Common Issues

### `base ref 'upstream/main' not found`
The remote or ref name differs. The script prints configured remotes — re-run with the correct ref (e.g. `origin/main`, or a fetched branch name).

### `fetch failed (offline?)`
The script falls back to cached remote-tracking refs and continues. Results reflect the last fetch — say so in the verdict, or re-run online for current data.

### `merge-tree unsupported`
Needs git ≥ 2.38 for `--write-tree`. On older git the conflict preview is skipped; rely on `overlap-files` and inspect diffs manually.

### Branch work missing from `branch-files`
Only happens if the branch has neither committed nor working-tree changes for a file. Committed + staged + unstaged + untracked are all included; if something's still missing it isn't a change on this branch.

## Examples

### Example 1: routine pre-pull check
User: "anything on upstream main that'll mess with my port-numbering branch before I pull it in?"
Actions: run the script against `upstream/main`; `base-ahead-by: 0`.
Result: `Verdict: GO (base: upstream/main, 0 incoming commits) — nothing pending; safe to pull.`

### Example 2: schema bump landed upstream
User: "is it safe to rebase onto main?"
Actions: script shows incoming commit touching `schemas/v4/` and `ResourceYamlMigrationService`; branch persists resources.
Result: `Verdict: NO-GO — upstream added schema v4 + migration; your branch writes v3 shape and has no v4 migration. Next: inspect git diff <mb>..upstream/main -- schemas/ before integrating.`

### Example 3: feature made moot
User: "check main before I merge"
Actions: incoming commit "render continuous port numbers across groups" touches `PortLayout.razor` (also in `overlap-files`); the branch adds the same behavior.
Result: `Verdict: REVIEW — possibly MOOT: upstream commit abc123 already adds continuous port numbering to PortLayout.razor, overlapping your work. Next: diff both implementations; decide whether to drop or rebase the branch.`

## When NOT to Use

- Reviewing the branch's own changes for correctness → that's a code review, not an upstream-impact check (use a review workflow).
- Actually performing the pull/merge/rebase → this skill never integrates; the user runs the git write command themselves.
- Non-RackPeek repos → the hotspot table is RackPeek-specific; the git mechanics generalize but the breakage heuristics won't.
- No divergence to assess (you're already up to date with the base) → the script returns GO immediately; nothing to analyze.
