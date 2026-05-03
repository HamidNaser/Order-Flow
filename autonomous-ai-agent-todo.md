# Autonomous AI Agent — TODO

This document captures the constraints, problems, and requirements identified for evolving the MCP prompt system from a user-driven workflow into a fully autonomous test-and-validate loop that runs during development.

---

## Current State

The MCP server exposes 7 prompts that form a complete lifecycle:

| Prompt | Purpose |
|---|---|
| `setup-localstack` | Spin up Docker infrastructure (LocalStack, MongoDB, Redis, Keycloak) |
| `build-and-run` | Restore/build both solutions, launch Aspire AppHosts, confirm end-to-end |
| `run-standard-orders` | Generate and trace N standard-priority orders through the pipeline |
| `run-express-orders` | Generate and trace N express-priority orders through the pipeline |
| `end-to-end-trace` | Send one order and trace it through every hop (queue → S3 → MongoDB) |
| `tear-down` | Kill .NET processes, stop and clean all Docker containers |

Today these are **user-driven** — a human invokes each prompt one at a time in Copilot Chat or Claude Desktop. The goal is to make the system **self-driving**: after a code change, the agent autonomously sets up infrastructure, builds, runs tests, validates results, and tears down.

---

## Constraints and Problems for Autonomous Use

### 1. Long-Running Processes and Terminal Management
- `build-and-run` launches AppHosts as background processes in separate terminals. The AI agent has limited ability to manage terminal lifecycle — it can start them but tracking "is it ready?" is fragile.
- There is no tool to reliably check if an Aspire AppHost has fully started all its child services.
- The prompts say "wait for the dashboard URL" but the agent cannot reliably parse background terminal output for that signal.

### 2. No Prompt Chaining / Orchestration Layer
- Each prompt is standalone. There is no way for the agent to execute `setup-localstack` → `build-and-run` → `run-standard-orders` → `tear-down` as a single composed workflow.
- Each prompt requires a separate user interaction today.
- For autonomous use, a **meta-prompt** or **workflow orchestrator** is needed that chains prompts with pass/fail gates between them.

### 3. Error Recovery Is Advisory, Not Executable
- The prompts say things like "if the build fails, report and stop" — but there is no structured error handling.
- The AI might continue past failures or stop and lose context.
- For autonomous testing, explicit **exit codes / machine-readable success conditions** are needed, not just "report the errors."

### 4. The Kill-All Problem (tear-down)
- The broad `Stop-Process -Name dotnet` in tear-down kills **every** dotnet process on the machine — not just ones from this project.
- In an autonomous loop where the agent itself runs in a dotnet process (or the MCP server is dotnet), it could kill itself.
- **Solution needed**: Track PIDs at launch time and kill only those specific processes.

### 5. State Awareness Between Prompts
- `build-and-run` has no way to know if `setup-localstack` already ran.
- Each prompt does its own pre-flight checks, but there is no shared state.
- If the autonomous system runs `build-and-run` without `setup-localstack`, it starts AppHosts against missing infrastructure.
- **Solution needed**: A state machine or status-check mechanism that validates "what is currently running" before proceeding.

### 6. Timing and Polling Are Heuristic
- Timeouts are hardcoded (30s for queue tracing, etc.).
- In an autonomous CI loop, infrastructure might be slower or faster.
- There is no adaptive retry — if `WaitForQueueMessage` times out, the prompt reports "not found" rather than retrying the step.
- **Solution needed**: Configurable timeouts and retry-with-backoff logic.

### 7. No Test Assertion Framework
- The prompts report results as markdown tables for humans to read.
- For autonomous validation, machine-readable pass/fail assertions are needed — e.g., "5 of 5 orders traced = PASS, <5 = FAIL" — that feed into a decision loop.
- **Solution needed**: Structured result objects (JSON) with explicit pass/fail fields.

### 8. Working Directory Assumptions
- Terminal commands use relative paths (`cd ../../OrderGateway/...`).
- If the agent's terminal working directory drifts from a previous command, these paths break silently.
- **Solution needed**: Every prompt should use absolute paths or explicitly reset the working directory at the start of each step.

---

## What Is Needed for Full Autonomy

### Meta-Prompt / Workflow Engine
A top-level orchestrator that chains prompts in sequence with gates:
```
setup-localstack  →  [PASS?]  →  build-and-run  →  [PASS?]  →  run-standard-orders  →  [PASS?]  →  run-express-orders  →  [PASS?]  →  tear-down
```
If any stage fails, the orchestrator should decide: retry, skip, or abort and tear down.

### PID Tracking
- When `build-and-run` launches AppHosts, record the process IDs.
- Pass those PIDs to `tear-down` so it kills only what was started.
- Could be stored in a temp file, environment variable, or in-memory state.

### Machine-Readable Assertions
Replace markdown reports with structured results:
```json
{
  "prompt": "run-standard-orders",
  "result": "PASS",
  "ordersSent": 5,
  "ordersTraced": 5,
  "failures": []
}
```
The orchestrator reads this to decide whether to proceed.

### State Checks Between Prompts
A lightweight `check-status` prompt or tool that returns:
- Is Docker running?
- Is LocalStack healthy?
- Are AppHosts running? (by PID or port check)
- Are queues available?

This runs before each prompt to validate preconditions.

### Absolute Paths in All Commands
Replace all relative `cd` navigation with absolute paths derived from a known workspace root. Example:
```
cd C:/Work/mine/Communication/OrderHub/ifx-aws-cli/local
```

### Configurable Timeouts
Move hardcoded timeouts (30s, 5s) to template parameters or a configuration file so they can be tuned for CI environments vs. local development.

### Health-Gate Pattern
Every prompt should end with a structured health check that the next prompt can consume. Don't proceed to the next prompt until the previous one confirms success with a machine-readable signal.

---

## Priority Order for Implementation

| Priority | Item | Reason |
|---|---|---|
| 1 | Absolute paths in all prompts | Prevents silent failures from directory drift |
| 2 | PID tracking for tear-down | Prevents killing unrelated processes or self |
| 3 | State-check prompt | Enables safe prompt ordering |
| 4 | Machine-readable assertions | Required for any automated decision-making |
| 5 | Meta-prompt / workflow engine | The actual orchestration layer |
| 6 | Configurable timeouts | Needed for CI vs. local tuning |
| 7 | Health-gate pattern | Polish for robust autonomous loops |
