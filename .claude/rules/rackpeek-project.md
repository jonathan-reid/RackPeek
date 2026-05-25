# RackPeek — Project Rules

**Single source of truth is `AGENTS.md` at the repo root.** It is imported below so
Claude Code loads it every session. Edit `AGENTS.md` for any project-fact change
(tech stack, layout, commands, gotchas); do NOT duplicate that content here.

@../../AGENTS.md

## Claude Code working notes

These consolidate the highest-risk gotchas already detailed in `AGENTS.md` — keep
them top-of-mind, the full context lives in the imported guide.

- **IMPORTANT: run `dotnet format` before any commit.** The `format` CI job runs
  first and fails the whole pipeline on a single style diff.
- **`just ci` (= `test-all`) is the pre-PR gate** — format → CLI tests → Docker build → E2E.
- **`TreatWarningsAsErrors` is repo-wide.** Fix warnings; never paper over with
  `#pragma warning disable`.
- **Revert `Headless = false` / `SlowMo` in `Tests.E2e/Infra/PlaywrightFixture.cs`**
  before committing — CI requires headless.
- **Feature work targets the `staging` branch**, not `main`.
- **All YAML mutation goes through `IResourceUseCase<T>` → `IResourceCollection`** —
  never write `config/config.yaml` directly. A persisted-shape change needs a new
  `schemas/vN+1/` + forward migration + `Tests/TestConfigs/vN+1/` fixtures.
