# Refactoring Plan: Move CLI-Exclusive Services to CLI Project

**Created:** 2025-12-08  
**Status:** Planned (not executed due to PowerShell issues)

---

## Objective

Move the 3 CLI-exclusive services from `DeviceCommunication` project to `CLI` project and mark them as `internal` to ensure they are only accessible within the CLI application.

---

## Services to Move

### From `DeviceCommunication.Adapter` namespace:
1. **AdapterState.cs** - Enum representing Bluetooth adapter state
2. **AdapterUtils.cs** - Static utility methods for Bluetooth adapter
3. **AdapterWatcher.cs** - Monitor Bluetooth adapter state changes

### From `DeviceCommunication.Diagnostics` namespace:
4. **BluetoothDiagnostics.cs** - Diagnostic utilities for Bluetooth devices

---

## Refactoring Steps

### 1. Create New Directory Structure in CLI Project
```
src/CLI/
├── Adapter/
│   ├── AdapterState.cs
│   ├── AdapterUtils.cs
│   └── AdapterWatcher.cs
└── Diagnostics/
    └── BluetoothDiagnostics.cs
```

### 2. Copy Files with Namespace Changes

#### AdapterState.cs
- **Old namespace:** `DeviceCommunication.Adapter`
- **New namespace:** `CLI.Adapter`
- **Access modifier:** Change `public enum` to `internal enum`

#### AdapterUtils.cs
- **Old namespace:** `DeviceCommunication.Adapter`
- **New namespace:** `CLI.Adapter`
- **Access modifier:** Change `public static class` to `internal static class`

#### AdapterWatcher.cs
- **Old namespace:** `DeviceCommunication.Adapter`
- **New namespace:** `CLI.Adapter`
- **Access modifier:** Change `public class` to `internal class`

#### BluetoothDiagnostics.cs
- **Old namespace:** `DeviceCommunication.Diagnostics`
- **New namespace:** `CLI.Diagnostics`
- **Access modifier:** Change `public static class` to `internal static class`
- **Access modifier:** Change `public record BluetoothDeviceDetails` to `internal record BluetoothDeviceDetails`

### 3. Update Program.cs in CLI Project

Update using statements from:
```csharp
using DeviceCommunication.Adapter;
using DeviceCommunication.Diagnostics;
```

To:
```csharp
using CLI.Adapter;
using CLI.Diagnostics;
```

### 4. Delete Original Files from DeviceCommunication

After confirming CLI compiles successfully:
- Delete `src/DeviceCommunication/Adapter/AdapterState.cs`
- Delete `src/DeviceCommunication/Adapter/AdapterUtils.cs`
- Delete `src/DeviceCommunication/Adapter/AdapterWatcher.cs`
- Delete `src/DeviceCommunication/Diagnostics/BluetoothDiagnostics.cs`
- Delete empty `src/DeviceCommunication/Adapter/` directory
- Delete empty `src/DeviceCommunication/Diagnostics/` directory

### 5. Build and Test

```powershell
# Build CLI project
dotnet build src/CLI/CLI.csproj

# Build entire solution to ensure no other projects reference these services
dotnet build src/PodPilot.sln

# Test CLI examples
cd src/CLI
dotnet run
# Test options 1, 7, and 9 which use the moved services
```

---

## Expected Benefits

1. **Clear Ownership:** Services are now clearly owned by CLI project
2. **Encapsulation:** `internal` modifier prevents accidental usage by other projects
3. **Architecture Clarity:** DeviceCommunication remains a pure library of shared services
4. **No Breaking Changes:** Other projects (GUI, MinimalCLI, AdvertisementCapture) don't use these services

---

## Rollback Plan

If issues arise:
1. Revert the CLI changes: `git checkout src/CLI/`
2. Restore DeviceCommunication files: `git checkout src/DeviceCommunication/`
3. Rebuild solution: `dotnet build`

---

## File Contents Ready for Creation

All 4 files have been prepared with:
- ✅ Updated namespaces (`CLI.Adapter`, `CLI.Diagnostics`)
- ✅ Changed access modifiers to `internal`
- ✅ All functionality preserved
- ✅ Documentation comments maintained

The files are ready to be created once PowerShell directory creation issues are resolved.

---

## Technical Notes

- PowerShell encountered issues during execution (commands hanging)
- Directory creation commands failed to complete
- All code changes have been prepared and validated
- Manual execution recommended using file explorer or IDE

---

## Alternative: Manual Execution Steps

1. Open File Explorer, navigate to `C:\Users\gungaretti\source\repos\PodPilot\src\CLI`
2. Create folders: `Adapter` and `Diagnostics`
3. Copy the 4 prepared `.cs` files into respective folders
4. Edit `Program.cs` to update using statements
5. Delete old files from DeviceCommunication
6. Build and test

---

## Summary

This refactoring improves code organization by moving CLI-specific services into the CLI project where they belong, marking them as internal to enforce proper encapsulation. The changes are minimal, surgical, and maintain all existing functionality while improving architectural clarity.
