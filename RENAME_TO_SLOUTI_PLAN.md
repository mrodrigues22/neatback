# Complete Renaming Plan: Slouti → Slouti

## Overview
This document provides a comprehensive step-by-step plan to rename the Slouti application to Slouti. The process involves updating project files, source code, namespaces, package identifiers, and documentation.

---

## 📋 Pre-Renaming Checklist

- [ ] **Backup the entire project** (create a copy or commit all changes to version control)
- [ ] **Close Visual Studio/VS Code** and all running instances of the application
- [ ] **Stop all running services** (Python service, WebSocket server, etc.)
- [ ] **Document current package identity** for reference during migration

---

## 🗂️ Phase 1: Project Structure Renaming

### 1.1 Root Directory
```
Current: c:\Users\vlnco\OneDrive - Corteva\Documents\Apps\slouti
Target:  c:\Users\vlnco\OneDrive - Corteva\Documents\Apps\slouti
```
- [ ] Rename the root directory from `slouti` to `slouti`

### 1.2 .NET Application Directory
```
Current: dotnet-app/
Target:  dotnet-app/ (can remain the same, or rename to slouti-app/)
```
- [ ] **Option A**: Keep as `dotnet-app/`
- [ ] **Option B**: Rename to `slouti-app/`

---

## 🔧 Phase 2: .NET Project Files

### 2.1 Solution File
**File**: `dotnet-app/Slouti.slnx`
- [ ] Rename file to `Slouti.slnx`
- [ ] Update internal references to `Slouti.csproj` → `Slouti.csproj`

### 2.2 Project File
**File**: `dotnet-app/Slouti.csproj`
- [ ] Rename file to `Slouti.csproj`
- [ ] Update the following properties:
  ```xml
  <PropertyGroup>
    <ProjectName>Slouti</ProjectName>
    <ApplicationTitle>Slouti</ApplicationTitle>
    <ApplicationDisplayName>Slouti</ApplicationDisplayName>
    <AssemblyName>Slouti</AssemblyName>
    <RootNamespace>Slouti</RootNamespace>
  </PropertyGroup>
  ```

### 2.3 User Project File
**File**: `dotnet-app/Slouti.csproj.user`
- [ ] Rename to `Slouti.csproj.user`
- [ ] Update any internal references to Slouti

### 2.4 NuGet Files
**Files**: `dotnet-app/obj/Slouti.csproj.nuget.*`
- [ ] Delete these files (they will be regenerated with correct names on build)
- [ ] `Slouti.csproj.nuget.dgspec.json`
- [ ] `Slouti.csproj.nuget.g.props`
- [ ] `Slouti.csproj.nuget.g.targets`

---

## 📦 Phase 3: Package Identity & Manifest

### 3.1 Package Manifest
**File**: `dotnet-app/Package.appxmanifest`
- [ ] Update `<Identity>` element:
  ```xml
  <Identity
    Name="Slouti"
    Publisher="CN=YourPublisher"
    Version="1.0.0.0" />
  ```
- [ ] Update `<Properties>`:
  ```xml
  <Properties>
    <DisplayName>Slouti</DisplayName>
    <PublisherDisplayName>YourPublisherName</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  ```
- [ ] Update `<Applications>`:
  ```xml
  <Application Id="Slouti"
    Executable="$targetnametoken$.exe"
    EntryPoint="$targetentrypoint$">
    <uap:VisualElements
      DisplayName="Slouti"
      Description="Slouti - Posture Monitoring"
      ...>
    </uap:VisualElements>
  </Application>
  ```

### 3.2 App Manifest
**File**: `dotnet-app/app.manifest`
- [ ] Update assembly identity:
  ```xml
  <assemblyIdentity name="Slouti" ... />
  ```
- [ ] Update description:
  ```xml
  <description>Slouti Application</description>
  ```

---

## 💻 Phase 4: Source Code Files

### 4.1 Namespace Declarations
Update all C# files to use the new namespace:

**Files to update**:
- [ ] `dotnet-app/App.xaml.cs`
- [ ] `dotnet-app/Imports.cs`
- [ ] `dotnet-app/Models/PostureData.cs`
- [ ] `dotnet-app/Services/NotificationService.cs`
- [ ] `dotnet-app/Services/WebSocketClient.cs`
- [ ] `dotnet-app/Views/MainPage.xaml.cs`

**Changes**:
```csharp
// FROM:
namespace Slouti;
namespace Slouti.Models;
namespace Slouti.Services;
namespace Slouti.Views;

// TO:
namespace Slouti;
namespace Slouti.Models;
namespace Slouti.Services;
namespace Slouti.Views;
```

### 4.2 XAML Files
Update XML namespace references:

**File**: `dotnet-app/App.xaml`
- [ ] Update `x:Class="Slouti.App"` → `x:Class="Slouti.App"`
- [ ] Update any other references to Slouti namespace

**File**: `dotnet-app/Views/MainPage.xaml`
- [ ] Update `x:Class="Slouti.Views.MainPage"` → `x:Class="Slouti.Views.MainPage"`
- [ ] Update any bindings or references to Slouti types

### 4.3 Using Directives
Update all `using Slouti.*;` statements to `using Slouti.*;` in:
- [ ] All .cs files that reference the application namespace

### 4.4 String Literals & Comments
Search for "Slouti" in all source files and update:
- [ ] Window titles
- [ ] Log messages
- [ ] Error messages
- [ ] User-facing text
- [ ] Comments and documentation
- [ ] Debug output strings

---

## 🗃️ Phase 5: Build & Output Directories

### 5.1 Clean Build Artifacts
- [ ] Delete `dotnet-app/bin/` directory
- [ ] Delete `dotnet-app/obj/` directory
- [ ] Delete `dotnet-app/BundleArtifacts/` directory
- [ ] Delete `dotnet-app/AppPackages/` directory

These will be regenerated with the correct name on the next build.

---

## 📝 Phase 6: Documentation & README Files

### 6.1 README Files
**Files to update**:
- [ ] `README.md`
- [ ] `README_NEW.md`
- [ ] `QUICKSTART.md`
- [ ] `HIGH_LEVEL_OVERVIEW.md`
- [ ] `DETAILED_TECHNICAL_GUIDE.md`
- [ ] `IMPLEMENTATION_PLAN.md`
- [ ] `IMPLEMENTATION_SUMMARY.md`
- [ ] `POSTURE_ANALYSIS_IMPLEMENTATION_GUIDE.md`
- [ ] `HEAD_BODY_TILT_IMPLEMENTATION_PLAN.md`
- [ ] `APP_IMAGES_REFERENCE.md`

**Changes**:
- Replace all occurrences of "Slouti" with "Slouti"
- Update any paths that reference "slouti" directory
- Update screenshots/images if they contain the app name

### 6.2 Python Service Documentation
**File**: `python-service/SETUP.md`
- [ ] Update references to the app name
- [ ] Update any configuration that mentions Slouti

---

## 🐍 Phase 7: Python Service (Optional Updates)

### 7.1 Source Code
**Files to check**:
- [ ] `python-service/src/main.py`
- [ ] `python-service/src/websocket_server.py`
- [ ] `python-service/src/posture_analyzer.py`
- [ ] `python-service/src/pose_detector.py`

**Changes**:
- Update any log messages or comments that reference Slouti
- Update any configuration variables if they include the app name

### 7.2 Setup Scripts
**File**: `setup-python-service.ps1`
- [ ] Update any references to Slouti in comments or output messages

---

## 🎨 Phase 8: Assets & Resources

### 8.1 Asset Files
**Directory**: `dotnet-app/Assets/`
- [ ] Review asset files for any that contain "Slouti" in filenames
- [ ] Update app icons/logos if they contain the "Slouti" branding
- [ ] Consider creating new branded assets for "Slouti"

---

## ⚙️ Phase 9: Configuration & Settings

### 9.1 Launch Settings
**File**: `dotnet-app/Properties/launchSettings.json`
- [ ] Update profile names and any references to Slouti

### 9.2 Publish Profiles
**Files**: `dotnet-app/Properties/PublishProfiles/*.pubxml`
- [ ] Update references to Slouti in publish profiles
- [ ] Update output paths if needed

---

## 🔍 Phase 10: Search & Replace Operations

### 10.1 Global Search & Replace
Perform case-sensitive search and replace across the entire project:

| Search Term | Replace With | Case Sensitive | Files to Include |
|------------|--------------|----------------|------------------|
| `Slouti` | `Slouti` | Yes | `*.cs`, `*.xaml`, `*.csproj`, `*.slnx`, `*.json`, `*.xml` |
| `slouti` | `slouti` | Yes | `*.md`, `*.ps1`, `*.py`, `*.txt` |
| `slouti` | `SLOUTI` | Yes | All files |

### 10.2 Manual Verification
After global replace, manually verify:
- [ ] No broken references in code
- [ ] All namespaces are correct
- [ ] XAML files compile correctly
- [ ] No hardcoded paths are broken

---

## 🏗️ Phase 11: Rebuild & Test

### 11.1 Initial Build
```powershell
# Navigate to project directory
cd dotnet-app

# Restore NuGet packages
dotnet restore Slouti.csproj

# Clean solution
dotnet clean Slouti.csproj

# Build solution
dotnet build Slouti.csproj
```

### 11.2 Verify Build Output
- [ ] Check that output assemblies are named `Slouti.exe`, `Slouti.dll`
- [ ] Verify package identity in generated manifest
- [ ] Check for any build warnings or errors

### 11.3 Run Application
- [ ] Launch the application
- [ ] Verify window title displays "Slouti"
- [ ] Test all functionality
- [ ] Check system tray icon tooltip
- [ ] Verify notifications show correct app name

### 11.4 Test Python Service Integration
- [ ] Start Python service
- [ ] Verify WebSocket connection works
- [ ] Test posture detection end-to-end
- [ ] Check logs for any references to old name

---

## 📱 Phase 12: Package & Deployment

### 12.1 Create New Package
```powershell
# Create MSIX package
dotnet publish Slouti.csproj -c Release -r win-x64 --self-contained
```

### 12.2 Uninstall Old Package
- [ ] Uninstall Slouti from Windows Settings
- [ ] Remove any leftover files in AppData
- [ ] Clear any registry entries (if applicable)

### 12.3 Install New Package
- [ ] Install Slouti package
- [ ] Verify it appears correctly in Start Menu
- [ ] Check app settings and permissions
- [ ] Test startup behavior

---

## 🧪 Phase 13: Testing Checklist

### 13.1 Functional Testing
- [ ] Application launches successfully
- [ ] Camera/pose detection initializes
- [ ] WebSocket connection establishes
- [ ] Posture monitoring works correctly
- [ ] Notifications trigger properly
- [ ] Settings persist correctly
- [ ] System tray integration works

### 13.2 Visual Testing
- [ ] Window title shows "Slouti"
- [ ] System tray tooltip correct
- [ ] Notification messages correct
- [ ] About/Settings dialogs show correct name
- [ ] Task Manager shows "Slouti"

### 13.3 Integration Testing
- [ ] Python service connects successfully
- [ ] Data flows correctly between components
- [ ] Error handling works as expected
- [ ] Logging shows correct app name

---

## 📚 Phase 14: Version Control & Documentation

### 14.1 Git Operations
```bash
# Stage all changes
git add .

# Commit with descriptive message
git commit -m "Rename application from Slouti to Slouti"

# Tag the version
git tag v2.0.0-slouti

# Push changes
git push origin main --tags
```

### 14.2 Update Documentation
- [ ] Create release notes for the rename
- [ ] Update GitHub repository name (if applicable)
- [ ] Update any external documentation
- [ ] Update issue templates with new name

---

## 🎯 Phase 15: Post-Rename Cleanup

### 15.1 Remove Old Artifacts
- [ ] Delete old Slouti packages from `AppPackages/`
- [ ] Clear temporary build directories
- [ ] Remove any old installer files

### 15.2 Update External References
- [ ] Update project README badges
- [ ] Update any CI/CD pipelines
- [ ] Update documentation links
- [ ] Notify users of the name change

---

## ⚠️ Common Pitfalls & Solutions

### Issue 1: Package Identity Conflict
**Problem**: Windows doesn't allow two apps with the same package identity.

**Solution**:
- Change the package identity in `Package.appxmanifest`
- Increment the version number
- Use a different publisher ID if needed

### Issue 2: Namespace Errors
**Problem**: XAML files can't find types after namespace change.

**Solution**:
- Ensure all `x:Class` attributes are updated
- Update `xmlns:local` declarations
- Rebuild the project completely

### Issue 3: WebSocket Connection Issues
**Problem**: Python service references old app name.

**Solution**:
- Update any hardcoded app names in Python code
- Check WebSocket endpoint configurations
- Verify port numbers haven't changed

### Issue 4: Asset Loading Failures
**Problem**: App can't find resources after rename.

**Solution**:
- Verify asset paths in manifest
- Check that build action is set correctly
- Ensure resources are copied to output directory

---

## 📋 Final Verification Checklist

- [ ] All files renamed appropriately
- [ ] All namespaces updated
- [ ] All using directives corrected
- [ ] Package manifest updated
- [ ] Solution and project files renamed
- [ ] Build succeeds without errors
- [ ] Application runs correctly
- [ ] Python service integrates properly
- [ ] Documentation updated
- [ ] Version control committed
- [ ] Old packages uninstalled
- [ ] New package tested
- [ ] User-facing text updated
- [ ] Icons/branding updated (if needed)
- [ ] No references to "Slouti" remain

---

## 🕐 Estimated Timeline

| Phase | Estimated Time | Complexity |
|-------|---------------|------------|
| Phase 1-3: Files & Project | 15-30 min | Low |
| Phase 4: Source Code | 30-45 min | Medium |
| Phase 5: Build Cleanup | 5 min | Low |
| Phase 6: Documentation | 20-30 min | Low |
| Phase 7: Python Service | 15-20 min | Low |
| Phase 8-10: Assets & Config | 20-30 min | Medium |
| Phase 11: Rebuild & Test | 30-45 min | Medium |
| Phase 12-13: Package & Test | 30-60 min | High |
| Phase 14-15: Cleanup | 15-30 min | Low |

**Total Estimated Time**: 3-5 hours

---

## 🚀 Quick Start Command Sequence

For experienced developers, here's a condensed command sequence:

```powershell
# 1. Backup project
cd ..
Copy-Item -Recurse slouti slouti-backup

# 2. Rename root directory
Rename-Item slouti slouti
cd slouti

# 3. Rename project files
cd dotnet-app
Rename-Item Slouti.csproj Slouti.csproj
Rename-Item Slouti.slnx Slouti.slnx
Rename-Item Slouti.csproj.user Slouti.csproj.user

# 4. Clean build artifacts
Remove-Item -Recurse -Force bin, obj, BundleArtifacts, AppPackages

# 5. Global find/replace (use your IDE or tools)
# Search: Slouti -> Replace: Slouti (in all files)

# 6. Rebuild
dotnet restore Slouti.csproj
dotnet build Slouti.csproj

# 7. Test
dotnet run --project Slouti.csproj
```

---

## 📞 Support & Resources

- **Backup Location**: Always maintain a backup before starting
- **Testing Environment**: Test in a clean environment after rename
- **Documentation**: Keep this guide for reference during the process

---

## ✅ Completion Sign-Off

Once all phases are complete:

- [ ] Project builds successfully
- [ ] All tests pass
- [ ] Application runs with new name
- [ ] Documentation is updated
- [ ] Changes are committed to version control
- [ ] Team members are notified
- [ ] Users are informed of the change

**Date Completed**: _______________
**Completed By**: _______________
**Version**: 2.0.0-slouti

---

*This plan was generated on December 12, 2025 for the Slouti to Slouti application rename.*
