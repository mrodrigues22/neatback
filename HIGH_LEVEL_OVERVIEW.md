# NeatBack - High Level Overview

## What is NeatBack?

NeatBack is a Windows desktop application that helps you maintain good posture while working at your computer. It uses your webcam and AI to monitor your posture in real-time and sends you notifications when you've been slouching for too long.

## The Big Picture

The application is split into two main components that work together:

### 1. **Python Service** (Backend)
The Python service is the "brain" that watches your posture. It:
- Captures video from your webcam
- Uses Google's MediaPipe AI to detect your body position
- Analyzes your neck angle to determine if you're sitting correctly
- Sends posture status updates continuously

### 2. **.NET Desktop App** (Frontend)
The .NET WinUI3 app is the user interface you interact with. It:
- Displays your current posture status
- Shows your neck angle in real-time
- Sends Windows notifications when you've had bad posture for 30+ seconds
- Provides a simple "Start Monitoring" button to begin tracking

## How They Communicate

The two services talk to each other using **WebSocket** technology, which allows real-time, two-way communication. Think of it like a phone call between the Python service and the .NET app that stays connected while you're monitoring.

```
┌─────────────────────┐         WebSocket          ┌──────────────────────┐
│   Python Service    │ ◄─────────────────────────► │   .NET Desktop App   │
│                     │                              │                      │
│ • Camera Capture    │  Sends posture data          │ • User Interface     │
│ • Pose Detection    │  (JSON over port 8765)       │ • Notifications      │
│ • Angle Analysis    │                              │ • Status Display     │
└─────────────────────┘                              └──────────────────────┘
```

## What Makes a "Good" Posture?

The app measures your **neck angle** - the angle formed between your:
- Ear (top point)
- Shoulder (middle point)
- Hip (bottom point)

**Good posture**: Neck angle between 80-100 degrees
**Bad posture**: Anything outside that range (usually when you're leaning forward)

## User Experience Flow

1. **Start the Python service** - This begins capturing video and analyzing your posture
2. **Launch the .NET app** - This opens the desktop window
3. **Click "Start Monitoring"** - The app connects to the Python service
4. **See real-time updates** - Your posture status and neck angle update continuously
5. **Get notified** - If you slouch for more than 30 seconds, you'll get a Windows toast notification

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **AI/ML** | MediaPipe | Body pose detection from webcam |
| **Backend** | Python 3.11 | Processing video and analyzing posture |
| **Frontend** | .NET 8 + WinUI3 | Windows desktop interface |
| **Computer Vision** | OpenCV | Capturing and processing video frames |
| **Communication** | WebSocket | Real-time data exchange |
| **Notifications** | Windows Toast | System notifications |

## Key Features

✅ **Real-time monitoring** - Analyzes your posture 10 times per second
✅ **Non-intrusive** - Runs quietly in the background
✅ **Smart notifications** - Only alerts you after 30 seconds of bad posture
✅ **Notification cooldown** - Won't spam you (30 second minimum between alerts)
✅ **Simple interface** - Clean, minimal UI showing only what you need

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
- Works best with good lighting
- Currently monitors left side only (uses left ear, shoulder, hip)
- No historical data or posture tracking over time

## Potential Future Enhancements

- 📊 Daily/weekly posture reports
- ⏰ Customizable alert thresholds
- 🎯 Different posture profiles (sitting vs standing desk)
- 📱 System tray icon for background monitoring
- 🔔 Customizable notification messages
- 💾 Posture history and analytics
- ⚙️ Auto-start with Windows
