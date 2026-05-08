---
name: repro-and-fix
description: Use when the user asks to investigate, reproduce, or fix a GitHub issue (e.g. "let's work on issue #1234", a github.com/Live-Charts/LiveCharts2/issues/N URL, or "reproduce and propose a fix"). Implements the LiveCharts workflow end-to-end — worktree, in-sample repro view, auto-launched sample, screenshot/log verification, diagnosis, fix, separate regression-test commit, and PR — with minimal user interaction.
---

# repro-and-fix

A user pointed you at a GitHub issue and asked you to reproduce + fix it. Run
the workflow below. The whole point is that the dev loop is *self-contained*:
you build the repro, launch the sample at that repro automatically, verify the
bug yourself (screenshot or logs), fix it, verify the fix, and commit. Don't
ask the user for visual confirmations you can get yourself.

## When this skill applies

- "Let's work on https://github.com/Live-Charts/LiveCharts2/issues/N"
- "Reproduce issue #N and propose a fix"
- "Investigate this bug report: <URL>"
- A user paste of an issue URL with no other instruction

If the user is asking about *theory* ("why does the gauge use a doughnut
geometry?") or a *non-issue* task (refactor, doc update, perf), this skill is
not the right tool — fall back to general workflow.

## Project conventions you must follow

These are durable rules from `~/.claude/projects/.../memory/MEMORY.md`. Re-read
the relevant memory file if uncertain — these are not optional:

- **Worktree per issue.** Create `../LiveCharts2-fix-<issue>-<slug>` first
  (`feedback_worktree_per_issue`). Don't work in the primary tree.
- **One concept per commit.** Fix and regression test go in *separate* commits.
  The regression-test commit must fail when the fix is reverted — verify this
  before pushing (`feedback_commit_separation`).
- **Snapshot tests for render-path changes.** If your fix touches paint, layout,
  z-index, or draw order, run `tests/SnapshotTests/` before claiming done — unit
  tests have missed catastrophic regressions
  (`feedback_run_snapshots_for_render_changes`).
- **Update docs when public API changes** in the same change set as a separate
  commit (`feedback_update_docs_for_api`).
- Check `Directory.Build.props` for `<LiveChartsVersion>` and use that value in
  user-facing text (`project_current_version`).

## Workflow

### 1. Read the issue and decide if it still reproduces

```bash
gh issue view <N> --repo Live-Charts/LiveCharts2 --json title,body,labels,state,comments
```

Pay attention to **comments**, not just the body. Issues commonly evolve:

- The original symptom may already be fixed in main (commenter says "newer
  releases fixed this") and the *current* bug is a downstream observation.
  This is exactly what happened with #2008 — the `GaugeValue` TemplateBinding
  bug in the title was already resolved; the live bug was a `CornerRadius=0`
  override in the comments.
- Multiple users may report different things in the same thread. Identify the
  most recent reporter's actual symptom.

If you suspect the title bug is already fixed: build the original repro
verbatim and verify *first*, before doing diagnosis. Cite the commenter who
said it's fixed in your PR description.

### 2. Set up the worktree

```bash
git worktree add ../LiveCharts2-fix-<issue>-<short-slug> -b fix/issue-<N>-<short-slug>
```

All edits, builds, and runs happen in the worktree. The primary tree stays
clean for other work.

### 3. Build the repro view (XAML platforms)

The repro convention is a **sample view inside the platform sample app**:

```
samples/AvaloniaSample/VisualTest/Issue<N>Repro/View.axaml
samples/AvaloniaSample/VisualTest/Issue<N>Repro/View.axaml.cs
```

Match the issue's repro setup as closely as you can — same `ControlTemplate`
shape, same property values, same paints. *Don't simplify* until you've
confirmed the bug fires; over-minimization can accidentally avoid the trigger.

Existing repros to use as templates:

- `samples/AvaloniaSample/VisualTest/Issue1986Repro/` — TabControl + ScrollViewer
- `samples/AvaloniaSample/VisualTest/Issue1417Repro/` — GeoMap reattach
- `samples/AvaloniaSample/VisualTest/Issue2008Repro/` — Gauge ControlTemplate

The View's code-behind should expose helpers that a Factos test can call
(e.g. `FindTemplatedGaugeSeries()`). This is how the regression test hooks in
without going through the visual tree.

Add the new path to the sample selector:

```csharp
// samples/ViewModelsSamples/Index.cs
"VisualTest/Issue<N>Repro",
```

### 4. Auto-launch the sample at your repro

The platform sample apps read `LVC_SAMPLE` from the environment and load that
sample on startup. Set it before launching:

```bash
LVC_SAMPLE=VisualTest/Issue<N>Repro \
  ./samples/AvaloniaSample/Platforms/AvaloniaSample.Desktop/bin/Debug/net10.0/AvaloniaSample.Desktop.exe
```

Or for cross-platform:

```bash
# WPF
$env:LVC_SAMPLE = "VisualTest/Issue<N>Repro"
./samples/WPFSample/bin/Debug/net10.0-windows/WPFSample.exe

# WinUI / MAUI / Uno: same pattern, env var set before launch
```

Build first if needed: `dotnet build samples/AvaloniaSample/Platforms/AvaloniaSample.Desktop -c Debug`.

### 5. Verify the bug yourself

Two paths — pick based on the bug type:

**A. Screenshot for visual bugs** (color, shape, position, rendering artifacts):

```powershell
# Run the app in background first (ensure it's the only AvaloniaSample.Desktop instance)
.\.claude\scripts\capture-window.ps1 `
    -ProcessName AvaloniaSample.Desktop `
    -OutPath .\.claude\screenshots\<issue>-before.png `
    -WaitSeconds 15
```

Then `Read` the PNG. You're multimodal — describe what you see, compare to a
control sample (often `Pies/Gauge1` or `General/FirstChart` for "what should
this look like"). If the bug is subtle (a few pixels), make the repro larger
(`Width=380`, `Height=380`) so it's clearly visible.

**B. Console logs for state/dataflow bugs** (binding doesn't fire, value
doesn't propagate, event order is wrong):

Add temporary `Console.WriteLine` calls along the suspected path:

```csharp
Console.WriteLine($"[issue<N>] OnPropertyChanged: {change.Property.Name} old={change.OldValue} new={change.NewValue}");
```

The platform sample writes to stdout when launched from the terminal. Run via
the bash tool with `run_in_background=true`, wait for the user to navigate (or
your env var to land you on the repro), kill the app, then `grep` the
captured output file. **Remove the diagnostics before committing.**

If neither approach decisively confirms the bug, escalate to the user:

> "I can see X, but I'm not certain whether that matches the symptom you're
> reporting. Can you confirm <specific question>?"

Don't loop screenshot questions on the user — capture once, analyze, then
proceed.

### 6. Diagnose the root cause

Trace from symptom backward through the rendering/data pipeline. Cross-check
against `MEMORY.md` — many bugs in this repo have related ancestors
(z-index issues, theme overrides, motion-property defaults, source-gen
behavior). If you find a matching memory note, use its diagnosis as a starting
point but verify against current code; memories go stale.

Common gotchas in this codebase:

- **Theme overrides user-set values** when the user's value equals the DP
  default — Avalonia skips `OnPropertyChanged` and `_userSets` never gets
  populated. `XamlGaugeSeries.EndInit` syncs IsSet DPs as a remediation; if
  you see this pattern in another wrapper, the same fix shape applies.
- **Series controls aren't in the visual tree.** `OnInitialized` and
  `TemplatedParent` may not fire/be set the way you'd expect. Use `EndInit`
  instead.
- **Theme rules run from the chart engine measure pass**, not at construction.
  `_isInternalSet` + `_userSets` is the existing precedence mechanism — don't
  reinvent it.
- **Motion properties animate from a stale "from" value.** A value flip from
  N→0 may render N for one frame.

### 7. Apply the fix

Smallest possible change. Don't refactor surrounding code. Don't add
comments unless the WHY is non-obvious — but if the bug is a subtle
interaction (Avalonia behavior, theme precedence, source-gen quirk), a comment
explaining *why this fix is necessary* is warranted because future readers
won't reconstruct it from memory.

### 8. Verify the fix

Re-run step 5 with the fix in place. Capture an "after" screenshot at the
same path:

```powershell
.\.claude\scripts\capture-window.ps1 `
    -ProcessName AvaloniaSample.Desktop `
    -OutPath .\.claude\screenshots\<issue>-after.png
```

Read both images and confirm the symptom is gone. Don't claim the fix works
without comparing.

### 9. Write the regression test

Test choice depends on the bug nature:

- **Programmatic state assertion** (the bug is in state propagation, not
  pixels): Factos test in `tests/SharedUITests/`. Navigate to the repro,
  query a helper method on the View that exposes the state under test,
  assert. Gate by `#if AVALONIA_UI_TESTING` if Avalonia-specific (most
  XAML-platform-quirk bugs are).
- **Pixel-perfect rendering**: snapshot test in `tests/SnapshotTests/`.
- **Pure C# logic** (chart engine, motion, math): MSTest in
  `tests/CoreTests/`.

**Verify the test fails without the fix.** Comment out your fix, rebuild,
re-run the test. Expect it to fail with a meaningful error. Restore the fix
before committing.

To run a single Factos UI test:

```bash
# In tests/UITests/Program.cs, set: var appToRun = "avalonia-desktop";
dotnet build tests/UITests/UITests.csproj -c Debug
cd tests/UITests/bin/Debug/net10.0 && dotnet UITests.dll
# Revert the appToRun change before committing — it's a local dev-loop tweak.
```

### 10. Commit and PR

Two commits, in this order:

1. `fix(<scope>): <one-line summary> (#<N>)` — only the fix.
2. `test(<scope>): regression for <symptom> (#<N>)` — only the test +
   repro view + index entry.

Commit message style follows recent history (`git log --oneline -10`).
Body: imperative, mention the root-cause mechanism, why the test catches it.
End with the standard `Co-Authored-By` line.

Push and open the PR with `gh pr create`. The PR body should include:

- **Summary** bullet list (what changed, what it closes)
- **Why** section (root cause walkthrough — the most valuable part)
- **Test plan** checklist with what you actually verified locally

### 11. Update memory

After the PR is open, write a `project_issue_<N>.md` memory and add a one-line
entry to `MEMORY.md`. Capture: **what was non-obvious** about the bug, what
diagnosis steps led to the fix, and what conventions/files the next person
needs. Don't re-document the code — just the insights.

## Anti-patterns to avoid

- **Asking the user "is the bug visible now?" repeatedly.** Capture a
  screenshot or read logs. If you can't decide from one capture + one
  question, you haven't framed the question precisely enough.
- **Simplifying the repro before confirming.** A "minimal" repro that doesn't
  trigger the bug is worse than a verbose one that does.
- **Skipping the test-fails-without-fix verification.** A passing test that
  also passes when reverted proves nothing.
- **Mixing fix + test + cleanup in one commit.** Project policy is strict
  here — every fix needs a separately revertible test commit.
- **Force-pushing for "cleanliness".** Never. Stack new commits.
- **Trusting memory over code.** Memory files name specific functions and
  files; verify they still exist before recommending — projects refactor.

## Tooling reference

- Issue triage: `gh issue view <N> --repo Live-Charts/LiveCharts2`
- Screenshot: `.claude/scripts/capture-window.ps1` (Windows; `-ProcessName`
  or `-WindowTitle`, `-OutPath`)
- Auto-launch sample: `LVC_SAMPLE=<path>` env var, supported by Avalonia,
  WPF, WinUI, MAUI, Uno
- Factos UI tests: `tests/UITests/`, set `appToRun` in `Program.cs` for
  local runs
- Build per-platform: `LiveCharts.<Platform>.slnx` solutions
