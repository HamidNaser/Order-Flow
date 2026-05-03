## Tear Down the Local Environment

Stop all running applications and clean up infrastructure containers and data.

### Step 1: Kill Running .NET Applications
First, find and kill all running AppHost, API, and Worker processes.

Run in terminal:
```
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -match 'AppHost|OrderGateway\.Api|OrderGateway\.OrderWorker|OrderHub\.Api|IngestStandard|IngestExpress|MessageOperations'
} | ForEach-Object {
    Write-Host "Killing: $($_.Id) - $($_.CommandLine)"
    Stop-Process -Id $_.Id -Force
}
```

If that doesn't find processes by command line, use this broader approach:
```
Get-Process -Name dotnet -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Found dotnet process: PID=$($_.Id)"
}
```

Then kill all dotnet processes if needed:
```
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

**Report**: List which processes were found and killed, or confirm none were running.

### Step 2: Verify Applications Are Stopped
Run in terminal:
```
Get-Process -Name dotnet -ErrorAction SilentlyContinue
```

Expected result: no dotnet processes remaining.
If any remain, kill them with `Stop-Process -Id <PID> -Force`.

### Step 3: Stop OrderGateway Infrastructure
Run in terminal:
```
cd OrderGateway/ifx-aws-cli/local
./stop.ps1
./clean.ps1 -Force
```

This stops all OrderGateway containers and removes volumes/data.

### Step 4: Stop OrderHub Infrastructure
Run in terminal:
```
cd ../../OrderHub/ifx-aws-cli/local
./stop.ps1
./clean.ps1 -Force
```

This stops all OrderHub containers (LocalStack, MongoDB, Redis, Keycloak) and removes volumes/data.

### Step 5: Verify Everything Is Stopped
Call `CheckLocalStackHealth` to confirm LocalStack is no longer reachable.
Expected result: unhealthy or unreachable.

### Step 6: Report Status
Summarize what was torn down:
- .NET applications: killed (list PIDs and names)
- OrderGateway infra: stopped and cleaned
- OrderHub infra: stopped and cleaned
- LocalStack: unreachable (confirmed)
- All containers and volumes removed

The environment is fully torn down when:
✓ All dotnet processes (AppHosts, APIs, Workers) are killed
✓ Both stop.ps1 scripts completed successfully
✓ Both clean.ps1 scripts completed successfully
✓ CheckLocalStackHealth reports unhealthy/unreachable
