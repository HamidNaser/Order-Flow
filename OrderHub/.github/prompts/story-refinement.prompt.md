---
argument-hint: 'story=US123456'
description: 'Review a Rally story and ensure every section is fully refined using problem context and the current codebase'
---

# Story Refinement Prompt

You are the tech lead refining Rally story ${input:story} so another developer can confidently pick it up. Follow the sequence below, asking clarifying questions whenever information is missing or contradictory.

1. **Pull Rally context**
   - Call #tool:cai/getRallyItem with `{ objectId: "${input:story}", includeChildren: true, includeDiscussions: true }` to load the full story context (tasks, discussions, notes).
   - **ALWAYS follow GitHub Enterprise links**: When you encounter any links in the Rally item that start with `https://ghe.order.com`, use the available GitHub tools to fetch and review that content. This includes links to PRs, issues, commits, or documentation. Never skip these links.
   - **Fetch external context when relevant**: If you find links to other external resources (not GitHub Enterprise), ask the user if they'd like you to fetch that content for context gathering using #tool:cai/webFetch before proceeding.
   - Summarize the current state of each description section, noting which ones are empty, stale, or conflicting.

2. **Cross-check the codebase**
   - Use the search tools available to you to look over the #tool:search/codebase and verify whether the story's described behavior aligns with actual code.
   - Identify concrete references (project names, classes, interfaces, methods, configuration, API endpoints, queue handlers, tests, etc.) that relate to the problem statement. Avoid pasting code snippets; rely on descriptive references only.

3. **Refine every story section except the Problem Statement**
   - For each section (e.g., The Work, What Should We Test, Acceptance Criteria/Testing, Non-functional requirements, Dependencies, Risks, Deployment/Release Plan), confirm it is complete, specific, and technically actionable.
   - Add or update details so the story reflects the full scope of required changes, including edge cases, data considerations, feature flags (via LaunchDarkly when applicable), observability, and testing.
   - Flag any inaccuracies, omissions, or misalignments between Rally context and the current codebase.

4. **Assess readiness and next actions**
   - Determine if the story is ready for development. If gaps remain, explicitly list the questions or prerequisites to resolve.
   - Suggest granular follow-up tasks or spike work when needed.
   - Recommend relevant tests (unit, integration, regression) and logging/monitoring updates without showing code.

5. **Format your output as specified below**
   - Use the structured format outlined in the "Expected output format" section.
   - Ensure clarity and conciseness, focusing on actionable insights.

6. **Address any feedback or clarifications**
   - If the user provides additional context or questions, incorporate that into your analysis and update the output accordingly.
   - Ultimately get to the point of being ready to update the story with all questions/feedback resolved.

7. **Update the Rally story**
   - Once all clarifications are addressed, call #tool:cai/updateRallyStory to apply your refinements.

8. **Check for tasks and recommend next steps**
   - After updating the story, verify if any tasks have been created for this story (review the `includeChildren` data from step 1).
   - If **no tasks exist**, explicitly recommend to the user: "This story has no tasks yet. Would you like me to help break it down into implementation tasks? You can run the `/task-refinement` prompt with `story=${input:story}` to create sequenced, implementation-ready tasks."
   - If tasks exist, briefly summarize their current state and readiness.

## Expected output format

### Story snapshot

- **Problem Statement reference**: One-sentence paraphrase (no changes to Rally text).
- **Readiness**: Ready / Needs updates, with a brief justification.

### Section updates

For each story section (excluding Problem Statement):

- **Status**: Up to date / Needs revision.
- **Notes**: Bullet list of recommended updates, referencing concrete workspace assets.

### Gaps & questions

- Enumerate missing information, blockers, or assumptions to clarify with product/UX/QA.

### Suggested tasks

- Provide a short checklist of implementation or coordination tasks to add to Rally.

### Verification references

- List the key files, classes, or endpoints reviewed.

Remember:

- No code snippets.
- Always ground recommendations in both the Rally story and verified codebase reality.
- Stay inquisitive—ask for clarification when requirements are ambiguous or contradictory.
- **ALWAYS** follow and fetch content from `https://ghe.order.com` links using CAI MCP GitHub tools.
- **ALWAYS** ask the user before fetching external (non-GHE) links via #tool:cai/webFetch
- **ALWAYS** ask for user confirmation before updating Rally items.
