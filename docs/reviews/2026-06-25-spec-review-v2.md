# Spec Review v2 - 2026-06-25

> Import note: copied from `C:\Users\Sev\Downloads\spec-review-2026-06-25-v2 (1).md` on
> 2026-06-25. This artifact appears to review a TerminalHQ/THQ spec pack, not PiPlay. It is retained
> as provided for owner review and should not be treated as PiPlay release evidence unless the owner
> confirms it belongs in this repository.

Status: review artifact; second pass after the `spec-workflow-review-2026-06-25.md` findings were closed. Findings here are not yet addressed in canonical docs.

Scope: full `docs/` tree, `AGENTS.md`, `README.md`, `scripts/`, version/build files. This pass assumes the first review's seven findings are resolved and looks for second-order gaps that a consistency/traceability pass does not surface — bridge-gate risk, Milestone 0 ownership, identity, contract completeness.

Checks run:

```text
./build.ps1                 # canonical; not run here (no pwsh in the review environment)
validate-pack.ps1 checks    # reproduced independently and run against the pack
```

Result: the pack passes its own gate — the reproduced validator checks report 0 errors, 0 warnings (12 register rows map to filenames, statuses agree, ADR-0012 correctly excluded as the spike, every non-deferred todo links known requirements). The seven prior findings are confirmed closed in the canonical docs, not merely marked closed: ADR-0007/0008 read `accepted` with owner docs realigned, the register maps to filenames, `validate-pack.ps1` enforces owner-path/status/requirement-link rules, the backlog cards were rewritten off the old cockpit/grid framing, pane IDs are durable across restart everywhere, and note external-edit conflict handling is now a data rule plus AT-012.

This is a disciplined pack. The findings below are about what the spec being *complete enough to feel finished* hides, not about its internal consistency.

## Findings

### 1. The rendering-path spike is the entire bet; its downside branch is unplanned and its decision rule can misfire

Severity: Critical

ADR-0012 is correctly named the one hard gate, but the branch asymmetry is severe. Pass keeps ADR-0009/0010 and unblocks five sequential terminal cards. Fail means 4b — owning a VT parser/renderer — which ADR-0009 itself lists under "Revisit when" as a trigger to reconsider the language for Rust. There is no 4b card in the backlog, no 4b estimate, and no 4b shape anywhere; the fallback is a direction, not a plan. A two-day box is also optimistic for measuring p95 input latency and backpressure across four bridge variants with a chunk-size sweep — that is a measurement harness plus a methodology, not a wiring task.

The decision rule has a subtler problem. The latency bar measures `keystroke -> glyph paint`, which is end-to-end (bridge + WebView + xterm.js render). The decision it gates ("fall back to 4b") is only sound if the *bridge* is the bottleneck. If end-to-end p95 is 60 ms and 45 ms of that is xterm.js/WebView render that 4b would not avoid, failing 4a on that number is the wrong call and burns the project into a renderer rewrite it did not need.

Recommended follow-up:

- Measure the bridge's *marginal* contribution (post-message to write() callback), not only end-to-end keystroke latency, and gate the 4a/4b decision on the marginal number.
- Write even a one-paragraph 4b shape and rough cost so a fail-result does not strand the plan with no scoped fallback.
- Re-scope the time-box around building the measurement harness, or accept it will run longer than two days.

Local evidence:

- `docs/90-adrs/0012-rendering-path-spike.md`
- `docs/90-adrs/0009-implementation-language-and-ui-stack.md` (the "Revisit when" triggers)
- `docs/05-implementation/risk-register.md` (R-010 mitigation is a direction, not a plan)

### 2. Milestone 0 does not own the domain records or the integrity tests it is supposed to land, and the two-track split conflicts with persistence

Severity: High

`invariants-and-integrity.md` calls round-trip, migration, ownership-reconcile, note-reconcile, and lock "the cheapest high-value tests in the suite" and says they "belong in the first test project and run in CI from Milestone 0." No backlog card owns them. THQ-0100's acceptance is project boundaries, the event/timeline skeleton, build discovery, and version stamping — it never lands the persisted record types (`Workspace`/`Page`/`TerminalSession`/`TerminalPane`/`Note`/`WorkbenchView`), `schemaVersion`, or the migration registry. So the highest-leverage tests in the pack are homeless.

This also exposes a tension in `v0-build-plan.md`. It states Track A and Track B "share no files until the integration cards," but THQ-0501 (Track A, P0, persistence) must serialize `TerminalSession`/`TerminalPane`, whose shape and ID semantics come from Track B (THQ-0203) and the adapter contract (THQ-0201). Two agents dispatched per the plan's own instruction collide on the domain types immediately.

The clean resolution is one statement: the persisted domain core, `schemaVersion`, the migration registry, and the five integrity tests are part of THQ-0100; both tracks depend on that shared `domain` project; the real split is {shell/UI} (Track A) vs {adapter/runtime} (Track B) over a shared domain core. With that, "share no files" becomes true (both share `domain`, not each other) and the integrity tests get a home.

Recommended follow-up:

- Expand THQ-0100 acceptance to include the persisted record types, `schemaVersion`, the migration registry, and the five headless integrity tests from `invariants-and-integrity.md`.
- Reword the `v0-build-plan.md` track description to "both tracks over a shared domain core," not "share no files."

Local evidence:

- `docs/05-implementation/todo-backlog-v0.md` (THQ-0100 acceptance; THQ-0501 depends on Track B types)
- `docs/03-domain/invariants-and-integrity.md` (tests "run in CI from Milestone 0")
- `docs/05-implementation/v0-build-plan.md` ("share no files until the integration cards")
- `docs/05-implementation/milestones.md` (Milestone 0)

### 3. Workspace identity (`<workspace-hash>`) is undefined, and the resume promise hangs off it

Severity: High

State lives under `%LOCALAPPDATA%\TerminalHQ\workspaces\<workspace-hash>` and `IWorkspaceService` is specified to "compute workspace ID/hash," but nothing defines the hash input. If it is `hash(rootPath)`, then renaming or moving the repo folder silently orphans every note, layout, and timeline — the exact "restore breaks silently" failure ADR-0008 exists to prevent, one level up at directory identity rather than schema. If it is a random id stored in `workspace.json`, then re-opening a folder needs a path-to-workspace index, and `recent.json` / `recentWorkspaces` is recency, not resolution. The product thesis is that the terminal workspace has memory; the memory is keyed on something the spec never states.

Recommended follow-up:

- Define the workspace identity rule in `state-and-storage.md`: what the hash is computed from, and the behavior when `rootPath` moves or is renamed (re-key with warning, prompt to relocate, or treat as a new workspace — pick one).

Local evidence:

- `docs/01-product/v0-scope.md` (storage path)
- `docs/04-architecture/services-and-interfaces.md` (`IWorkspaceService` "compute workspace ID/hash")
- `docs/03-domain/state-and-storage.md` (no identity rule)

### 4. The command palette is required-by-scope, SHOULD-by-requirements, and has no backlog card; the validator cannot catch this class

Severity: Medium

`v0-scope.md` lists the command palette under *Required V0 features* with an explicit action list (including Save/Restore layout, Copy diagnostics, and the region toggles several acceptance tests lean on). `requirements-index.md` marks the same thing THQ-REQ-012 as SHOULD. The backlog has no card referencing THQ-REQ-012 and nothing that builds the palette. The validator does not flag this because requirement-link enforcement is one-directional: it checks cards reference known requirements, never that requirements have a card. Reproducing the check shows five requirements with no card; four are correctly MAY/roadmap (014/015/016/021), but THQ-REQ-012 is a live gap that will quietly not get built.

Recommended follow-up:

- Add a palette card (Track A, around the THQ-03xx/04xx range) and reconcile whether the palette is MUST (per v0-scope) or SHOULD (per the index).
- Add a reverse-coverage check to `validate-pack.ps1`: warn when any MUST/SHOULD requirement has no non-deferred card linking it. This closes the whole class, not just this instance.

Local evidence:

- `docs/01-product/v0-scope.md` (palette under Required V0 features)
- `docs/01-product/requirements-index.md` (THQ-REQ-012 = SHOULD)
- `docs/05-implementation/todo-backlog-v0.md` (no card links THQ-REQ-012)
- `scripts/validate-pack.ps1` (requirement check is card-to-req only)

### 5. Backpressure has no return channel in the adapter contract

Severity: Medium

`backend-adapter-contract.md` says that if a subscriber is slow, the adapter bounds its buffer and pauses PTY reads. The primary subscriber, xterm.js, is across the WebView2 boundary, and `PostWebMessage*` is fire-and-forget — the adapter receives no signal that the renderer is behind, so its "bounded buffer" only bounds the pre-post .NET side while bytes accumulate invisibly in the JS heap. Pausing PTY reads on real end-to-end queue depth requires an explicit credit/ack message from the JS side back to the host. That mechanism is exactly the backpressure bar the spike must hit, and the contract does not name it.

Recommended follow-up:

- Specify the flow-control return message (JS-to-host credit or drain-ack) in `backend-adapter-contract.md` before THQ-0202 hardens around its absence, and have the THQ-0200 spike exercise it.

Local evidence:

- `docs/04-architecture/backend-adapter-contract.md` (output delivery / backpressure)
- `docs/90-adrs/0012-rendering-path-spike.md` (backpressure bar)

### 6. Restore replay of a "raw tail as inert text" is not inert

Severity: Medium

`state-and-storage.md` and `backend-adapter-contract.md` offer two restore-replay options as if equivalent: the serialized xterm.js buffer, or a capped tail of the raw output stream, re-fed "as inert text." They are not equivalent. Re-feeding a raw VT tail re-executes whatever escape sequences it contains — cursor moves, title sets, OSC 133 marks, even query sequences that expect a response. "Inert" only holds for the serialized buffer (already-rendered cells) or a tail that has been stripped of control sequences.

Recommended follow-up:

- Choose the serialized-buffer path, or mandate control-sequence sanitization for the raw-tail path; do not present them as equivalent.
- Add one sentence for a visible separator between replayed history and the new shell's first prompt, so the live boundary is unambiguous.

Local evidence:

- `docs/03-domain/state-and-storage.md` (restore policy, capped-tail replay)
- `docs/04-architecture/backend-adapter-contract.md` (scrollback and restore)

## Secondary notes

- **In-process multi-window writes.** The `.lock` guards against two processes (`invariants-and-integrity.md` §5), but `windows[]` is real in the v0 schema, and two windows of one process both mutate `page.layout`/`workspace.json` through debounced atomic writes — last-write-wins clobbers. §5 claims to cover concurrency; it should name in-process coordination even though the multi-window UI is deferred. (`docs/03-domain/layout-and-window-model.md`, `docs/03-domain/invariants-and-integrity.md`.)
- **ADR-0013 vs ADR-0014 seam for non-git builds.** ADR-0013 is deliberately git-independent so zip packages carry the same identity as the repo; ADR-0014's verifier requires HEAD, a clean tree, and `sourceCommit`. A zip/non-git build — an explicitly supported distribution mode — can never be `releaseEvidence: true` and the verifier fail-closes. State that zip builds are intentionally never release-verified, or give the verifier a non-git provenance mode. (`docs/90-adrs/0013-version-and-build-number.md`, `docs/90-adrs/0014-publish-provenance-and-deploy-verification.md`.)
- **cwd-gone on restore.** Layout restore has an explicit fallback; the cwd-deleted case (worktree removed, repo moved — see Finding 3) is only implied. Mirror the explicit rule: cwd missing falls back to the workspace root with a `RestoreWarning`. (`docs/03-domain/state-and-storage.md`, `docs/02-ux/terminal-stage-spec.md`.)
- **Page-note file identity.** Page notes are keyed on disk by owner-id (`notes/pages/<page-id>.md`) while scratch notes use note-id (`notes/scratch/<note-id>.md`). Fine only if "one page-scoped note per page" is intended; that constraint is not written down, and the `Note` model otherwise allows multiple by `ownerId`. (`docs/03-domain/state-and-storage.md`, `docs/03-domain/domain-model.md`.)
- **Activity feed across log rolls.** `event-model.md` rotates at a size threshold and reads recent-event queries from the tail of the current file only. Immediately after a roll, the activity feed is near-empty until new events accumulate. Minor, but the feed is discontinuous across rolls by design. (`docs/04-architecture/event-model.md`.)

## Design tension (non-blocking)

The pack builds a robust forward-migration system (ADR-0008) and also pre-reserves a large amount of schema — `windows[]`, multi-monitor bounds, five `LibraryObject` kinds, `FileEntry`/`WorktreeRef`, floating Open-As targets — as "modeled now, UI deferred." These pull against each other: forward migration is precisely the mechanism that makes pre-modeling unnecessary, so the insurance is bought twice. Given the migration registry, biasing toward modeling less in v0 and letting migration absorb additions would keep the v0 surface smaller. This is a judgment call against the "load-bearing facts live in files" preference, not a defect — recorded so the trade is explicit.

## What to do next

Stop expanding the spec and start THQ-0200. The pack is past the point of diminishing returns on documentation, and the single highest-uncertainty fact in the design — the WebView2/xterm.js bridge — is still unmeasured. Findings 1, 2, 3, and 5 are the ones to resolve first because they shape the spike and the scaffold; 4 and 6 can ride along.
