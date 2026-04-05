---
name: ralph-checklist
description: Create or update a ralph checklist for iterative engineering work. Use when setting up a test-fix checklist, baseline-update checklist, bug-fix checklist, or similar workflow that should prepare a tracker markdown file from a template, then run a file-based C# launcher that invokes copilot against that tracker file.
---

# Overview

Use this skill when the user wants a reusable tracker document that guides iterative work across many small items, especially when each item should be processed one at a time from a checklist.

This skill uses a bundled file-based C# launcher to process a tracker checklist. The tracker markdown file carries the instructions, and the launcher invokes `copilot` with a prompt that points at that file.

There are four steps:
1. identify the task scope and the repeated per-item instructions the user wants.
2. create the tracker markdown file from the bundled template `templates/ralph-checklist.local.md.template`, replacing the placeholders with task-specific details.
3. run the bundled C# checklist runner `tools/ralph-checklist.cs` with the tracker file path, which will repeatedly invoke Copilot with a prompt pointing at the tracker file until the tracker timestamp stops changing.

# Scope discovery

Before creating the tracker, determine the checklist items using the narrowest scope that answers the user's request.  
For instance, if we're trying to fix failing tests in a test class, just run that test class and create a checklist item for each failing test. No need to run other tests or read the test code.

# Creating Tracker File

A tracker created with this skill should use one markdown file built from the bundled template.

That tracker file contains exactly these three parts:

1. General instructions that tell the agent to pick the first unchecked item, work on it, create a commit, and update the checklist in the same file.
2. Per-item instructions describing what should be done for each checklist item.
3. A markdown checklist where each work item starts as `- [ ]`.

Use the bundled template and fill in its placeholders instead of improvising the structure from scratch.

## Template replacements

When preparing the tracker file, replace these values at minimum:

- `<REPLACE WITH UTC TIMESTAMP>`
- `<PATH TO THIS FILE>` with the absolute path to the tracker file being created
- `<PER-ITEM INSTRUCTIONS>` with the user's requested repeated instructions for each item
- `<CHECKLIST OF ITEMS>` with concrete checklist entries, each starting as `- [ ]`

## Per-item instructions

- The user prompt should let you determine the per-item instructions. If not, then ask for clarification.

## Checklist rules

- The user prompt should let you determine the checklist items. For example, if the user wants to fix failing tests, look at the failing tests (by running the smallest set that the user mentioned) and create a checklist item for each one.
- Every work item must start as `- [ ] <item>`.
- Once the worker has run on an item in the loop, mark it as `- [x] <item>` whether that attempt succeeded or not. This keeps the loop moving forward through the checklist instead of getting stuck retrying the same item.
- If the attempt did not fully succeed, keep the item checked and add a short plain-markdown note immediately below it describing the failure or blocker.
- Keep completed items in the file for history. Do not delete them unless the user explicitly asks.
- Do not leave an item unchecked just because it failed or was blocked after the worker already attempted it.
- Keep checklist items flat and concrete. Prefer one failing test, bug, file, or tightly-related cluster per item.
- If the checklist tracks a list of C# members (failing tests, tests to be ported, ...), each item should be the fully-qualified member name.

## File placement

- For personal local usage, prefer `.claude/<name>.local.md` for the tracker file.
- If the user wants a team-shared version, place the tracker file in a shared repo location they request.

## Tracker guidance

The tracker file should usually include:

- Frontmatter with `started_at` and `loop_status`
- General instructions
- Per-item instructions
- A checklist section where every item begins as `- [ ]`

## Bundled templates

Use the template in this skill as the default starting point:

- `templates/ralph-checklist.local.md.template`

Copy it and fill in the task-specific details instead of improvising the structure from scratch.

## Bundled automation

Use the bundled file-based C# program at `tools/ralph-checklist.cs` to launch Copilot against the tracker file in non-interactive mode.

- `dotnet run --file .github/skills/ralph-checklist/tools/ralph-checklist.cs -- <tracker.md>` repeatedly launches `copilot -p` with the prompt `Follow instructions in <absolute-path-to-tracker.md>`.
- The runner stops when the tracker frontmatter changes `loop_status` away from `active` or when there is no progress (the tracker's last-write timestamp does not change after a Copilot invocation).
- Extra arguments after the document path are forwarded to `copilot` unchanged.

Use this tool as the entrypoint after the tracker file is prepared. The tracker markdown file carries the instructions and checklist; the runner simply points Copilot at that file.
