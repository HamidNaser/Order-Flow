## Build and Run the Order Processing Platform

Build both solutions and launch the Aspire AppHosts to run all services locally.

### Step 1: Restore and Build OrderGateway
Run in terminal:
```
dotnet restore OrderGateway/OrderGateway.sln
dotnet build OrderGateway/OrderGateway.sln
```

If the build fails, report the errors and stop. Do not proceed with a broken build.

### Step 2: Restore and Build OrderHub
Run in terminal:
```
dotnet restore OrderHub/OrderHub.slnx
dotnet build OrderHub/OrderHub.slnx
```

If the build fails, report the errors and stop. Do not proceed with a broken build.

### Step 3: Start OrderHub AppHost
OrderHub orchestrates 5 services via Aspire. Run in a **new terminal**:
```
$env:DOTNET_ENVIRONMENT='localstack'; $env:ASPNETCORE_ENVIRONMENT='localstack'
dotnet run --project OrderHub/src/OrderHub.AppHost/OrderHub.AppHost.csproj
```

This is a long-running process — it will keep running in the background.
Wait for the Aspire dashboard URL to appear in the output (typically `https://localhost:15xxx`).

**Report**: "OrderHub AppHost started — dashboard at [URL]"

### Step 4: Start OrderGateway AppHost
OrderGateway orchestrates API + Worker via Aspire. Run in a **separate new terminal**:
```
$env:DOTNET_ENVIRONMENT='localstack'; $env:ASPNETCORE_ENVIRONMENT='localstack'
dotnet run --project OrderGateway/src/OrderGateway.AppHost/OrderGateway.AppHost.csproj
```

This is also a long-running process — it will keep running in the background.
Wait for the Aspire dashboard URL to appear in the output.

**Report**: "OrderGateway AppHost started — dashboard at [URL]"

### Step 5: Confirm End-to-End Readiness
Verify all services are operational:

1. **Keycloak OIDC** — Run in terminal:
   ```
   curl http://localhost:8081/realms/ordergateway-local/.well-known/openid-configuration
   ```
   Should return a JSON document with issuer, token_endpoint, etc.

2. **LocalStack health** — Call `CheckLocalStackHealth` to confirm SQS and S3 are healthy.

3. **Queue infrastructure** — Call `GetAllQueueDepths` to confirm all queues exist:
   - `order-gateway-incoming`
   - `order-hub-standard-order`
   - `order-hub-express-order`

4. **Send a test order** — Call `GenerateAndSendOrders` with count=1, priority="standard" to place a test event on the gateway queue.

5. **Trace processing** — Call `WaitForQueueMessage` on `order-hub-standard-order` with timeoutSeconds=30 to confirm the order flows through the pipeline.

### Step 6: Report Status
Summarize the platform state:
- OrderGateway build: success/failed
- OrderHub build: success/failed
- OrderHub AppHost: running (dashboard URL)
- OrderGateway AppHost: running (dashboard URL)
- Keycloak OIDC: reachable/unreachable
- LocalStack: healthy/unhealthy
- Test order: processed/pending/failed

The platform is fully operational when:
✓ Both solutions build successfully
✓ Both AppHosts are running with dashboards accessible
✓ Keycloak OIDC discovery resolves
✓ A test order flows from gateway queue through to the downstream queue
