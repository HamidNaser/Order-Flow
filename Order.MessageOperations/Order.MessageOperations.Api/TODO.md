# Order.MessageOperations - TODO

> Last Updated: April 15, 2026

---

## Completed ✅

### Phase 1: API Project Setup
- [x] Create `Order.MessageOperations.Api` project scaffold
- [x] Configure `appsettings.json` with LocalStack endpoints and queue mappings
- [x] Create typed configuration (`MessageOperationsOptions.cs`)
- [x] Set up dependency injection in `Program.cs`

### Phase 2: Models
- [x] Create `SavedMessage.cs` - message and batch DTOs
- [x] Create `OperationRequests.cs` - request DTOs for POST endpoints
- [x] Create S3 DTOs (bucket info, object info, content response)

### Phase 3: Services
- [x] Port `MessageStorageService.cs` from ReplayConsole
- [x] Port `QueueReplayService.cs` from DlqReplayService
- [x] Create `S3OperationsService.cs` with list/content/sync operations
- [x] Fix nullable DateTime/long conversions for AWS SDK v4

### Phase 4: Controllers
- [x] Create `QueuesController.cs` - queue listing, status, peek
- [x] Create `BatchesController.cs` - batch listing, manifest, messages
- [x] Create `ReplayController.cs` - download, replay, download-and-replay
- [x] Create `S3Controller.cs` - buckets, objects, metadata, content, sync

### Phase 5: Integration
- [x] Add project to solution (`dotnet sln add`)
- [x] Verify build succeeds
- [x] Create comprehensive README.md documentation

### Phase 6: MCP Server Project
- [x] Create `Order.MessageOperations.Mcp` project scaffold
- [x] Add MCP SDK dependencies (ModelContextProtocol 0.2.0-preview.1)
- [x] Create typed HTTP client (`MessageOperationsClient.cs`)
- [x] Configure `IHttpClientFactory` with base URL and timeouts
- [x] Create configuration options class (`McpServerOptions.cs`)

### Phase 7: MCP Tool Implementation
- [x] Create `QueueTools.cs`:
  - [x] `ListConfiguredQueues` tool
  - [x] `ListLocalStackQueues` tool
  - [x] `GetQueueStatus` tool
  - [x] `PeekQueueMessages` tool

- [x] Create `BatchTools.cs`:
  - [x] `ListBatches` tool
  - [x] `GetBatchDetails` tool
  - [x] `GetBatchMessages` tool

- [x] Create `ReplayTools.cs`:
  - [x] `DownloadMessages` tool
  - [x] `ReplayFromBatch` tool
  - [x] `DownloadAndReplay` tool

- [x] Create `S3Tools.cs`:
  - [x] `ListS3Buckets` tool
  - [x] `ListS3Objects` tool
  - [x] `GetS3ObjectMetadata` tool
  - [x] `GetS3ObjectContent` tool
  - [x] `SyncS3FromBatch` tool

### Phase 8: MCP Server Setup
- [x] Create `Program.cs` with MCP server bootstrap
- [x] Register all tools with the MCP server using DI
- [x] Configure stdio transport
- [x] Add proper error handling and logging
- [x] Build succeeds

---

## In Progress 🚧

*(none)*

---

## Completed - Orders Database ✅

### Phase 11: Orders Read-Only Database Layer
- [x] Add MongoDB.Driver (v3.4.0) package to API project
- [x] Create read-only DTOs (`OrderModels.cs`) decoupled from OrderHub entities
- [x] Create `OrderQueryService` with lightweight BSON-mapped document classes
- [x] Implement query methods: GetById, GetByConsumer, CountByConsumer, Search, GetSummary, FindByProvider, GetRecent
- [x] Create `OrdersController` (7 GET endpoints under `/api/v1/communications`)
- [x] Add MongoDB connection string to `appsettings.json` (LocalStack default: `mongodb://127.0.0.1:27018`)
- [x] Register `IMongoClient` and `OrderQueryService` in DI
- [x] Create `OrderTools.cs` MCP tools (6 tools)
- [x] Add order client methods to `MessageOperationsClient`
- [x] Register `OrderTools` in MCP server
- [x] Build succeeds (both projects)

---

## Completed - Testing & Deployment ✅

### Phase 9: Testing & Validation
- [x] Start API and verify Swagger UI works (`http://localhost:5100/swagger`)
- [x] Test queue listing against LocalStack (6 queues found)
- [x] Test queue status endpoint (returns attributes)
- [x] Test batch operations (returns empty array - no batches yet)
- [x] Test S3 operations (3 buckets found)
- [x] Create `.vscode/mcp.json` for VS Code integration

### Phase 10: Documentation & Deployment
- [x] Update README.md with MCP project structure
- [x] Create `.vscode/mcp.json` for VS Code integration

---

## TODO 📋

### Optional Enhancements
- [ ] Test MCP server startup manually
- [ ] Verify tools appear in Copilot/Claude
- [ ] Add AppHost integration (optional)
- [ ] Document any AWS credential requirements
- [ ] Test communications endpoints against LocalStack MongoDB
- [ ] Verify communications MCP tools work end-to-end
- [ ] Add DocumentDB/AWS MongoDB connection string for deployed environments

---

## Future Enhancements 🔮

- [ ] Add health check endpoint for MCP server
- [ ] Add message filtering by content/attributes
- [ ] Add batch deletion endpoint
- [ ] Add queue purge endpoint (with confirmation)
- [ ] Add message count summary across all queues
- [ ] Add S3 object upload capability
- [ ] Add WebSocket support for real-time queue monitoring
- [ ] Add metrics/telemetry
- [ ] Add rate limiting for API calls

---

## Known Issues ⚠️

1. **Build Warnings**: 4 CS0618 deprecation warnings for `FallbackCredentialsFactory` and `AttributeNames` - these are AWS SDK v4 changes, can be updated later
2. **File Lock Issues**: If build fails with MSB3027, ensure no other processes are running the OrderGateway

---

## Notes 📝

- API runs on `http://localhost:5100`
- LocalStack SQS endpoint: `http://sqs.us-east-1.localhost.localstack.cloud:4566`
- LocalStack S3 endpoint: `http://localhost:4566`
- Message batches stored in `downloaded-messages/` directory
- No authentication required (internal tooling only)
