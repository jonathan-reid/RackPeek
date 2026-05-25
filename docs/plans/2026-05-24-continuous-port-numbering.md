# Continuous Port Numbering Implementation Plan

Created: 2026-05-24
Author: jonathan.e.reid@gmail.com
Status: VERIFIED
Approved: Yes
Iterations: 0
Worktree: No
Type: Feature

## Summary

**Goal:** Switch/firewall (and all port-bearing hardware) render port numbers continuously across port groups — a 12-port group followed by a 4-port group displays 1-12 then 13-16, instead of 1-12 then 1-4. Display-only: the stored `config.yaml` port-group structure and connection `PortReference` keys `(PortGroup, PortIndex)` are unchanged.

## Out of Scope

- No schema change, no migration, no new `schemas/vN` directory. `PortReference.PortIndex` remains the per-group physical index everywhere it is stored or compared.
- No new `data-testid` attributes on `PortLayout.razor` (it has none today); its labels are verified by the live-container browser check (TS-002), not the automated suite.

## Approach

**Chosen:** A shared static helper `PortNumbering.Offset(IReadOnlyList<Port>?, int groupIndex)` in `Shared.Rcl/Connections/`, called at every render site as `offset + index + 1`. Resource port-lists needed for offsets are fetched **asynchronously and awaited in `OnParametersSetAsync`** and cached; render-time label/tooltip methods stay purely in-memory (no blocking I/O).
**Why:** One pure, unit-testable arithmetic function keeps all seven sites consistent and isolates the off-by-one risk; awaiting the fetches in `OnParametersSetAsync` (rather than a render-time blocking `.Result`) keeps the render path non-blocking. We do NOT introduce any new render-time `.Result` call — `PortLayout`'s pre-existing one is left untouched (out of scope to refactor).

## Context for Implementer

- **Brace style: write same-line (K&R) braces, e.g. `public static class PortNumbering {`.** `AGENTS.md` §5 claims Allman, but every committed `.cs` (`Port.cs`, `AnsiStripper.cs`, `ConsoleRunner.cs`) uses same-line braces and passes CI's full `dotnet format --verify-no-changes`. Match the committed code, then run `dotnet format --verify-no-changes` to confirm zero diff before commit. Do not trust the AGENTS.md "Allman" line.
- The offset of a port group = sum of `Count` (treat null `Count` as 0) of all port groups *before* it in that resource's `Ports` list. Group 0 always has offset 0.
- `IResourceCollection.GetByNameAsync(name)` returns the full resource including its `Ports` list; cast `is IPortResource` to read `.Ports` (see `PortLayout.GetDestinationPortGroup`, line 161-182).
- `List<Port>?` satisfies `IReadOnlyList<Port>?`, so the helper accepts `resource.Ports` directly.
- Existing E2E connections only use group-0 ports (offset 0), so their `"Port 1"` selector labels are unaffected by this change.

## Runtime Environment

- **Web UI:** Blazor Server on `http://localhost:8080`.
- **Live verify against `config-quindar/` (TS-002):** `docker compose` pulls `aptacode/rackpeek:latest` from the registry and does NOT build local source, so first build+tag the local image, then bring compose up:
  ```bash
  docker build -t aptacode/rackpeek:latest -f RackPeek.Web/Dockerfile .
  docker compose up -d        # binds config-quindar/config.yaml → /app/config
  ```
  `config-quindar/config.yaml` already holds the `usw-pro-max-poe` switch (12× rj45@1 + 4× rj45@2.5). Tear down with `docker compose down`.
- **Automated E2E (TS-001):** `just test-e2e` (rebuilds the `rackpeek:ci` image first; required because `Shared.Rcl` changed).

## Assumptions

- `Repository.GetByNameAsync` returns a resource whose `Ports` reflect the same list/order used to derive `PortGroupIndex` at the call site. Tasks 2-4 depend on this.

## Goal Verification

### Truths

1. Creating a cross-group connection through the UI leaves the persisted `config.yaml` port-group `count` values and the connection's stored `PortReference` `(PortGroup, PortIndex)` unchanged — only rendered labels differ (no data/shape change).
2. A given physical port in group N displays the same continuous number (`offset + index + 1`) everywhere it appears: hardware-details port grid, port-group visualizer, the connection-modal port dropdown, and any connection that references it from the other side.

## E2E Test Scenarios

### TS-001: Continuous numbering + cross-group connection (automated)
**Priority:** Critical
**Preconditions:** Fresh container (empty config), Hardware → Access Points list.
**Mapped Tasks:** Task 1, Task 3, Task 4, Task 5

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create AP "src", add port group `rj45 / 1 / count 3`, then add `sfp+ / 2.5 / count 2` | Two groups render on the card |
| 2 | Read the visualizer label of group 1 (index 1), port 0 | Label shows `4` (offset 3 + 0 + 1), and port 1 shows `5` |
| 3 | Create AP "dst", add one group `rj45 / 1 / count 2` | One group on dst |
| 4 | On "src", click group 1 port 0 to open the connection modal | Modal opens, side A seeded with src group 1 |
| 5 | Read the side-A port dropdown options | Options read `Port 4`, `Port 5` (continuous, not `Port 1`/`Port 2`) |
| 6 | Connect src `Port 4` ↔ dst `Port 1`, submit | Connection created |
| 7 | Open "dst" card, read the `title` tooltip of group 0 port 0 | Tooltip reads `src (port 4)` — destination offset computed from src's groups |
| 8 | Back on "src", read the `title` tooltip of group 1 **port 0** | Tooltip reads `dst (port 1)`, NOT `Available`. This is the persisted-index gate: the visualizer keys connections on the physical port index (`PortIndex = i`, independent of the dropdown's `value`), so src port 0 only shows connected if the stored `PortIndex` is the physical 0. A corrupted dropdown `value="@(offset+i)"` would persist `PortIndex=3`, which matches no rendered port (group has indices 0-1), so port 0 would read `Available` and the assertion fails |

### TS-002: Live container shows 13-16 on the real switch (manual/browser)
**Priority:** Critical
**Preconditions:** Local image built + `docker compose up` with `config-quindar/` (see Runtime Environment); browser at `http://localhost:8080`.
**Mapped Tasks:** Task 2, Task 3, Task 4

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to the `usw-pro-max-poe` switch hardware-details page | Port grid (PortLayout) renders both groups |
| 2 | Inspect the 2.5 GbE (second) group labels | Ports read `13`-`16`, not `1`-`4` |
| 3 | Create a connection from a 2.5 GbE port (e.g. `13`) to another port; reopen/view it | The connection references the correct physical port; local label reads `13`+ and any destination `(port N / count)` uses the other resource's continuous numbering |
| 4 | Inspect persisted `config-quindar/config.yaml` | Port-group `count` values unchanged; stored connection `PortReference` uses the per-group physical index (e.g. PortIndex 0 for port "13") |

## E2E Results

| Scenario | Priority | Result | Fix Attempts | Notes |
|----------|----------|--------|--------------|-------|
| TS-001 | Critical | PASS | 0 | Automated `Port_Numbers_Are_Continuous_Across_Groups` in AccessPointCardTests; full Tests.E2e suite 65/65. Labels 4/5, dropdown Port 4/Port 5, dst tooltip `src (port 4)`, src tooltip `dst (port 1)` (persisted-index gate). |
| TS-002 | Critical | PASS | 0 | Live `rackpeek:ci` container with `config-quindar/` bind-mounted; usw-pro-max-poe 2.5 GbE group renders 13-16 (Playwright headless against the running image). |

**Not Verified:** None — all task DoD criteria and both E2E scenarios have automated/live verification (unit 7/7, CLI 209/209, E2E 65/65, TS-002 live).

## Progress Tracking

- [x] Task 1: Add `PortNumbering.Offset` helper + unit test
- [x] Task 2: Wire offsets into PortLayout.razor (3 sites)
- [x] Task 3: Wire offsets into PortGroupVisualizer.razor (2 sites)
- [x] Task 4: Wire offsets into PortConnectionModal.razor dropdowns (2 sites)
- [x] Task 5: Add E2E regression test + POM helpers

## Implementation Tasks

### Task 1: Add `PortNumbering.Offset` helper and unit test

**Objective:** Create the single pure function that computes a port group's starting offset (sum of preceding groups' `Count`, null `Count` treated as 0). All render sites in later tasks call it. Verified directly by a unit test.

**Files:**
- Create: `Shared.Rcl/Connections/PortNumbering.cs`
- Test: `Tests/PortNumberingTests.cs`

**Key Decisions / Notes:**
- Namespace `Shared.Rcl.Connections` (folder convention; `PortGroupEditor.razor` does `@using Shared.Rcl.Connections`). `Port` is in `RackPeek.Domain.Resources.SubResources`.
- Signature: `public static int Offset(IReadOnlyList<Port>? ports, int groupIndex)`. Return 0 when `ports is null` or `groupIndex <= 0`; otherwise sum `ports[g].Count ?? 0` for `g` in `[0, min(groupIndex, ports.Count))` (clamp guards an out-of-range index).
- Same-line braces (see Context for Implementer). ImplicitUsings is on, so `System`/`System.Collections.Generic` need no explicit using.
- Tests project already references `Shared.Rcl` (`Tests/Tests.csproj:28`). Use xUnit `[Theory]`/`[InlineData]` — one test class, a handful of rows: null/empty → 0; group 0 → 0; `[3,4,2]` group 1 → 3, group 2 → 7; a null-`Count` group in the middle (`[3, null, 2]` group 2 → 3, pinning the "null Count = 0" convention); groupIndex beyond list clamps to total.

**Definition of Done:**
- [ ] `Offset(null, 1) == 0`, `Offset([3,4], 0) == 0`, `Offset([12,4], 1) == 12`, `Offset([3,4,2], 2) == 7`, `Offset([3, null, 2], 2) == 3` (null Count counts as 0), and an out-of-range groupIndex returns the full sum without throwing.
- [ ] Verify: `dotnet test Tests --filter FullyQualifiedName~PortNumbering` and `dotnet format --verify-no-changes`

### Task 2: Wire offsets into PortLayout.razor (3 render sites)

**Objective:** Render continuous numbers in the hardware-details port grid. Local labels (connected line 47, free line 74) use this resource's offset; the destination line (57) uses the *other* resource's offset, reusing the `_portResources` cache that `GetDestinationPortGroup` already populates.

**Files:**
- Modify: `Shared.Rcl/Connections/PortLayout.razor`

**Key Decisions / Notes:**
- Add a field `int _localOffset;`. In `OnParametersSetAsync` (after loading `_connections`), when `ResourceName` is non-empty `await Repository.GetByNameAsync(ResourceName)` (async — do NOT use the blocking `.Result`) and set `_localOffset = PortNumbering.Offset((res as IPortResource)?.Ports, PortGroupIndex)`.
- Line 47 and line 74: replace `@(index + 1)` with `@(_localOffset + index + 1)`.
- Line 57: replace `@(other.PortIndex + 1)` with `@(DestinationOffset(other) + other.PortIndex + 1)`, where `DestinationOffset(PortReference other)` returns `PortNumbering.Offset(_portResources[other.Resource]?.Ports, other.PortGroup)` reading the entry the **existing** `GetDestinationPortGroup` already populated at line 35 in the same render iteration. This adds NO new fetch — it reuses the cache the existing code fills. (Do not refactor `GetDestinationPortGroup`'s pre-existing blocking `.Result`; it is out of scope.)
- `@using Shared.Rcl.Connections` if the helper namespace is not already in scope.
- Perf: no new I/O on the render path; the local fetch is one awaited call per `OnParametersSetAsync`.

**Definition of Done:**
- [ ] On a resource whose first group has count 12, the second group's first connected/free port renders `13`; a connection's destination fragment reads `(port <destOffset+idx+1> / count)` using the destination resource's groups.
- [ ] Verify: `dotnet build RackPeek.sln` clean + `dotnet format --verify-no-changes` (behavioural proof via TS-002 in spec-verify Phase B)

### Task 3: Wire offsets into PortGroupVisualizer.razor (2 render sites)

**Objective:** Render continuous numbers in the interactive visualizer label (line 51) and the destination port number inside the tooltip (line 116). The tooltip's destination offset must come from the other resource's groups — prefetched **async** so the tooltip method stays pure (no render-time blocking I/O, the issue Codex flagged).

**Files:**
- Modify: `Shared.Rcl/Connections/PortGroupVisualizer.razor`

**Key Decisions / Notes:**
- `@using RackPeek.Domain.Resources.Servers` for `IPortResource`.
- Add `int _localOffset;` and `private readonly Dictionary<string, IReadOnlyList<Port>?> _portResources = new();`.
- Extend the existing async `OnParametersSetAsync` (it currently only loads `_connections`):
  - (a) After loading `_connections`, `await Repository.GetByNameAsync(ResourceName)` and set `_localOffset = PortNumbering.Offset((res as IPortResource)?.Ports, PortGroupIndex)`.
  - (b) **Async preload** destinations: for each connection in `_connections` that touches `(ResourceName, PortGroupIndex)`, resolve the `other` `PortReference`; if `other.Resource` is not already a key in `_portResources`, `await Repository.GetByNameAsync(other.Resource)` and store `(res as IPortResource)?.Ports`. This fills the cache before any render.
- Local label (line 51): replace `@(index + 1)` with `@(_localOffset + index + 1)`.
- Destination tooltip — `GetTooltip` stays **synchronous and pure** (no `.Result`, no `await`): after `other` is resolved, `var destOffset = PortNumbering.Offset(_portResources.GetValueOrDefault(other.Resource), other.PortGroup);` and return `$"{other.Resource} (port {destOffset + other.PortIndex + 1})"`. A cache miss yields offset 0 gracefully (preload covers every connected port, so misses should not occur for rendered connections).

**Definition of Done:**
- [ ] Group-1 (after a 12-port group) visualizer labels read `13`+; a connected port's `title` tooltip reads `<dest> (port <destOffset+idx+1>)` using the destination resource's groups.
- [ ] No blocking `.Result`/`.Wait()` added to any render-path method in this component (all I/O is awaited in `OnParametersSetAsync`).
- [ ] Verify: covered by TS-001 (Task 5); `dotnet build` clean + `dotnet format --verify-no-changes`

### Task 4: Wire offsets into PortConnectionModal.razor port dropdowns (2 render sites)

**Objective:** Make the side-A and side-B port `<select>` option labels continuous so the dropdown matches the visualizer beneath it. The submitted value (`value="@i"`) stays the per-group physical index — only the visible label changes.

**Files:**
- Modify: `Shared.Rcl/Connections/PortConnectionModal.razor`

**Key Decisions / Notes:**
- Line 78: `<option value="@i">Port @(PortNumbering.Offset(_resourceA?.Ports, _portA.PortGroup) + i + 1)</option>`. `_resourceA` (IPortResource) and `_portA.PortGroup` are already in memory here (set in the `_resourceAIndex`/`_groupAIndex` setters) — no fetch needed.
- Line 140: same for side B using `_resourceB?.Ports` and `_portB.PortGroup`.
- Invariant: the loop body only runs when `_groupA` (resp. `_groupB`) is non-null, and the `_groupAIndex`/`_groupBIndex` setters guarantee `_resourceA`/`_resourceB` is non-null whenever the group is set. So `_resourceA?.Ports` is never null here; the `?.` is a belt-and-suspenders guard that yields offset 0 rather than throwing. Add a one-line code comment saying so, so a maintainer does not delete the null-conditional as dead code.
- `value="@i"` MUST remain `i` (the physical index) — `HandleSubmit` stores `_portAIndex`/`_portBIndex` as `PortIndex`. Do not change the option `value`. (TS-001's round-trip step machine-checks this.)
- `@using Shared.Rcl.Connections` if not already in scope (this file is itself in that namespace).

**Definition of Done:**
- [ ] After selecting a group preceded by a 12-port group, the port dropdown lists `Port 13`…`Port 16`; submitting still persists the physical per-group `PortIndex` (machine-checked by TS-001 step 8's src-side tooltip gate, plus the unchanged config in TS-002 step 4).
- [ ] Verify: covered by TS-001 (Task 5); `dotnet build` clean + `dotnet format --verify-no-changes`

### Task 5: Add E2E regression test + PortsPom helpers

**Objective:** Add one workflow E2E test (TS-001) that proves continuous numbering in the visualizer and connection dropdown, and that a cross-group connection's destination reference uses the other resource's offset. This is the durable regression guard for the wiring in Tasks 2-4.

**Files:**
- Modify: `Tests.E2e/PageObjectModels/PortsPom.cs` (add read helpers)
- Modify: `Tests.E2e/AccessPointCardTests.cs` (add the test method)

**Key Decisions / Notes:**
- Reuse `AccessPointCardPom` — it already exposes `AddPortGroupAsync`, `OpenConnectionFromPortAsync`, `CreateConnectionAsync` via `PortsPom`. The render logic is shared across all hardware kinds (all use `PortGroupEditor` → `PortGroupVisualizer`), so AP is a valid proxy for the switch.
- Add to `PortsPom`: `PortLabelTextAsync(prefix, groupIndex, portIndex)` → `Port(...).InnerTextAsync()`; `PortTooltipAsync(prefix, groupIndex, portIndex)` → `Port(...).GetAttributeAsync("title")`; `PortOptionLabelsAsync(prefix, side)` → read `<option>` text contents of `PortASelect`/`PortBSelect`.
- Why a tooltip cross-check, not a repo/YAML read: `Tests.E2e` drives a Testcontainers-hosted image over Playwright and has no in-process repository handle, and there is no GET inventory HTTP endpoint (only `MapPost /api/inventory`). The visualizer keys `GetConnection` on the physical `PortIndex = i` (independent of the dropdown's option `value`), so asserting the src-side tooltip is the persisted-physical-index gate — it fails if the stored index is the display number instead of 0.
- Follow the existing AP connection test (`AccessPointCardTests.cs:320-390`) for setup/teardown idioms: unique `Guid` names, `[Fact]`, self-cleaning, same-line braces.
- Steps mirror TS-001: src (groups `3` then `2`) → assert group-1 port-0 label `4`; open connection → assert side-A options `Port 4`/`Port 5`; connect src `Port 4` ↔ dst `Port 1`; on dst assert group-0 port-0 tooltip `"<src> (port 4)"`; back on src assert group-1 port-0 tooltip `"<dst> (port 1)"` (persisted-index gate, NOT `Available`).
- One E2E test class addition only (reuse existing AP test file) — no per-site test split.

**Definition of Done:**
- [ ] New `[Fact]` passes: group-1 first port label is `4`, side-A dropdown options are `Port 4`/`Port 5`, the dst tooltip reads `<src> (port 4)`, and the src group-1 port-0 tooltip reads `<dst> (port 1)` (NOT `Available`) — proving the persisted `PortIndex` is the physical per-group index, not the display number.
- [ ] Verify: `just test-e2e` green (full E2E suite, 0 failures) + `dotnet format --verify-no-changes`
