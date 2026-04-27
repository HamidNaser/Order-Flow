---
argument-hint: 'story=US123456 task=TA654321'
description: Pickup an existing Rally Task (TA…) and drive it from Not Started to Completed with disciplined workflow (retrieve, activate, implement, validate, wrap-up) ensuring quality gates and traceability.
---

# Task Pickup & Rally Execution Prompt

## Overview


Use this when: you have a specific Rally Task ID and need to begin (or continue) implementation work tied to an existing Story. Supply the IDs when invoking:

Inputs (provide after a colon when running the prompt, e.g. `/task-pickup: story=US123456 task=TA654321`):

- Story ID: ${input:story}
- Task ID: ${input:task}

These variables will be substituted wherever referenced below.

## Core Process Flow

```text
Task Identification → Context Retrieval → Task Activation → Implementation & Validation → Task Wrap-up
```

## Preconditions

- You have (or are given) a Rally Task ID (starts with TA) and its parent Story ID (starts with US)
- Repository is in a clean working state (no unrelated uncommitted changes)
- Necessary credentials/secrets configured locally (if implementation requires runtime validation)

If any are missing, pause and request clarification instead of guessing.

## Tooling Summary

- Retrieve Task / Story context: #tool:cai/getRallyItem (supply `objectId` or `FormattedID`, add `includeChildren=true` / `includeDiscussions=true` as needed)
- Broad task queries or filters: #tool:cai/listRallyItems (set `listRallyObj: "Task"` and use `customQuery` when necessary)
- Update Task state / progress: #tool:cai/updateRallyTask

## Step-by-Step Instructions

### 1. Input & Validation

1. Confirm both IDs (Story: ${input:story}, Task: ${input:task}). If only Task provided, request Story if needed to disambiguate project.
2. Record them for reference in commit messages and logs.

### 2. Retrieve Task

1. Call #tool:cai/getRallyItem with `{ objectId: "${input:task}", includeChildren: true }` to pull the task details (comments, attachments, discussions as needed).
2. If Rally reports an ambiguous project/workspace:
   - Call #tool:cai/getRallyItem for `${input:story}` with `includeChildren=true` (and `includeDiscussions=true` when context is required) to capture the Project reference.
   - Retry #tool:cai/getRallyItem for `${input:task}` while supplying the retrieved `project` identifier.
   - When you need to filter by state/owner or search broadly, use #tool:cai/listRallyItems with `listRallyObj: "Task"`, an explicit `customQuery`, and the appropriate `project` scope.
3. If the task still cannot be located: stop and request human assistance (do NOT fabricate task details).

### 3. Activate Task (If Needed)

1. If Task state is not In-Progress, call #tool:cai/updateRallyTask for ${input:task} to set state = In-Progress.
2. (Optional) If the process tracks hours, initialize or adjust Estimate / ToDo fields only if instructed—avoid speculative changes.

### 4. Clarify Scope & Acceptance Criteria

1. Read Task description and notes carefully. Extract:
   - Objective (concise)
   - Required code areas / components
   - Acceptance criteria / success signals
   - Explicit exclusions (what NOT to change)
2. If acceptance criteria are ambiguous, call #tool:cai/getRallyItem for `${input:story}` with `includeChildren=true` / `includeDiscussions=true` and derive additional context (business rules, domain constraints, feature flag requirements, dependencies).
3. If still ambiguous: summarize uncertainties and pause for human direction (avoid silent assumptions that risk rework).

### 5. Create an Implementation Micro-Plan

Produce a short checklist (3–10 bullets) covering:

1. Files to add or modify
2. Public API / contract impacts (if any)
3. Data model / schema impacts (if any)
4. Test additions / updates (unit + integration when applicable)
5. Observability updates (logging, metrics, feature flags) if relevant
6. Edge cases to verify

Get human confirmation only if scope risk is high; otherwise proceed.

### 6. Execute Incrementally

1. Implement one micro-step at a time; keep changes tightly scoped.
2. After each code edit:
   - Run build / compile
   - Run focused unit tests (and integration tests if impacted)
   - Check for new errors (use `get_errors` if environment supports)
3. Maintain a single active in-progress todo; close it before starting the next.
4. Avoid unrelated refactors unless the Task explicitly calls for cleanup (log potential improvements separately).

### 7. Validation & Quality Gates

Perform at minimum:

- Build succeeds with no warnings of new origin (if warnings appear, evaluate necessity)
- All existing tests pass
- New / updated tests cover success + at least one edge case
- No regressions in adjacent functionality you touched
- Feature flag / configuration pathways honored (if applicable)
- Logging includes correlation IDs and contextual fields per project conventions

### 8. Task Wrap-Up

1. If Task outcome matches acceptance criteria, call #tool:cai/updateRallyTask for ${input:task} to:
   - Set state = Completed (only if truly done)
   - (Optional) Update ToDo = 0 and Actuals if that workflow is used and data is known
2. Prepare commit message template:

   ```text
   feat(Task ${input:task} / Story ${input:story}): <concise implementation summary>

   - What changed:
     * …
   - Acceptance criteria satisfied:
     * …
   - Tests:
     * Added/Updated <files>
   - Notes:
   * Reference: ${input:task} (${input:story})
   ```

3. Ensure no stray debug artifacts remain.
4. Surface any follow-up / technical debt as separate clearly labeled notes (do not silently extend scope).

### 9. Blocker Handling

If blocked (missing config, ambiguous domain rule, external dependency failure):

- Document: Blocker description, attempted steps, suspected root cause, next recommendation
- Stop further changes until clarified.

## Definition of Done (DoD)

- Task state updated to Completed (or In-Progress with explicit blocker documented)
- All acceptance criteria demonstrably met
- Only relevant code modified (no collateral churn)
- Tests added/updated & all pass
- Build clean; no new critical warnings
- Commit message references both TA and US IDs
- Observability/logging conforms to existing patterns

## Quality Checklist (Quick Scan)

- [ ] Retrieved and verified correct Task & Story
- [ ] Task moved to In-Progress before coding
- [ ] Scope & acceptance criteria explicitly summarized
- [ ] Micro-plan executed incrementally
- [ ] Tests cover happy path + edge
- [ ] No unrelated refactors
- [ ] Logging/correlation intact
- [ ] Commit message structured & references IDs
- [ ] Task state updated appropriately

## Communication Guidelines

Use concise, high-signal updates:

- Start: "Picked up ${input:task} (${input:story}) – scope: `<summary>`"
- Midpoint (if long-running): progress + any emerging risks
- Completion: validation summary + DoD confirmation
- Blocked: blocker summary + required input

## Common Pitfalls & Avoidance

| Pitfall | Avoidance |
|---------|-----------|
| Over-expanding scope | Explicitly list exclusions; defer enhancements |
| Skipping acceptance clarification | Always extract criteria textually before coding |
| Large unreviewable commit | Commit in focused, logical chunks if policy allows |
| Missing tests | Add tests early (before or alongside implementation) |
| Silent feature flag assumption | Verify flag existence / defaults in configuration |

## Adaptation Notes

For very small trivial tasks (e.g., typo fix) you may streamline: still retrieve & activate the Task, implement, validate, update state, and commit referencing TA/US IDs.

---

**Usage**: Apply this prompt whenever you begin work on a specific Rally Task to ensure disciplined, traceable, and high-quality execution.
