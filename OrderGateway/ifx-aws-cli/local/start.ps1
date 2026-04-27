#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Starts the local development environment with all required services.

.DESCRIPTION
    This script starts Docker Compose services, waits for health checks to pass,
    monitors initialization containers, and displays a service summary.

.EXAMPLE
    .\start.ps1
    Starts all local development services.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Color functions
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Helper function to test if Docker is running
function Test-DockerRunning {
    $ErrorActionPreference = 'SilentlyContinue'
    $null = docker info 2>&1
    $result = $LASTEXITCODE -eq 0
    $ErrorActionPreference = 'Stop'
    return $result
}

# Helper function to wait for service health check
function Wait-ForService {
    param(
        [string]$ServiceName,
        [int]$TimeoutSeconds = 60,
        [string]$HealthEndpoint = $null
    )

    Write-Info "Waiting for $ServiceName to be healthy (timeout: ${TimeoutSeconds}s)..."
    $elapsed = 0
    $interval = 2

    while ($elapsed -lt $TimeoutSeconds) {
        $status = docker-compose ps --format json $ServiceName 2>$null | ConvertFrom-Json

        if ($status) {
            # Check if service has health check
            if ($status.Health -eq "healthy") {
                Write-Success "$ServiceName is healthy"
                return $true
            }
            elseif ($status.State -eq "running" -and -not $status.Health) {
                Write-Success "$ServiceName is running (no health check configured)"
                return $true
            }
        }

        Start-Sleep -Seconds $interval
        $elapsed += $interval
        Write-Host "." -NoNewline
    }

    Write-Host ""
    Write-Error "$ServiceName failed to become healthy within ${TimeoutSeconds}s"
    return $false
}

# Helper function to wait for container to exit
function Wait-ForContainer {
    param(
        [string]$ContainerName,
        [int]$TimeoutSeconds = 300
    )

    Write-Info "Waiting for $ContainerName to complete (timeout: ${TimeoutSeconds}s)..."
    $elapsed = 0
    $interval = 2

    while ($elapsed -lt $TimeoutSeconds) {
        $status = docker-compose ps -a --format json $ContainerName 2>$null | ConvertFrom-Json

        if ($status -and $status.State -eq "exited") {
            $exitCode = $status.ExitCode
            if ($exitCode -eq 0) {
                Write-Success "$ContainerName completed successfully"
                return $true
            }
            else {
                Write-Error "$ContainerName exited with code $exitCode"
                Write-Info "View logs with: docker-compose logs $ContainerName"
                return $false
            }
        }

        Start-Sleep -Seconds $interval
        $elapsed += $interval
        if ($elapsed % 10 -eq 0) {
            Write-Host "." -NoNewline
        }
    }

    Write-Host ""
    Write-Error "$ContainerName did not complete within ${TimeoutSeconds}s"
    return $false
}

# Helper function to get service ports from Docker
function Get-ServicePorts {
    param(
        [string]$ServiceName,
        [string]$InternalPort
    )

    try {
        $container = docker-compose ps --format json $ServiceName 2>$null | ConvertFrom-Json
        if ($container -and $container.Publishers) {
            foreach ($publisher in $container.Publishers) {
                if ($publisher.TargetPort -eq [int]$InternalPort) {
                    return $publisher.PublishedPort
                }
            }
        }
    }
    catch {
        # Fall back to internal port if we can't get the published port
    }

    return $InternalPort
}

# Helper function to display service summary
function Show-ServiceSummary {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Local Development Environment - Service Summary" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    # Get actual port mappings from Docker
    $localstackPort = Get-ServicePorts -ServiceName "localstack" -InternalPort "4566"
    $redisPort = Get-ServicePorts -ServiceName "redis" -InternalPort "6380"

    $services = @(
        @{ Name = "LocalStack"; URL = "http://localhost:$localstackPort"; Description = "AWS Services (S3, SQS)" }
        @{ Name = "Redis"; URL = "redis://localhost:$redisPort"; Description = "Cache" }
    )

    foreach ($service in $services) {
        Write-Host "  $($service.Name.PadRight(15))" -NoNewline -ForegroundColor White
        Write-Host " $($service.URL.PadRight(35))" -NoNewline -ForegroundColor Gray
        Write-Host " $($service.Description)" -ForegroundColor DarkGray
    }

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Success "All services are running!"
    Write-Host ""
    Write-Info "Next steps:"
    Write-Host "  • Check service status: .\status.ps1"
    Write-Host "  • View logs: .\logs.ps1 [service-name]"
    Write-Host "  • Stop services: .\stop.ps1"
    Write-Host ""
}

# Main script execution
try {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Starting Local Development Environment" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    # Check if Docker is running
    Write-Info "Checking Docker Desktop status..."
    if (-not (Test-DockerRunning)) {
        Write-Error "Docker Desktop is not running. Please start Docker Desktop and try again."
        exit 1
    }
    Write-Success "Docker Desktop is running"

    # Navigate to the correct directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    Set-Location $scriptDir

    # Check if docker-compose.yml exists
    if (-not (Test-Path "docker-compose.yml")) {
        Write-Error "docker-compose.yml not found in current directory: $scriptDir"
        exit 1
    }

    # Check if LocalStack is already running on port 4566
    Write-Info "Checking for existing LocalStack instance..."
    $localstackRunning = $false
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:4566/_localstack/health" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $localstackRunning = $true
            Write-Success "Found existing LocalStack at localhost:4566 (likely from order-hub)"
        }
    }
    catch {
        Write-Info "No existing LocalStack found, will start new instance"
    }

    # Start services with or without LocalStack based on detection
    if ($localstackRunning) {
        Write-Info "Starting services (using existing LocalStack)..."
        docker-compose up -d
    }
    else {
        Write-Info "Starting services (including LocalStack)..."
        docker-compose --profile with-localstack up -d
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Docker Compose services"
        exit 1
    }
    Write-Success "Docker Compose services started"

    Write-Host ""

    # Wait for LocalStack to be healthy (if we started it, otherwise check connectivity)
    if ($localstackRunning) {
        Write-Info "Verifying LocalStack connectivity..."
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:4566/_localstack/health" -TimeoutSec 5 -UseBasicParsing
            Write-Success "LocalStack is accessible"
        }
        catch {
            Write-Warning "Cannot connect to existing LocalStack. Check if order-hub is running."
        }
    }
    else {
        if (-not (Wait-ForService -ServiceName "localstack" -TimeoutSeconds 60)) {
            Write-Error "LocalStack failed to start. Check logs with: docker-compose logs localstack"
            exit 1
        }
    }

    # Wait for Redis to be running
    if (-not (Wait-ForService -ServiceName "redis" -TimeoutSeconds 30)) {
        Write-Warning "Redis may not be running properly. Check logs with: docker-compose logs redis"
    }

    Write-Host ""

    # Always wait for initialization containers since they run on every start
    # Wait for localstack-int to complete
    $localstackIntStatus = docker-compose ps -a --format json localstack-int 2>$null | ConvertFrom-Json
    if ($localstackIntStatus) {
        if (-not (Wait-ForContainer -ContainerName "localstack-int" -TimeoutSeconds 300)) {
            Write-Error "LocalStack initialization failed"
            Write-Info "Check logs with: docker-compose logs localstack-int"
            exit 1
        }
    }
    else {
        Write-Warning "localstack-int container not found (may not be configured yet)"
    }

    # Verify all persistent services are running
    Write-Host ""
    Write-Info "Verifying all persistent services are running..."
    $allRunning = $true

    $requiredServices = @("localstack", "redis")
    foreach ($service in $requiredServices) {
        $status = docker-compose ps --format json $service 2>$null | ConvertFrom-Json
        if (-not $status -or ($status.State -ne "running" -and $status.Health -ne "healthy")) {
            Write-Warning "$service is not running properly"
            $allRunning = $false
        }
    }

    if (-not $allRunning) {
        Write-Warning "Some services may not be running properly. Check status with: .\status.ps1"
    }

    # Display service summary
    Show-ServiceSummary

    exit 0
}
catch {
    Write-Error "An unexpected error occurred: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
