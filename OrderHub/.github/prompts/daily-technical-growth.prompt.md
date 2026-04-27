````prompt
---
argument-hint: 'date=2026-03-22 focus=aws,iam,github-actions output=performance-highlights confidence=strict'
description: 'Capture new technical expertise and applied experience gained today, with performance-review-ready highlights and career profile bullets.'
---

# Daily Technical Growth Prompt

Capture technical growth for `${input:date|default:today}` from available session/workspace evidence.

## Inputs

- Date: `${input:date|default:today}`
- Focus topics: `${input:focus|default:all}`
- Output style: `${input:output|default:performance-highlights}`
- Confidence mode: `${input:confidence|default:strict}`
- Evidence preference: `${input:evidence|default:auto}`

If inputs are missing, continue with sensible defaults. Ask clarifying questions only if blocked.

## Mandatory Output Sections (exact order)

1. **New Technical Expertise Gained Today**
2. **Applied Technical Experience Today**
3. **Performance Review Highlights**
4. **Career Profile Bullets**
5. **Evidence and Confidence**

## Quality Rules

- Distinguish `Learned` vs `Applied` vs `Led`
- Use evidence-based statements only
- Do not inflate impact without proof
- Prefer concrete technology/action/outcome wording
- Mark assumptions explicitly when evidence is partial

## Evidence Priority

1. User notes/session transcript
2. Workspace changes (`search/changes`, diffs, changed files)
3. Build/test/problem/terminal/task evidence
4. PR/workflow context when available

## Output Expectations

- Emphasize what is newly gained today (not generic capabilities)
- Include measurable or observable outcomes when available
- Keep bullets concise and high signal
- Include confidence per major claim

## Example Invocation

`/daily-technical-growth: date=2026-03-22 focus=iam,oidc,github-actions output=performance-highlights confidence=strict`

````
