# NeatBack - Posture Tracking App

A simple Windows desktop application that tracks your posture using webcam and AI, and sends notifications when you slouch.

## Tech Stack

- **Python + MediaPipe**: Posture detection
- **.NET WinUI3**: Desktop app and notifications
- **WebSocket**: Communication between Python and .NET

## Project Structure

```
neatback/
├── python-service/          # Python posture detection service
│   ├── src/
│   │   ├── pose_detector.py
│   │   ├── posture_analyzer.py
│   │   ├── websocket_server.py
│   │   └── main.py
│   └── requirements.txt
├── dotnet-app/              # WinUI3 desktop app
│   ├── Models/
│   ├── Services/
│   └── Views/
└── README.md
```

## Setup Instructions

### 1. Python Service Setup

**Note:** MediaPipe requires Python 3.11 (not 3.12+). Install Python 3.11.9 from [python.org](https://www.python.org/downloads/release/python-3119/) if needed.

1. Navigate to the python-service directory:
```powershell
cd python-service
```

2. Create and activate a virtual environment (use Python 3.11):
```powershell
py -3.11 -m venv venv
.\venv\Scripts\Activate.ps1
```

3. Install dependencies:
```powershell
pip install -r requirements.txt
```

### 2. .NET App Setup

1. Navigate to the dotnet-app directory:
```powershell
cd dotnet-app
```

2. Restore and build the project:
```powershell
dotnet restore
dotnet build
```

## Running the Application

### Step 1: Start the Python Service

1. Open a PowerShell terminal and navigate to `python-service`
2. Activate the virtual environment:
```powershell
.\venv\Scripts\Activate.ps1
```

3. Run the service:
```powershell
python src\main.py
```

You should see: `WebSocket server starting on ws://localhost:8765` and `Posture tracking started...`

### Step 2: Run the .NET App

1. Open another PowerShell terminal and navigate to `dotnet-app`
2. Run the application:
```powershell
dotnet run
```

3. Click "Start Monitoring" in the application window

## How It Works

1. **Python Service**: 
   - Captures video directly from your webcam using OpenCV
   - Uses MediaPipe to detect facial landmarks (478 points)
   - Analyzes head pitch angle and distance from camera
   - Sends posture analysis results via WebSocket

2. **.NET Desktop App**:
   - Connects to the Python service via WebSocket
   - Receives and displays posture analysis results
   - Displays real-time posture status
   - Shows your current neck angle
   - Sends notifications if you maintain bad posture for 30+ seconds

3. **Posture Detection**:
   - Good posture: Neck angle between 80-100 degrees
   - Bad posture: Neck angle outside this range (slouching forward)

## Usage Tips

- Sit with good posture initially to calibrate your position
- The app will show "Good Posture ✓" when you're sitting correctly
- If you slouch for more than 30 seconds, you'll receive a notification
- Notifications are limited to once every 30 seconds to avoid spam

## Troubleshooting

**Python service won't start:**
- Make sure your webcam is not in use by another application
- Check that all dependencies are installed: `pip list`

**Can't connect to Python service:**
- Ensure the Python service is running first
- Check that port 8765 is not blocked by firewall

**.NET app won't build:**
- Make sure you have .NET 8 SDK installed
- Run `dotnet --version` to verify

**No notifications appearing:**
- Check Windows notification settings
- Ensure notifications are enabled for the NeatBack app

## Future Improvements

- Settings panel to adjust sensitivity
- System tray icon to run in background
- Posture history and statistics
- Multiple notification levels
- Configurable notification intervals

## License

MIT License - Feel free to use and modify as needed.
