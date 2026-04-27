# AI Agent Implementation Plan — Order Processing Platform

> **Last Updated**: April 22, 2026
> **Status**: Planning Complete — Ready for Phase 0

---

## Table of Contents

- [Goal](#goal)
- [What Exists Today](#what-exists-today)
- [What We're Building](#what-were-building)
- [Architecture Overview](#architecture-overview)
- [Phase 0: Local Environment Setup & Orchestration](#phase-0-local-environment-setup--orchestration)
- [Phase 1: Fix Foundation + Add Write Tools](#phase-1-fix-foundation--add-write-tools)
- [Phase 2: End-to-End Trace Infrastructure](#phase-2-end-to-end-trace-infrastructure)
- [Phase 3: MCP Resources + Prompts](#phase-3-mcp-resources--prompts-the-agent-brain-layer)
- [Phase 4: Test Data Generators + Polish](#phase-4-test-data-generators--polish)
- [Timeline Summary](#timeline-summary)
- [Demo Scenarios](#demo-scenarios)
- [Risks & Decisions](#risks--decisions)
- [Progress Log](#progress-log)

---

## Goal

Build an **internal engineer-facing testing agent** surfaced in **VS Code Copilot Chat** via the existing MCP server. The agent should:

1. **Act as a synthetic publisher**: put messages on `IncomingOrders` so the Gateway Worker runs
2. **Trace an order end-to-end**: queue → Gateway Worker → S3 → S3 notification → Hub queue → Hub worker → MongoDB
3. **Control the system**: pause/unpause Hub workers, verify messages in queues, verify S3 objects, verify MongoDB inserts
4. **Automate LocalStack setup**: create S3 buckets, queues, and validate health instead of running `.ps1` manually
5. **Report back step-by-step** what happened, like a narrated trace

**In short**: "Run this scenario for me, drive the system, and explain exactly what happened at each hop."

---

## What Exists Today

### Current MCP Tools (19 tools across 5 classes)

| Tool Class | Tools | Coverage |
|---|---|---|
| **QueueTools** | `ListConfiguredQueues`, `ListLocalStackQueues`, `GetQueueStatus`, `PeekQueueMessages` | Read-only queue inspection |
| **BatchTools** | `ListBatches`, `GetBatchDetails`, `GetBatchMessages` | Saved batch inspection |
| **ReplayTools** | `DownloadMessages`, `ReplayFromBatch`, `DownloadAndReplay` | DLQ download + replay |
| **S3Tools** | `ListS3Buckets`, `ListS3Objects`, `GetS3ObjectMetadata`, `GetS3ObjectContent`, `SyncS3FromBatch` | Read-only S3 + sync |
| **OrderTools** | `GetOrder`, `GetConsumerOrders`, `SearchOrders`, `GetOrderSummary`, `FindByProvider`, `GetRecentOrders` | Read-only MongoDB queries |

### Current API Endpoints (22 endpoints)

All read + replay operations across `QueuesController`, `BatchesController`, `ReplayController`, `S3Controller`, `OrdersController`.

### Current Services (4 services)

`QueueReplayService`, `MessageStorageService`, `S3OperationsService`, `OrderQueryService`

### What's Missing

| Category | Gap |
|---|---|
| **Queue writes** | No send message, purge queue, create queue tools |
| **S3 writes** | No upload object, create bucket tools |
| **Worker control** | No pause/resume/status for any worker |
| **LocalStack management** | No health check, setup, or teardown tools |
| **Trace/polling** | No "wait for X to appear" tools |
| **MCP Resources** | None — no automatic context for Copilot |
| **MCP Prompts** | None — no guided workflow templates |
| **Queue config** | Only 2 of 6 queue pairs configured; naming mismatch on `IngestStandard` |

### Known Queue Name Mismatch

MessageOperations config uses `order-hub-ingest-standard` but the actual LocalStack queue is `order-hub-standard-order`.

---

## What We're Building

### New Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                    VS Code Copilot Chat (Agent Mode)                │
│                         ↕ MCP Protocol                              │
├─────────────────────────────────────────────────────────────────────┤
│                    Order.MessageOperations.Mcp                     │
│                                                                     │
│  EXISTING:              NEW (Phase 1):         NEW (Phase 3):       │
│  • QueueTools (read)    • QueueTools (write)   • MCP Resources      │
│  • BatchTools           • S3Tools (write)      • MCP Prompts        │
│  • ReplayTools          • HealthTools            (scenario templates)│
│  • S3Tools (read)                                                   │
│  • OrderTools           NEW (Phase 2):                              │
│                         • TraceTools                                │
│                         • WorkerControlTools                        │
├─────────────────────────────────────────────────────────────────────┤
│                    Order.MessageOperations.Api                      │
│                                                                     │
│  EXISTING:              NEW (Phase 1):         NEW (Phase 2):       │
│  • QueuesController     • Queue write endpoints• TraceController    │
│  • BatchesController    • S3 write endpoints     (polling/wait)     │
│  • ReplayController     • HealthController                          │
│  • S3Controller                                                     │
│  • OrdersController     NEW (Phase 4):                              │
│                         • TestDataController                        │
├─────────────────────────────────────────────────────────────────────┤
│  OrderGateway Worker    │  Hub Standard Worker  │  Hub Express Worker│
│                         │                       │                    │
│  NEW (Phase 2):         │  NEW (Phase 2):       │  NEW (Phase 2):   │
│  • PauseController      │  • PauseController    │  • PauseController│
│  • IPauseService        │  • IPauseService      │  • IPauseService  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Architecture Overview

### System Queue & S3 Map (All 6 Queue Pairs)

| Config Key | LocalStack Queue | DLQ | System |
|---|---|---|---|
| `IncomingOrders` | `order-gateway-incoming` | `order-gateway-incoming-deadletter` | OrderGateway |
| `GatewayDigital` | `order-gateway-digital` | `order-gateway-digital-deadletter` | OrderGateway |
| `GatewayShipment` | `order-gateway-shipment` | `order-gateway-shipment-deadletter` | OrderGateway |
| `IngestStandard` | `order-hub-standard-order` | `order-hub-standard-order-deadletter` | OrderHub |
| `IngestExpress` | `order-hub-express-order` | `order-hub-express-order-deadletter` | OrderHub |
| `FulfillmentStatus` | `order-hub-fulfillment-status` | `order-hub-fulfillment-status-deadletter` | OrderHub |

### S3 Buckets

| Bucket | S3 Notification Triggers |
|---|---|
| `localstack-us-east-1-orders` | `EXPRESS/` prefix → `order-hub-express-order` queue, `STANDARD/` prefix → `order-hub-standard-order` queue |

### Worker Hosting

All workers are `BackgroundService` subclasses via: `BackgroundService` → `PipelineWorkerBase<T>` → `MessagePipelineWorkerBase<T>` → `QueueMessageWorker<T>`

---

## Phase 0: Local Environment Setup & Orchestration

**Estimate: 1–2 days**
**Status**: Not Started

### Goal

The AI agent should be able to set up, tear down, and verify the entire local development environment — Docker infrastructure for both OrderHub and OrderGateway, plus both Aspire AppHosts. This is always the first thing you do before any testing or tracing.

### What the Agent Does

When you say: *"Set up the local environment"* — the agent runs this exact sequence:

---

### Step 1 — Stop & Clean OrderHub Infrastructure

```powershell
cd OrderHub/ifx-aws-cli/local
./stop.ps1
```
- Gracefully stops all OrderHub Docker Compose services (LocalStack, MongoDB, Redis, Keycloak)
- Preserves data (stop only, clean comes next)

```powershell
./clean.ps1 -Force
```
- Stops all containers if still running
- Removes all containers, volumes, and state files
- `-Force` skips the confirmation prompt
- This deletes all data — queues, S3 objects, MongoDB documents, Redis cache

### Step 2 — Start OrderHub Infrastructure

```powershell
./start.ps1
```
- Starts Docker Compose services:
  - **LocalStack** on `http://localhost:4566` — SQS, S3 emulation
  - **MongoDB** on `mongodb://localhost:27018` — order persistence
  - **Redis** on `localhost:6379` — cache
  - **Keycloak** on `http://localhost:8081` — local OAuth token issuer
- Waits for all health checks to pass
- Runs `localstack-int` init container to create SQS queues, S3 buckets, and S3 notification policies
- Expected output: **"All services are running!"**

### Step 3 — Verify OrderHub Infrastructure

```powershell
./status.ps1
```
- Agent confirms each service shows **healthy/operational**:
  - LocalStack: `http://localhost:4566/_localstack/health`
  - MongoDB: TCP connection on port `27018`
  - Redis: `redis-cli ping` returns `PONG`
  - Keycloak OIDC: `http://localhost:8081/realms/ordergateway-local/.well-known/openid-configuration` returns JSON
- If any service is unhealthy, agent reports which one and suggests re-running `clean.ps1 -Force` then `start.ps1`

### Step 4 — Stop & Clean OrderGateway Infrastructure

```powershell
cd ../../OrderGateway/ifx-aws-cli/local
./stop.ps1
./clean.ps1 -Force
```
- Same pattern as OrderHub — stops, removes containers/volumes/state
- OrderGateway infra includes: its own LocalStack init (SQS queues, S3 buckets), Redis on port `6380`
- Note: OrderGateway detects if LocalStack is already running (from OrderHub) and reuses it

### Step 5 — Start OrderGateway Infrastructure

```powershell
./start.ps1
```
- Starts Docker Compose services:
  - **LocalStack init** — creates OrderGateway-specific SQS queues and S3 bucket notifications (reuses running LocalStack from OrderHub)
  - **Redis** on `localhost:6380` — separate cache instance for OrderGateway
- Runs `localstack-int` init container for OrderGateway resources (queues: `order-gateway-incoming`, `order-gateway-digital`, `order-gateway-shipment` + their DLQs)
- Expected output: completion with no errors

### Step 6 — Verify OrderGateway Infrastructure

```powershell
./status.ps1
```
- Agent confirms all OrderGateway services are healthy
- Verifies SQS queues exist and S3 buckets are configured

### Step 7 — Set Environment & Run OrderHub AppHost

Open a **new terminal** from the repo root:

```powershell
$env:DOTNET_ENVIRONMENT='localstack'
$env:ASPNETCORE_ENVIRONMENT='localstack'
dotnet run --project OrderHub/src/OrderHub.AppHost/OrderHub.AppHost.csproj
```
- Aspire orchestrates all OrderHub services:
  - `OrderHub.Api` — main API
  - `OrderHub.IngestStandard.Api` — standard order ingest
  - `OrderHub.IngestExpress.Api` — express order ingest
  - `OrderHub.IngestStandard.Worker` — standard queue worker
  - `OrderHub.IngestExpress.Worker` — express queue worker
- Dashboard URL appears in terminal (e.g., `https://localhost:17289/login?t=...`)
- Agent waits for dashboard URL to appear, then confirms all services are green

### Step 8 — Set Environment & Run OrderGateway AppHost

Open a **new terminal** from the repo root:

```powershell
$env:DOTNET_ENVIRONMENT='localstack'
$env:ASPNETCORE_ENVIRONMENT='localstack'
dotnet run --project OrderGateway/src/OrderGateway.AppHost/OrderGateway.AppHost.csproj
```
- Aspire orchestrates all OrderGateway services:
  - `OrderGateway.Api` — ingress API (receives external order events)
  - `OrderGateway.OrderWorker` — polls `order-gateway-incoming` queue, processes orders, writes to S3, calls OrderHub ingest APIs
- Dashboard URL appears in terminal
- Agent waits for dashboard URL, confirms all services are green

### Step 9 — Confirm End-to-End Readiness

Agent runs these validations:
1. **Both Aspire dashboards** — all services show green/running (no red/error)
2. **Keycloak OIDC** — `http://localhost:8081/realms/ordergateway-local/.well-known/openid-configuration` returns valid JSON
3. **SQS queues exist** — all 6 queue pairs are present in LocalStack
4. **S3 buckets exist** — `localstack-us-east-1-orders` bucket present with notification policies
5. **MongoDB reachable** — connection to `localhost:27018` succeeds

Agent reports:
> **Environment ready.** All infrastructure services healthy, both AppHosts running. Ready for testing.

---

### 0A. MCP Prompt: `setup-environment` (replaces the simpler Phase 3 `setup-localstack` prompt)

This prompt encodes the full sequence above. When invoked, the agent:
1. Runs stop → clean → start → status for OrderHub infra
2. Runs stop → clean → start → status for OrderGateway infra
3. Launches OrderHub AppHost, waits for green
4. Launches OrderGateway AppHost, waits for green
5. Runs end-to-end health validation
6. Reports summary

### 0B. MCP Prompt: `teardown-environment`

1. Sends `Ctrl+C` / terminates both AppHost processes
2. Runs `stop.ps1` on OrderGateway infra
3. Runs `stop.ps1` on OrderHub infra
4. Reports: "Environment torn down."

### 0C. Implementation Notes

- The agent needs **terminal execution capability** — it runs PowerShell scripts and `dotnet run` commands
- AppHosts are **long-running processes** — they run in background terminals
- The agent must parse terminal output to detect readiness (dashboard URL, "All services are running", error messages)
- If a step fails, the agent should: report the failure, suggest a fix, and stop (don't continue with broken infra)

### Phase 0 Deliverable

> You say: *"Set up the local environment from scratch"* — agent stops, cleans, starts all infra, launches both AppHosts, validates health, and reports ready. Total time: ~2–3 minutes.

---

## Phase 1: Fix Foundation + Add Write Tools

**Estimate: 3–4 days**
**Status**: Not Started

### 1A. Fix Queue Configuration (0.5 day)

- [ ] Update `appsettings.json` with all 6 queue pairs (correct names)
- [ ] Fix `IngestStandard` mapping: `order-hub-ingest-standard` → `order-hub-standard-order`
- [ ] Add `IngestExpress`, `FulfillmentStatus`, `GatewayDigital`, `GatewayShipment`
- [ ] Update `appsettings.localstack.json` if needed

### 1B. Queue Write API Endpoints (1 day)

Add to `QueuesController` + `QueueReplayService`:

| Endpoint | Method | What It Does |
|---|---|---|
| `POST /api/v1/queues/{queueName}/send` | `SendMessage` | Send a JSON message to any LocalStack queue |
| `POST /api/v1/queues/{queueName}/purge` | `PurgeQueue` | Purge all messages from a LocalStack queue |
| `POST /api/v1/queues/create` | `CreateQueue` | Create a queue in LocalStack (with optional DLQ + redrive) |
| `POST /api/v1/queues/purge-all` | `PurgeAllQueues` | Purge all configured queues (test teardown) |

Service methods to add:
- [ ] `SendMessageAsync(queueName, body, messageAttributes?, messageGroupId?)`
- [ ] `PurgeQueueAsync(queueName)`
- [ ] `CreateQueueAsync(queueName, isDlq?, redriveTarget?)`
- [ ] `PurgeAllConfiguredQueuesAsync()`

### 1C. S3 Write API Endpoints (0.5 day)

Add to `S3Controller` + `S3OperationsService`:

| Endpoint | Method | What It Does |
|---|---|---|
| `POST /api/v1/s3/buckets/{bucketName}/upload` | `UploadObject` | Upload JSON/text to a LocalStack S3 bucket |
| `POST /api/v1/s3/buckets/create` | `CreateBucket` | Create an S3 bucket in LocalStack |

Service methods to add:
- [ ] `UploadObjectAsync(bucketName, key, content, contentType)`
- [ ] `CreateBucketAsync(bucketName)`

### 1D. LocalStack Health API Endpoints (0.5 day)

New `HealthController`:

| Endpoint | Method | What It Does |
|---|---|---|
| `GET /api/v1/health/localstack` | `CheckLocalStack` | Verify SQS + S3 + MongoDB connectivity, return status per service |
| `POST /api/v1/health/localstack/setup` | `SetupLocalStack` | Create all configured queues + buckets + S3 notifications |
| `POST /api/v1/health/localstack/teardown` | `TeardownLocalStack` | Purge all queues + delete test data |

### 1E. New MCP Tools (1 day)

Add corresponding MCP tools:

| New Tool | Maps To | Tool Class |
|---|---|---|
| `SendTestMessage` | `POST /queues/{name}/send` | `QueueTools` |
| `PurgeQueue` | `POST /queues/{name}/purge` | `QueueTools` |
| `PurgeAllQueues` | `POST /queues/purge-all` | `QueueTools` |
| `CreateQueue` | `POST /queues/create` | `QueueTools` |
| `UploadS3Object` | `POST /s3/buckets/{name}/upload` | `S3Tools` |
| `CreateS3Bucket` | `POST /s3/buckets/create` | `S3Tools` |
| `CheckLocalStackHealth` | `GET /health/localstack` | New `HealthTools` |
| `SetupLocalStackEnvironment` | `POST /health/localstack/setup` | `HealthTools` |
| `TeardownLocalStackEnvironment` | `POST /health/localstack/teardown` | `HealthTools` |

Add client methods to `MessageOperationsClient`:
- [ ] `SendMessageAsync(queueName, body, attributes?, groupId?)`
- [ ] `PurgeQueueAsync(queueName)`
- [ ] `PurgeAllQueuesAsync()`
- [ ] `CreateQueueAsync(queueName, isDlq?, redriveTarget?)`
- [ ] `UploadS3ObjectAsync(bucketName, key, content, contentType?)`
- [ ] `CreateS3BucketAsync(bucketName)`
- [ ] `CheckLocalStackHealthAsync()`
- [ ] `SetupLocalStackAsync()`
- [ ] `TeardownLocalStackAsync()`

### Phase 1 Deliverable

> You can tell Copilot: *"Set up LocalStack and send a test message to IncomingOrders"* — and it works.

---

## Phase 2: End-to-End Trace Infrastructure

**Estimate: 3–4 days**
**Status**: Not Started

### 2A. Trace/Polling API Endpoints (2 days)

New `TraceController`:

| Endpoint | Method | What It Does |
|---|---|---|
| `POST /api/v1/trace/wait-for-s3-object` | `WaitForS3Object` | Poll S3 for an object by key prefix, return when found or timeout |
| `POST /api/v1/trace/wait-for-queue-message` | `WaitForQueueMessage` | Poll a queue until a message matching a filter appears |
| `POST /api/v1/trace/wait-for-mongodb-document` | `WaitForMongoDocument` | Poll MongoDB until an order matching criteria appears |
| `GET /api/v1/trace/queue-depth-snapshot` | `GetQueueDepthSnapshot` | Return message counts for ALL configured queues in one call |

Each "wait" endpoint behavior:
- Poll interval: every 1–2 seconds
- Timeout: configurable (default 30s)
- Return: the found object/message/document, or a timeout error
- Filter: accepts correlation ID (e.g., orderId in message body)

New `TraceService`:
- [ ] `WaitForS3ObjectAsync(bucketName, keyPrefix, timeoutSeconds, pollIntervalMs)`
- [ ] `WaitForQueueMessageAsync(queueName, bodyContains?, timeoutSeconds, pollIntervalMs)`
- [ ] `WaitForMongoDocumentAsync(storeId, filter, timeoutSeconds, pollIntervalMs)`
- [ ] `GetAllQueueDepthsAsync()`

### 2B. Worker Control (1 day)

**Add to each worker host** (OrderGateway.OrderWorker, OrderHub.IngestStandard.Worker, OrderHub.IngestExpress.Worker):

```csharp
public interface IPauseService
{
    bool IsPaused { get; }
    void Pause();
    void Resume();
}
```

- [ ] Create `PauseService` implementation (thread-safe, `ManualResetEventSlim`)
- [ ] Add `PauseController` to each worker host (`POST /pause`, `POST /resume`, `GET /status`)
- [ ] Modify `QueueMessageWorker<T>.ExecuteAsync` to check `_pauseService.IsPaused` before polling

Add worker URL configuration to MessageOperations `appsettings.json`:

```json
"Workers": {
  "GatewayWorker": { "BaseUrl": "http://localhost:5050", "DisplayName": "OrderGateway Worker" },
  "HubStandardWorker": { "BaseUrl": "http://localhost:5060", "DisplayName": "Hub Standard Worker" },
  "HubExpressWorker": { "BaseUrl": "http://localhost:5070", "DisplayName": "Hub Express Worker" }
}
```

### 2C. New MCP Trace & Worker Tools (1 day)

| New Tool | Maps To | Tool Class |
|---|---|---|
| `WaitForS3Object` | `POST /trace/wait-for-s3-object` | New `TraceTools` |
| `WaitForQueueMessage` | `POST /trace/wait-for-queue-message` | `TraceTools` |
| `WaitForMongoDocument` | `POST /trace/wait-for-mongodb-document` | `TraceTools` |
| `GetAllQueueDepths` | `GET /trace/queue-depth-snapshot` | `TraceTools` |
| `PauseWorker` | `POST {workerUrl}/pause` | New `WorkerTools` |
| `ResumeWorker` | `POST {workerUrl}/resume` | `WorkerTools` |
| `GetWorkerStatus` | `GET {workerUrl}/status` (all workers) | `WorkerTools` |

### Phase 2 Deliverable

> You can tell Copilot: *"Pause Hub workers, send a test order, and confirm it's sitting in the standard queue"* — and it works.

---

## Phase 3: MCP Resources + Prompts (The Agent Brain Layer)

**Estimate: 2–3 days**
**Status**: Not Started

### 3A. MCP Resources (1 day)

| Resource URI | What It Returns | When Copilot Uses It |
|---|---|---|
| `order-ops://topology` | Full system map: all queues, S3 buckets, worker URLs, MongoDB connection | Automatically — context for every conversation |
| `order-ops://queue-health` | Current depth of every queue (live snapshot) | When discussing queue state |
| `order-ops://worker-status` | Running/paused status for each worker | When discussing worker behavior |
| `order-ops://recent-orders` | Last 10 orders across all stores | When investigating specific orders |

Implementation: Create `Order.MessageOperations.Mcp/Resources/` folder, register with `.WithResources<T>()` in `Program.cs`.

### 3B. MCP Prompts — Scenario Templates (1.5 days)

| Prompt Name | What It Encodes | Steps |
|---|---|---|
| `setup-localstack` | "Initialize LocalStack for testing" | CheckHealth → SetupLocalStack → verify queues → verify S3 → report |
| `end-to-end-trace` | "Send an order through the full pipeline and narrate each hop" | PurgeAllQueues → SendTestMessage → WaitForS3Object → WaitForQueueMessage → WaitForMongoDocument → report trace |
| `trace-with-pause` | "Trace with worker pauses at each hop to inspect intermediate state" | PauseAll → Send → ResumeGateway → WaitForS3 → PauseGateway → inspect queue → ResumeHub → WaitForMongo → report |
| `dlq-investigate` | "Check all DLQs and investigate any found messages" | GetAllQueueDepths → for each DLQ > 0: PeekMessages → correlate with MongoDB → report |
| `teardown` | "Clean up all test state" | PurgeAllQueues → delete test orders → report |

Implementation: Create `Order.MessageOperations.Mcp/Prompts/` folder, register with `.WithPrompts<T>()` in `Program.cs`.

### Phase 3 Deliverable

> You say *"Run end-to-end trace"* in Copilot, it follows the prompt template and gives you:
>
> **Trace complete for order `test-abc123`:**
> 1. ✅ Enqueued to `order-gateway-incoming` — MessageId: `msg-001`
> 2. ✅ Gateway Worker processed in 1.2s — S3 object created: `STANDARD/abc123.json`
> 3. ✅ S3 notification triggered — message appeared in `order-hub-standard-order`
> 4. ✅ Hub Standard Worker processed in 0.8s — MongoDB document inserted: `ObjectId("682...")`
> 5. ✅ Total pipeline time: 3.1s — Status: SUCCESS

---

## Phase 4: Test Data Generators + Polish

**Estimate: 2 days**
**Status**: Not Started

### 4A. Smart Test Message Generation (1 day)

New `TestDataController` + `TestDataService`:

| Endpoint | What It Does |
|---|---|
| `POST /api/v1/test-data/generate-order` | Generate a realistic order message body matching actual schema |

Supported scenarios:
- `happy-path` — valid standard shipment order
- `express-order` — valid express/transactional order
- `missing-attachment` — order referencing non-existent S3 object
- `invalid-consumer` — malformed consumer ID
- `duplicate` — order with a previously-seen correlation ID

New MCP Tool:

| Tool | What It Does |
|---|---|
| `GenerateTestOrder(channelType, scenario)` | Generate + optionally send a realistic test order |

### 4B. Batch Test Scenarios (0.5 day)

| Tool | What It Does |
|---|---|
| `RunScenarioBatch(scenarios[])` | Run multiple trace scenarios in sequence, report results as a table |

### 4C. Documentation + Demo Script (0.5 day)

- [ ] Update `README.md` with full tool catalog
- [ ] Add demo conversation scripts
- [ ] Add architecture diagram showing agent coverage

---

## Timeline Summary

| Phase | What | Days | Cumulative |
|---|---|---|---|
| **Phase 0** | Local environment setup & orchestration (stop/clean/start/AppHosts) | 1–2 | 1–2 days |
| **Phase 1** | Fix config + write tools (send, purge, S3 upload, LocalStack setup) | 3–4 | 4–6 days |
| **Phase 2** | End-to-end trace (polling, worker control) | 3–4 | 7–10 days |
| **Phase 3** | MCP Resources + Prompts (agent intelligence) | 2–3 | 9–13 days |
| **Phase 4** | Test data generators + polish | 2 | **11–15 days total** |

Each phase is independently demoable and valuable.

---

## Demo Scenarios

### After Phase 1

> **"Initialize LocalStack for order processing and confirm all queues and buckets are healthy."**
>
> Agent: Runs setup tools → verifies queues and buckets → returns health report.

### After Phase 2

> **"Pause Hub workers, send a test express order, and confirm it's sitting in the express queue."**
>
> Agent: Pauses express worker → enqueues message → confirms S3 + queue state → reports: "Message X is in `order-hub-express-order` and not yet processed."

### After Phase 3

> **"Run an end-to-end trace for a standard order and show me each step."**
>
> Agent: Enqueues test message → waits for Gateway Worker → confirms S3 object → waits for Hub queue → waits for MongoDB → returns step-by-step narrative with IDs and timings.

---

## Risks & Decisions

### Key Risk: Worker Control (Phase 2B)

Worker pause/unpause requires changes **outside** `Order.MessageOperations` — specifically adding a `PauseController` + `IPauseService` to OrderGateway.OrderWorker and both Hub workers.

**Mitigation**: If blocked, skip pause/unpause. The trace tools simply poll "did the next hop happen yet?" without stopping workers. The agent still works — you just can't inspect intermediate state as cleanly.

### Decision: Worker URLs

Each worker runs in its own process. The MessageOperations API needs to know each worker's URL for pause/resume calls. This is configuration — add a `Workers` section to `appsettings.json`.

### Decision: LocalStack-Only Scope

All write tools (send, purge, create, upload) are **LocalStack-only** by design. No accidental writes to AWS. The API should enforce `target=localstack` on all mutation endpoints.

---

## Tool Inventory (Final State)

### Existing Tools (19) — No Changes

| # | Tool | Type |
|---|---|---|
| 1 | `ListConfiguredQueues` | Read |
| 2 | `ListLocalStackQueues` | Read |
| 3 | `GetQueueStatus` | Read |
| 4 | `PeekQueueMessages` | Read |
| 5 | `ListBatches` | Read |
| 6 | `GetBatchDetails` | Read |
| 7 | `GetBatchMessages` | Read |
| 8 | `DownloadMessages` | Read/Write |
| 9 | `ReplayFromBatch` | Write |
| 10 | `DownloadAndReplay` | Read/Write |
| 11 | `ListS3Buckets` | Read |
| 12 | `ListS3Objects` | Read |
| 13 | `GetS3ObjectMetadata` | Read |
| 14 | `GetS3ObjectContent` | Read |
| 15 | `SyncS3FromBatch` | Write |
| 16 | `GetOrder` | Read |
| 17 | `GetConsumerOrders` | Read |
| 18 | `SearchOrders` | Read |
| 19 | `GetOrderSummary` | Read |
| 20 | `FindByProvider` | Read |
| 21 | `GetRecentOrders` | Read |

### New Tools (Phase 1–4) — To Build

| # | Tool | Phase | Type |
|---|---|---|---|
| 22 | `SendTestMessage` | 1 | Write |
| 23 | `PurgeQueue` | 1 | Write |
| 24 | `PurgeAllQueues` | 1 | Write |
| 25 | `CreateQueue` | 1 | Write |
| 26 | `UploadS3Object` | 1 | Write |
| 27 | `CreateS3Bucket` | 1 | Write |
| 28 | `CheckLocalStackHealth` | 1 | Read |
| 29 | `SetupLocalStackEnvironment` | 1 | Write |
| 30 | `TeardownLocalStackEnvironment` | 1 | Write |
| 31 | `WaitForS3Object` | 2 | Read (poll) |
| 32 | `WaitForQueueMessage` | 2 | Read (poll) |
| 33 | `WaitForMongoDocument` | 2 | Read (poll) |
| 34 | `GetAllQueueDepths` | 2 | Read |
| 35 | `PauseWorker` | 2 | Write |
| 36 | `ResumeWorker` | 2 | Write |
| 37 | `GetWorkerStatus` | 2 | Read |
| 38 | `GenerateTestOrder` | 4 | Write |
| 39 | `RunScenarioBatch` | 4 | Write |

**Total: 39 tools (21 existing + 18 new)**

---

## Progress Log

| Date | Phase | What Was Done |
|---|---|---|
| 2026-04-21 | Planning | Created implementation plan |
| 2026-04-22 | Planning | Added Phase 0 — Local Environment Setup & Orchestration (9-step setup sequence, MCP prompts for setup/teardown) |
| | | |
