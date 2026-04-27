# LocalStack Setup and Run Guide

wsl --shutdown
wsl --update

## Prerequisites
- Docker Desktop (running)
- .NET 8 SDK

## Understanding LocalStack Infrastructure Behavior

### S3 Test Events
When S3 bucket notifications are configured, AWS S3 (and LocalStack) emit a test event to verify the notification setup is working correctly. These **S3 test events** are:
- **Operational infrastructure events** (not business messages)
- Automatically generated when notification policies are applied
- Emitted with `"Event": "s3:TestEvent"` in the message body
- Handled gracefully by the `OrderHandler` which recognizes and completes them without processing

**Why you might see this in logs:**
- If a worker pod crashes or restarts after S3 notifications are configured, any residual test events in the queue will be consumed
- The handler logs `Information: "Received S3 test event notification; acknowledging"` and completes the message without processing

**Queue Initialization:**
- During LocalStack init (`localstack-int` service), both main order queues and DLQs are purged after notification configuration to ensure a clean slate for workers
- This prevents workers from processing stale test events on startup

### Order Hub Scenario Labels
Use these four scenario labels when demonstrating end-to-end flow:

1. **Weather Alert** (closure or delay notice)
2. **Safety Alert** (incident response notice)
3. **Room-Change Alert** (location update)
4. **Reminder Alert** (schedule reminder)

For Level B framing, this app should be referred to as **Order Hub**.

---
###  Set the DOTNET_ENVIRONMENT environment variable:
$env:DOTNET_ENVIRONMENT = "localstack"; Write-Host "DOTNET_ENVIRONMENT set to: $env:DOTNET_ENVIRONMENT"

### Now verify it's set:
$env:DOTNET_ENVIRONMENT

### Get number of records
docker exec -it orderhub-mongodb mongosh orders --quiet --eval "db.orders.countDocuments()"


## Step 1: Start LocalStack Infrastructure

```powershell
cd C:\Work\Communication\OrderHub\ifx\local
.\start.ps1
```

Optional: use the fast aws-cli init path:

```powershell
cd C:\Work\Communication\OrderHub\ifx-aws-cli\local
.\start.ps1

.\start.ps1
```

```powershell
dotnet run --project .\\src\\OrderHub.AppHost
```

### To kill the application
```powershell
Ctrl+C
```

Wait for: `All services are running!` ✅

This starts LocalStack (port 4566), MongoDB (27017), and Redis (6379).

Notes:
- `ifx/local` is Terraform-based.
- `ifx-aws-cli/local` is the fast aws-cli path.
- If LocalStack is already running, the script reuses it instead of starting a new one.

Terraform vs aws-cli (local):
- Terraform: repeatable, closer to IaC; slower startup and more state files.
- aws-cli: fast and lightweight; more manual and less repeatable.

---

## Step 2: Copy LocalStack Configuration (One-Time Setup)

Copy the content from `src\\OrderHub.Common\appsettings.localstack.json` to your user secrets file:

**Location:** `C:\Users\{YourUsername}\AppData\Roaming\Microsoft\UserSecrets\8ac250ea-eb62-482d-8e9f-fef9d28e0e51\secrets.json`

*Note: This configuration is shared across all projects automatically.*

---

## Step 3: Build and Run

```powershell
cd C:\Work\Communication\OrderHub\src

# Build the solution
dotnet build OrderHub.slnx --no-incremental

# Run the AppHost
dotnet run --project OrderHub.AppHost --no-build
```

**Dashboard URL:** `https://localhost:17289/login?t=...` (shown in terminal)

All APIs and Workers will start automatically via Aspire.

---

## Stop Everything

- **AppHost:** Press `Ctrl+C` in the terminal
- **LocalStack:** Run `.\ifx\local\stop.ps1`

---

## Troubleshooting

**Build fails with "SDK could not be resolved"?**
- Kill lingering processes: `Get-Process dotnet | Stop-Process -Force`
- Rebuild: `dotnet build OrderHub.slnx --no-incremental`

**Can't connect to dashboard?**
- Make sure AppHost is still running (check terminal)
- Use the exact URL with token from terminal output

**Services failing to start?**
- Verify LocalStack is running: `.\ifx\local\status.ps1`
- Check that `secrets.json` has the LocalStack configuration
