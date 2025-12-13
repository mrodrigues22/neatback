# Slouti Privacy Policy

**Last Updated:** December 13, 2025

Your privacy is important to us. This privacy policy explains how Slouti processes data, what data is collected, and how it is used to provide you with posture monitoring services.

## Overview

Slouti is a desktop posture monitoring application that uses your computer's webcam to analyze your sitting posture in real-time. This privacy policy applies to the Slouti application, including both the desktop interface and the Python analysis service.

**Key Privacy Principles:**
- 🔒 **All processing is local** - No data is transmitted to external servers or the cloud
- 📹 **Video is never stored** - Camera frames are processed in memory and immediately discarded
- 🏠 **Runs on your device only** - All analysis happens on your local computer
- 👤 **No personal identification** - We do not collect names, emails, or identifying information
- 🔓 **Open source** - All code is transparent and auditable

## Data We Process

Slouti processes the following data types locally on your device:

### Camera Data
- **Real-time video frames** from your webcam
- **Facial landmarks** detected using MediaPipe Face Landmarker (478 points on your face)
- **Head pose measurements** including pitch angle, roll angle, and yaw angle
- **Distance measurements** calculated from interpupillary distance (IPD)

**Important:** Video frames are processed in real-time and are immediately discarded after analysis. No video recordings are saved to your device or transmitted anywhere.

### Posture Analysis Data
- **Current posture status** (good or bad posture)
- **Baseline calibration data** when you save your "good posture" reference
- **Posture metrics** including:
  - Head pitch angle (degrees)
  - Head roll angle (degrees)
  - Distance from camera (centimeters)
  - Duration of bad posture (seconds)
- **Posture statistics** including:
  - Total bad posture duration
  - Longest bad posture streak
  - Longest good posture streak

### Configuration Data
- **Sensitivity thresholds** for posture detection (pitch, distance, head roll, shoulder tilt)
- **Notification settings** (timing intervals)
- **User preferences** for sensitivity scales (1-5 rating)

### System Data
- **WebSocket connection status** between the desktop app and Python service
- **Camera device availability** and access status
- **Application performance data** (frame processing time, detection confidence)

## How We Use Data

Slouti uses the data described above solely for the following purposes:

### Posture Monitoring
- Analyze your head position and orientation in real-time
- Compare current posture against your saved baseline calibration
- Detect slouching, forward leaning, head tilting, and shoulder imbalance
- Calculate duration of poor posture

### User Notifications
- Send desktop notifications when bad posture is detected for 5 seconds
- Send repeat notifications every 20 seconds while bad posture continues
- Display real-time posture status, metrics, and feedback in the application interface

### Calibration
- Store your "good posture" baseline (head pitch, roll, and distance values)
- Use baseline for comparison to detect posture deviations

### Application Functionality
- Maintain WebSocket connection between desktop app and Python service
- Display live camera preview with facial landmark overlay
- Update UI with real-time metrics and statistics
- Process user adjustments to sensitivity thresholds

## Data Storage

### Local Storage Only
All data is stored temporarily in your device's memory (RAM) during application runtime. Slouti stores the following persistent data on your local device:

- **Baseline posture data** - Saved when you click "Save Good Posture"
- **User preferences** - Sensitivity thresholds and notification settings
- **Application configuration** - Default settings and thresholds

### What Is NOT Stored
- ❌ Video recordings or camera frames
- ❌ Images of your face
- ❌ Facial landmark coordinates (except during active processing)
- ❌ Historical posture data across sessions
- ❌ Personal identification information

### Data Deletion
When you close the Slouti application:
- All real-time processing data is automatically removed from memory
- Calibration data remains saved for future sessions
- You can reset calibration by saving a new baseline posture

To completely remove all Slouti data, uninstall the application through Windows Settings.

## Data Sharing

**Slouti does NOT share any data with third parties, external servers, or cloud services.**

- No data is transmitted over the internet
- All communication between the desktop app and Python service happens locally via WebSocket on `localhost:8765`
- No analytics, telemetry, or usage data is collected or sent
- No data is sold, rented, or shared with advertisers or third parties

## Camera Access and Permissions

### How We Access Your Camera
Slouti requires access to your computer's webcam to function. Camera access is used exclusively for:
- Capturing real-time video frames for facial landmark detection
- Displaying live preview in the application interface
- Analyzing head pose and distance measurements

### Camera Permissions
- You will be prompted to grant camera access when you first start monitoring
- You can revoke camera access at any time through Windows Settings → Privacy → Camera
- The application will not function without camera access, as it is essential to posture monitoring

### Camera Data Security
- Camera frames are processed directly in memory using OpenCV and MediaPipe
- No frames are written to disk or transmitted over any network
- Camera access ends immediately when you click "Stop Monitoring" or close the application

## Third-Party Technologies

Slouti uses the following third-party technologies, all of which run locally on your device:

### MediaPipe Face Landmarker (Google)
- **Purpose:** Detect 478 facial landmarks for head pose estimation
- **Privacy:** Processes data entirely on your device; no data sent to Google
- **Model file:** Downloaded once during setup (~10 MB)
- **License:** Apache 2.0

### OpenCV (Open Source Computer Vision Library)
- **Purpose:** Camera capture, image processing, and 3D pose calculation
- **Privacy:** Runs entirely locally with no external connections
- **License:** Apache 2.0

### Python Libraries
- **websockets:** Local communication only (localhost)
- **numpy:** Mathematical computations
- **All processing is local with no external network requests**

## Children's Privacy

Slouti does not knowingly collect data from children under the age of 13. The application does not collect personal information, identify users, or store identifying data. Parents or guardians should supervise children's use of the application and webcam access.

## Security

### Local Processing Security
- All video processing happens in memory and is never persisted to disk
- WebSocket communication uses `ws://localhost:8765` (local-only connection)
- No authentication or user accounts are required
- No passwords or credentials are collected

### Best Practices
- Keep your Windows operating system and security software up to date
- Only download Slouti from trusted sources
- Review Windows camera permissions regularly
- Close the application when not in use to free system resources

## Your Rights and Controls

### You Have the Right To:
- **Start and stop monitoring** at any time using the application controls
- **Deny camera access** through Windows privacy settings
- **Reset calibration data** by saving a new baseline posture
- **Adjust sensitivity thresholds** to customize detection behavior
- **Uninstall the application** to remove all stored preferences and configuration

### How to Exercise Your Rights:
- **Stop camera access:** Click "Stop Monitoring" or close the application
- **Revoke permissions:** Windows Settings → Privacy → Camera → Turn off for Slouti
- **Reset settings:** Adjust sliders or recalibrate your baseline posture
- **Delete all data:** Uninstall Slouti through Windows Settings → Apps

## Data Retention

### Active Session Data
- **Real-time video frames:** Discarded immediately after processing (milliseconds)
- **Posture analysis results:** Cleared when monitoring stops or app closes
- **Statistics:** Reset when the application restarts

### Persistent Data
- **Baseline calibration:** Retained until you save a new baseline or uninstall
- **User preferences:** Retained until you change settings or uninstall
- **Configuration files:** Removed upon uninstallation

## Changes to This Privacy Policy

We may update this privacy policy from time to time. When we make changes, we will update the "Last Updated" date at the top of this policy. We encourage you to review this policy periodically.

For significant changes to data collection or usage practices, we will provide notice through the application or in release notes.

## Open Source and Transparency

Slouti is open-source software. You can review the complete source code to verify our privacy practices:

- **Desktop app:** .NET 8 WinUI3 application (C#)
- **Python service:** OpenCV + MediaPipe analysis service
- **All code is visible** and can be audited for privacy compliance

## Technical Details

### How Posture Detection Works
1. **Capture:** OpenCV captures webcam frames at ~30 FPS
2. **Detect:** MediaPipe identifies 478 facial landmarks in each frame
3. **Calculate:** OpenCV solvePnP algorithm computes 3D head pose (pitch, roll, yaw)
4. **Measure:** Interpupillary distance determines distance from camera
5. **Analyze:** Compare current posture to your saved baseline
6. **Notify:** Send alerts if bad posture exceeds time thresholds
7. **Discard:** Frame is removed from memory

### Data Flow Architecture
```
Camera → OpenCV Capture → MediaPipe Detection → Pose Calculation
    ↓
Memory Processing (No Storage)
    ↓
WebSocket (localhost:8765) → Desktop App UI
    ↓
Desktop Notifications (Windows Toast)
    ↓
Automatic Memory Cleanup
```

## Compliance

### Regional Privacy Laws
While Slouti does not transmit data externally or collect personal information, we respect privacy regulations including:
- **GDPR** (European Union)
- **CCPA** (California)
- **Other U.S. state privacy laws**

Since all processing is local and no personal data is collected or transmitted, most data privacy regulations do not apply to Slouti's operation.

## Contact Information

If you have questions, concerns, or feedback about this privacy policy or Slouti's privacy practices, please:

- **Review the source code:** Available in the project repository
- **Submit an issue:** Through the project's issue tracker
- **Contact the developer:** Through the project's communication channels

## Summary

**What Slouti Does:**
- ✅ Uses your camera to analyze posture locally
- ✅ Processes video frames in real-time memory
- ✅ Stores only your baseline calibration and preferences
- ✅ Sends local desktop notifications

**What Slouti Does NOT Do:**
- ❌ Record or save video
- ❌ Send data to the internet or cloud
- ❌ Collect personal information
- ❌ Share data with third parties
- ❌ Track your identity or behavior

---

**Your privacy is paramount.** Slouti is designed with privacy-by-default principles, ensuring that your posture monitoring remains completely private and secure on your own device.
