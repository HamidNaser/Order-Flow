````prompt
---
argument-hint: 'date=2026-03-22 mode=both focus=release,api,infra audience=team'
description: 'Generate a daily standup update in short and/or detailed formats with Yesterday, Today, and Blockers.'
---

# Daily Standup Update Prompt

Generate a standup update for `${input:date|default:today}`.

## Inputs

- Date: `${input:date|default:today}`
- Mode: `${input:mode|default:both}` (`short`, `detailed`, or `both`)
- Focus: `${input:focus|default:all}`
- Audience: `${input:audience|default:team}`
- Evidence preference: `${input:evidence|default:auto}`

If inputs are missing, continue with sensible defaults. Ask clarifying questions only if blocked.

## Required Sections

Always include:

1. **Yesterday**
2. **Today**
3. **Blockers**

## Mode Rules

- If mode is `short`: output concise standup only.
- If mode is `detailed`: output detailed standup only.
- If mode is `both`: output short first, then detailed.

### Short format requirements

- `Yesterday`: 1–3 concise bullets
- `Today`: 1–3 concise bullets
- `Blockers`: short list or `No blockers`
- Keep it brief and spoken-standup friendly.

### Detailed format requirements

- `Yesterday`: completed work + validation evidence + impact
- `Today`: priority plan + dependencies + expected outcome
- `Blockers`: blocker + owner/dependency + next action (or `No blockers`)

## Evidence Priority

1. User notes/session transcript
2. Workspace changes (`search/changes`, changed files)
3. Build/test/problem signals
4. Terminal/task context

Do not fabricate work. Mark assumptions when evidence is incomplete.

## Example Invocation

`/daily-standup-update: date=2026-03-22 mode=both focus=event-audit,iam,ci audience=team`

````
