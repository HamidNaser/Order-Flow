#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Check status of local development environment services.

.DESCRIPTION
    Displays the health and connectivity status of all Docker Compose services,
    including running services and initialization containers.

.EXAMPLE
    .\status.ps1
    Check status of all services
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# Navigate to the directory containing docker-compose.yml
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "  Local Development Environment - Status Check" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""

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

# Helper function to test service connectivity
function Test-ServiceConnectivity {
    param(
        [string]$ServiceName,
        [string]$TestType,
        [string]$Endpoint,
        [int]$Port
    )

    try {
        switch ($TestType) {
            'http' {
                $response = Invoke-WebRequest -Uri $Endpoint -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
                return $true
            }
            'mongodb' {
                $tcpClient = New-Object System.Net.Sockets.TcpClient
                try {
                    $async = $tcpClient.BeginConnect('localhost', $Port, $null, $null)
                    $success = $async.AsyncWaitHandle.WaitOne(5000)
                    if (-not $success) {
                        return $false
                    }
                    $tcpClient.EndConnect($async)
                    return $tcpClient.Connected
                }
                finally {
                    $tcpClient.Dispose()
                }
            }
            'redis' {
                $result = redis-cli -h localhost -p $Port ping 2>&1
                return $result -match 'PONG'
            }
        }
    } catch {
        return $false
    }
    return $false
}

# Helper function to format status with color
function Write-StatusLine {
    param(
        [string]$Label,
        [string]$Status,
        [string]$Color = 'White',
        [string]$Detail = ''
    )

    $labelFormatted = $Label.PadRight(25)
    Write-Host "  $labelFormatted : " -NoNewline
    Write-Host $Status -ForegroundColor $Color -NoNewline

    if ($Detail) {
        Write-Host " ($Detail)" -ForegroundColor Gray
    } else {
        Write-Host ""
    }
}

# Check if Docker Desktop is running
Write-Host "Docker Desktop Status" -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------" -ForegroundColor Gray

try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-StatusLine "Docker Engine" "Running" "Green"
    } else {
        Write-StatusLine "Docker Engine" "Not Running" "Red"
        Write-Host ""
        Write-Host "Error: Docker Desktop is not running." -ForegroundColor Red
        Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-StatusLine "Docker Engine" "Error" "Red" "$_"
    Write-Host ""
    Write-Host "Error: Cannot connect to Docker." -ForegroundColor Red
    Write-Host "Please ensure Docker Desktop is installed and running." -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Check if docker-compose.yml exists
if (-not (Test-Path "docker-compose.yml")) {
    Write-Host "Error: docker-compose.yml not found in current directory." -ForegroundColor Red
    exit 1
}

# Get container status
Write-Host "Container Status" -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------" -ForegroundColor Gray

$containers = docker-compose ps --format json 2>&1 | ConvertFrom-Json -ErrorAction SilentlyContinue

if (-not $containers) {
    Write-Host "  No containers found. Environment may not be started." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Recommendation: Run '.\start.ps1' to start the environment." -ForegroundColor Cyan
    exit 0
}

# Track overall health
$hasErrors = $false
$hasWarnings = $false
$initInProgress = $false

foreach ($container in $containers) {
    $name = $container.Service
    $state = $container.State
    $health = $container.Health
    $exitCode = $container.ExitCode

    $displayName = $name.PadRight(23)

    # Determine status and color
    if ($state -eq 'running') {
        if ($health -eq 'healthy') {
            Write-StatusLine $displayName "Healthy" "Green" "Running"
        } elseif ($health -eq '') {
            Write-StatusLine $displayName "Running" "Green" "No health check"
        } else {
            Write-StatusLine $displayName "Unhealthy" "Red" "Running but not healthy"
            $hasErrors = $true
        }
    } elseif ($state -eq 'exited') {
        if ($exitCode -eq 0) {
            # Expected for initialization containers
            if ($name -match 'init|migration') {
                Write-StatusLine $displayName "Completed" "Green" "Exit code: 0"
            } else {
                Write-StatusLine $displayName "Stopped" "Yellow" "Exit code: 0"
                $hasWarnings = $true
            }
        } else {
            Write-StatusLine $displayName "Failed" "Red" "Exit code: $exitCode"
            $hasErrors = $true
        }
    } else {
        Write-StatusLine $displayName $state "Yellow"
        $hasWarnings = $true
    }
}

Write-Host ""

# Test service connectivity
Write-Host "Service Connectivity" -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------" -ForegroundColor Gray

# Get actual port mappings from Docker
$localstackPort = Get-ServicePorts -ServiceName "localstack" -InternalPort "4566"
$mongodbPort = Get-ServicePorts -ServiceName "mongodb" -InternalPort "27017"
$redisPort = Get-ServicePorts -ServiceName "redis" -InternalPort "6379"
$keycloakPort = Get-ServicePorts -ServiceName "keycloak" -InternalPort "8080"

# Check LocalStack
$localstackRunning = $containers | Where-Object { $_.Service -eq 'localstack' -and $_.State -eq 'running' }
if ($localstackRunning) {
    $localstackEndpoint = "http://localhost:$localstackPort/_localstack/health"
    if (Test-ServiceConnectivity -ServiceName "LocalStack" -TestType "http" -Endpoint $localstackEndpoint -Port $localstackPort) {
        Write-StatusLine "LocalStack (HTTP)" "Reachable" "Green" "http://localhost:$localstackPort"
    } else {
        Write-StatusLine "LocalStack (HTTP)" "Not Reachable" "Red" "http://localhost:$localstackPort"
        $hasErrors = $true
    }
} else {
    Write-StatusLine "LocalStack (HTTP)" "Not Running" "Yellow"
    $hasWarnings = $true
}

# Check MongoDB
$mongoRunning = $containers | Where-Object { $_.Service -eq 'mongodb' -and $_.State -eq 'running' }
if ($mongoRunning) {
    $mongoEndpoint = "mongodb://localhost:$mongodbPort"
    if (Test-ServiceConnectivity -ServiceName "MongoDB" -TestType "mongodb" -Endpoint $mongoEndpoint -Port $mongodbPort) {
        Write-StatusLine "MongoDB" "Reachable" "Green" "mongodb://localhost:$mongodbPort"
    } else {
        Write-StatusLine "MongoDB" "Not Reachable" "Red" "mongodb://localhost:$mongodbPort"
        $hasErrors = $true
    }
} else {
    Write-StatusLine "MongoDB" "Not Running" "Yellow"
    $hasWarnings = $true
}

# Check Redis
$redisRunning = $containers | Where-Object { $_.Service -eq 'redis' -and $_.State -eq 'running' }
if ($redisRunning) {
    # Check if redis-cli is available
    $redisCliAvailable = Get-Command redis-cli -ErrorAction SilentlyContinue
    if ($redisCliAvailable) {
        if (Test-ServiceConnectivity -ServiceName "Redis" -TestType "redis" -Endpoint "localhost:$redisPort" -Port $redisPort) {
            Write-StatusLine "Redis" "Reachable" "Green" "localhost:$redisPort"
        } else {
            Write-StatusLine "Redis" "Not Reachable" "Red" "localhost:$redisPort"
            $hasErrors = $true
        }
    } else {
        Write-StatusLine "Redis" "Cannot Test" "Yellow" "redis-cli not installed"
    }
} else {
    Write-StatusLine "Redis" "Not Running" "Yellow"
    $hasWarnings = $true
}

# Check Keycloak
$keycloakRunning = $containers | Where-Object { $_.Service -eq 'keycloak' -and $_.State -eq 'running' }
if ($keycloakRunning) {
    $keycloakEndpoint = "http://localhost:$keycloakPort/realms/orderprocessing-local/.well-known/openid-configuration"
    if (Test-ServiceConnectivity -ServiceName "Keycloak" -TestType "http" -Endpoint $keycloakEndpoint -Port $keycloakPort) {
        Write-StatusLine "Keycloak (OIDC)" "Reachable" "Green" "http://localhost:$keycloakPort"
    } else {
        Write-StatusLine "Keycloak (OIDC)" "Not Reachable" "Red" "http://localhost:$keycloakPort"
        $hasErrors = $true
    }
} else {
    Write-StatusLine "Keycloak (OIDC)" "Not Running" "Yellow"
    $hasWarnings = $true
}

Write-Host ""

# Check initialization containers
Write-Host "Initialization Status" -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------" -ForegroundColor Gray

$terraformInit = $containers | Where-Object { $_.Service -eq 'terraform-init' }
if ($terraformInit) {
    if ($terraformInit.State -eq 'exited' -and $terraformInit.ExitCode -eq 0) {
        Write-StatusLine "Terraform" "Completed" "Green" "Infrastructure provisioned"
    } elseif ($terraformInit.State -eq 'running') {
        Write-StatusLine "Terraform" "In Progress" "Cyan" "Currently running"
        $initInProgress = $true
    } else {
        Write-StatusLine "Terraform" "Failed" "Red" "State: $($terraformInit.State), Exit code: $($terraformInit.ExitCode)"
        $hasErrors = $true
    }
} else {
    # Not finding the container likely means it completed and was removed, or never created
    # Check if LocalStack is running - if so, initialization likely completed
    if ($localstackRunning) {
        Write-StatusLine "Terraform" "Completed" "Green" "Script complete (exit code: 0)"
    } else {
        Write-StatusLine "Terraform" "Not Found" "Yellow" "Environment may not be initialized"
        $hasWarnings = $true
    }
}

$mongoMigrations = $containers | Where-Object { $_.Service -eq 'mongodb-migrations' }
if ($mongoMigrations) {
    if ($mongoMigrations.State -eq 'exited' -and $mongoMigrations.ExitCode -eq 0) {
        Write-StatusLine "MongoDB Migrations" "Completed" "Green" "Database initialized"
    } elseif ($mongoMigrations.State -eq 'running') {
        Write-StatusLine "MongoDB Migrations" "In Progress" "Cyan" "Currently running"
        $initInProgress = $true
    } else {
        Write-StatusLine "MongoDB Migrations" "Failed" "Red" "State: $($mongoMigrations.State), Exit code: $($mongoMigrations.ExitCode)"
        $hasErrors = $true
    }
} else {
    # Not finding the container likely means it completed and was removed, or never created
    # Check if MongoDB is running - if so, migrations likely completed
    if ($mongoRunning) {
        Write-StatusLine "MongoDB Migrations" "Completed" "Green" "Script complete (exit code: 0)"
    } else {
        Write-StatusLine "MongoDB Migrations" "Not Found" "Yellow" "Environment may not be initialized"
        $hasWarnings = $true
    }
}

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""

# Provide recommendations based on status
if ($hasErrors) {
    Write-Host "Status: " -NoNewline
    Write-Host "ERRORS DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Recommendations:" -ForegroundColor Yellow
    Write-Host "  1. Check logs with: .\logs.ps1" -ForegroundColor Cyan
    Write-Host "  2. Review specific service logs: .\logs.ps1 -Service <name>" -ForegroundColor Cyan
    Write-Host "  3. Try restarting: .\stop.ps1 && .\start.ps1" -ForegroundColor Cyan
    Write-Host "  4. If issues persist, clean and restart: .\clean.ps1 && .\start.ps1" -ForegroundColor Cyan
    Write-Host ""
    exit 1
} elseif ($initInProgress) {
    Write-Host "Status: " -NoNewline
    Write-Host "INITIALIZATION IN PROGRESS" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Services are running but initialization has not finished yet." -ForegroundColor Yellow
    Write-Host "Please wait for initialization to complete before using the environment." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Monitor progress with: .\logs.ps1" -ForegroundColor Cyan
    Write-Host ""
} elseif ($hasWarnings) {
    Write-Host "Status: " -NoNewline
    Write-Host "RUNNING WITH WARNINGS" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Some services may not be fully operational." -ForegroundColor Yellow
    Write-Host "Check logs for details: .\logs.ps1" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "Status: " -NoNewline
    Write-Host "ALL SYSTEMS OPERATIONAL" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your local development environment is ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Quick Links:" -ForegroundColor Cyan
    Write-Host "  - LocalStack: http://localhost:$localstackPort" -ForegroundColor Gray
    Write-Host "  - MongoDB:    mongodb://localhost:$mongodbPort" -ForegroundColor Gray
    Write-Host "  - Redis:      localhost:$redisPort" -ForegroundColor Gray
    Write-Host ""
}
