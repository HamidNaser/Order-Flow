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
function Test-ServiceHealth {
    param(
        [string]$Name,
        [string]$ServiceName,
        [string]$Port,
        [string]$TestType = "tcp"
    )

    Write-Host "  [$Name]" -NoNewline

    # Get container status
    $status = docker-compose ps --format json $ServiceName 2>$null | ConvertFrom-Json

    if (-not $status) {
        Write-Host " NOT RUNNING" -ForegroundColor Red
        return $false
    }

    $state = $status.State
    $health = $status.Health

    if ($state -eq "running" -and ($health -eq "healthy" -or -not $health)) {
        Write-Host " RUNNING" -ForegroundColor Green -NoNewline

        # Test connectivity based on type
        if ($TestType -eq "http") {
            $url = "http://localhost:$Port"
            try {
                $response = Invoke-WebRequest -Uri $url -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
                Write-Host " | HTTP OK ($url)" -ForegroundColor Green
                return $true
            }
            catch {
                Write-Host " | HTTP FAILED ($url)" -ForegroundColor Yellow
                return $false
            }
        }
        elseif ($TestType -eq "tcp") {
            try {
                $tcpClient = New-Object System.Net.Sockets.TcpClient
                $tcpClient.Connect("localhost", $Port)
                $tcpClient.Close()
                Write-Host " | TCP OK (localhost:$Port)" -ForegroundColor Green
                return $true
            }
            catch {
                Write-Host " | TCP FAILED (localhost:$Port)" -ForegroundColor Yellow
                return $false
            }
        }
        else {
            Write-Host "" -ForegroundColor Green
            return $true
        }
    }
    elseif ($state -eq "running" -and $health -eq "starting") {
        Write-Host " STARTING" -ForegroundColor Yellow
        return $false
    }
    elseif ($state -eq "exited") {
        Write-Host " EXITED" -ForegroundColor Gray
        return $false
    }
    else {
        Write-Host " UNHEALTHY ($state)" -ForegroundColor Red
        return $false
    }
}

# Check Docker Desktop
Write-Host "Docker Desktop:" -ForegroundColor White
try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [Docker] RUNNING" -ForegroundColor Green
    }
    else {
        Write-Host "  [Docker] NOT RUNNING" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Host "  [Docker] NOT RUNNING" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Check persistent services
Write-Host "Persistent Services:" -ForegroundColor White

$localstackPort = Get-ServicePorts -ServiceName "localstack" -InternalPort "4566"
$redisPort = Get-ServicePorts -ServiceName "redis" -InternalPort "6380"

$localstackHealthy = Test-ServiceHealth -Name "LocalStack" -ServiceName "localstack" -Port $localstackPort -TestType "http"
$redisHealthy = Test-ServiceHealth -Name "Redis" -ServiceName "redis" -Port $redisPort -TestType "tcp"

Write-Host ""

# Check initialization containers
Write-Host "Initialization Containers:" -ForegroundColor White

$terraformStatus = docker-compose ps -a --format json terraform-init 2>$null | ConvertFrom-Json
if ($terraformStatus) {
    Write-Host "  [terraform-init]" -NoNewline
    if ($terraformStatus.State -eq "exited") {
        if ($terraformStatus.ExitCode -eq 0) {
            Write-Host " COMPLETED" -ForegroundColor Green
        }
        else {
            Write-Host " FAILED (exit code: $($terraformStatus.ExitCode))" -ForegroundColor Red
        }
    }
    elseif ($terraformStatus.State -eq "running") {
        Write-Host " RUNNING" -ForegroundColor Yellow
    }
    else {
        Write-Host " $($terraformStatus.State.ToUpper())" -ForegroundColor Gray
    }
}
else {
    Write-Host "  [terraform-init] NOT FOUND" -ForegroundColor Gray
}

Write-Host ""

# Summary
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""

$allHealthy = $localstackHealthy -and $redisHealthy

if ($allHealthy) {
    Write-Host "Status: ALL SERVICES HEALTHY" -ForegroundColor Green
}
else {
    Write-Host "Status: SOME SERVICES UNHEALTHY" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Quick Commands:" -ForegroundColor White
Write-Host "  View logs:       .\logs.ps1 [service-name]" -ForegroundColor Gray
Write-Host "  Restart:         .\start.ps1" -ForegroundColor Gray
Write-Host "  Stop:            .\stop.ps1" -ForegroundColor Gray
Write-Host "  Clean:           .\clean.ps1" -ForegroundColor Gray
Write-Host ""

# Check if any containers are in bad state
$badContainers = docker-compose ps -a --format json 2>$null | ConvertFrom-Json | Where-Object {
    $_.State -ne "running" -and $_.State -ne "exited" -and $_.State -ne "removing"
}

if ($badContainers) {
    Write-Host "Containers in unexpected state:" -ForegroundColor Yellow
    foreach ($container in $badContainers) {
        Write-Host "  - $($container.Service): $($container.State)" -ForegroundColor Yellow
    }
    Write-Host ""
}

exit 0
