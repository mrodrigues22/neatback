# Python Service Installer Fix Plan

## Problem
The Python service executable (`slouti-service.exe`) was successfully built with PyInstaller but is **not included** in the MSIX installer package. This means when users install the app, the Python service is missing and the app won't function properly.

## Current State
- ✅ Python service builds successfully to `python-service\dist\slouti-service.exe`
- ❌ Python service is not copied to dotnet-app during build
- ❌ Python service is not included in the MSIX package
- ❌ Installer doesn't bundle the Python service

## Solution Overview
We need to configure the build process to:
1. Copy the Python service executable to the dotnet app's output directory
2. Include it in the MSIX package
3. Ensure it's deployed with the app

---

## Step-by-Step Implementation

### Step 1: Configure Slouti.csproj to Include Python Service

**Location:** `dotnet-app\Slouti.csproj`

**Action:** Add build targets to copy the Python service executable and its dependencies

Add this section before the closing `</Project>` tag:

```xml
<!-- Copy Python Service files to output directory -->
<ItemGroup>
    <PythonServiceFiles Include="..\python-service\dist\slouti-service.exe" />
</ItemGroup>

<Target Name="CopyPythonService" AfterTargets="Build">
    <Message Text="Copying Python service files..." Importance="high" />
    <Copy SourceFiles="@(PythonServiceFiles)" 
          DestinationFolder="$(OutDir)PythonService" 
          SkipUnchangedFiles="true" />
</Target>

<!-- Include Python Service in MSIX package -->
<ItemGroup>
    <Content Include="..\python-service\dist\slouti-service.exe" 
             Link="PythonService\slouti-service.exe">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

**Why:** This ensures the Python service is copied to the build output and marked as content to be included in the MSIX package.

---

### Step 2: Verify Python Service Path in Code

**Location:** Check `dotnet-app\Services\WebSocketClient.cs` or wherever the Python service is launched

**Action:** Ensure the code looks for the Python service in the correct location relative to the app package

The path should be something like:
```csharp
string pythonServicePath = Path.Combine(
    Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
    "PythonService",
    "slouti-service.exe"
);
```

---

### Step 3: Update Package.appxmanifest

**Location:** `dotnet-app\Package.appxmanifest`

**Action:** Verify the app has permission to run executable files

Ensure these capabilities are present:
```xml
<Capabilities>
    <rescap:Capability Name="runFullTrust" />
</Capabilities>
```

And the application execution alias if needed:
```xml
<uap5:Extension Category="windows.appExecutionAlias">
    <uap5:AppExecutionAlias>
        <desktop:ExecutionAlias Alias="slouti-service.exe" />
    </uap5:AppExecutionAlias>
</uap5:Extension>
```

---

### Step 4: Pre-Build Python Service

**Create a new file:** `build-all.ps1` in the root directory

```powershell
# Build script to compile Python service and then the app

Write-Host "Step 1: Building Python service..." -ForegroundColor Cyan
Push-Location python-service

# Check if pyinstaller exists
if (-not (Get-Command pyinstaller -ErrorAction SilentlyContinue)) {
    Write-Host "Installing PyInstaller..." -ForegroundColor Yellow
    pip install pyinstaller
}

# Build the Python service
if (Test-Path "slouti-service.spec") {
    pyinstaller slouti-service.spec --clean
} else {
    # Create a basic spec if it doesn't exist
    pyinstaller src/main.py --name slouti-service --onefile --clean --add-data "src/face_landmarker.task;." --add-data "src/pose_landmarker.task;."
}

if (-not (Test-Path "dist\slouti-service.exe")) {
    Write-Host "ERROR: Python service build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host "✓ Python service built successfully" -ForegroundColor Green
Pop-Location

Write-Host "`nStep 2: Building .NET app..." -ForegroundColor Cyan
dotnet build dotnet-app\Slouti.csproj -c Release

Write-Host "`nStep 3: Building installer..." -ForegroundColor Cyan
msbuild Installer\Installer.wapproj /p:Configuration=Release /p:Platform=x64

Write-Host "`n✓ Build complete!" -ForegroundColor Green
Write-Host "Installer location: Installer\AppPackages\" -ForegroundColor Yellow
```

---

### Step 5: Alternative - Manual Copy Method (Simpler)

If the above MSBuild integration is complex, use this simpler approach:

**Create:** `copy-python-service.ps1` in the root directory

```powershell
# Simple script to copy Python service to the right location

$pythonServiceExe = "python-service\dist\slouti-service.exe"
$destinations = @(
    "dotnet-app\bin\Debug\net8.0-windows10.0.19041.0\win-x64\PythonService",
    "dotnet-app\bin\Release\net8.0-windows10.0.19041.0\win-x64\PythonService",
    "dotnet-app\bin\x64\Release\net8.0-windows10.0.19041.0\PythonService"
)

if (-not (Test-Path $pythonServiceExe)) {
    Write-Host "ERROR: Python service not found at $pythonServiceExe" -ForegroundColor Red
    Write-Host "Run: cd python-service; pyinstaller slouti-service.spec --clean" -ForegroundColor Yellow
    exit 1
}

foreach ($dest in $destinations) {
    if (-not (Test-Path $dest)) {
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
    }
    Copy-Item $pythonServiceExe $dest -Force
    Write-Host "✓ Copied to: $dest" -ForegroundColor Green
}

Write-Host "`n✓ Python service copied to all build output directories" -ForegroundColor Green
```

---

## Build Process (Updated Workflow)

### Option A: Using build-all.ps1 (Automated)
```powershell
# From the root directory
.\build-all.ps1
```

### Option B: Manual Steps
```powershell
# Step 1: Build Python service
cd python-service
pyinstaller slouti-service.spec --clean
cd ..

# Step 2: Copy Python service
.\copy-python-service.ps1

# Step 3: Build the app
dotnet build dotnet-app\Slouti.csproj -c Release /p:Platform=x64

# Step 4: Build the installer
msbuild Installer\Installer.wapproj /p:Configuration=Release /p:Platform=x64
```

---

## Verification Checklist

After implementing the fix and building the installer:

- [ ] Python service builds successfully (`python-service\dist\slouti-service.exe` exists)
- [ ] Python service is in the dotnet app output (`dotnet-app\bin\Release\...\PythonService\slouti-service.exe`)
- [ ] MSIX package includes the Python service (extract .msix and check)
- [ ] Install the app and verify the executable is in the installation directory
- [ ] Run the app and confirm it successfully starts the Python service
- [ ] Check Windows Event Viewer for any app errors

---

## Testing the Fix

### Extract and Inspect MSIX Package
```powershell
# Navigate to installer output
cd Installer\AppPackages\Installer_*_Test

# Extract the MSIX bundle
Expand-Archive Installer_*.msixbundle -DestinationPath extracted

# Check if Python service is included
Get-ChildItem -Recurse extracted | Where-Object { $_.Name -eq "slouti-service.exe" }
```

### After Installation
```powershell
# Find installation path
$installPath = (Get-AppxPackage | Where-Object { $_.Name -like "*Slouti*" }).InstallLocation

# Check if Python service exists
Test-Path "$installPath\PythonService\slouti-service.exe"
```

---

## Common Issues and Solutions

### Issue 1: "Python service not found" at runtime
**Solution:** The path in the C# code is incorrect. Update to use `Package.Current.InstalledLocation.Path`

### Issue 2: Python service files not copying
**Solution:** Make sure to rebuild the entire solution, not just the installer project

### Issue 3: MSIX package too large
**Solution:** PyInstaller creates a large executable. Consider using `--exclude-module` to remove unnecessary packages

### Issue 4: Python service won't start
**Solution:** Check Windows Defender or antivirus - they may be blocking the executable

---

## Next Steps

1. **Implement Step 1** (modify Slouti.csproj) for automatic inclusion
2. **Create build-all.ps1** for one-command building
3. **Test the build** and verify Python service is included
4. **Create installer** and test installation
5. **Verify runtime** functionality

---

## Quick Fix (If You Need Installer NOW)

If you need a working installer immediately without code changes:

1. Build Python service: `cd python-service; pyinstaller slouti-service.spec --clean`
2. Manually copy `python-service\dist\slouti-service.exe` to `dotnet-app\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PythonService\`
3. Create the PythonService folder if it doesn't exist
4. Rebuild the installer: `msbuild Installer\Installer.wapproj /p:Configuration=Release /p:Platform=x64`

This is a temporary workaround - implement the proper solution above for sustainable builds.
