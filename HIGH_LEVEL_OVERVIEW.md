# NeatBack - High Level Overview

## What is NeatBack?

NeatBack is a Windows desktop application that helps you maintain good posture while working at your computer. It uses your webcam and AI to monitor your posture in real-time and sends you notifications when you've been slouching for too long.

## The Big Picture

The application is split into two main components that work together:

### 1. **Python Service** (Backend)
The Python service is the "brain" that analyzes your posture. It:
- Captures video frames directly from your webcam using OpenCV
- Uses Google's MediaPipe Face Landmarker to detect facial landmarks
- Calculates head pitch angle using 3D pose estimation (PnP algorithm)
- Measures distance from camera based on face size
- Tracks posture statistics and bad posture duration
- Sends real-time posture analysis and frame previews to the client

### 2. **.NET Desktop App** (Frontend)
The .NET WinUI3 app is the user interface. It:
- Connects to the Python service via WebSocket
- Receives posture analysis results and frame previews from Python service
- Displays live camera preview with posture status overlay
- Shows pitch angle, distance, and bad posture duration
- Allows baseline calibration and threshold adjustment
- Sends Windows notifications based on configurable timing
- Provides visual progress bar for bad posture duration

## How They Communicate

The two services talk to each other using **WebSocket** technology, which allows real-time, two-way communication. Think of it like a phone call between the Python service and the .NET app that stays connected while you're monitoring.

```
┌─────────────────────┐         WebSocket          ┌──────────────────────┐
│   Python Service    │ ◄─────────────────────────► │   .NET Desktop App   │
│                     │                              │                      │
│ • Camera Capture    │  Sends posture data          │ • User Interface     │
│ • Face Detection    │  & frame previews            │ • Display Results    │
│ • Pose Analysis     │  (JSON over port 8765)       │ • Notifications      │
│ • Angle Calculation │                              │ • User Controls      │
└─────────────────────┘                              └──────────────────────┘
```

## What Makes a "Good" Posture?

The app measures your **head pitch angle** and **distance from screen** using facial landmarks:
- **Pitch angle**: The forward/backward tilt of your head (negative values = looking down)
- **Distance**: How far your face is from the camera/screen

**Good posture**: Determined by calibration - you save your ideal posture as a baseline
**Bad posture**: When your head pitch or distance deviates beyond configurable thresholds from your baseline
- Default pitch threshold: -10 degrees (looking down too much)
- Default distance threshold: 10 cm (too close to screen)

## User Experience Flow

1. **Start the Python service** - This starts the WebSocket server waiting for connections
2. **Launch the .NET app** - This opens the desktop window
3. **Click "Start Monitoring"** - The app connects to the Python service and starts camera
4. **Calibrate your posture** - Sit correctly and click "Save Good Posture" to set your baseline
5. **Adjust thresholds** (optional) - Fine-tune pitch and distance sensitivity with sliders
6. **See real-time updates** - Your posture status, pitch angle, and distance update continuously
7. **Get notified** - If you have bad posture, you'll get warnings at 5 seconds and then every 20 seconds

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **AI/ML** | MediaPipe Face Landmarker | Facial landmark detection for head pose |
| **Backend** | Python 3.11 | Processing frames and analyzing posture |
| **Frontend** | .NET 8 + WinUI3 | Windows desktop interface with camera |
| **Computer Vision** | OpenCV | Frame processing and PnP algorithm |
| **Communication** | WebSocket | Real-time bidirectional data exchange |
| **Notifications** | Windows Toast | System notifications |

## Key Features

✅ **Real-time monitoring** - Analyzes your posture continuously as frames are sent
✅ **Personalized calibration** - Save your ideal posture as a baseline reference
✅ **Adjustable thresholds** - Customize pitch angle and distance sensitivity
✅ **Smart notifications** - Initial alert at 5 seconds, then every 20 seconds of bad posture
✅ **Visual feedback** - Progress bar shows bad posture duration
✅ **Statistics tracking** - Monitor your posture streaks and total durations
✅ **Live camera preview** - See yourself with real-time landmark overlay

## System Requirements

- **Operating System**: Windows 10/11
- **Python**: Version 3.11.x (MediaPipe doesn't support 3.12+ yet)
- **.NET**: .NET 8 SDK
- **Hardware**: Webcam required

## Privacy & Security

- 🔒 **All processing is local** - No data is sent to the cloud
- 📹 **Video is not saved** - Frames are processed and immediately discarded
- 🏠 **Runs on localhost** - Only your computer can access the posture data
- 🔓 **Open source** - All code is visible and auditable

## Current Limitations

- Only monitors when you actively click "Start Monitoring"
- Requires both services to be running (Python + .NET)
- Works best with good lighting and clear face visibility
- Requires initial calibration for accurate baseline
- Statistics reset when application closes

## Potential Future Enhancements

- � Persistent statistics storage (save/load posture history)
- 📊 Daily/weekly posture reports and charts
- 🎯 Multiple posture profiles (sitting vs standing desk)
- 📱 System tray icon for background monitoring
- 🔔 Customizable notification sounds and messages
- 📈 Export posture data for analysis
- ⚙️ Auto-start with Windows
- 🎥 Record posture violation clips
