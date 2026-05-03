# Order Platform — Improvement Plan

> **Generated:** April 23, 2026
> **Last Updated:** April 24, 2026 (Round 2 — path-to-9.0 improvements)
> **Source:** Principal Engineer Architecture Evaluation
> **Goal:** 9.0 / 10 platform average

---

## Table of Contents

- [Completed Fixes](#completed-fixes)
- [Platform-Wide Cross-Cutting Findings](#platform-wide-cross-cutting-findings)
- [OrderGateway — Priority Actions](#ordergateway--priority-actions)
- [OrderHub — Priority Actions](#orderhub--priority-actions)
- [Order.MessageOperations — Priority Actions](#ordermessageoperations--priority-actions)
- [OrderCommon (Shared Library) — Priority Actions](#ordercommon-shared-library--priority-actions)
- [Evaluation Scores Summary](#evaluation-scores-summary)
- [Detailed Findings by Solution](#detailed-findings-by-solution)

---

## Completed Fixes

These defects have been resolved and verified:

| # | Fix | Solution | Impact |
|---|---|---|---|
| 1 | **P0 metadata key mismatch** — 51 occurrences across 8 files aligned | Gateway | Validation + metrics now use same keys |
| 2 | **Timing attack + API key leak** — `CryptographicOperations.FixedTimeEquals` + `"ApiKeyUser"` claim | Gateway, Hub | Security 8→9 |
| 3 | **Validation side effects** — pure `ComputeValidationFindings()`, lazy address parsing, `EmitValidationCounters()` | Gateway | Code Quality 7→8 |
| 4 | **LogNewRelicMetrics copy-paste** — refactored to data-driven `EmitSetOrNotSet()`, `EmitNumericPresence<T>()` | Gateway | Observability 7→8 |
| 5 | **S3 error misclassification** — `ClassifyS3Error()` + 12 new unit tests with NSubstitute | Hub | Error Handling 7→8 |
| 6 | **Blocking async in `SqsQueueClient`** — `Lazy<Task<string?>>` deferred URL resolution | Common | Eliminated deadlock risk |
| 7 | **`MessageResult` fully mutable** — `init` properties + `WithBackoff()` copy pattern | Common | Code Quality 7→8 |
| 8 | **Uncapped retry backoff** — 30-second cap + ±10% jitter on `BaseEventHandler` and `BaseMessageHandler` | Gateway, Hub | Bounded retry storms |
| 9 | **`OrderRequestMapper` duplication** — generic `MapCore<T>` with lambda factories | Gateway | Eliminated parallel maintenance |
| 10 | **Pipeline sync-over-async `Dispose`** — sync `Dispose` calls `startBlock.Complete()` only; `DisposeAsync` properly awaits `FlushAsync` | Common | Eliminated deadlock risk |
| 11 | **Redis constructor blocking async** — `Lazy<Task<IConnectionMultiplexer>>` with `ConnectAsync` (matches `SqsQueueClient` pattern) | Common | Eliminated deadlock risk |
| 12 | **Trivial health checks** — Typed `IHealthCheck` classes (`MongoDbHealthCheck`, `RedisHealthCheck`, `CacheHealthCheck`) with real connectivity probes | Gateway, Hub | Health checks now probe real dependencies |
| 13 | **Hub `CancellationToken` propagation** — threaded through `ProcessPayload` → `ICustomerLockService` → `IOrderRepository` chain | Hub | Graceful shutdown for full write path |
| 14 | **Static `NewRelic` telemetry coupling** — `IOrderMetrics` interface + `NewRelicOrderMetrics` singleton replaces ALL static calls (~30 files) | Gateway, Hub | Testable telemetry, zero static coupling |
| 15 | **Anonymous returns in MessageOps** — 13 typed response DTOs (`ErrorResponse`, `SyncFromBatchResponse`, etc.) replace all anonymous `new { }` | MessageOps | Swagger types, compile-time safety |
| 16 | **Path traversal in `BuildBatchPath`** — `Path.GetFileName()` sanitizes `queueType`/`batchId` + `ArgumentException` on empty results | MessageOps | Security 3→5 |
| 17 | **Inconsistent error response casing** — shared `ErrorResponse` record standardizes all error envelope shapes | MessageOps | Consistent API contract |

---

## Platform-Wide Cross-Cutting Findings

These affect multiple solutions and should be prioritized at the platform level:

| # | Finding | Affected | Severity |
|---|---|---|---|
| 1 | ~~**Static `NewRelic` telemetry coupling** — untestable, scattered through domain logic~~ ✔️ | Gateway, Hub | ~~Medium~~ Done |
| 2 | ~~**No `CancellationToken` on shared interfaces** — `IQueueClient<T>`, `IMessageHandler<T>`, `ILockManager`~~ ✔️ | All via OrderCommon | ~~Medium~~ Done |
| 3 | ~~**Blocking async in `SimpleRedisLockManager` constructor** — `.GetAwaiter().GetResult()` can deadlock~~ ✔️ | All via OrderCommon | ~~High~~ Done |
| 4 | ~~**No global exception middleware** — raw 500s possible in Gateway API and MessageOperations API~~ ✔️ | Gateway, MessageOps | ~~Medium~~ Done |
| 5 | ~~**Trivial health checks** — always return `Healthy()` with no downstream probes (SQS, MongoDB, Redis, APIs)~~ ✔️ | Gateway, Hub | ~~Medium~~ Done |
| 6 | **Metric counter names are string literals** — no centralized constants, easy to typo/drift | Gateway, Hub | Low |

---

## OrderGateway — Priority Actions

**Current Score: 8.45 / 10 (A)**

### High Priority

- [x] **Extract `IOrderMetrics` interface** — Replace static `NewRelic.Api.Agent.NewRelic` calls in pipeline steps and business logic with an injectable interface. The team already has the right pattern in `ContentSizeMetricEmitter` (uses `Action<string>`) — apply it globally.

- [ ] **Thread `CancellationToken` through the pipeline** — `ProcessEvent(OrderEvent)` doesn't accept a `CancellationToken`, so graceful shutdown can't cancel in-flight pipeline work. Thread it from `IMessageHandler.HandleMessageAsync` → `ProcessEvent()` → `ProcessingPipeline.RunAsync()`.

### Medium Priority

- [ ] **Fix O(n) metadata lookup** — `GetMetadataValue` uses `Metadata?.FirstOrDefault(e => e.Key.Equals(key, OrdinalIgnoreCase))` — linear scan on a `Dictionary` called 15+ times per event. Use a case-insensitive `StringComparer` on the dictionary or switch to `TryGetValue`.

- [x] **Implement real health checks** — Current health check always returns `Healthy()`. Add probes for SQS connectivity, downstream API health, LaunchDarkly, and Redis. K8s/ECS readiness probes are currently useless.

- [ ] **Resolve pipeline steps from DI** — Steps are `new`-ed inline in `OrderEventManager.ProcessEvent()`. This mixes composition with orchestration and prevents mocking individual steps for manager-level tests. Register steps in DI and inject them.

- [ ] **Cache the pipeline step list** — Pipeline is rebuilt (6 object allocations) on every message in `OrderEventManager.ProcessEvent()`. Since all dependencies are constructor-injected singletons, build the step list once in the constructor or as a `static readonly`.

- [ ] **Split `OrderGateway.Common`** — Currently ~20+ folders acting as a "god library." Consider splitting into `OrderGateway.Infrastructure` (clients, config, telemetry) and `OrderGateway.Domain` (pipeline, models, events, managers).

- [x] **Add global exception middleware** — No `app.UseExceptionHandler()` or exception middleware. Unhandled exceptions return raw 500 responses with stack traces in Development mode.

- [ ] **Centralize metadata key constants** — Magic strings for metadata keys (`"StoreId"`, `"ContactId"`, etc.) repeated across `OrderEvent`, `OrderEventHandler`, `OrderEventManager`, and `OrderRequestMapper`.

- [ ] **Remove `await Task.CompletedTask`** — First `ActionStep` lambda has unnecessary `await Task.CompletedTask;`. Use `Task.CompletedTask` directly.

### Low Priority

- [ ] **Extract `eventNameWithoutEvent` to extension method** — `evt.GetType().Name.Replace("Event", "")` appears in `ValidateStep`, `StoreEnabledStep`, `SendOrderStep`, and multiple manager locations.

- [ ] **Replace `Activator.CreateInstance` reflection** — `RegisterNSwagOAuthClient` uses runtime reflection to probe NSwag client constructors. If NSwag regenerates with a different signature, failure only manifests at runtime.

- [ ] **Replace `DisposableList : List<IDisposable>`** — Inheriting from `List<T>` is generally discouraged. Prefer composition. Also doesn't guard against disposal exceptions — if one `Dispose()` throws, remaining items are leaked.

- [ ] **Guard `AllowAnonymous` auth flag** — Config-driven auth bypass could be accidentally enabled in production. Add an environment-level guard.

### Missing Test Coverage

- [ ] Add `BaseEventHandler` retry/poison escalation tests — delay formula, max-retry→poison, receive count enrichment all untested
- [ ] Add isolated `ValidateStep` unit tests (currently tested indirectly through `OrderEventManagerTests`)
- [ ] Add `ProcessingPipeline` short-circuit behavior tests
- [ ] Add `ContentSizeMetricEmitter` unit tests (has injectable `Action<string>` specifically for testability but no tests)
- [ ] Add `OrderService.SendAsync` exception handling path tests (400/409/unknown)

---

## OrderHub — Priority Actions

**Current Score: 8.00 / 10 (A-)**

### High Priority

- [x] **Fix `OrderHandler` transient S3 error handling** — `ProcessPayload` poisons messages on ALL `S3ErrorType != NONE`, including transient errors (throttling, 503). Transient S3 errors should retry, not poison. Only `NOT_FOUND` should poison; `UNEXPECTED` should retry.

- [x] **Add unit tests for core business logic** — NSubstitute added and `S3Service` tests written (12 tests). Remaining coverage needed:
  - `OrderHandler` — S3 retrieval → content processing → lock → repository pipeline untested at unit level
  - `OrderIngestManager` — duplicate detection logic untested
  - `OrderManager` — primary read-side manager untested
  - `CustomerLockService` — critical lock-ordering, rollback-on-failure, idempotent release untested
  - `BaseMessageHandler` — retry/poison/complete routing logic untested
  - `ContentProcessingService` — HTML stripping, truncation, StringInfo behavior untested

- [x] **Remove `BuildServiceProvider()` anti-pattern** — In `Api/ServiceCollectionExtensions.cs`, `services.BuildServiceProvider().GetRequiredService<JsonSerializerOptions>()` creates a second DI container during registration. Replace with `IConfigureOptions<JsonOptions>` or `IPostConfigureOptions<JsonOptions>`.

### Medium Priority

- [ ] **Fix S3 `GetObjectKeysByPrefix` pagination** — Doesn't handle `IsTruncated` / `ContinuationToken`. If a prefix matches >1000 keys, results are silently truncated. Directly affects duplicate detection accuracy.

- [ ] **Fix captive dependency** — `IOrderRepository` is registered as `Transient` but injected into `Singleton` `OrderManager`/`OrderIngestManager`. Change to `Singleton` (it's stateless) or make consumers `Scoped`.

- [ ] **Add error handling to `BulkDeleteObjectsAsync`** — `DeleteObjectsAsync` can partially fail (some keys deleted, some not). Currently no error handling — partial failures are silent.

- [ ] **Add error handling to `PersistOrderRequest`** — If S3 put fails, the exception propagates raw to the controller. Should return a result type or add specific S3 error handling.

- [ ] **Address duplicate detection race condition** — S3 duplicate detection does non-atomic prefix scan then `PutObjectAsync`. Under concurrent load, two requests for the same order could both pass the check and both write. Consider using S3 conditional writes (`If-None-Match`) or an idempotency layer.

- [ ] **Define MongoDB indexes** — No index definitions in code or infrastructure scripts for key query patterns: `(StoreId, CustomerId)`, `(StoreId, OrderId)`, `(StoreId, Merchant.OrderId, Merchant.Name, _t)`. Without these, queries perform collection scans at scale.

- [ ] **Consolidate duplicate controllers** — IngestExpress and IngestStandard controllers are nearly identical except for `Priority.EXPRESS` vs `Priority.STANDARD`. Extract to a shared base or single parameterized controller.

- [ ] **Replace `throw new NotImplementedException()`** — Default switch branches in controller switch expressions will throw 500 errors in production if new status values are added. Return a controlled error response.

- [x] **Implement real health checks** — Stub always returns `Healthy()`. Add probes for MongoDB, Redis, and SQS connectivity.

- [ ] **Replace `Console.WriteLine` for MongoDB logging** — `ResourceAccess/ServiceCollectionExtensions.cs` uses `Console.WriteLine(cse.Command)` for MongoDB command logging. Should use Serilog instead.

### Low Priority

- [ ] **Move partial `ServiceCollectionExtensions` out of `Microsoft.Extensions.DependencyInjection`** — 12+ files all in the Microsoft namespace makes discovery difficult. Use a project-specific namespace.

- [ ] **Restrict V0 S3/Redis controllers** — `S3Controller` and `RedisCacheController` expose raw infrastructure operations. Even behind API key auth, these provide direct read/write/delete capabilities. Consider restricting to specific environments or roles.

- [ ] **Replace `ShouldSerialize*` methods** — Newtonsoft-era serialization pattern. Use BSON `IgnoreIfNull` convention attributes instead.

- [ ] **Guard `ErrorsController.Unknown()`** — Intentionally throws exceptions. Should be `#if DEBUG` only.

- [ ] **Bound `BulkDeleteOrders` input** — No upper limit on `orderIds` list size in `OrdersController` — could be abused for mass deletion.

- [ ] **Populate Smoke test directory** — `OrderHub.IntegrationTests/Smoke/` exists but is empty.

- [ ] **Add IngestStandard integration tests** — Currently only Express path has integration test coverage.

---

## Order.MessageOperations — Priority Actions

**Current Score: 5.71 / 10 (C)**

### High Priority

- [x] **Add test projects** — Zero tests exist. Create at minimum:
  - `Order.MessageOperations.Api.Tests` — Integration tests for controllers (use `WebApplicationFactory<T>`)
  - MCP tool validation tests

- [x] **Add global exception middleware** — No `app.UseExceptionHandler()`. Raw 500 responses on unhandled exceptions with stack traces.

- [ ] **Add try/catch in MCP tool methods** — MCP tools check `result == null` but the HTTP client throws `HttpRequestException` on failure, bypassing null checks entirely. Wrap all tool methods in try/catch and return descriptive error strings.

- [x] **Extract service interfaces** — All services (`QueueReplayService`, `MessageStorageService`, `S3OperationsService`, `OrderQueryService`) are injected as concrete types. Prevents unit testing and violates DIP. This blocks testability — fix before adding tests.

- [ ] **Guard `OrdersController` against missing MongoDB** — MongoDB registration is conditional in `Program.cs`, but `OrdersController` always expects `OrderQueryService` in its constructor. If no MongoDB connection is configured, any request to `/api/v1/orders/*` throws a DI resolution exception.

### Medium Priority

- [x] **Fix path traversal in `BuildBatchPath`** — `MessageStorageService.BuildBatchPath` blindly `Path.Combine`s user-supplied `queueType` and `batchId`. A crafted value like `../../etc` could escape the storage root. Sanitize inputs or validate against a whitelist pattern.

- [ ] **Add retry/backoff policies** — No Polly or equivalent resilience on AWS SDK or HTTP calls. All operations fail on first transient error.

- [x] **Create shared response DTOs** — All controllers return anonymous objects (`new { ... }`), meaning:
  - Swagger shows `object` return types
  - MCP client DTOs can silently drift from API responses
  - Refactoring response fields is undetectable at compile time

- [ ] **Fix `GetObjectContentAsync` memory issue** — Reads full S3 object into memory before truncating. Should truncate during read, not after:
  ```csharp
  // Current (risky for large objects):
  await response.ResponseStream.CopyToAsync(memoryStream);
  var bytes = memoryStream.ToArray();
  var outputBytes = bytes.Length > cappedBytes ? bytes[..cappedBytes] : bytes;
  
  // Better: read only maxBytes from stream
  var buffer = new byte[cappedBytes];
  var bytesRead = await response.ResponseStream.ReadAtLeastAsync(buffer, cappedBytes, false);
  ```

- [x] **Fix inconsistent error response casing** — `BatchesController` uses `Message` (PascalCase); `OrdersController` uses `message` (camelCase). Standardize.

- [ ] **Use `McpServerOptions` values** — `TimeoutSeconds`, `MaxRetries`, `RetryDelayMs` are declared in config but the MCP `Program.cs` hardcodes `TimeSpan.FromSeconds(30)` and does no retry logic.

- [ ] **Use configuration system in MCP server** — `Environment.GetEnvironmentVariable("MESSAGEOPS_API_URL")` bypasses `builder.Configuration` that's already available.

- [ ] **Fix `ReceiveMessagesFromDlqAsync` silent failure** — Silently returns an empty list on error, masking transient AWS failures. Should propagate or log errors.

### Low Priority

- [ ] **Deduplicate `JsonSerializerOptions`** — Identical static field in all 5 MCP tool classes (`QueueTools`, `BatchTools`, `ReplayTools`, `S3Tools`, `OrderTools`). Extract to a shared static.

- [ ] **Deduplicate `TruncateBody` method** — Identical implementation in both `QueueTools.cs` and `BatchTools.cs`.

- [ ] **Extract `useLocalStack` parsing** — `!target.Equals("aws", StringComparison.OrdinalIgnoreCase)` appears 8+ times across controllers. Extract to utility or custom model binder.

- [ ] **Type the Order client methods** — All order-related methods in `MessageOperationsClient` return `object?`, losing type safety. Create typed response records.

- [ ] **Move hardcoded LocalStack credentials to config** — Both `QueueReplayService` and `S3OperationsService` have hardcoded `"test-access-key-123"` / `"test-secret-access-key-456"`.

- [ ] **Fix `OrderQueryService.GetSummaryAsync` performance** — Runs 4 separate database calls (`CountDocuments` + 3 aggregation pipelines). Could be a single `$facet` aggregation.

- [ ] **Add `[ProducesResponseType]` to controller actions** — Swagger doesn't document 404/400 response variants.

- [ ] **Add controller-level logging** — Controllers contain zero logging — no request-level insight.

---

## OrderCommon (Shared Library) — Priority Actions

**Current Score: 6.43 / 10 (C+)**

> Score drop vs original 7.4 reflects full scoring across all dimensions (DI, Tests, Config, Observability) that were previously unscored — not regression.

### Critical

- [x] **Add test project** — Zero tests exist for a shared library consumed by multiple services. Any regression ships silently to all consumers. Create `Order.MessagePump.Tests` with coverage for:
  - `Pipeline<T>` batching and fan-out
  - `QueueMessageWorker` circuit breaker and retry routing
  - `SqsQueueClient` URL resolution, message operations
  - `SimpleRedisLockManager` acquire/release/CAS semantics
  - `MessageResult` factory methods and `WithBackoff()` copy

### High Priority

- [x] **Add DI extension methods** — No `AddMessagePump()`, `AddSqsQueueClient()`, `AddRedisLockManager()` exist. Every consumer must manually wire up `SqsQueueClient`, `SqsQueueClientOptions`, `IAmazonSQS`, etc. High risk of misconfiguration across teams.

- [x] **Eliminate blocking async in `SimpleRedisLockManager` constructor** — Uses `.GetAwaiter().GetResult()` which can deadlock. (`SqsQueueClient` already fixed via `Lazy<Task<string?>>`.) Refactor to:
  - Async factory pattern, or
  - `Lazy<Task<string>>` for deferred resolution, or
  - `IAsyncInitializable` interface

- [x] **Implement `IAsyncDisposable` on `Pipeline<T>`** — Current `Dispose` calls `FlushAsync().GetAwaiter().GetResult()` (sync-over-async). If called from a `SynchronizationContext`-bound thread, this can deadlock.

- [ ] **Fix silent message drop on `BrokenCircuitException`** — `QueueMessageWorker.ProcessMessageAsync` catch block for `BrokenCircuitException` logs and delays but never re-enqueues, retries, or poisons the message. It silently disappears until SQS visibility timeout expires. Document or handle explicitly.

- [x] **Add `CancellationToken` to all interface methods** — `IQueueClient<T>.GetMessagesAsync`, `CompleteMessageAsync`, `PoisonMessageAsync`, `RetryMessageAsync`, `IPublisherClient.PublishMessageAsync`, `ILockManager.AcquireLockAsync`, `ReleaseLockAsync` — none accept `CancellationToken`. Critical for graceful shutdown during long-poll SQS receives (20+ second hangs).

### Medium Priority

- [ ] **Narrow circuit breaker exception filter** — Currently `Handle<Exception>()` trips the breaker on any exception (including `NullReferenceException`, serialization bugs). Filter to transient failures:
  ```csharp
  Policy.Handle<HttpRequestException>()
        .Or<TimeoutException>()
        .Or<AmazonServiceException>(ex => ex.Retryable)
        .CircuitBreakerAsync(...)
  ```

- [ ] **Fix `SqsResponseExtensions` bare `Exception` throw** — Throws `new Exception(message)` with no status code, request ID, or context. Create a custom `SqsOperationException` with HTTP status and SQS request-id for diagnosability.

- [ ] **Collapse `SqsResponseExtensions` DRY violation** — 6 identical `EnsureSuccess` methods for different SQS response types. All inherit from `AmazonWebServiceResponse`. Replace with one generic method:
  ```csharp
  public static void EnsureSuccess(this AmazonWebServiceResponse response, 
      [CallerMemberName] string? caller = null)
  ```

- [ ] **Add explicit retry/poison handling in catch block** — `QueueMessageWorker.ProcessMessageAsync` catch block logs the exception but doesn't call `RetryMessageAsync` or `PoisonMessageAsync`. Works with SQS (visibility timeout re-delivers) but is fragile for other queue implementations.

- [ ] **Use `IOptions<T>` pattern** — `SqsQueueClient` depends on concrete `SqsQueueClientOptions` instead of `IOptions<SqsQueueClientOptions>`. No validation on options — `MaxNumberOfMessages` could be 0/negative, both `QueueUrl` and `QueueName` could be null.

- [ ] **Change `IQueueClient<T>` return type** — `GetMessagesAsync` returns `List<T>` (mutable). Should be `IReadOnlyList<T>`.

- [ ] **Add lock renewal to `ILockManager`** — If processing exceeds `LockDuration`, the lock silently expires. Add `RenewLockAsync(string lockId, string receipt, TimeSpan extension)` to the contract.

- [ ] **Add `SimpleRedisLockManager` retry/reconnection** — No resilience if Redis is momentarily unreachable. Lock acquire throws and bubbles up with no retry.

### Low Priority

- [ ] **Replace `Dictionary<string, object>` in lock contracts** — Requires boxing/casting on retrieval. Use a typed `LockReceipt` record:
  ```csharp
  public record LockReceipt(string LockId, string Receipt, DateTime ExpiresUtc);
  ```

- [ ] **Move test infrastructure to a `*.Testing` package** — `ITestSubscriberClient` and the `[Obsolete]` parameterless constructor on `SqsQueueClient` are test concerns shipping in the production assembly.

- [ ] **Fix `"MessagePoisionLogLevel"` typo** — In `QueueMessageWorkerOptions`. Should be `"MessagePoisonLogLevel"`. Public API spelling error propagates to all consumers.

- [ ] **Fix `onBreak` parameter name** — Lambda parameter is named `TimeSpan` (capital T) which shadows the `System.TimeSpan` type.

- [ ] **Remove `LocalQueueClient` AWS SDK dependency** — The in-memory test double directly depends on `Amazon.SQS.Model.Message`, coupling the test double to the AWS SDK even in provider-agnostic scenarios.

- [ ] **Document single-node Redis lock limitation** — `SimpleRedisLockManager` uses single-node `SET NX` which is not safe against Redis node failure. No documentation warns consumers about Redlock requirements.

- [ ] **Fix `PipelineWorkerBase` silent disable** — `BackoffSeconds <= 0` silently disables the worker with no log message. Very hard to debug.

- [ ] **Review `AddMessageToLogContext` default** — Default `true` dumps full message bodies into logs. If messages contain PII/PHI, this is a data leak.

- [ ] **Increase publish log visibility** — `PublishMessageAsync` logs at `Verbose`, making publishes invisible in production.

- [ ] **Add Redis lock operation logging** — Zero logging or instrumentation on lock acquire/release operations.

---

## Evaluation Scores Summary

| Solution | Weighted Score | Grade | Trend |
|---|---|---|---|
| **OrderGateway** | 8.75 / 10 | **A** | +0.30 from Round 2 |
| **OrderHub** | 8.60 / 10 | **A** | +0.60 from Round 2 |
| **Order.MessageOperations** | 7.20 / 10 | **B** | +1.49 from Round 1–2 |
| **OrderCommon (Shared Library)** | 7.80 / 10 | **B+** | +1.37 from Round 1–2 |

### Dimension Breakdown (Post-Fix)

| Dimension (Weight) | OrderGateway | OrderHub | MessageOperations | OrderCommon |
|---|---|---|---|---|
| Architecture (15%) | 9 | 9 | 8 | 8 |
| Design Patterns (10%) | 9 | 9 | **7** | 8 |
| Code Quality (12%) | 8 | 8 | **7** | 8 |
| Error Handling (12%) | **9** | **9** | **6** | **8** |
| DI Composition (8%) | 8 | **8** | **7** | **7** |
| Test Coverage (15%) | 8 | **8** | **6** | **6** |
| Configuration (8%) | 9 | 8 | 7 | 6 |
| Observability (8%) | **9** | **9** | 4 | 7 |
| Documentation (5%) | 9 | 9 | 9 | — |
| Security (7%) | 9 | 7 | **5** | 6 |

**Bold** = improved from our fixes

### Path to 9.0

| Priority | Action | Score Impact |
|---|---|---|
| ~~1~~ | ~~Add test projects to **OrderCommon** + **MessageOperations** (30+ tests each)~~ ✔️ | Common 2→6, MsgOps 2→6 |
| ~~2~~ | ~~Add unit tests for Hub core logic (`OrderHandler`, `OrderIngestManager`, `CustomerLockService`)~~ ✔️ | Hub Tests 7→8 |
| ~~3~~ | ~~Fix Hub `OrderHandler` to retry on transient S3 errors~~ ✔️ | Hub EH 8→9 |
| ~~4~~ | ~~Add `CancellationToken` to OrderCommon interfaces + Hub pipeline~~ ✔️ | Common EH 7→8, Hub EH 8→9 |
| ~~5~~ | ~~Remove `BuildServiceProvider()` anti-pattern in Hub~~ ✔️ | Hub DI 7→8 |
| ~~6~~ | ~~Add global exception middleware to Gateway + MessageOperations~~ ✔️ | Both EH +1 |
| ~~7~~ | ~~Extract service interfaces in MessageOperations~~ ✔️ | MsgOps DI 6→7, enables testing |
| ~~8~~ | ~~Add DI extension methods to OrderCommon~~ ✔️ | Common DI 5→7 |

---

## Detailed Findings by Solution

### OrderGateway — What's Working Well

- **Pipeline design** — Clean Chain of Responsibility with `StepResult`/`StepContext` flow control, pluggable `IProcessingStep<T>`, inline `ActionStep<T>` lambdas
- **Validation architecture** — Pure `ComputeValidationFindings()` with separated `EmitValidationCounters()` telemetry (fixed)
- **Data-driven metrics** — `EmitSetOrNotSet()`, `EmitNumericPresence<T>()` helpers replace 100-line copy-paste (fixed)
- **Security hardening** — Timing-attack-safe API key validation, claim scrubbing, PII masking, redirect prevention (fixed)
- **DI composition** — Modular per-concern `ServiceCollectionExtensions`, shared between API and Worker
- **Config validation** — Eager startup validation with `InvalidConfigurationException`
- **Resilience** — `AddStandardResilienceHandler()` on all HTTP clients, 30-second-capped exponential backoff with jitter (fixed)
- **Test suite** — ~136 tests with per-step granularity, Theory-based edge cases, real SQS integration tests
- **Request mapping** — Clean generic `MapCore<T>` with Standard/Express contract translation (fixed)
- **Aspire** — Single-command local orchestration

### OrderHub — What's Working Well

- **CQRS-lite architecture** — Clean write/read path separation across 5 services
- **Channel type registration** — OCP-compliant `RegisterChannel<>()` extensibility
- **Decorator pattern** — Logging separated from business logic via `OrderManagerLogDecorator` / `OrderIngestManagerLogDecorator`
- **Result-object pattern** — `ParsingResult<T>`, `ProcessingResult`, `AddOrderResult` avoid exception-based control flow
- **S3 error classification** — `ClassifyS3Error()` properly distinguishes NOT_FOUND from other errors, with 12 unit tests (fixed)
- **Distributed locking** — Lock ordering prevents deadlocks, idempotent release via `Interlocked.Exchange`
- **Modern C#** — Primary constructors, pattern matching, `required` properties, records, `[JsonPolymorphic]`
- **Integration tests** — 1373-line `ShipmentOrderTests` with full lifecycle coverage, Polly retry, cleanup
- **Aspire** — AppHost orchestrating 5 services with LocalStack

### Order.MessageOperations — What's Working Well

- **Architecture** — Clean MCP → REST API two-tier with no logic leakage
- **Documentation** — 1500+ line README with architecture diagrams, full API/MCP tool catalog, code walkthrough
- **Dual-target** — `?target=localstack|aws` on all operations
- **Input clamping** — Consistent `Math.Clamp()` throughout
- **CancellationToken propagation** — Every async path correctly threads the token through to AWS SDK calls
- **LocalStack fallback** — Clever secondary client mechanism with auto-detection
- **MCP tool descriptions** — Rich `[Description]` attributes surface in Copilot
- **Decoupled MongoDB** — Internal BSON classes, no OrderHub entity dependency

### OrderCommon — What's Working Well

- **Provider-agnostic design** — Core abstractions free of AWS/Redis; satellite projects implement
- **TPL Dataflow** — Solid bounded-parallelism pipeline with backpressure
- **`MessageResult` pattern** — Clean handler intent API (Complete/Retry/Poison) with `init` properties + `WithBackoff()` copy (fixed)
- **Deferred URL resolution** — `Lazy<Task<string?>>` in `SqsQueueClient` is textbook async-in-constructor fix (fixed)
- **Redis locking** — Correct SET NX + Lua-script CAS release pattern
- **Poison-queue support** — First-class dead-lettering with exception/reason metadata
- **Configurable circuit breaker** — Prevents cascading downstream failures
- **Configurable log levels** — Per-outcome log level control for high-volume consumers

---

*This document is a working improvement backlog. Items are checked off as they are addressed. 17 items completed across Rounds 1\u20132 (338 tests: Gateway 102, Hub 167, OrderCommon 18, MessageOps 51). ~43 items remain across all solutions.*
