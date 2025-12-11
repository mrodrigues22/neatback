# NeatBack - Advanced Posture Tracking Application

A real-time posture monitoring system that uses facial landmark detection to track your sitting posture and alerts you when you slouch.

## 🎯 Overview

NeatBack uses advanced computer vision to monitor your posture by tracking facial landmarks and head position. Unlike traditional pose detection that requires full-body visibility, this system focuses on head pitch angle and distance from the camera for more accurate sitting posture analysis.

## ✨ Features

- 📸 **Real-time Face Detection**: Uses MediaPipe Face Landmarker with 478 facial landmarks
- 📐 **Head Pose Estimation**: Calculates pitch angle using PnP (Perspective-n-Point) algorithm  
- 📏 **Distance Measurement**: Measures distance from camera using interpupillary distance (IPD)
- ✅ **Baseline Calibration**: Save your good posture as a reference point
- ⚠️ **Smart Detection**: Detects slouching (looking down) and leaning forward
- 🔔 **Desktop Notifications**: Alerts after 5 seconds of bad posture, then every 20 seconds
- 📊 **Live Metrics**: Real-time display of pitch angle, distance, and bad posture duration
- ⚙️ **Adjustable Thresholds**: Customize sensitivity for pitch and distance detection

## 🏗️ Architecture

```
┌─────────────────────┐
│   WinUI Desktop App │
│  (C# / .NET 8.0)    │
│                     │
│  - Camera Capture   │
│  - UI & Controls    │
│  - Notifications    │
└──────────┬──────────┘
           │ WebSocket
           │ (sends frames as base64 JPEG)
           ▼
┌─────────────────────┐
│   Python Service    │
│  (MediaPipe + CV)   │
│                     │
│  - Face Detection   │
│  - Pose Estimation  │
│  - Posture Analysis │
└─────────────────────┘
```

## 🔬 How It Works

### Posture Detection Algorithm

1. **Face Landmark Detection**: MediaPipe detects 478 facial landmarks in real-time
2. **3D Head Pose Calculation**: 
   - Uses 6 key facial points (eyes, nose, mouth corners, chin)
   - Maps to 3D face model coordinates
   - Applies OpenCV solvePnP to calculate head orientation
3. **Pitch Angle Extraction**: Converts rotation matrix to Euler angles
4. **Distance Calculation**: Uses interpupillary distance (IPD) and similar triangles
5. **Baseline Comparison**: Compares current posture to saved good posture
6. **Bad Posture Detection**: Triggers when:
   - Head pitch < -10° (looking down) OR
   - Distance > 10 cm closer than baseline (leaning forward)

### Key Facial Landmarks Used

- **Index 33**: Left eye outer corner
- **Index 263**: Right eye outer corner
- **Index 1**: Nose tip
- **Index 61**: Left mouth corner
- **Index 291**: Right mouth corner
- **Index 199**: Chin
- **Index 473/468**: Left/right pupil (for distance calculation)

## 🚀 Setup Instructions

### Prerequisites

- **Python 3.8+** with pip
- **.NET 8.0 SDK**
- **Windows 10/11** (for WinUI app)
- **Webcam**

### Quick Setup (Automated)

Run the setup script from the project root:

```powershell
.\setup-python-service.ps1
```

This will automatically:
1. Install Python dependencies
2. Download MediaPipe Face Landmarker model (~10 MB)
3. Verify setup

### Manual Setup

#### 1. Python Service

```bash
cd python-service
pip install -r requirements.txt
```

Download the MediaPipe model:
```powershell
cd python-service\src
Invoke-WebRequest -Uri "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task" -OutFile "face_landmarker.task"
```

See [python-service/SETUP.md](python-service/SETUP.md) for details.

#### 2. .NET Application

```bash
cd dotnet-app
dotnet restore
dotnet build
```

## 🎮 Running the Application

### Step 1: Start Python Service

```bash
cd python-service\src
python main.py
```

Expected output:
```
============================================================
NeatBack Posture Analysis Service
============================================================
Starting WebSocket server...
Waiting for client connection...
============================================================
WebSocket server starting on ws://localhost:8765
```

### Step 2: Run WinUI App

```bash
cd dotnet-app
dotnet run
```

Or open `NeatBack.slnx` in Visual Studio and press F5.

## 📖 Usage Guide

1. **Start Monitoring**: Click "Start Monitoring" to begin camera capture
2. **Position Yourself**: Sit in your best posture with face clearly visible
3. **Save Baseline**: Click "Save Good Posture" to set your reference posture
4. **Get Monitored**: The app will now track your posture continuously
5. **Receive Alerts**: You'll get notifications when posture deteriorates

### Understanding the Metrics

- **Head Pitch**: Angular deviation from your baseline (negative = looking down)
- **Distance**: Current distance from camera in centimeters
- **Bad Duration**: How long you've been in bad posture (seconds)
- **Status**: Real-time feedback on current posture state

### Adjusting Sensitivity

Use the sliders to customize detection thresholds:
- **Pitch Threshold**: How much head tilt triggers bad posture (-30° to 0°)
- **Distance Threshold**: How close you can lean forward (5-20 cm)

## 🛠️ Technologies Used

### Python Service
- **MediaPipe 0.10+**: Face landmark detection
- **OpenCV 4.8+**: PnP algorithm, image processing
- **WebSockets 12+**: Real-time communication
- **NumPy**: Matrix operations

### .NET Application
- **WinUI 3**: Modern Windows UI framework
- **Windows.Media.Capture**: Camera access and frame capture
- **System.Net.WebSockets**: WebSocket client

## 📁 Project Structure

```
neatback/
├── dotnet-app/              # WinUI application
│   ├── Models/
│   │   └── PostureData.cs
│   ├── Services/
│   │   ├── WebSocketClient.cs
│   │   └── NotificationService.cs
│   ├── Views/
│   │   ├── MainPage.xaml
│   │   └── MainPage.xaml.cs
│   └── NeatBack.csproj
├── python-service/          # Analysis service
│   ├── src/
│   │   ├── main.py
│   │   ├── pose_detector.py
│   │   ├── posture_analyzer.py
│   │   └── websocket_server.py
│   ├── requirements.txt
│   └── SETUP.md
├── setup-python-service.ps1
├── README.md
└── POSTURE_ANALYSIS_IMPLEMENTATION_GUIDE.md
```

## 🔧 Configuration

### Default Settings

**Detection Thresholds:**
- Pitch Threshold: -10 degrees
- Distance Threshold: 10 cm

**Warning Timing:**
- Initial Warning: 5 seconds
- Repeat Warning: Every 20 seconds

**Processing:**
- Frame Rate: 1 frame per second
- Camera Resolution: 640x480 (resized)

All settings are adjustable via the UI while running.

## 🐛 Troubleshooting

### Python Service Issues

**"face_landmarker.task not found"**
```bash
# Download manually:
cd python-service\src
Invoke-WebRequest -Uri "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task" -OutFile "face_landmarker.task"
```

**"No module named 'mediapipe'"**
```bash
pip install -r python-service/requirements.txt
```

**"Address already in use"**
- Port 8765 is occupied
- Stop other instances: `Get-Process python | Stop-Process`
- Or change port in `websocket_server.py`

### .NET Application Issues

**Camera not working**
- Check Windows Settings → Privacy → Camera
- Ensure no other app is using the camera
- Try restarting the app

**Can't connect to service**
- Verify Python service is running
- Check Windows Firewall settings for port 8765
- Ensure both services are on same machine

**"No face detected"**
- Improve lighting conditions
- Ensure face is clearly visible and centered
- Remove obstructions (masks, hands, etc.)
- Adjust camera angle

## 💡 Performance Tips

- Use good, even lighting
- Position camera at eye level
- Sit 50-70 cm from camera
- Avoid cluttered backgrounds
- Close resource-intensive applications

## 📚 Documentation

- [Posture Analysis Implementation Guide](POSTURE_ANALYSIS_IMPLEMENTATION_GUIDE.md)
- [Python Service Setup](python-service/SETUP.md)
- [High-Level Overview](HIGH_LEVEL_OVERVIEW.md)
- [Detailed Technical Guide](DETAILED_TECHNICAL_GUIDE.md)

## 🚢 Building for Release

### .NET Application
```bash
cd dotnet-app
dotnet publish -c Release -r win-x64 --self-contained
```

### Python Service
```bash
pip install pyinstaller
cd python-service/src
pyinstaller --onefile --add-data "face_landmarker.task;." main.py
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## 📄 License

MIT License - See LICENSE file for details

## 🙏 Credits

- **MediaPipe** by Google - Face landmark detection
- **OpenCV** - Computer vision algorithms
- **WinUI 3** by Microsoft - Modern Windows UI

## 📧 Support

For issues or questions, please open a GitHub issue or refer to the documentation guides.

---

**Note**: This application requires a functional webcam and processes video frames locally. No data is transmitted outside your machine.
