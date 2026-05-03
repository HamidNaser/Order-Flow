## Setup Local Infrastructure Environment

Follow these steps to set up the complete local infrastructure for order processing.
This includes LocalStack (SQS + S3), MongoDB, Redis, and Keycloak.

### Step 1: Verify Prerequisites
Run these commands in a terminal to confirm required tools are installed:
```
dotnet --version
docker --version
pwsh --version
```

- .NET SDK must be 8.0 or higher
- Docker Desktop must be installed and **running**
- PowerShell 7+ must be available

If any tool is missing, report it and stop. Do not proceed without all prerequisites.

### Step 2: Stop and Clean OrderHub Infrastructure
Run in terminal:
```
cd OrderHub/ifx-aws-cli/local
./stop.ps1
./clean.ps1 -Force
```

This stops any existing OrderHub containers and removes volumes/data for a clean start.
It is safe to run even if nothing is currently running.

### Step 3: Stop and Clean OrderGateway Infrastructure
Run in terminal:
```
cd ../../OrderGateway/ifx-aws-cli/local
./stop.ps1
./clean.ps1 -Force
```

This stops any existing OrderGateway containers and removes volumes/data.

### Step 4: Start OrderHub Infrastructure
Run in terminal:
```
cd ../../OrderHub/ifx-aws-cli/local
./start.ps1
```

**Wait for "All services are running!"** in the output before proceeding.
This starts: LocalStack, MongoDB, Redis, and Keycloak.

Then verify the services are up:
```
./status.ps1
```

Expected services after this step:
- LocalStack: `http://localhost:4566`
- MongoDB: `mongodb://localhost:27018`
- Redis: `localhost:6379`
- Keycloak: `http://localhost:8081`

### Step 5: Start OrderGateway Infrastructure
Run in terminal:
```
cd ../../OrderGateway/ifx-aws-cli/local
./start.ps1
```

**Wait for "All services are running!"** in the output before proceeding.

Then verify:
```
./status.ps1
```

### Step 6: Verify Queue and S3 Infrastructure
Call `CheckLocalStackHealth` to verify SQS and S3 connectivity.
Call `GetAllQueueDepths` to confirm all configured queues exist:
- `order-gateway-incoming` (entry point for OrderGateway)
- `order-hub-standard-order` (standard priority processing)
- `order-hub-express-order` (express priority processing)

All queues should exist with depth of 0 on a fresh start.

### Step 7: Report Status
Summarize the full environment state:
- Prerequisites: dotnet version, docker version, pwsh version
- OrderHub infra: running/stopped
- OrderGateway infra: running/stopped
- LocalStack: endpoint + health (SQS healthy, S3 healthy)
- MongoDB: `mongodb://localhost:27018`
- Redis: `localhost:6379`
- Keycloak: `http://localhost:8081`
- SQS queues found and their depths
- S3 buckets found

The environment is fully ready when:
✓ All prerequisites are installed
✓ OrderHub and OrderGateway infrastructure containers are running
✓ CheckLocalStackHealth shows both SQS and S3 as healthy
✓ All 3 queues exist with depth 0
