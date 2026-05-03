## Run {{count}} Standard Orders End-to-End

Execute this scenario step by step, reporting progress after each step.

### Step 1: Pre-flight Check
Call `CheckLocalStackHealth` to verify LocalStack is running.
Call `GetAllQueueDepths` to capture the BEFORE queue depths.
**Report**: "Starting with X messages across Y queues"

### Step 2: Generate and Send Orders
Call `GenerateAndSendOrders` with:
- count = {{count}}
- priority = "standard"
- channelType = "STANDARD"
- storeId = from the storeNote below
{{storeNote}}

> **Important**: The OrderGateway has a feature flag (`orders.enableordergateway`) that gates
> processing by store ID. Orders with a store ID not in the flag will be silently consumed
> and discarded. In local config, `10001` and `10317` are enabled.

**Report for each order**: Order #N — [description] — sent to [queue] — messageId: [id]

### Step 3: Verify Queue Delivery
Call `GetAllQueueDepths` to capture the AFTER queue depths.
**Report**: "Queue depths changed: [queue] went from X to Y (+Z messages)"

Compare before/after to confirm {{count}} new messages arrived on `order-gateway-incoming`.

### Step 4: Trace Message Processing (if OrderGateway is running)
For each order, call `WaitForQueueMessage` on `order-hub-standard-order` with:
- bodyContains = the order's OrderReferenceId
- timeoutSeconds = 30

**Report for each**: Order #N — [FOUND/NOT FOUND] — [elapsed]ms

If messages are NOT found on the downstream queue, the OrderGateway may not be running.
That's OK — the messages are sitting on `order-gateway-incoming` waiting to be processed.

### Step 5: Summary
Create a results table:
| # | Store | Order Ref | Sent | Gateway Queue | Downstream Queue |
|---|-------|-----------|------|---------------|------------------|

Report success rate: X of {{count}} orders sent successfully, Y traced through pipeline.
