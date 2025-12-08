# CLI Services Refactoring - Quick Start

## Summary

This refactoring moves 3 CLI-only services from `DeviceCommunication` to the `CLI` project for better code organization.

## Services Being Moved

1. **AdapterWatcher** - Monitors Bluetooth adapter state changes
2. **AdapterUtils** - Utility functions for adapter management  
3. **AdapterState** - Enum for adapter state (On/Off)
4. **BluetoothDiagnostics** - Generates diagnostic reports

These services are **only used by the main CLI application** and not by GUI, MinimalCLI, AdvertisementCapture, or ConnectionTestCLI.

## Quick Start

### Option 1: Automated (Recommended)

```powershell
# Run from repository root
cd c:\Users\gungaretti\source\repos\PodPilot.worktrees\worktree-2025-12-08T12-54-28

# Preview changes (dry run)
.\src\Plans\Move-CLI-Services.ps1 -WhatIf

# Execute refactoring
.\src\Plans\Move-CLI-Services.ps1
```

The script will:
- ✅ Create `CLI\Services` directory
- ✅ Copy and update 4 service files
- ✅ Update namespaces from `DeviceCommunication.*` to `CLI.Services`
- ✅ Update `Program.cs` imports
- ✅ Remove old files from `DeviceCommunication`
- ✅ Build and verify the solution

### Option 2: Manual

Follow the step-by-step instructions in [`src/Plans/REFACTOR-Move-CLI-Services.md`](REFACTOR-Move-CLI-Services.md)

## After Refactoring

### 1. Test the CLI

```powershell
dotnet run --project src\CLI\CLI.csproj
```

Test each example to ensure everything works:
- Example 1: Monitor adapter state (uses AdapterWatcher)
- Example 7: Bluetooth diagnostics (uses BluetoothDiagnostics)

### 2. Verify the Changes

```powershell
# Check moved files exist
dir src\CLI\Services

# Check old files are gone
dir src\DeviceCommunication\Adapter      # Should not exist
dir src\DeviceCommunication\Diagnostics  # Should not exist
```

### 3. Commit

```powershell
git add -A
git commit -m "Refactor: Move CLI-only services to CLI project

- Moved AdapterWatcher, AdapterUtils, AdapterState to CLI\Services
- Moved BluetoothDiagnostics to CLI\Services
- Updated namespaces to CLI.Services
- Removed empty DeviceCommunication subdirectories

These services are only used by the CLI application and not shared
with GUI or other projects. Moving them reduces coupling and improves
code organization."
```

## Documentation

- **Full Analysis:** [`CLI-Exclusive-Components-Implementation.md`](CLI-Exclusive-Components-Implementation.md)
- **Manual Steps:** [`REFACTOR-Move-CLI-Services.md`](REFACTOR-Move-CLI-Services.md)
- **Original Plan:** [`../../../PodPilot/CLI-Exclusive-Components-Analysis.md`](../../../PodPilot/CLI-Exclusive-Components-Analysis.md)

## Rollback

If something goes wrong:

```powershell
git checkout src\DeviceCommunication\Adapter\
git checkout src\DeviceCommunication\Diagnostics\
git clean -fd src\CLI\Services\
git checkout src\CLI\Program.cs
```

## Why This Refactoring?

✅ **Better Organization:** CLI-only code lives in the CLI project
✅ **Reduced Coupling:** DeviceCommunication doesn't contain CLI-specific services
✅ **YAGNI Principle:** Move shared when actually needed, not speculatively
✅ **Clearer Dependencies:** Easier to see what each project actually uses

---

**Status:** Ready to execute
**Script:** `src/Plans/Move-CLI-Services.ps1`
**Estimated Time:** < 1 minute
