#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Stops the local development environment services.

.DESCRIPTION
    This script stops all Docker Compose services gracefully, preserving all data.
    Use clean.ps1 if you want to remove all data.

.EXAMPLE
    .\stop.ps1
    Stops all local development services.
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

# Main script execution
try {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Stopping Local Development Environment" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    # Navigate to the correct directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    Set-Location $scriptDir

    # Check if docker-compose.yml exists
    if (-not (Test-Path "docker-compose.yml")) {
        Write-Error "docker-compose.yml not found in current directory: $scriptDir"
        exit 1
    }

    # Stop Docker Compose services
    Write-Info "Stopping Docker Compose services..."
    docker-compose stop
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to stop Docker Compose services"
        exit 1
    }

    Write-Host ""
    Write-Success "All services stopped successfully"

    # Display stopped containers
    Write-Host ""
    Write-Info "Stopped containers:"
    docker-compose ps -a

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Info "Data has been preserved. Services can be restarted with: .\start.ps1"
    Write-Warning "To remove all data and containers, use: .\clean.ps1"
    Write-Host ""

    exit 0
}
catch {
    Write-Error "An unexpected error occurred: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
