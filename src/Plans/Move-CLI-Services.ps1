# Move CLI-Only Services Refactoring Script
# This script automates the process of moving CLI-only services from DeviceCommunication to CLI project

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Move CLI-Only Services to CLI Project" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "[DRY RUN MODE - No changes will be made]" -ForegroundColor Yellow
    Write-Host ""
}

# Define paths
$repoRoot = Split-Path -Parent $PSScriptRoot
$cliServicesDir = Join-Path $repoRoot "src\CLI\Services"
$dcAdapterDir = Join-Path $repoRoot "src\DeviceCommunication\Adapter"
$dcDiagnosticsDir = Join-Path $repoRoot "src\DeviceCommunication\Diagnostics"
$cliProgramFile = Join-Path $repoRoot "src\CLI\Program.cs"

# Files to move
$filesToMove = @(
    @{
        Source = Join-Path $dcAdapterDir "AdapterState.cs"
        Dest = Join-Path $cliServicesDir "AdapterState.cs"
        OldNamespace = "namespace DeviceCommunication.Adapter;"
        NewNamespace = "namespace CLI.Services;"
    },
    @{
        Source = Join-Path $dcAdapterDir "AdapterUtils.cs"
        Dest = Join-Path $cliServicesDir "AdapterUtils.cs"
        OldNamespace = "namespace DeviceCommunication.Adapter"
        NewNamespace = "namespace CLI.Services"
    },
    @{
        Source = Join-Path $dcAdapterDir "AdapterWatcher.cs"
        Dest = Join-Path $cliServicesDir "AdapterWatcher.cs"
        OldNamespace = "namespace DeviceCommunication.Adapter;"
        NewNamespace = "namespace CLI.Services;"
    },
    @{
        Source = Join-Path $dcDiagnosticsDir "BluetoothDiagnostics.cs"
        Dest = Join-Path $cliServicesDir "BluetoothDiagnostics.cs"
        OldNamespace = "namespace DeviceCommunication.Diagnostics;"
        NewNamespace = "namespace CLI.Services;"
    }
)

# Step 1: Create CLI\Services directory
Write-Host "[1/6] Creating Services directory in CLI project..." -ForegroundColor Yellow
if (-not $WhatIf) {
    New-Item -Path $cliServicesDir -ItemType Directory -Force | Out-Null
    Write-Host "  ✓ Created: $cliServicesDir" -ForegroundColor Green
} else {
    Write-Host "  Would create: $cliServicesDir" -ForegroundColor Gray
}
Write-Host ""

# Step 2: Copy and update namespace in each file
Write-Host "[2/6] Copying files and updating namespaces..." -ForegroundColor Yellow
foreach ($file in $filesToMove) {
    $fileName = Split-Path $file.Source -Leaf
    Write-Host "  Processing: $fileName" -ForegroundColor Cyan
    
    if (-not (Test-Path $file.Source)) {
        Write-Host "    ✗ Source file not found: $($file.Source)" -ForegroundColor Red
        continue
    }
    
    # Read content
    $content = Get-Content $file.Source -Raw
    
    # Replace namespace
    $updatedContent = $content -replace [regex]::Escape($file.OldNamespace), $file.NewNamespace
    
    if (-not $WhatIf) {
        # Write to new location
        Set-Content -Path $file.Dest -Value $updatedContent -NoNewline
        Write-Host "    ✓ Copied and updated: $($file.Dest)" -ForegroundColor Green
    } else {
        Write-Host "    Would copy: $($file.Source) -> $($file.Dest)" -ForegroundColor Gray
        Write-Host "    Would replace: '$($file.OldNamespace)' -> '$($file.NewNamespace)'" -ForegroundColor Gray
    }
}
Write-Host ""

# Step 3: Update Program.cs imports
Write-Host "[3/6] Updating CLI\Program.cs imports..." -ForegroundColor Yellow
if (Test-Path $cliProgramFile) {
    $programContent = Get-Content $cliProgramFile -Raw
    
    # Remove old using statements
    $programContent = $programContent -replace "using DeviceCommunication\.Adapter;\r?\n", ""
    $programContent = $programContent -replace "using DeviceCommunication\.Diagnostics;\r?\n", ""
    
    # Add new using statement at the top (after the first using if it exists)
    if ($programContent -notmatch "using CLI\.Services;") {
        # Find the position after the first 'using' line
        $lines = $programContent -split "`r?`n"
        $insertIndex = 0
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^using ") {
                $insertIndex = $i
                break
            }
        }
        $lines = @($lines[0..$insertIndex]) + @("using CLI.Services;") + @($lines[($insertIndex+1)..($lines.Count-1)])
        $programContent = $lines -join "`n"
    }
    
    if (-not $WhatIf) {
        Set-Content -Path $cliProgramFile -Value $programContent -NoNewline
        Write-Host "  ✓ Updated imports in Program.cs" -ForegroundColor Green
    } else {
        Write-Host "  Would update imports in Program.cs" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ Program.cs not found: $cliProgramFile" -ForegroundColor Red
}
Write-Host ""

# Step 4: Remove old files
Write-Host "[4/6] Removing old files from DeviceCommunication..." -ForegroundColor Yellow
foreach ($file in $filesToMove) {
    if (Test-Path $file.Source) {
        if (-not $WhatIf) {
            Remove-Item $file.Source -Force
            Write-Host "  ✓ Removed: $($file.Source)" -ForegroundColor Green
        } else {
            Write-Host "  Would remove: $($file.Source)" -ForegroundColor Gray
        }
    }
}
Write-Host ""

# Step 5: Remove empty directories
Write-Host "[5/6] Removing empty directories..." -ForegroundColor Yellow
$dirsToCheck = @($dcAdapterDir, $dcDiagnosticsDir)
foreach ($dir in $dirsToCheck) {
    if (Test-Path $dir) {
        $items = Get-ChildItem $dir
        if ($items.Count -eq 0) {
            if (-not $WhatIf) {
                Remove-Item $dir -Force
                Write-Host "  ✓ Removed empty directory: $dir" -ForegroundColor Green
            } else {
                Write-Host "  Would remove empty directory: $dir" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ⊘ Directory not empty, skipping: $dir" -ForegroundColor Yellow
        }
    }
}
Write-Host ""

# Step 6: Build and test
Write-Host "[6/6] Building solution..." -ForegroundColor Yellow
if (-not $WhatIf) {
    $slnPath = Join-Path $repoRoot "src\PodPilot.sln"
    if (Test-Path $slnPath) {
        Write-Host "  Building: $slnPath" -ForegroundColor Cyan
        $buildResult = dotnet build $slnPath --nologo --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Build successful!" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Build failed!" -ForegroundColor Red
            Write-Host "  Output: $buildResult" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ⚠ Solution file not found, skipping build" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Would build solution" -ForegroundColor Gray
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Refactoring Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  • Moved 4 service files to CLI\Services" -ForegroundColor White
Write-Host "  • Updated namespaces from DeviceCommunication.* to CLI.Services" -ForegroundColor White
Write-Host "  • Updated Program.cs imports" -ForegroundColor White
Write-Host "  • Removed old files and empty directories" -ForegroundColor White
Write-Host ""

if ($WhatIf) {
    Write-Host "This was a dry run. Run without -WhatIf to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Run the CLI to verify: dotnet run --project src\CLI\CLI.csproj" -ForegroundColor White
    Write-Host "  2. Run tests if applicable" -ForegroundColor White
    Write-Host "  3. Commit the changes: git add -A && git commit -m 'Refactor: Move CLI-only services to CLI project'" -ForegroundColor White
}
Write-Host ""
