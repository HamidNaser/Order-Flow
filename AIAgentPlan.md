# AI Agent Implementation Plan — Order Processing Platform

> **Last Updated**: May 2, 2026
> **Status**: Phase 1 ✅ | Phase 2A/2C ✅ | Phase 3B ✅ | Phase 4A ✅ | Phase 4C ✅ — Core Agent Complete, 2B Dropped

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
- [Phase 5: Log & Trace Access Tools](#phase-5-log--trace-access-tools-autonomous-debugging)
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

**Estimate: 2 days** (2B dropped)
**Status**: ✅ Complete (2A + 2C)

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

### ~~2B. Worker Control~~ — ❌ Dropped

> **Decision (May 2, 2026):** Dropped. Pause/resume requires production code changes across 3 solutions (OrderGateway, OrderHub, OrderCommon) — specifically adding `IPauseService` + `PauseController` to each worker and modifying the base `QueueMessageWorker<T>.ExecuteAsync`. This is invasive, and the trace/polling tools (2A) already provide end-to-end visibility without pausing. For autonomous AI debugging, log/trace access (Phase 5) gives far better signal than stopping queue polling. See Phase 5 for the replacement.

### 2C. New MCP Trace Tools (1 day)

| New Tool | Maps To | Tool Class |
|---|---|---|
| `WaitForS3Object` | `POST /trace/wait-for-s3-object` | New `TraceTools` |
| `WaitForQueueMessage` | `POST /trace/wait-for-queue-message` | `TraceTools` |
| `WaitForMongoDocument` | `POST /trace/wait-for-mongodb-document` | `TraceTools` |
| `GetAllQueueDepths` | `GET /trace/queue-depth-snapshot` | `TraceTools` |

### Phase 2 Deliverable

> You can tell Copilot: *"Send a test order and trace it through every hop"* — and it works. The agent polls for the message at each stage (queue → S3 → queue → MongoDB) without needing to pause workers.

---

## Phase 3: MCP Resources + Prompts (The Agent Brain Layer)

**Estimate: 2–3 days**
**Status**: 3A ⬜ Not Started | 3B ✅ Complete

### 3A. MCP Resources (1 day)

| Resource URI | What It Returns | When Copilot Uses It |
|---|---|---|
| `order-ops://topology` | Full system map: all queues, S3 buckets, worker URLs, MongoDB connection | Automatically — context for every conversation |
| `order-ops://queue-health` | Current depth of every queue (live snapshot) | When discussing queue state |
| `order-ops://worker-status` | Running/paused status for each worker | When discussing worker behavior |
| `order-ops://recent-orders` | Last 10 orders across all stores | When investigating specific orders |

Implementation: Create `Order.MessageOperations.Mcp/Resources/` folder, register with `.WithResources<T>()` in `Program.cs`.

### 3B. MCP Prompts — Scenario Templates (1.5 days) — ✅ Complete

Implemented with externalized `.md` template files in `Prompts/Templates/` using `{{placeholder}}` replacement at runtime.

| Prompt Name | Template File | What It Does | Status |
|---|---|---|---|
| `setup-localstack` | `setup-localstack.md` | Full 7-step infra setup: stop/clean/start OrderHub & OrderGateway, verify health | ✅ Done |
| `build-and-run` | `build-and-run.md` | Build both solutions, start both AppHosts in background terminals, verify end-to-end | ✅ Done |
| `run-standard-orders` | `run-standard-orders.md` | Generate N standard orders, send to gateway, trace through pipeline, summary table | ✅ Done |
| `run-express-orders` | `run-express-orders.md` | Generate N express orders, send to gateway, trace through pipeline, summary table | ✅ Done |
| `end-to-end-trace` | `end-to-end-trace.md` | Single order full pipeline trace with timing at each of 4 hops | ✅ Done |
| `tear-down` | `tear-down.md` | Kill app processes, stop/clean OrderGateway & OrderHub infra, verify shutdown | ✅ Done |

**Total: 7 prompts** (6 template files — `run-standard-orders` and `run-express-orders` share similar structure but use different `{{placeholder}}` values)

Implementation: `Order.MessageOperations.Mcp/Prompts/OrderPrompts.cs` with `LoadTemplate()` method, registered with `.WithPrompts<OrderPrompts>()` in `Program.cs`. Template `.md` files copied to output via `.csproj` Content items.

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

### 4C. Documentation + Demo Script (0.5 day) — ✅ Complete

- [x] Update `README.md` with full tool catalog
- [x] Updated prompt documentation (7 prompts, template file docs, how-to-add-a-new-prompt)
- [x] Updated architecture diagram showing agent coverage
- [x] Reorganized "Other Things You Can Ask" into categories
- [x] Port alignment (API on 5100, mcp.json, READMEs all consistent)

---

## Phase 5: Log & Trace Access Tools (Autonomous Debugging)

**Estimate: 2–4 days (Tier 1: 1–2 days, Tier 2: 2–3 days)**
**Status**: ⬜ Not Started

### Goal

Give the AI agent visibility into **what happened inside worker code** — logs, exceptions, stack traces, distributed traces. This is the missing piece for autonomous debugging: the agent can already observe inputs (queue messages) and outputs (S3 objects, MongoDB documents), but cannot see *why* something failed.

### Why This Replaces Phase 2B (Worker Control)

Pause/resume only controls queue polling — it can't show the AI what happened inside the code. Log access gives the AI the actual error messages, stack traces, and distributed traces it needs to reason about failures and fix code autonomously.

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    ILogQueryService                       │
│                                                          │
│  GetServiceLogs(serviceName, last, filter)                │
│  GetDistributedTrace(correlationId)                      │
│  GetErrorSummary(serviceName, since)                     │
│  SearchLogs(query, services[], timeRange)                │
├──────────────────────────────────────────────────────────┤
│  AspireLogQueryService    │  CloudWatchLogQueryService   │
│  (Tier 1 — local)         │  (Tier 2 — production)       │
│  OpenTelemetry + Docker   │  CloudWatch / AppInsights    │
│  container logs           │  / Splunk adapters           │
└──────────────────────────────────────────────────────────┘
```

Configuration-driven — swapped by `appsettings.{environment}.json`:

```json
// appsettings.localstack.json
"LogSources": {
  "Provider": "Aspire",
  "AspireEndpoint": "http://localhost:18889"
}

// appsettings.aws.json
"LogSources": {
  "Provider": "CloudWatch",
  "LogGroup": "/ecs/order-gateway",
  "Region": "us-east-1"
}
```

### Tier 1 — Local (Aspire / OpenTelemetry / Docker) — ~1–2 days

New `LogController` + `ILogQueryService`:

| Endpoint | Method | What It Does |
|---|---|---|
| `GET /api/v1/logs/{serviceName}` | `GetServiceLogs` | Get recent structured logs from a specific service |
| `GET /api/v1/logs/trace/{correlationId}` | `GetDistributedTrace` | Get the full distributed trace for a correlation ID |
| `GET /api/v1/logs/{serviceName}/errors` | `GetErrorSummary` | Get recent errors/exceptions with stack traces |
| `POST /api/v1/logs/search` | `SearchLogs` | Search logs across services by text, level, time range |

New MCP Tools:

| Tool | What It Does |
|---|---|
| `GetServiceLogs` | Read recent logs from a worker — AI sees exceptions, warnings, processing details |
| `GetDistributedTrace` | Follow a single order's trace across all services — AI sees where it broke |
| `GetErrorSummary` | Quick check: "any errors in the last 5 minutes?" |
| `SearchLogs` | Full-text search across all service logs |

Data source: Aspire's OpenTelemetry collector (OTLP endpoint) and/or Docker container logs via Docker API.

### Tier 2 — Production (CloudWatch / Splunk / AppInsights) — ~2–3 days

Same `ILogQueryService` interface, different implementations:
- `CloudWatchLogQueryService` — queries AWS CloudWatch Logs
- `SplunkLogQueryService` — queries Splunk (note: generic Splunk MCP tools exist but lack order-pipeline awareness)
- `AppInsightsLogQueryService` — queries Azure Application Insights (if applicable)

The value over existing enterprise MCP tools (e.g., `mcp_cai-mcp_searchSplunkLogs`): these are **order-aware** — they know the service names, correlation ID fields, and log structure. One call like `GetDistributedTrace(orderId)` vs. manually constructing Splunk queries.

### Autonomous Debugging Flow (enabled by this phase)

```
AI writes new feature → builds → sends test order via SendTestMessage
    → WaitForMongoDocument times out (order never arrived)
    → PeekQueueMessages on DLQ: message IS there (worker rejected it)
    → GetServiceLogs("OrderGateway.OrderWorker", last: 20)
      → sees: "FormatException: Invalid date format in field 'orderDate' at OrderProcessor.cs:45"
    → AI fixes the date parsing code → rebuilds → resends → success
```

### Phase 5 Deliverable

> The AI agent can autonomously debug failures by reading application logs and distributed traces — no human needs to open the Aspire dashboard or set breakpoints.

---

## Timeline Summary

| Phase | What | Days | Cumulative | Status |
|---|---|---|---|---|
| **Phase 0** | Local environment setup & orchestration (stop/clean/start/AppHosts) | 1–2 | 1–2 days | ✅ Covered by prompts |
| **Phase 1** | Fix config + write tools (send, purge, S3 upload, LocalStack setup) | 3–4 | 4–6 days | ✅ Complete |
| **Phase 2** | End-to-end trace (polling) — ~~worker control dropped~~ | 2 | 6–8 days | ✅ Complete (2A+2C) |
| **Phase 3** | MCP Resources + Prompts (agent intelligence) | 2–3 | 8–11 days | 3A ⬜ / 3B ✅ |
| **Phase 4** | Test data generators + polish | 2 | 10–13 days | ✅ Complete |
| **Phase 5** | Log & Trace Access (autonomous debugging) | 2–4 | 12–17 days | ⬜ Not started |

Each phase is independently demoable and valuable.

---

## Demo Scenarios

### After Phase 1

> **"Initialize LocalStack for order processing and confirm all queues and buckets are healthy."**
>
> Agent: Runs setup tools → verifies queues and buckets → returns health report.

### After Phase 2

> **"Send a test express order and trace it through the full pipeline."**
>
> Agent: Sends test message → polls S3 for object → polls Hub queue for downstream message → polls MongoDB for final document → reports step-by-step trace with timings.

### After Phase 3

> **"Run an end-to-end trace for a standard order and show me each step."**
>
> Agent: Enqueues test message → waits for Gateway Worker → confirms S3 object → waits for Hub queue → waits for MongoDB → returns step-by-step narrative with IDs and timings.

---

## Risks & Decisions

### ~~Key Risk: Worker Control (Phase 2B)~~ — Resolved (Dropped)

> **Decision (May 2, 2026):** Dropped entirely. Worker pause/resume required production code changes across 3 solutions (adding `IPauseService` + `PauseController` to each worker + modifying base `QueueMessageWorker<T>`). Trace/polling tools (Phase 2A) already provide full end-to-end visibility. For autonomous AI debugging, log/trace access (Phase 5) is the right tool — it shows *what happened inside the code*, not just *whether queue polling stopped*.

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

| # | Tool | Phase | Type | Status |
|---|---|---|---|---|
| 22 | `SendTestMessage` | 1 | Write | ✅ Done |
| 23 | `PurgeQueue` | 1 | Write | ✅ Done |
| 24 | `PurgeAllQueues` | 1 | Write | ✅ Done |
| 25 | ~~`CreateQueue`~~ | 1 | Write | ❌ Dropped (infra handles) |
| 26 | `UploadS3Object` | 1 | Write | ✅ Done |
| 27 | ~~`CreateS3Bucket`~~ | 1 | Write | ❌ Dropped (infra handles) |
| 28 | `CheckLocalStackHealth` | 1 | Read | ✅ Done |
| 29 | ~~`SetupLocalStackEnvironment`~~ | 1 | Write | ❌ Dropped (docker-compose) |
| 30 | ~~`TeardownLocalStackEnvironment`~~ | 1 | Write | ❌ Dropped (docker-compose) |
| 31 | `WaitForS3Object` | 2 | Read (poll) | ✅ Done |
| 32 | `WaitForQueueMessage` | 2 | Read (poll) | ✅ Done |
| 33 | `WaitForMongoDocument` | 2 | Read (poll) | ✅ Done |
| 34 | `GetAllQueueDepths` | 2 | Read | ✅ Done |
| 35 | ~~`PauseWorker`~~ | ~~2~~ | ~~Write~~ | ❌ Dropped (2B removed) |
| 36 | ~~`ResumeWorker`~~ | ~~2~~ | ~~Write~~ | ❌ Dropped (2B removed) |
| 37 | ~~`GetWorkerStatus`~~ | ~~2~~ | ~~Read~~ | ❌ Dropped (2B removed) |
| 38 | `GenerateTestOrders` | 4 | Read | ✅ Done |
| 39 | `GenerateAndSendOrders` | 4 | Write | ✅ Done |
| 40 | `RunScenarioBatch` | 4 | Write | ⬜ Nice-to-have |
| 41 | `GetServiceLogs` | 5 | Read | ⬜ Planned |
| 42 | `GetDistributedTrace` | 5 | Read | ⬜ Planned |
| 43 | `GetErrorSummary` | 5 | Read | ⬜ Planned |
| 44 | `SearchLogs` | 5 | Read | ⬜ Planned |

**Total: 33 tools built (21 existing + 12 new), 7 dropped, 5 planned**

### MCP Prompts (Phase 3B)

| # | Prompt | Template File | What It Does | Status |
|---|---|---|---|---|
| 1 | `setup-localstack` | `setup-localstack.md` | Full 7-step infra setup (stop/clean/start OrderHub & OrderGateway, verify) | ✅ Done |
| 2 | `build-and-run` | `build-and-run.md` | Build both solutions, start AppHosts, verify end-to-end | ✅ Done |
| 3 | `run-standard-orders` | `run-standard-orders.md` | Generate N standard orders, send, trace, summarize | ✅ Done |
| 4 | `run-express-orders` | `run-express-orders.md` | Generate N express orders, send, trace, summarize | ✅ Done |
| 5 | `end-to-end-trace` | `end-to-end-trace.md` | Single order full pipeline trace with 4-hop timing | ✅ Done |
| 6 | `tear-down` | `tear-down.md` | Kill app processes, stop/clean infra, verify shutdown | ✅ Done |

**Total: 7 prompts (6 template files, externalized to `Prompts/Templates/*.md`)**

---

## Progress Log

| Date | Phase | What Was Done |
|---|---|---|
| 2026-04-21 | Planning | Created implementation plan |
| 2026-04-22 | Planning | Added Phase 0 — Local Environment Setup & Orchestration |
| 2026-04-23 | Planning | Dropped CreateQueue, CreateBucket, Setup/Teardown tools; removed 3 unused queue pairs from docker-compose |
| 2026-04-23 | Phase 1A | Fixed IngestStandard queue name, added IngestExpress config entry |
| 2026-04-23 | Phase 1B | Implemented 3 queue write endpoints (send, purge, purge-all) + service methods |
| 2026-04-23 | Phase 1C | Implemented S3 upload endpoint + service method |
| 2026-04-23 | Phase 1D | Implemented HealthController with LocalStack SQS+S3 connectivity check |
| 2026-04-23 | Phase 1E | Built 5 MCP tools (SendTestMessage, PurgeQueue, PurgeAllQueues, UploadS3Object, CheckLocalStackHealth), client methods, DTOs |
| 2026-04-23 | Phase 1  | Added 15 unit tests for new Phase 1 code (queues: 7, s3: 4, health: 4) |
| 2026-04-23 | Phase 2A | Built ITraceService + TraceService with 4 polling methods (WaitForS3Object, WaitForQueueMessage, WaitForMongoDocument, GetAllQueueDepths) |
| 2026-04-23 | Phase 2A | Built TraceController with 4 endpoints + request/response models |
| 2026-04-23 | Phase 2C | Built 4 MCP TraceTools + client methods + DTOs; registered in MCP server |
| 2026-04-23 | Phase 2  | Added 7 unit tests for TraceController |
| 2026-04-23 | Testing  | All code compiles (0 errors, 0 warnings), 73 tests passing |
| 2026-04-29 | Phase 4A | Built ITestDataService + TestDataService — realistic order generation in gateway (base64 OrderEvent) and ingest (Shipment/Digital JSON) formats |
| 2026-04-29 | Phase 4A | Built TestDataController — POST /api/v1/test-data/generate-orders with priority, channelType, count, storeId, format params |
| 2026-04-29 | Phase 4A | Built 2 MCP tools: GenerateTestOrders (generate only) + GenerateAndSendOrders (generate + send to queue) |
| 2026-04-29 | Phase 3B | Built OrderPrompts class with 4 MCP prompts: setup-localstack, run-standard-orders, run-express-orders, end-to-end-trace |
| 2026-04-29 | Phase 3B | Registered TestDataTools + OrderPrompts in MCP Program.cs |
| 2026-04-29 | Testing  | Added 34 unit tests (9 TestDataController + 17 TestDataService + 8 theory variations). Total: 107 tests passing |
| 2026-05-02 | Phase 3B | Externalized all prompts from C# inline strings to `.md` template files with `{{placeholder}}` replacement |
| 2026-05-02 | Phase 3B | Created 3 new prompts: `build-and-run`, `tear-down`, expanded `setup-localstack` to full 7-step process |
| 2026-05-02 | Phase 4C | Updated both READMEs: prompt counts, template docs, architecture, "Other Things You Can Ask" categories |
| 2026-05-02 | Phase 4C | Fixed port alignment: launchSettings.json → 5100, mcp.json → 5100, README → 5100 (was 55701) |
| 2026-05-02 | Planning | Created `autonomous-ai-agent-todo.md` documenting 8 constraints + 7 priorities for future autonomous use |
| 2026-05-02 | Planning | Dropped Phase 2B (Worker Control) — trace/polling tools sufficient, avoids production code changes |
| 2026-05-02 | Planning | Added Phase 5: Log & Trace Access Tools for autonomous debugging (config-driven: Aspire local, CloudWatch/Splunk production) |

## What's Done vs. What's Left

### ✅ Complete (Demoable Agent)

- **Phase 1 (full)**: Config fix, queue write endpoints, S3 upload, health check, 5 MCP tools, 15 unit tests
- **Phase 2A+2C**: Trace/polling service with 4 methods, controller with 4 endpoints, 4 MCP trace tools, 7 unit tests
- **Phase 3B**: 7 MCP prompts (setup-localstack, build-and-run, run-standard-orders, run-express-orders, end-to-end-trace, tear-down) — externalized to `.md` template files
- **Phase 4A**: TestDataService + TestDataController + 2 MCP tools (GenerateTestOrders, GenerateAndSendOrders) + 34 unit tests
- **Phase 4C**: Both READMEs fully updated, port alignment, template documentation
- **Docker cleanup**: Removed 3 unused queue pairs from docker-compose.yml
- **Total MCP tools**: 33 (21 pre-existing + 12 new)
- **Total MCP prompts**: 7 (6 template files in `Prompts/Templates/`)
- **Total tests**: 107 (51 pre-existing + 56 new)

### ⬜ Next Up

| Item | Phase | Effort | Value |
|---|---|---|---|
| **Phase 3A: MCP Resources** (system topology, queue health, recent orders) | 3 | ~1 day | Auto-loaded context for AI — every conversation starts informed |
| **Phase 5 Tier 1: Local Log Access** (Aspire/OpenTelemetry/Docker logs) | 5 | ~1-2 days | AI can read worker logs and stack traces for autonomous debugging |

### ⬜ Future / Nice-to-Have

| Item | Phase | Effort | Value |
|---|---|---|---|
| Phase 4B: Batch test scenarios (RunScenarioBatch) | 4 | ~0.5 day | Run multiple scenarios in sequence, report as table |
| Phase 5 Tier 2: Production log access (CloudWatch/Splunk/AppInsights) | 5 | ~2-3 days | Order-aware log queries for production environments |
| Phase 6: CI/CD integration | 6 | ~3 days | GitHub Actions hooks — separate concern |
| TraceService unit tests | 2 | ~0.5 day | Tests for the polling logic itself (currently tested at controller level) |

### ❌ Dropped

| Item | Phase | Reason |
|---|---|---|
| ~~Phase 2B: Worker Control (Pause/Resume/Status)~~ | 2 | Requires production code changes across 3 solutions; trace/polling tools sufficient; replaced by Phase 5 log access |
| ~~CreateQueue, CreateBucket, Setup/Teardown tools~~ | 1 | Infrastructure scripts (docker-compose) handle this |


-----
## Marketplace Comparison

This section compares our MCP server to Cindy's Plugin Marketplaces (`C:\Work\Copilot-Marketplace` and `C:\Work\Claude-Marketplace`) for future reference.

### What the Marketplaces Provide

Both marketplaces are **prompt engineering frameworks** — collections of markdown-based skills and agent personas.

| | Copilot-Marketplace | Claude-Marketplace |
|---|---|---|
| **Plugins** | 3 (spec-driven-skills, delivery-team, rims-dev-tools) | 5 (workflow-automation, dealer-persona, delivery-team, support-engineering, rims-dev-tools) |
| **Skills** | 20 | ~67 |
| **Agents** | Sub-agents within skills | 62 standalone agent personas |
| **How they work** | AI reads `SKILL.md` → follows structured instructions | Same |
| **What they produce** | PRDs, architecture docs, code, test strategies, reviews | Same, plus persona-driven reviews, support triage, capacity tracking |

### What Ours Provides (Different Layer)

| | Marketplace Skills | Our MCP Server |
|---|---|---|
| **Category** | Instructed Intelligence — teaches the AI what to think | Tooled Intelligence — gives the AI the ability to act |
| **Artifact** | Markdown files (`SKILL.md`) | .NET 8 REST API + MCP tool server |
| **Runs as** | Text read by AI at conversation start | Two processes: API on port 5100, MCP server over stdio |
| **Mechanism** | AI reads instructions, follows them | AI discovers tools, calls them via MCP protocol |
| **Domain** | Generic software development process | Order processing pipeline (SQS, S3, MongoDB, LocalStack) |
| **Without the AI** | Useless — just text files | Still works — callable REST API |
| **With the AI** | Makes AI behave like a specialist (architect, security auditor, etc.) | Gives AI ability to send messages, poll queues, query databases |

### How They're Complementary

```
┌─────────────────────────────────────────────────────────────┐
│  Marketplace Skills (Claude/Copilot)                        │
│  "Create a PRD for adding order validation"                 │
│  "Architect the new retry handler"                          │
│  "Generate tests for the validation rule"                   │
│                                                             │
│  → Teaches AI HOW to develop software                       │
├─────────────────────────────────────────────────────────────┤
│  Our MCP Agent (Order.MessageOperations)                    │
│  "Send test order to IncomingOrders queue"                  │
│  "Wait for it in MongoDB"                                   │
│  "Check all queue depths"                                   │
│                                                             │
│  → Gives AI HANDS to operate the system                     │
└─────────────────────────────────────────────────────────────┘
```

### The Combined Vision

With both layers active, you could say:

> *"Add a new order validation rule that rejects orders with missing customer IDs, implement it, and verify it works end-to-end"*

- **Marketplace skills** handle: requirements → architecture → code generation → test writing → PR creation
- **Our MCP tools** handle: send a test order with missing customer ID → trace it → confirm rejection → send a valid order → confirm it succeeded

### Key Terminology

| Term | What It Means |
|---|---|
| **MCP Server** | Our project — a .NET application exposing tools over the Model Context Protocol |
| **MCP Tool** | A function the AI can call (e.g., `SendTestMessage`, `WaitForS3Object`) |
| **MCP Resource** | Auto-loaded context the AI reads (planned Phase 3A — e.g., system topology) |
| **MCP Prompt** | A scenario template the AI follows (planned Phase 3B — e.g., "end-to-end-trace") |
| **Skill** | A markdown instruction set (what the marketplaces provide) |
| **Agent (marketplace)** | A markdown persona definition — not a running process |
| **Agent (ours)** | The AI + our MCP tools combined — a system that can reason AND act |

Neither marketplace has anything for order processing, SQS, S3, or LocalStack. They don't overlap with what we're building — they sit above it as a complementary layer.
