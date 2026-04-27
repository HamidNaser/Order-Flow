#!/usr/bin/env pwsh

<#
.SYNOPSIS
    View logs from local development environment services.

.DESCRIPTION
    Display logs from Docker Compose services with filtering options.

.PARAMETER Service
    Optional service name to show logs for (localstack, mongodb, redis, keycloak, terraform-init, mongodb-migrations).
    If not specified, shows logs from all services.

.PARAMETER Follow
    Stream logs in real-time (like tail -f).

.PARAMETER Tail
    Number of lines to show from the end of logs. Default is 50.

.EXAMPLE
    .\logs.ps1
    Show last 50 lines from all services

.EXAMPLE
    .\logs.ps1 -Service localstack
    Show last 50 lines from LocalStack service

.EXAMPLE
    .\logs.ps1 -Service mongodb -Follow
    Stream MongoDB logs in real-time

.EXAMPLE
    .\logs.ps1 -Tail 100
    Show last 100 lines from all services

.EXAMPLE
    .\logs.ps1 -Service terraform-init -Tail 200
    Show last 200 lines from terraform-init container (including exited)
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('localstack', 'mongodb', 'redis', 'keycloak', 'terraform-init', 'mongodb-migrations')]
    [string]$Service,

    [Parameter()]
    [switch]$Follow,

    [Parameter()]
    [ValidateRange(1, 10000)]
    [int]$Tail = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Navigate to the directory containing docker-compose.yml
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "==> Viewing logs for local development environment" -ForegroundColor Cyan
Write-Host ""

# Check if Docker Desktop is running
try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Docker Desktop is not running." -ForegroundColor Red
        Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "Error: Cannot connect to Docker." -ForegroundColor Red
    Write-Host "Please ensure Docker Desktop is installed and running." -ForegroundColor Yellow
    exit 1
}

# Check if docker-compose.yml exists
if (-not (Test-Path "docker-compose.yml")) {
    Write-Host "Error: docker-compose.yml not found in current directory." -ForegroundColor Red
    exit 1
}

# Build docker-compose logs command
$composeArgs = @('logs')

# Add tail parameter
$composeArgs += '--tail', $Tail.ToString()

# Add follow flag if specified
if ($Follow) {
    $composeArgs += '--follow'
}

# Add timestamps
$composeArgs += '--timestamps'

# Add service name if specified
if ($Service) {
    $composeArgs += $Service
    Write-Host "Showing logs for service: $Service" -ForegroundColor Green
} else {
    Write-Host "Showing logs for all services" -ForegroundColor Green
}

if ($Follow) {
    Write-Host "Streaming logs (press Ctrl+C to stop)..." -ForegroundColor Yellow
} else {
    Write-Host "Showing last $Tail lines..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host ""

# Execute docker-compose logs
try {
    docker-compose $composeArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Warning: docker-compose logs returned non-zero exit code." -ForegroundColor Yellow

        if ($Service) {
            Write-Host "The '$Service' service may not exist or may have exited." -ForegroundColor Yellow
            Write-Host "Run '.\status.ps1' to check service status." -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host ""
    Write-Host "Error viewing logs: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Gray

if (-not $Follow) {
    Write-Host ""
    Write-Host "Tip: Use -Follow to stream logs in real-time" -ForegroundColor Cyan
    Write-Host "Tip: Use -Tail <number> to show more/fewer lines" -ForegroundColor Cyan
    Write-Host "Tip: Run '.\status.ps1' to check overall service health" -ForegroundColor Cyan
}
