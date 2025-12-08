# Move CLI-Only Services to CLI Project

## Overview
This script moves the 3 CLI-only services from `DeviceCommunication` to the `CLI` project.

## Services to Move
1. `AdapterWatcher.cs`
2. `AdapterUtils.cs`
3. `AdapterState.cs`
4. `BluetoothDiagnostics.cs`

## Steps to Execute

### 1. Create Services Directory
```powershell
New-Item -Path "src\CLI\Services" -ItemType Directory -Force
```

### 2. Copy Files to New Location
```powershell
# Copy adapter files
Copy-Item "src\DeviceCommunication\Adapter\AdapterState.cs" "src\CLI\Services\" -Force
Copy-Item "src\DeviceCommunication\Adapter\AdapterUtils.cs" "src\CLI\Services\" -Force
Copy-Item "src\DeviceCommunication\Adapter\AdapterWatcher.cs" "src\CLI\Services\" -Force

# Copy diagnostics file
Copy-Item "src\DeviceCommunication\Diagnostics\BluetoothDiagnostics.cs" "src\CLI\Services\" -Force
```

### 3. Update Namespaces in Copied Files

**AdapterState.cs:**
```csharp
// Change namespace from:
namespace DeviceCommunication.Adapter;
// To:
namespace CLI.Services;
```

**AdapterUtils.cs:**
```csharp
// Change namespace from:
namespace DeviceCommunication.Adapter
// To:
namespace CLI.Services
```

**AdapterWatcher.cs:**
```csharp
// Change namespace from:
namespace DeviceCommunication.Adapter;
// To:
namespace CLI.Services;
```

**BluetoothDiagnostics.cs:**
```csharp
// Change namespace from:
namespace DeviceCommunication.Diagnostics;
// To:
namespace CLI.Services;
```

### 4. Update Program.cs Imports

**File:** `src\CLI\Program.cs`

**Change lines 1-4 from:**
```csharp
using DeviceCommunication.Adapter;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Diagnostics;
```

**To:**
```csharp
using CLI.Services;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
```

### 5. Remove Old Files
```powershell
# Remove from DeviceCommunication
Remove-Item "src\DeviceCommunication\Adapter\AdapterState.cs" -Force
Remove-Item "src\DeviceCommunication\Adapter\AdapterUtils.cs" -Force
Remove-Item "src\DeviceCommunication\Adapter\AdapterWatcher.cs" -Force
Remove-Item "src\DeviceCommunication\Diagnostics\BluetoothDiagnostics.cs" -Force

# Remove empty directories
Remove-Item "src\DeviceCommunication\Adapter" -Force
Remove-Item "src\DeviceCommunication\Diagnostics" -Force
```

### 6. Build and Test
```powershell
# Build the solution
dotnet build src\PodPilot.sln

# Run the CLI to verify
dotnet run --project src\CLI\CLI.csproj
```

## Verification Checklist

- [ ] Services directory created in CLI project
- [ ] All 4 files copied to CLI\Services
- [ ] Namespaces updated in all 4 files
- [ ] Program.cs imports updated
- [ ] Old files removed from DeviceCommunication
- [ ] Solution builds without errors
- [ ] CLI runs and all examples work

## Rollback

If something goes wrong, you can restore the original files:
```powershell
git checkout src\DeviceCommunication\Adapter\
git checkout src\DeviceCommunication\Diagnostics\
git clean -fd src\CLI\Services\
```

## Notes

- The services are only used by the main CLI application
- Moving them makes the codebase cleaner and more maintainable  
- GUI, MinimalCLI, AdvertisementCapture, and ConnectionTestCLI do not use these services
- This change reduces coupling between CLI and DeviceCommunication

