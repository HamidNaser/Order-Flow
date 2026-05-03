## End-to-End Order Trace ({{priority}})

Trace a single order through every stage of the pipeline, reporting timing at each hop.

### Step 1: Pre-flight
Call `CheckLocalStackHealth`.
Call `GetAllQueueDepths` to snapshot current state.

### Step 2: Send Order
Call `GenerateAndSendOrders` with count=1, priority="{{priorityLower}}", storeId from the note below.
{{storeNote}}

> **Important**: The OrderGateway has a feature flag (`orders.enableordergateway`) that gates
> processing by store ID. Orders with a store ID not in the flag will be silently consumed
> and discarded. In local config, `10001` and `10317` are enabled.

Note the OrderReferenceId and StoreId from the response.
**Report**: "Sent order [ref] to [queue] at [time]"

### Step 3: Trace — Gateway Queue
Call `WaitForQueueMessage` on `order-gateway-incoming` with:
- bodyContains = OrderReferenceId
- timeoutSeconds = 5
**Report**: "✓ Gateway queue: found in [X]ms" or "✗ Gateway queue: not found"

### Step 4: Trace — Downstream Queue
Call `WaitForQueueMessage` on `{{downstreamQueue}}` with:
- timeoutSeconds = 30
**Report**: "✓ Downstream queue: found in [X]ms" or "✗ Not found (OrderGateway may not be running)"

### Step 5: Trace — S3 Persistence
Call `WaitForS3Object` on the orders bucket with:
- keyPrefix = StoreId or OrderReferenceId
- timeoutSeconds = 30
**Report**: "✓ S3: object [key] found in [X]ms" or "✗ Not found (IngestAPI may not be running)"

### Step 6: Trace — MongoDB Persistence
Call `WaitForMongoDocument` with:
- storeId = the order's StoreId
- timeoutSeconds = 30
**Report**: "✓ MongoDB: document [id] found in [X]ms" or "✗ Not found (worker may not be running)"

### Step 7: Summary
```
Order: [OrderReferenceId]
Store:  [StoreId]
Priority: {{priority}}

Pipeline Trace:
  → Gateway Queue (order-gateway-incoming):  [✓/✗] [Xms]
  → Downstream Queue ({{downstreamQueue}}):    [✓/✗] [Xms]
  → S3 Persistence:                          [✓/✗] [Xms]
  → MongoDB Persistence:                     [✓/✗] [Xms]

Total pipeline time: [X]ms
Services running: [list which stages succeeded]
```
