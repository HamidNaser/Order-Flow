# Order.MessageOperations

A professional-grade API and MCP server for managing message queues, S3 objects, and replay operations across OrderHub and OrderGateway systems.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [API Reference](#api-reference)
  - [Queues](#queues-endpoints)
  - [Batches](#batches-endpoints)
  - [Replay](#replay-endpoints)
  - [S3](#s3-endpoints)
- [MCP Integration](#mcp-integration)
  - [How It Works](#how-it-works)
  - [Available Tools](#available-mcp-tools)
  - [Example Interactions](#example-interactions)
- [Configuration](#configuration)
- [Getting Started](#getting-started)
- [Testing](#testing)
  - [Testing with OrderGateway OrderWorker](#testing-with-the-ordergateway-orderworker)
  - [Using MCP Server with Copilot](#using-the-mcp-server-with-copilot)
  - [Verifying Message Processing](#verifying-message-processing)
- [Deployment](#deployment)
- [Code Walkthrough](#code-walkthrough)
  - [High-Level Data Flow](#high-level-data-flow)
  - [API Project](#api-project-ordermessageoperationsapi)
  - [MCP Project](#mcp-project-ordermessageoperationsmcp)
  - [Design Decisions](#design-decisions-summary)

---

## Overview

**Order.MessageOperations** provides a unified interface for:

- **Queue Inspection**: List and inspect LocalStack/AWS SQS queues
- **Message Retrieval**: Peek at messages without consuming them
- **Batch Management**: Save, list, and load message batches from disk
- **Replay Operations**: Download messages from AWS DLQs and replay to LocalStack
- **S3 Operations**: List buckets/objects, retrieve content, sync referenced objects

This system is designed to be consumed by AI assistants (via MCP) for troubleshooting and local development workflows.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Copilot / AI Agent                          │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ MCP Protocol (stdio or HTTP)
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Order.MessageOperations.Mcp                     │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                        MCP Server                             │  │
│  │  - Registers tools (listQueues, peekMessages, replay, etc.)   │  │
│  │  - Validates inputs via schemas                               │  │
│  │  - Calls internal HTTP client to reach the API                │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ HTTP (localhost:5100)
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   Order.MessageOperations.Api                      │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │   Queues    │ │   Batches   │ │   Replay    │ │     S3      │   │
│  │ Controller  │ │ Controller  │ │ Controller  │ │ Controller  │   │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘   │
│                              │                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                        Services                               │  │
│  │  QueueReplayService │ MessageStorageService │ S3OpsService    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    ▼                              ▼
          ┌─────────────────┐            ┌─────────────────┐
          │   LocalStack    │            │    AWS (QA)     │
          │  SQS / S3       │            │   SQS / S3      │
          │  localhost:4566 │            │   (optional)    │
          └─────────────────┘            └─────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Copilot / AI Agent** | User-facing assistant that invokes MCP tools |
| **Order.MessageOperations.Mcp** | Thin MCP adapter exposing tools, forwards to API |
| **Order.MessageOperations.Api** | REST API with business logic for all operations |
| **LocalStack** | Local AWS emulation for development/testing |
| **AWS** | Production/QA environment (optional, for downloads) |

---

## API Reference

Base URL: `http://localhost:5100`

### Queues Endpoints

#### GET /api/v1/queues

List all configured queue mappings from `appsettings.json`.

**Response:**
```json
[
  {
    "queueKey": "OrderEvents",
    "displayName": "OrderGateway Order Events",
    "localStackQueueName": "order-gateway-incoming",
    "awsDlqName": "order-gateway-incoming-deadletter",
    "awsSourceQueueName": "order-gateway-incoming",
    "enabled": true
  }
]
```

---

#### GET /api/v1/queues/localstack

List all queues currently existing in LocalStack.

**Response:**
```json
[
  "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming",
  "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming-deadletter"
]
```

---

#### GET /api/v1/queues/{queueName}/status

Get attributes for a specific LocalStack queue.

**Parameters:**
- `queueName` (path): The queue name (e.g., `order-gateway-incoming`)

**Response:**
```json
{
  "ApproximateNumberOfMessages": "42",
  "ApproximateNumberOfMessagesNotVisible": "0",
  "ApproximateNumberOfMessagesDelayed": "0",
  "CreatedTimestamp": "1713196800",
  "LastModifiedTimestamp": "1713196900"
}
```

---

#### GET /api/v1/queues/{queueName}/messages

Peek at messages in a LocalStack queue (non-destructive).

**Parameters:**
- `queueName` (path): The queue name
- `count` (query, optional): Number of messages to peek (1-10, default: 5)

**Response:**
```json
[
  {
    "messageId": "abc123",
    "attributes": { "SentTimestamp": "1713196800000" },
    "messageAttributes": {},
    "body": "{\"eventType\":\"OrderProcessed\",...}",
    "bodySize": 256
  }
]
```

---

### Batches Endpoints

#### GET /api/v1/batches

List all saved message batches on disk.

**Response:**
```json
[
  {
    "queueType": "incomingorders",
    "batchIds": [
      "2026-04-15_143022_batch-a1b2c3d4e5f6",
      "2026-04-14_091500_batch-x7y8z9w0v1u2"
    ]
  }
]
```

---

#### GET /api/v1/batches/{queueType}/{batchId}

Get the manifest for a specific batch.

**Parameters:**
- `queueType` (path): The queue type folder name
- `batchId` (path): The batch identifier

**Response:**
```json
{
  "batchId": "2026-04-15_143022_batch-a1b2c3d4e5f6",
  "queueType": "OrderEvents",
  "createdAt": "2026-04-15T14:30:22Z",
  "sourceDlq": "order-gateway-incoming-deadletter",
  "messageCount": 25,
  "messageIds": ["msg-1", "msg-2", "..."]
}
```

---

#### GET /api/v1/batches/{queueType}/{batchId}/messages

Load all messages from a saved batch.

**Parameters:**
- `queueType` (path): The queue type folder name
- `batchId` (path): The batch identifier

**Response:**
```json
[
  {
    "messageId": "msg-1",
    "body": "{...}",
    "messageAttributes": {},
    "attributes": {},
    "messageGroupId": null,
    "downloadedAt": "2026-04-15T14:30:22Z",
    "sourceDlq": "order-gateway-incoming-deadletter"
  }
]
```

---

### Replay Endpoints

#### POST /api/v1/replay/download

Download messages from an AWS queue to a local batch.

**Request Body:**
```json
{
  "queueKey": "OrderEvents",
  "awsQueueName": "order-gateway-incoming-deadletter",
  "maxMessages": 100,
  "messageId": null
}
```

**Response:**
```json
{
  "downloaded": 42,
  "batchPath": "C:\\...\\downloaded-messages\\incomingorders\\2026-04-15_143022_batch-...",
  "queueKey": "OrderEvents",
  "awsQueueName": "order-gateway-incoming-deadletter"
}
```

---

#### POST /api/v1/replay/from-batch

Replay a saved batch to a LocalStack queue.

**Request Body:**
```json
{
  "queueType": "incomingorders",
  "batchId": "2026-04-15_143022_batch-a1b2c3d4e5f6",
  "localStackQueueName": "order-gateway-incoming"
}
```

**Response:**
```json
{
  "replayed": 42,
  "total": 42,
  "localStackQueueName": "order-gateway-incoming"
}
```

---

#### POST /api/v1/replay/download-and-replay

Download from AWS and immediately replay to LocalStack.

**Request Body:**
```json
{
  "queueKey": "OrderEvents",
  "maxMessages": 50,
  "messageId": null
}
```

**Response:**
```json
{
  "queueKey": "OrderEvents",
  "replayed": 50
}
```

---

### S3 Endpoints

#### GET /api/v1/s3/buckets

List S3 buckets.

**Parameters:**
- `target` (query, optional): `localstack` (default) or `aws`

**Response:**
```json
[
  {
    "name": "order-attachments",
    "creationDate": "2026-04-01T00:00:00Z"
  }
]
```

---

#### GET /api/v1/s3/buckets/{bucketName}/objects

List objects in an S3 bucket.

**Parameters:**
- `bucketName` (path): The bucket name
- `prefix` (query, optional): Filter by key prefix
- `maxKeys` (query, optional): Max results (default: 100, max: 1000)
- `target` (query, optional): `localstack` (default) or `aws`

**Response:**
```json
[
  {
    "key": "orders/2026/04/attachment-001.pdf",
    "size": 102400,
    "lastModified": "2026-04-15T10:00:00Z",
    "eTag": "\"abc123...\"",
    "storageClass": "STANDARD"
  }
]
```

---

#### GET /api/v1/s3/buckets/{bucketName}/objects/metadata

Get metadata for a specific S3 object.

**Parameters:**
- `bucketName` (path): The bucket name
- `key` (query, required): The object key
- `target` (query, optional): `localstack` (default) or `aws`

**Response:**
```json
{
  "bucket": "order-attachments",
  "key": "orders/2026/04/attachment-001.pdf",
  "contentLength": 102400,
  "contentType": "application/pdf",
  "eTag": "\"abc123...\"",
  "lastModified": "2026-04-15T10:00:00Z"
}
```

---

#### GET /api/v1/s3/buckets/{bucketName}/objects/content

Get the content of an S3 object (text/JSON only).

**Parameters:**
- `bucketName` (path): The bucket name
- `key` (query, required): The object key
- `maxBytes` (query, optional): Max bytes to return (default: 256KB, max: 1MB)
- `target` (query, optional): `localstack` (default) or `aws`

**Response:**
```json
{
  "bucket": "order-attachments",
  "key": "orders/2026/04/message.json",
  "contentType": "application/json",
  "contentLength": 1024,
  "content": "{\"subject\":\"Hello\",...}"
}
```

---

#### POST /api/v1/s3/sync-from-batch

Sync S3 objects referenced in batch messages to LocalStack.

**Request Body:**
```json
{
  "queueType": "incomingorders",
  "batchId": "2026-04-15_143022_batch-a1b2c3d4e5f6",
  "useAwsFallback": true
}
```

**Response:**
```json
{
  "synced": 5,
  "totalMessages": 42,
  "useAwsFallback": true
}
```

---

### Communications Endpoints

> **Read-only access to the OrderHub orders database (MongoDB/DocumentDB).**
> These endpoints are decoupled from the OrderHub business layer — they query the same `orders.orders` collection directly.

#### GET /api/v1/communications/{storeId}/{communicationId}

Get a single communication record by StoreId and CommunicationId.

**Parameters:**
- `storeId` (path): The Common Org ID
- `communicationId` (path): The MongoDB ObjectId of the communication

**Response:**
```json
{
  "communicationId": "6625a1b2c3d4e5f600000001",
  "channelType": "SHIPMENT",
  "storeId": "CoOrg123",
  "customerId": "customer-456",
  "orderFlow": "OUTGOING",
  "fulfillmentStatus": "SUCCESS",
  "subject": "Thank you for your order",
  "to": [{ "address": "customer@example.com", "displayName": "John Doe" }],
  "from": { "address": "sales@store-example.com", "displayName": "Sales Team" },
  "provider": { "name": "SendGrid", "communicationId": "sg-msg-789" },
  "orderDateUtc": "2026-04-15T14:30:00Z"
}
```

---

#### GET /api/v1/communications/{storeId}/customer/{customerId}

List communications for a specific consumer, paginated, sorted by date descending.

**Parameters:**
- `storeId` (path): The Common Org ID
- `consumerId` (path): The customer ID
- `limit` (query, optional): Max results (default: 50, max: 200)
- `offset` (query, optional): Skip N records (default: 0)

**Response:**
```json
{
  "storeId": "CoOrg123",
  "customerId": "customer-456",
  "totalCount": 120,
  "returned": 50,
  "limit": 50,
  "offset": 0,
  "communications": [ ... ]
}
```

---

#### GET /api/v1/communications/{storeId}/customer/{customerId}/count

Get a count of orders for a customer.

**Response:**
```json
{
  "storeId": "CoOrg123",
  "customerId": "customer-456",
  "count": 120
}
```

---

#### GET /api/v1/communications/{storeId}/search

Search communications with flexible filter criteria.

**Parameters (all query, all optional except storeId):**
- `consumerId`: Filter by customer
- `channelType`: `SHIPMENT` or `TEXT`
- `fulfillmentStatus`: `IN_PROGRESS`, `SUCCESS`, or `FAILED`
- `orderFlow`: `INCOMING` or `OUTGOING`
- `providerName`: e.g., `SendGrid`, `Twilio`
- `providerId`: The provider's communication ID
- `fromDate`: Start date (UTC, ISO 8601)
- `toDate`: End date (UTC, ISO 8601)
- `limit`: Max results (default: 50, max: 200)
- `offset`: Skip N records (default: 0)

**Response:**
```json
{
  "storeId": "CoOrg123",
  "filters": { "channelType": "SHIPMENT", "fulfillmentStatus": "FAILED" },
  "returned": 3,
  "communications": [ ... ]
}
```

---

#### GET /api/v1/communications/{storeId}/summary

Get count breakdowns for a CoOrg — by channel type, delivery status, and direction.

**Response:**
```json
{
  "storeId": "CoOrg123",
  "totalCount": 5430,
  "byChannelType": { "SHIPMENT": 4200, "TEXT": 1230 },
  "byFulfillmentStatus": { "SUCCESS": 5100, "FAILED": 280, "IN_PROGRESS": 50 },
  "byOrderFlow": { "OUTGOING": 3500, "INCOMING": 1930 }
}
```

---

#### GET /api/v1/communications/{storeId}/provider/{providerName}/{providerOrderId}

Find a communication by provider details (useful with SendGrid/Twilio message IDs).

**Parameters:**
- `providerName` (path): e.g., `SendGrid`
- `providerOrderId` (path): The provider's message ID
- `channelType` (query, optional): `SHIPMENT` or `TEXT`

---

#### GET /api/v1/communications/{storeId}/recent

List the most recent communications for a CoOrg regardless of consumer.

**Parameters:**
- `limit` (query, optional): Max results (default: 20, max: 200)

---

## MCP Integration

### How It Works

1. **Copilot calls an MCP tool**
   - Example: User asks "Show me the queues in LocalStack"
   - Copilot invokes the `listLocalStackQueues` tool

2. **MCP server validates and routes**
   - Validates input against JSON schema
   - Calls the corresponding API endpoint

3. **API executes the operation**
   - Controller receives request
   - Service talks to LocalStack or AWS
   - Returns JSON response

4. **MCP server formats response**
   - Transforms API response into MCP-friendly format
   - Returns structured data to Copilot

5. **Copilot presents results**
   - Formats the response for the user
   - Can chain multiple tool calls for complex workflows

### Available MCP Tools

| Tool | API Endpoint | Description |
|------|--------------|-------------|
| `listConfiguredQueues` | `GET /api/v1/queues` | Show available queue mappings |
| `listLocalStackQueues` | `GET /api/v1/queues/localstack` | List actual queues in LocalStack |
| `getQueueStatus` | `GET /api/v1/queues/{name}/status` | Get message counts and attributes |
| `peekQueueMessages` | `GET /api/v1/queues/{name}/messages` | View messages without consuming |
| `listBatches` | `GET /api/v1/batches` | Show saved message batches |
| `getBatchDetails` | `GET /api/v1/batches/{type}/{id}` | Get batch manifest |
| `getBatchMessages` | `GET /api/v1/batches/{type}/{id}/messages` | Load saved messages |
| `downloadMessages` | `POST /api/v1/replay/download` | Download from AWS to local batch |
| `replayFromBatch` | `POST /api/v1/replay/from-batch` | Replay saved batch to LocalStack |
| `downloadAndReplay` | `POST /api/v1/replay/download-and-replay` | One-shot download + replay |
| `listS3Buckets` | `GET /api/v1/s3/buckets` | List S3 buckets |
| `listS3Objects` | `GET /api/v1/s3/buckets/{bucket}/objects` | List objects in bucket |
| `getS3ObjectMetadata` | `GET /api/v1/s3/buckets/{bucket}/objects/metadata` | Get object metadata |
| `getS3ObjectContent` | `GET /api/v1/s3/buckets/{bucket}/objects/content` | Read object content |
| `syncS3FromBatch` | `POST /api/v1/s3/sync-from-batch` | Sync S3 refs to LocalStack |
| `getCommunication` | `GET /api/v1/communications/{storeId}/{id}` | Get a single communication record |
| `getCustomerOrders` | `GET /api/v1/communications/{storeId}/customer/{customerId}` | List orders for a customer |
| `searchCommunications` | `GET /api/v1/communications/{storeId}/search` | Search with flexible filters |
| `getCommunicationSummary` | `GET /api/v1/communications/{storeId}/summary` | Get count breakdowns |
| `findByProvider` | `GET /api/v1/communications/{storeId}/provider/{name}/{id}` | Find by provider details |
| `getRecentCommunications` | `GET /api/v1/communications/{storeId}/recent` | List most recent activity |

### Example Interactions

**Listing queues:**
```
User: "Show me the queues in LocalStack"

Copilot calls: listLocalStackQueues()

Response:
{
  "queues": [
    "order-gateway-incoming",
    "order-gateway-incoming-deadletter"
  ]
}

Copilot: "I found 2 queues in LocalStack:
- order-gateway-incoming
- order-gateway-incoming-deadletter"
```

**Checking queue depth:**
```
User: "How many messages are in the order queue?"

Copilot calls: getQueueStatus({ queueName: "order-gateway-incoming" })

Response:
{
  "ApproximateNumberOfMessages": "42",
  "ApproximateNumberOfMessagesNotVisible": "0"
}

Copilot: "The order queue has approximately 42 messages ready."
```

**Replaying messages:**
```
User: "Replay the latest batch to LocalStack"

Copilot calls: listBatches()
Copilot calls: replayFromBatch({ queueType: "incomingorders", batchId: "2026-04-15_..." })

Response:
{
  "replayed": 25,
  "total": 25
}

Copilot: "Successfully replayed 25 messages to LocalStack."
```

---

## Configuration

### appsettings.json

```json
{
  "MessageOperations": {
    "AwsRegion": "us-east-1",
    "AwsAccountId": "123456789012",
    "Environment": "qa",
    "LocalStackEndpoint": "http://localhost:4566",
    "LocalStackSqsEndpoint": "http://sqs.us-east-1.localhost.localstack.cloud:4566",
    "LocalStackS3Endpoint": "http://localhost:4566",
    "BatchSize": 10,
    "MessageStoragePath": "downloaded-messages",
    "S3CachePath": "downloaded-messages/s3-cache",
    "Queues": {
      "OrderEvents": {
        "DisplayName": "OrderGateway Order Events",
        "LocalStackQueueName": "order-gateway-incoming",
        "AwsDlqName": "order-gateway-incoming-deadletter",
        "AwsSourceQueueName": "order-gateway-incoming",
        "Enabled": true
      },
      "IngestStandard": {
        "DisplayName": "OrderHub Ingest Auto",
        "LocalStackQueueName": "order-hub-ingest-standard",
        "AwsDlqName": "order-hub-ingest-standard-deadletter",
        "AwsSourceQueueName": "order-hub-ingest-standard",
        "Enabled": true
      }
    }
  }
}
```

### Queue Mapping Fields

| Field | Description |
|-------|-------------|
| `DisplayName` | Human-readable name for the queue |
| `LocalStackQueueName` | Queue name in LocalStack |
| `AwsDlqName` | Dead-letter queue name in AWS |
| `AwsSourceQueueName` | Primary queue name in AWS |
| `Enabled` | Whether this queue is active |

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- LocalStack running (`docker-compose up` or `./start.ps1`)
- AWS credentials configured (for AWS operations)

### Running the API

```bash
cd src/Order.MessageOperations.Api
dotnet run
```

The API will start at `http://localhost:5100`.

### Verify with Swagger

Open `http://localhost:5100/swagger` in your browser to explore the API.

### Running with LocalStack

1. Start LocalStack:
   ```bash
   cd ifx-aws-cli/local
   ./start.ps1
   ```

2. Start the API:
   ```bash
   dotnet run --project src/Order.MessageOperations.Api
   ```

3. Test queue listing:
   ```bash
   curl http://localhost:5100/api/v1/queues/localstack
   ```

---

## Testing

### Testing with the OrderGateway OrderWorker

The OrderGateway OrderWorker expects SQS message bodies to be **Base64-encoded JSON** of the `OrderEvent` object.

#### Message Format

**SQS Message Structure:**
```
Message.Body = Base64( JSON(OrderEvent) )
```

**OrderEvent JSON Structure:**
```json
{
  "Type": "Order",
  "SubType": "Outbound order from CRM",
  "Description": "Test order description",
  "CreatedOn": "4/15/2026 6:00:00 PM",
  "Metadata": {
    "StoreId": "10001",
    "ContactId": "1382673902",
    "UserId": "765432112",
    "OrderReferenceId": "1959159664",
    "OrderItemId": "25701927660",
    "MessageId": "9da8510f-d11a-427f-ab0b-53df6f3fcbe2",
    "OrderTitle": "Test - Questions about your vehicle",
    "RecipientAddress": "customer@example.com",
    "SenderAddress": "store@order-example.com",
    "OrderFlowType": "Outbound",
    "OrderReferenceId": "321f5d79-3cff-f011-a315-0050568841d48",
    "Classification": "AutoOrder",
    "HasAttachments": "False"
  }
}
```

#### Send a Test Message (PowerShell)

```powershell
# 1. Create the OrderEvent JSON
$orderEvent = @'
{"Type":"Order","SubType":"Outbound order from CRM","Description":"Test order","CreatedOn":"4/15/2026 6:00:00 PM","Metadata":{"StoreId":"10001","ContactId":"123456","UserId":"789012","OrderTitle":"Test Subject","RecipientAddress":"customer@example.com","SenderAddress":"store@order-test.com","OrderFlowType":"Outbound","OrderReferenceId":"test-001","Classification":"ManualOrder","HasAttachments":"False"}}
'@

# 2. Base64 encode it
$base64Body = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($orderEvent))

# 3. Send to LocalStack SQS
aws --endpoint-url=http://localhost:4566 sqs send-message `
  --queue-url "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming" `
  --message-body $base64Body
```

#### View Messages in Queue

```powershell
# Peek at messages (non-destructive)
aws --endpoint-url=http://localhost:4566 sqs receive-message `
  --queue-url "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming" `
  --visibility-timeout 0 `
  --attribute-names All `
  --message-attribute-names All `
  --output json
```

#### Decode a Message Body

```powershell
# After receiving a message, decode the Base64 body
$msg = aws --endpoint-url=http://localhost:4566 sqs receive-message `
  --queue-url "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming" `
  --visibility-timeout 0 --output json | ConvertFrom-Json

[System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($msg.Messages[0].Body)) | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

#### Check Message Counts

```powershell
$queues = @(
  "order-gateway-incoming",
  "order-gateway-incoming-deadletter",
  "order-hub-standard-order",
  "order-hub-standard-order-deadletter",
  "order-hub-express-order",
  "order-hub-express-order-deadletter"
)

foreach ($q in $queues) {
  $url = "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/$q"
  $result = aws --endpoint-url=http://localhost:4566 sqs get-queue-attributes `
    --queue-url $url `
    --attribute-names ApproximateNumberOfMessages ApproximateNumberOfMessagesNotVisible `
    --output json | ConvertFrom-Json
  Write-Host "$q : $($result.Attributes.ApproximateNumberOfMessages) messages"
}
```

#### Purge a Queue

```powershell
aws --endpoint-url=http://localhost:4566 sqs purge-queue `
  --queue-url "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/order-gateway-incoming"
```

---

### Using the MCP Server with Copilot

#### Prerequisites

1. **Start LocalStack** (for SQS/S3 operations):
   ```powershell
   cd Communication\OrderGateway\ifx-aws-cli\local
   ./start.ps1
   ```

2. **Start the API**:
   ```powershell
   dotnet run --project Communication\Order.MessageOperations\Order.MessageOperations.Api
   ```

3. **Configure VS Code MCP** (already done in `.vscode/mcp.json`):
   ```json
   {
     "servers": {
       "order-message-ops": {
         "type": "stdio",
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "${workspaceFolder}/Order.MessageOperations/Order.MessageOperations.Mcp/Order.MessageOperations.Mcp.csproj"
         ],
         "env": {
           "MESSAGEOPS_API_URL": "http://localhost:5100"
         }
       }
     }
   }
   ```
   > **Note:** Update `MESSAGEOPS_API_URL` to match the port shown when the API starts.

#### Start the MCP Server

1. Open **Command Palette** (`Ctrl+Shift+P`)
2. Type **"MCP: List Servers"** and select it
3. Find **"order-message-ops"** and click **Start**

#### Use Copilot Chat in Agent Mode

1. Open **Copilot Chat** (`Ctrl+Alt+I`)
2. Switch to **Agent mode** (click the dropdown at the top, select "Agent")
3. Ask natural language questions:

**Queue Operations:**
```
What queues are available in LocalStack?
```
```
Show me the message count for each queue
```
```
Show me messages in the order-gateway-incoming queue
```

**S3 Operations:**
```
What S3 buckets are available?
```
```
List objects in the order-attachments bucket
```

**Batch Operations:**
```
List all saved message batches
```
```
Show me the messages in batch 2026-02-02_013708_batch-eb0c977fbdc245e8
```

**Replay Operations:**
```
Download messages from the QA order deadletter queue
```
```
Replay batch 2026-04-15_xyz to LocalStack
```

#### Example Copilot Conversation

```
User: "What queues are available and how many messages are in each?"

Copilot: I'll check the queues for you.
[Invokes list-queues tool]
[Invokes get-queue-message-count tool for each queue]

Here are the queue message counts:

| Queue | Messages |
|-------|----------|
| order-gateway-incoming | 1 |
| order-gateway-incoming-deadletter | 0 |
| order-hub-standard-order | 0 |
| order-hub-express-order | 0 |
```

```
User: "Send a test order event to the gateway queue"

Copilot: I'll help you send a test message. Here's what you need to do:
[Provides Base64-encoded message command]
```

---

### Verifying Message Processing

When you send a correctly formatted message and run the **OrderGateway OrderWorker**, you should see logs like:

```
[DBG] OrderEventManager[] ProcessEvent {
  "Metadata.StoreId": "17204",
  "Metadata.ContactId": "1382673902",
  "Metadata.RecipientAddress": "customer@example.com",
  "Metadata.SenderAddress": "store@order-example.com",
  "Metadata.Classification": "AutoOrder",
  "OrderEvent": {
    "Type": "Order",
    "SubType": "Outbound order from CRM",
    "IsAutomatedMessage": true
  }
}

[DBG] Feature flag 'orders.enableordergateway' evaluated to True
```

> **Note:** The OrderWorker will fail at the `SendCommunicationStep` if external services (OAuth server, IngestStandard API) are not running. This is expected for local testing - the message format is correct, but downstream dependencies are unavailable.

---

## Deployment

### VS Code MCP Configuration

After building the MCP server, add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "order-message-ops": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/Order.MessageOperations.Mcp"],
      "env": {
        "MESSAGEOPS_API_URL": "http://localhost:5100"
      }
    }
  }
}
```

### Claude Desktop Configuration

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "order-message-ops": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/Order.MessageOperations.Mcp"]
    }
  }
}
```

---

## Project Structure

```
Order.MessageOperations.Api/
├── Order.MessageOperations.Api.csproj
├── Program.cs                           # Entry point, DI setup
├── appsettings.json                     # Configuration
├── README.md                            # This file
├── TODO.md                              # Project progress tracking
├── Configuration/
│   └── MessageOperationsOptions.cs      # Typed configuration
├── Models/
│   ├── SavedMessage.cs                  # Message and batch models
│   └── Requests/
│       └── OperationRequests.cs         # Request DTOs
├── Services/
│   ├── QueueReplayService.cs            # SQS operations
│   ├── MessageStorageService.cs         # Batch persistence
│   └── S3OperationsService.cs           # S3 operations
└── Controllers/
    └── V1/
        ├── QueuesController.cs          # /api/v1/queues
        ├── BatchesController.cs         # /api/v1/batches
        ├── ReplayController.cs          # /api/v1/replay
        └── S3Controller.cs              # /api/v1/s3

Order.MessageOperations.Mcp/
├── Order.MessageOperations.Mcp.csproj
├── Program.cs                           # MCP server entry point
├── Configuration/
│   └── McpServerOptions.cs              # MCP configuration
├── Client/
│   └── MessageOperationsClient.cs       # Typed HTTP client + DTOs
└── Tools/
    ├── QueueTools.cs                    # Queue-related MCP tools
    ├── BatchTools.cs                    # Batch-related MCP tools
    ├── ReplayTools.cs                   # Replay-related MCP tools
    └── S3Tools.cs                       # S3-related MCP tools
```

---

## Service Details

### QueueReplayService

Handles all SQS-related operations:
- **AWS Client**: Uses `FallbackCredentialsFactory` for AWS operations
- **LocalStack Client**: Uses dummy credentials with configurable endpoint
- **Fallback Support**: Automatically retries with alternate LocalStack endpoint if primary fails

### MessageStorageService

Manages batch persistence:
- **Batch Format**: Each batch is a folder with `manifest.json` and `message-NNN.json` files
- **Path Resolution**: Supports relative and absolute paths
- **Thread Safety**: File operations are async and isolated per batch

### S3OperationsService

Handles S3 operations:
- **Dual Client**: Supports both AWS and LocalStack S3
- **Content Retrieval**: Caps response size to prevent memory issues
- **S3 Reference Extraction**: Parses S3 event notifications from message bodies
- **Caching**: Downloaded objects are cached locally before upload to LocalStack

### MCP Server (Order.MessageOperations.Mcp)

The MCP server is a thin adapter layer that exposes API operations as Copilot-discoverable tools:

- **SDK**: Uses ModelContextProtocol 0.2.0-preview.1 (official .NET MCP SDK)
- **Transport**: stdio (standard input/output) for communication with Copilot
- **DI Integration**: Uses .NET dependency injection with `IHttpClientFactory`
- **Tool Discovery**: Tools are discovered via `[McpServerToolType]` and `[McpServerTool]` attributes

**Configuration via Environment Variable:**
```bash
MESSAGEOPS_API_URL=http://localhost:5100
```

**Starting the MCP Server:**
```bash
dotnet run --project src/Order.MessageOperations.Mcp
```

---

## Error Handling

All endpoints return consistent error responses:

```json
{
  "message": "Queue 'nonexistent' not found in configuration"
}
```

HTTP status codes:
- `200 OK`: Successful operation
- `400 Bad Request`: Invalid input
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Unexpected error

---

## Health Check

```bash
curl http://localhost:5100/health
```

Returns `Healthy` when the API is running.

---

## Code Walkthrough

This section provides a detailed explanation of the codebase architecture and key implementation details.

### High-Level Data Flow

```
User: "How many messages are in the order queue?"
       ↓
[Copilot/Claude] 
       ↓ Calls GetQueueStatus(queueName: "order-gateway-incoming")
       ↓
[MCP Server - QueueTools.GetQueueStatus()]
       ↓ Validates input
       ↓ Calls _client.GetQueueStatusAsync("order-gateway-incoming")
       ↓
[MessageOperationsClient.GetQueueStatusAsync()]
       ↓ HTTP GET http://localhost:5100/api/v1/queues/order-gateway-incoming/status
       ↓
[API - QueuesController.GetQueueStatus()]
       ↓ Calls _queueReplayService.GetLocalStackQueueAttributesAsync()
       ↓
[QueueReplayService.GetLocalStackQueueAttributesAsync()]
       ↓ AWS SDK call to LocalStack SQS
       ↓
[LocalStack SQS] → Returns queue attributes
       ↓
[Response flows back up the chain]
       ↓
Copilot: "The order queue has 42 messages ready."
```

### API Project (Order.MessageOperations.Api)

#### Entry Point - Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register standard ASP.NET services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Bind configuration from appsettings.json with path resolution
builder.Services.Configure<MessageOperationsOptions>(options =>
{
    builder.Configuration.GetSection("MessageOperations").Bind(options);
    // Resolve relative paths to absolute paths for message/S3 storage
});

// Register core services as singletons (created once, reused)
builder.Services.AddSingleton<MessageStorageService>();
builder.Services.AddSingleton<QueueReplayService>();
builder.Services.AddSingleton<S3OperationsService>();

var app = builder.Build();

// Enable Swagger for all environments (internal tooling only)
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

**Key Points:**
- Services are registered as singletons because AWS SDK clients are thread-safe and expensive to create
- Swagger is enabled for all environments since this is internal tooling
- Configuration binding resolves relative paths to absolute paths

#### Configuration - MessageOperationsOptions.cs

```csharp
public class MessageOperationsOptions
{
    public string AwsRegion { get; set; } = "us-east-1";
    public string LocalStackEndpoint { get; set; } = "http://localhost:4566";
    public string LocalStackSqsEndpoint { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 10;
    public string MessageStoragePath { get; set; } = "downloaded-messages";
    public Dictionary<string, QueueMappingOptions> Queues { get; set; } = new();
}

public class QueueMappingOptions
{
    public string DisplayName { get; set; } = string.Empty;
    public string LocalStackQueueName { get; set; } = string.Empty;
    public string AwsDlqName { get; set; } = string.Empty;
    public string AwsSourceQueueName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
```

**Key Points:**
- Typed configuration maps the `MessageOperations` section from `appsettings.json`
- Each queue has a key (like "OrderEvents") that maps to LocalStack and AWS queue names
- Sensible defaults allow the app to work out of the box with LocalStack

#### Core Service - QueueReplayService.cs

```csharp
public class QueueReplayService : IDisposable
{
    private readonly IAmazonSQS _awsSqsClient;       // For AWS operations
    private readonly IAmazonSQS _localStackSqsClient; // For LocalStack operations

    public QueueReplayService(IOptions<MessageOperationsOptions> config, ...)
    {
        // Create AWS client with real credentials
        var awsCredentials = FallbackCredentialsFactory.GetCredentials();
        _awsSqsClient = new AmazonSQSClient(awsCredentials, ...);

        // Create LocalStack client with dummy credentials
        _localStackSqsClient = CreateLocalStackSqsClient(_localStackSqsEndpoint);
    }
}
```

**Dual Client Pattern:**
- Two SQS clients: one for AWS (real credentials), one for LocalStack (dummy credentials)
- Fallback support: if primary LocalStack endpoint fails, tries alternate endpoint format

**Key Methods:**
| Method | Purpose |
|--------|---------|
| `DownloadFromAwsDlqAsync` | Downloads messages from AWS DLQ, saves locally |
| `ReplayToLocalStackAsyncByName` | Sends saved messages to LocalStack queue |
| `DownloadAndReplayAsync` | Combines download + replay in one operation |
| `ListLocalStackQueuesAsync` | Lists all queues in LocalStack |
| `PeekLocalStackMessagesAsync` | Views messages without consuming them |

#### Controller Pattern - QueuesController.cs

```csharp
[ApiController]
[Route("api/v1/queues")]
public class QueuesController : ControllerBase
{
    private readonly QueueReplayService _queueReplayService;

    public QueuesController(QueueReplayService queueReplayService)
    {
        _queueReplayService = queueReplayService;  // Injected via DI
    }

    [HttpGet("localstack")]
    public async Task<IActionResult> ListLocalStackQueues(CancellationToken ct)
    {
        var queues = await _queueReplayService.ListLocalStackQueuesAsync(ct);
        return Ok(queues.OrderBy(q => q).ToList());
    }

    [HttpGet("{queueName}/status")]
    public async Task<IActionResult> GetQueueStatus(string queueName, CancellationToken ct)
    {
        var attributes = await _queueReplayService.GetLocalStackQueueAttributesAsync(queueName, ct);
        return Ok(attributes);
    }
}
```

**Key Points:**
- Controllers are thin orchestrators - business logic lives in services
- Route-based API: each method maps to an HTTP endpoint
- Dependency injection handles service instantiation

---

### MCP Project (Order.MessageOperations.Mcp)

#### Entry Point - Program.cs

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr only (MCP uses stdout for communication)
builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Warning);

// Get API URL from environment variable (no rebuild needed to change)
var apiBaseUrl = Environment.GetEnvironmentVariable("MESSAGEOPS_API_URL") 
    ?? "http://localhost:5100";

// Register typed HTTP client with IHttpClientFactory
builder.Services.AddHttpClient<MessageOperationsClient>((sp, client) =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register MCP server with all tool classes
builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "order-message-ops" })
    .WithStdioServerTransport()       // Uses stdin/stdout for Copilot communication
    .WithTools<QueueTools>()          // Scans for [McpServerTool] attributes
    .WithTools<BatchTools>()
    .WithTools<ReplayTools>()
    .WithTools<S3Tools>();

await builder.Build().RunAsync();
```

**Key Points:**
- Logs to stderr only (stdout is reserved for MCP protocol)
- Environment variable for API URL allows configuration without rebuilding
- `WithStdioServerTransport()` enables communication with Copilot via stdin/stdout

#### HTTP Client - MessageOperationsClient.cs

```csharp
public class MessageOperationsClient
{
    private readonly HttpClient _httpClient;

    public MessageOperationsClient(HttpClient httpClient, ...)
    {
        _httpClient = httpClient;  // Managed by IHttpClientFactory
    }

    // Each method maps 1:1 with an API endpoint
    public async Task<List<QueueMappingDto>> ListConfiguredQueuesAsync(CancellationToken ct)
    {
        return await GetAsync<List<QueueMappingDto>>("/api/v1/queues", ct) ?? [];
    }

    public async Task<Dictionary<string, string>> GetQueueStatusAsync(string queueName, CancellationToken ct)
    {
        return await GetAsync<Dictionary<string, string>>(
            $"/api/v1/queues/{Uri.EscapeDataString(queueName)}/status", ct) ?? new();
    }

    // Generic HTTP helpers with error handling
    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"API request failed: {response.StatusCode} - {error}");
        }
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }
}
```

**Key Points:**
- Typed HTTP client wraps `HttpClient` with strongly-typed methods
- URI escaping handles special characters in queue names
- Compile-time type safety and easy mocking for tests

#### MCP Tool Pattern - QueueTools.cs

```csharp
[McpServerToolType]  // Marks class as containing MCP tools
public class QueueTools
{
    private readonly MessageOperationsClient _client;

    public QueueTools(MessageOperationsClient client)
    {
        _client = client;  // Injected via DI
    }

    [McpServerTool]  // Marks method as an MCP tool
    [Description("List all SQS queues currently existing in LocalStack.")]
    public async Task<string> ListLocalStackQueues(CancellationToken ct = default)
    {
        var queues = await _client.ListLocalStackQueuesAsync(ct);
        
        if (queues.Count == 0)
            return "No queues found in LocalStack.";

        var result = new { count = queues.Count, queues = queueNames };
        return JsonSerializer.Serialize(result, JsonOptions);  // MCP tools return strings
    }

    [McpServerTool]
    [Description("Get status for a specific LocalStack queue.")]
    public async Task<string> GetQueueStatus(
        [Description("Queue name (e.g., 'order-gateway-incoming')")] 
        string queueName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            return "Error: queueName is required.";

        var attributes = await _client.GetQueueStatusAsync(queueName, ct);
        return JsonSerializer.Serialize(new { queueName, attributes }, JsonOptions);
    }
}
```

**Key Attributes:**
| Attribute | Purpose |
|-----------|---------|
| `[McpServerToolType]` | Marks class as containing MCP tools |
| `[McpServerTool]` | Marks method as an MCP tool |
| `[Description]` on method | Description Copilot sees when listing tools |
| `[Description]` on parameter | Tells Copilot what each argument does |

**Key Points:**
- Constructor receives `MessageOperationsClient` via dependency injection
- Each tool method calls the HTTP client, then formats result as JSON
- MCP protocol expects string responses
- `CancellationToken` is automatically handled by MCP framework

---

### Design Decisions Summary

| Decision | Reason |
|----------|--------|
| **Two separate projects** | API can be tested independently; MCP is a thin adapter |
| **Typed HTTP client** | Compile-time safety, easy mocking for tests |
| **Singleton services in API** | AWS SDK clients are thread-safe and expensive to create |
| **JSON string returns from MCP** | MCP protocol expects string responses |
| **Environment variable for API URL** | No rebuild needed when changing endpoints |
| **No authentication** | Internal tooling only, protected by network |
| **Swagger enabled always** | This is operational tooling, not production-facing |

---

## License

Internal use only - Order Processing Team

