---
name: compiler-bugfix
description: Investigate Roslyn compiler issues - repro, root-cause, fix, and report.
---
# Compiler Triage and Fix Agent

This document is a **canned instruction set** for an AI assistant (or a human following the same steps) to investitage Roslyn **compiler** issues (C#/VB)
and produce a high-signal report plus optional commits.
Each deliverable in turn will be delegated to a sub-agent.

The user may invoke this agent with something like "@compiler-bugfix Apply your instructions to https://github.com/dotnet/roslyn/issues/NNNNN"

---

If critical information is missing, ask at most **1–3 short clarifying questions**. Otherwise proceed with best-effort defaults and note assumptions.

## Common mistakes to avoid

- **Do NOT combine the repro test and the fix into one step.** The repro commit must exist independently and pass before any production code changes.
- **Do NOT read entire test files as they can be large. Check file sizes before you read the entire file.** Instead, just APPEND the repro test and any new tests to the end of a new file in the CSharp15 project.
- **Do NOT implement a fix before explicitly presenting fix options to the user (step 5).** Even if the fix seems obvious, present options first.
- **Do NOT skip the final report (step 7).** It is a required deliverable, not optional.
- **Do NOT proceed to the next phase without completing all steps in the current phase.** See the phase gates below.

Here's a checklist of the phases and deliverables:
- [ ] Phase 1. Create an initial repro test that PASSES capturing current behavior (commit 1, sug-agent 1)
  - [ ] Deliverable A: "Repro confirmed" (commit 1, sub-agent 1) — OR one of the early-exit outcomes below:
    - [ ] Outcome A2: "Cannot reproduce" — stop here, report to user
    - [ ] Outcome A3: "By-design" — stop here, report to user with spec references
- [ ] Phase 2. Attempt repro reduction (sub-agent 1)
- [ ] Phase 3. Root cause analysis (sub-agent 2)
  - [ ] Deliverable B: Identify root cause (analysis sub-agent 2)
- [ ] Phase 4. Fix options (must be explicit, sub-agent 2)
  - [ ] Deliverable C: Fix options (analysis sub-agent 2)
- [ ] Phase 5. Implement recommended fix (sub-agent 3)
  - [ ] Deliverable D: Fix (later commits, sub-agent 3)
- [ ] Phase 6. Final report (for issue comment, main agent)
  - [ ] Deliverable E: Issue comment text (main agent)
---

### Phase 1. Create an initial repro test that PASSES capturing current behavior (commit 1, sug-agent 1)

Goal: create a test that demonstrates the current behavior *as observed today* and passes.

Recommended patterns:

- If the bug is a wrong diagnostic:
  - Assert the **current** diagnostic set exactly.
- If the bug is "no diagnostic but should be diagnostic" (or vice versa):
  - Assert the **current** behavior (even if incorrect).
- If the bug is wrong constant folding/emit:
  - Assert the **current** emitted IL or constant value.
- If the bug is a crash:
  - If it currently crashes, the "capturing current behavior" test may need to be:
    - a passing test that asserts the crash occurs is generally undesirable
    - prefer instead to treat crashes as "expected to be fixed": create a test that demonstrates the crash and mark as skipped only if policy allows
  - If policy disallows skip, you may need to treat crash issues as exceptions and make commit 1 a failing test.

**IMPORTANT**: Add a comment in the test explaining that commit 1 intentionally captures current behavior and will be flipped when fixing.

After adding the test:
- run only the smallest test scope to confirm stability
- commit as **Commit 1**:
  - `"Add repro for #<issueNumber> (captures current behavior)"`

**Deliverable A**: "Repro confirmed" (commit 1, sub-agent 1)

- Add a **passing test** that captures the current (possibly incorrect) behavior.
- The test must be stable and self-contained.
- The commit message should clearly indicate it captures current behavior.
- Comments must indicate actual vs. expected behavior clearly.

**GATE: Do NOT proceed to step 3 until you have:**
- Written a test asserting current (buggy) behavior
- Built and run the test to confirm it passes
- Committed with the prescribed message

#### Phase 1 alternate outcome: Cannot reproduce

If the repro test shows that the compiler already behaves **correctly** (i.e., the reported bug does not manifest):

1. Do **not** commit a misleading test.
2. **Stop immediately** and present the following message to the user:

> **Cannot reproduce.** The compiler appears to behave correctly for the reported scenario.
> Tested with: `<compiler / SDK version>`
> Observed behavior: `<what actually happened>`
> If the issue still occurs in your environment, please share a self-contained repro and the exact toolchain version.

3. **Do not proceed to Phase 2.**

#### Phase 1 alternate outcome: Behavior is by-design

If the reported behavior appears intentional, consult the language specifications to confirm:

- Search the [csharplang](https://github.com/dotnet/csharplang) repo (proposals, meeting notes, and the `proposals/` folder) for discussions around the relevant feature.
- Search the [csharpstandard](https://github.com/dotnet/csharpstandard) repo for normative spec text that governs the behavior.

If the spec or design discussion confirms the behavior is intentional:

1. Do **not** commit a repro test.
2. **Stop immediately** and present the following message to the user:

> **By-design.** The observed behavior is consistent with the C# specification / language design.
> Relevant spec section: `<section title and link>`
> Relevant csharplang discussion: `<link if found>`
> Summary: `<one-paragraph explanation of why the behavior is correct>`
> If you believe the spec or design should be changed, please open a discussion on the [csharplang](https://github.com/dotnet/csharplang) repository.

3. **Do not proceed to Phase 2.**

---
### Phase 2. Attempt repro reduction (sub-agent 1)

Iteratively simplify the repro snippet while keeping the captured behavior identical.
Each reduction step must be validated by re-running the single test.
After reducing the repro, commit as **Commit 2**:
  - `"Add simplified repro for #<issueNumber> (captures current behavior)"`

**GATE: Do NOT proceed to phase 3 until:**
- The repro test (ideally simplified one) fix is committed

### Phase 3. Root cause analysis (sub-agent 2)

Figure out the root cause and summarize it. Include:
- The specific file(s) and method(s) where the bug originates.
- A brief explanation of *why* the current code produces incorrect behavior.

**Deliverable B**: Identify root cause (analysis sub-agent 2)

- Identify the root cause and explain what is happening.

### Phase 4. Fix options (must be explicit, sub-agent 2)

Present options using this structure:

| Option | Description | Risk | Complexity |
|--------|-------------|------|------------|
| A | ... | ... | ... |
| B | ... | ... | ... |

**Recommended: Option X because ...**

**Deliverable C**: Fix options (analysis sub-agent 2)

- Provide some options for fixing the root cause.
- Choose a recommended option and explain why.


### 5. Implement fix (sub-agent 3)

- Apply minimal, targeted production change(s).
- Update the original repro test to assert **correct** behavior.
- Add any additional tests needed for coverage.
- Build and run all affected tests to confirm the fix.

Commit structure:
- **Commit N**: `"Fix <short description> (#<issueNumber>)"`
- **Commit N+1** (optional): `"[test] Add additional coverage for <area> (#<issueNumber>)"`

**Deliverable D**: Fix (later commits, sug-agent 3)

- Implement the fix.
- Update the initial test to assert **correct** behavior (it may become failing before the fix, then pass after).
- Add any extra tests needed for coverage.

**GATE: Do NOT proceed to step 7 until:**
- The production fix is committed
- All repro tests pass with corrected assertions
- No unrelated test regressions

### Phase 6. Final report (for issue comment, main agent)

This step is **required**, not optional.

Produce a markdown report containing:

1. **Repro**
   - minimized code snippet
   - exact observed behavior vs desired behavior

2. **Test**
   - test name and location

3. **Root cause**
   - brief explanation with references to relevant code

4. **Fix options**
   - bullets with tradeoffs
   - recommended choice

5. **Fix summary**
   - what changed
   - why it's safe

6. **Commits**
   - list commits (commit 1 repro, commit 2 simplify repro, commit 3 fix, etc.) and branch name if created


**Deliverable E**: Issue comment text (main agent)

Provide a ready-to-post comment with:

- minimized repro
- branch/commit references (if applicable)
- root cause summary
- fix options + chosen fix
- any follow-ups / risks