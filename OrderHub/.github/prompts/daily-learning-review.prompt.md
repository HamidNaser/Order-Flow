````prompt
---
argument-hint: 'date=2026-03-20 focus=aws,iam,github-actions incident=latest-failed-workflow output=incident-knowledge-pack'
description: 'Generate an incident-first knowledge document with two mandatory sections: Domain Knowledge and Technical Knowledge (A→Z explanations with root cause and fix mechanics).' 
---

# Daily Learning Review Prompt

You are running an end-of-day knowledge capture for ${input:date|default:today}. The goal is to produce complete technical understanding from real incidents, not a generic summary.

## Inputs

- Date: `${input:date|default:today}`
- Focus topics: `${input:focus|default:all}`
- Incident focus: `${input:incident|default:most-impactful-failures-today}`
- Evidence preference: `${input:evidence|default:auto}`
- Output goal: `${input:output|default:incident-knowledge-pack}`

If inputs are missing, continue with sensible defaults. Ask a clarifying question only if blocked.

## Mandatory output shape

Always output in this exact order:

1. **Domain Knowledge**
2. **Technical Knowledge**

Optional sections can follow after these two.

## Primary operating mode

- **Incident-first by default**: prioritize concrete failures that happened (especially AWS/GitHub Actions/IAM/runtime infra) over broad topic summaries.
- **Depth over breadth**: explain fewer incidents deeply rather than many incidents shallowly.
- If the user gives one specific incident (for example: access denied in GitHub Action), produce a full deep-dive for that incident first.

## Workflow

### 1) Scope and evidence availability

1. Confirm scope is one workday.
2. Check for session transcript/export.
3. If transcript is unavailable, continue using workspace/terminal/workflow evidence and mark confidence per incident.

### 2) Collect evidence (without duplication)

Gather sources in this order:

1. Session transcript / pasted notes
2. Workflow/run evidence (failed GitHub Action logs, job names, error lines, remediation runs)
3. Workspace changes (`search/changes`, git diff/log)
4. Build and test signals (`read/problems`, `execute/testFailure`, build output)
5. Terminal signals (`read/terminalLastCommand`, terminal output)
6. PR/Rally context (comments, reviews, notes)

Do not repeatedly query the same source unless additional context is needed.

### 3) Build Domain Knowledge section

Create a connected domain explanation that includes:

- Business objective of today's work
- Domain entities and workflows involved
- How incidents impacted domain outcomes
- Before/after domain state and what changed
- A concise "connect-the-dots" mental model

When incidents are mostly technical infrastructure, keep domain concise and still explain why the incident mattered to delivery, reliability, or release flow.

Write this section in clear, plain language that is easy for non-native English readers.

### 4) Build Technical Knowledge section (A→Z per incident)

For each meaningful incident, include a complete start-to-finish explanation:

- **A. Problem and objective**
- **B. Symptoms and exact error signals**
- **C. Surface cause**
- **D. Root cause**
- **E. Architecture/component flow**
- **F. Technology fundamentals involved**
- **G. Why initial attempts failed**
- **H. Exact fix implemented**
- **I. Why the fix works**
- **J. Verification and evidence**
- **K. Prevention pattern**
- **Z. Big-picture takeaway**

For each incident, include a short mini-glossary for terms that may be unclear.

Also include this mandatory subsection per incident:

- **Mechanics (What changed in the system)**
	- Before state (what permission/configuration/path was missing or incorrect)
	- Change applied (exact action, such as running IAM setup workflow)
	- After state (what permission/configuration/path now exists)
	- Why this changed the outcome

For AWS/IAM/GitHub Actions incidents specifically, explain:

- Which principal needed access (role/user/workflow identity)
- Which resource was accessed
- Which action was denied
- Which policy/trust condition blocked it
- Which setup/remediation action introduced the missing permission/trust

### 5) Knowledge growth synthesis

After the technical section, add:

- Knowledge gaps that were closed today
- Remaining uncertainties (only when evidence is incomplete)
- Confidence level per gap (High/Medium/Low)

Do not output homework-style research tasks unless user explicitly requests a study plan.

### 6) Optional persistence

Offer to save the report to:

- `notes/daily-learning/${input:date|default:today}.md`

Do not create or update files without explicit user confirmation.

## Output quality rules

- Distinguish facts from inference
- Avoid generic "go research this" recommendations as primary output
- Provide complete direct explanations from available evidence
- If evidence is partial, state assumptions clearly and keep explanation useful
- Include concrete identifiers when available (workflow name, job, resource, role, denied action)
- Prefer practical language: "what failed", "why", "what changed", "why it now works"

## Example invocation

`/daily-learning-review: date=2026-03-20 focus=aws,iam,github-actions incident=access-denied-codebuild-runner evidence=auto output=incident-knowledge-pack`

Remember: this prompt must produce complete, connected understanding for career growth, not a shallow recap.

````