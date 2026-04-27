#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Cleans the local development environment completely.

.DESCRIPTION
    This script stops all Docker Compose services, removes all containers,
    volumes, and Terraform state files. This will delete all data.

.PARAMETER Force
    Skip confirmation prompt.

.EXAMPLE
    .\clean.ps1
    Cleans the environment after confirmation.

.EXAMPLE
    .\clean.ps1 -Force
    Cleans the environment without confirmation.
#>

[CmdletBinding()]
param(
    [switch]$Force
)

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
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  Cleaning Local Development Environment" -ForegroundColor Red
    Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host ""

    # Navigate to the correct directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    Set-Location $scriptDir

    # Check if docker-compose.yml exists
    if (-not (Test-Path "docker-compose.yml")) {
        Write-Error "docker-compose.yml not found in current directory: $scriptDir"
        exit 1
    }

    # Prompt for confirmation unless -Force is specified
    if (-not $Force) {
        Write-Warning "This will stop and remove all containers, volumes, and data!"
        Write-Warning "This action cannot be undone."
        Write-Host ""
        $confirmation = Read-Host "Are you sure you want to continue? (yes/no)"

        if ($confirmation -ne "yes") {
            Write-Info "Cleanup cancelled."
            exit 0
        }
    }

    Write-Host ""

    # Stop and remove Docker Compose services and volumes
    Write-Info "Stopping and removing all Docker Compose services and volumes..."
    docker-compose down -v
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to clean Docker Compose services"
        exit 1
    }
    Write-Success "Docker Compose services and volumes removed"

    Write-Host ""

    # Remove Terraform state files if they exist
    $terraformDir = Join-Path $scriptDir "terraform"
    if (Test-Path $terraformDir) {
        Write-Info "Removing Terraform state files..."

        $stateFiles = @(
            "terraform.tfstate",
            "terraform.tfstate.backup",
            ".terraform.lock.hcl",
            "errored.tfstate"
        )

        $removedFiles = 0
        foreach ($file in $stateFiles) {
            $filePath = Join-Path $terraformDir $file
            if (Test-Path $filePath) {
                Remove-Item $filePath -Force
                Write-Success "Removed $file"
                $removedFiles++
            }
        }

        # Remove .terraform directory if it exists
        $terraformCacheDir = Join-Path $terraformDir ".terraform"
        if (Test-Path $terraformCacheDir) {
            Remove-Item $terraformCacheDir -Recurse -Force
            Write-Success "Removed .terraform directory"
            $removedFiles++
        }

        if ($removedFiles -gt 0) {
            Write-Success "Removed $removedFiles Terraform state file(s)"
        }
        else {
            Write-Info "No Terraform state files found to remove"
        }
    }

    # Remove .localstack_data directory if it exists
    $localstackDir = Join-Path $scriptDir ".localstack_data"
    if (Test-Path $localstackDir) {
        Remove-Item $localstackDir -Recurse -Force
        Write-Success "Removed .localstack_data directory"
        $removedFiles++
    }

    # Display cleanup summary
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Success "Local development environment cleaned successfully!"
    Write-Host ""
    Write-Info "Cleanup summary:"
    Write-Host "  ✓ All containers stopped and removed"
    Write-Host "  ✓ All volumes removed (data deleted)"
    Write-Host "  ✓ Terraform state files removed"
    Write-Host ""
    Write-Info "To start fresh, run: .\start.ps1"
    Write-Host ""

    exit 0
}
catch {
    Write-Error "An unexpected error occurred: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
