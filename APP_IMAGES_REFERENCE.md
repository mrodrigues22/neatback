# NeatBack App Images Reference

## ⚠️ IMPORTANT: Unpackaged App Configuration

**This app runs as UNPACKAGED** (`WindowsPackageType = None` in NeatBack.csproj).

For unpackaged apps, **ONLY the ICO file is used** for all system icons:
- Window title bar
- Taskbar 
- Alt-Tab switcher
- Task Manager

**All PNG files below are IGNORED** unless you package this as an MSIX.

## Image Assets Configuration

| File Name | Size (pixels) | Format | Usage | Status |
|-----------|---------------|--------|-------|--------|
| **logo-square.ico** | 16x16, 32x32, 48x48, 256x256 (multi-res) | ICO | **ALL ICONS** (window, taskbar, alt-tab, etc.) | ✅ **ACTIVE** - `NeatBack.csproj` Line 23 |
| **Square44x44Logo.scale-200.png** | 88x88 | PNG | Taskbar icon (packaged only) | ❌ INACTIVE (unpackaged app) |
| **Square44x44Logo.targetsize-24_altform-unplated.png** | 24x24 | PNG | Small icon (packaged only) | ❌ INACTIVE (unpackaged app) |
| **Square150x150Logo.scale-200.png** | 300x300 | PNG | Start menu tile (packaged only) | ❌ INACTIVE (unpackaged app) |
| **Wide310x150Logo.scale-200.png** | 620x300 | PNG | Wide tile (packaged only) | ❌ INACTIVE (unpackaged app) |
| **SplashScreen.scale-200.png** | 620x300 | PNG | Splash screen (packaged only) | ❌ INACTIVE (unpackaged app) |
| **LockScreenLogo.scale-200.png** | 48x48 | PNG | Lock screen (packaged only) | ❌ INACTIVE (unpackaged app) |
| **StoreLogo.png** | 50x50 | PNG | Store listing (packaged only) | ❌ INACTIVE (unpackaged app) |
| **logo-square.png** | Variable | PNG | Generic app logo | Not currently used |
| **app-logo.png** | Variable | PNG | Custom app logo | Not currently used |

## Scale Factors Explained

Windows uses scale factors for high-DPI displays:
- **scale-100**: Base size (100% scaling) - e.g., 44x44
- **scale-200**: 2x size (200% scaling) - e.g., 88x88 for high-DPI displays
- **targetsize-XX**: Exact pixel size regardless of scaling

## Quick Reference by Use Case

### Window Display
- **Title Bar Icon**: `logo-square.ico` (16x16, 32x32, 48x48, 256x256)

### System Integration
- **Taskbar**: `Square44x44Logo.scale-200.png` (88x88)
- **Start Menu Medium Tile**: `Square150x150Logo.scale-200.png` (300x300)
- **Start Menu Wide Tile**: `Wide310x150Logo.scale-200.png` (620x300)

### Special Contexts
- **Splash Screen**: `SplashScreen.scale-200.png` (620x300)
- **Store Listing**: `StoreLogo.png` (50x50)
- **Unplated Small Icon**: `Square44x44Logo.targetsize-24_altform-unplated.png` (24x24)

## Notes

1. All PNG files should have **transparent backgrounds** for proper integration
2. The `.ico` file should contain **multiple resolutions** in one file
3. Scale-200 images are for **200% DPI** (typical for modern high-resolution displays)
4. Image files must be in the `dotnet-app/Assets/` directory
5. After updating images, rebuild the app package for changes to take effect

## Creating ICO Files

To create a multi-resolution ICO file from PNG files:
- Use online tools like [icoconvert.com](https://icoconvert.com)
- Or use ImageMagick: `magick convert icon-16.png icon-32.png icon-48.png icon-256.png logo-square.ico`
