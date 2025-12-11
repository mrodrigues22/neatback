# NeatBack - Posture Tracking App MVP Implementation Plan

> **Note**: This document describes an early implementation plan using body pose detection. The actual implementation uses **MediaPipe Face Landmarker** for more accurate sitting posture detection, and the Python service captures frames directly from the webcam rather than receiving them from the .NET app. See [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for the current implementation.

## Project Overview
A simple Windows desktop application that:
- Tracks your posture using webcam and AI
- Sends notifications when you slouch
- Runs locally with no cloud dependency

**Tech Stack:**
- **Python + MediaPipe**: Posture detection
- **.NET WinUI3**: Desktop app and notifications
- **WebSocket**: Communication between Python and .NET

---

## MVP Project Structure

```
neatback/
├── python-service/          # Python posture detection
│   ├── src/
│   │   ├── pose_detector.py
│   │   ├── posture_analyzer.py
│   │   ├── websocket_server.py
│   │   └── main.py
│   └── requirements.txt
├── dotnet-app/              # WinUI3 app
│   ├── NeatBack.sln
│   └── NeatBack/
│       ├── MainWindow.xaml
│       ├── Services/
│       │   ├── WebSocketClient.cs
│       │   └── NotificationService.cs
│       └── Models/
│           └── PostureData.cs
└── README.md
```

---

## Step 1: Setup Python Service

### 1.1 Install Dependencies
```bat
cd python-service
py -3 -m venv venv
call venv\Scripts\activate
pip install mediapipe opencv-python websockets
pip freeze > requirements.txt
```

### 1.2 Create Pose Detector

**File: `python-service/src/pose_detector.py`**

```python
import cv2
import mediapipe as mp

class PoseDetector:
    def __init__(self):
        self.mp_pose = mp.solutions.pose
        self.pose = self.mp_pose.Pose(
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )
    
    def detect(self, frame):
        """Detect pose in frame and return landmarks."""
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = self.pose.process(rgb_frame)
        return results.pose_landmarks if results else None
    
    def close(self):
        self.pose.close()
```

### 1.3 Create Posture Analyzer

**File: `python-service/src/posture_analyzer.py`**

```python
import numpy as np

class PostureAnalyzer:
    def calculate_angle(self, p1, p2, p3):
        """Calculate angle between three points."""
        v1 = np.array([p1.x - p2.x, p1.y - p2.y])
        v2 = np.array([p3.x - p2.x, p3.y - p2.y])
        cos_angle = np.dot(v1, v2) / (np.linalg.norm(v1) * np.linalg.norm(v2))
        angle = np.arccos(np.clip(cos_angle, -1.0, 1.0))
        return np.degrees(angle)
    
    def analyze(self, landmarks):
        """Check if posture is good or bad."""
        # MediaPipe landmark indices
        LEFT_EAR = 7
        LEFT_SHOULDER = 11
        LEFT_HIP = 23
        
        # Calculate neck angle (ear-shoulder-hip)
        neck_angle = self.calculate_angle(
            landmarks[LEFT_EAR],
            landmarks[LEFT_SHOULDER],
            landmarks[LEFT_HIP]
        )
        
        # Good posture: neck angle between 80-100 degrees
        is_good_posture = 80 <= neck_angle <= 100
        
        return {
            "is_good": is_good_posture,
            "neck_angle": round(neck_angle, 1)
        }
```

### 1.4 Create WebSocket Server

**File: `python-service/src/websocket_server.py`**

```python
import asyncio
import websockets
import json

class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        
    async def register(self, websocket):
        self.clients.add(websocket)
        print(f"Client connected")
        
    async def unregister(self, websocket):
        self.clients.remove(websocket)
        print(f"Client disconnected")
        
    async def send(self, data):
        """Send data to all connected clients."""
        if not self.clients:
            return
        message = json.dumps(data)
        await asyncio.gather(
            *[client.send(message) for client in self.clients],
            return_exceptions=True
        )
    
    async def handler(self, websocket, path):
        await self.register(websocket)
        try:
            await websocket.wait_closed()
        finally:
            await self.unregister(websocket)
    
    async def start(self):
        print(f"WebSocket server starting on ws://{self.host}:{self.port}")
        async with websockets.serve(self.handler, self.host, self.port):
            await asyncio.Future()
```

### 1.5 Main Service

**File: `python-service/src/main.py`**

```python
import cv2
import asyncio
from pose_detector import PoseDetector
from posture_analyzer import PostureAnalyzer
from websocket_server import WebSocketServer

class PostureService:
    def __init__(self):
        self.detector = PoseDetector()
        self.analyzer = PostureAnalyzer()
        self.ws_server = WebSocketServer()
        self.cap = None
        self.running = False
    
    def open_camera(self):
        self.cap = cv2.VideoCapture(0)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    
    async def process_frame(self):
        """Process one frame and analyze posture."""
        ret, frame = self.cap.read()
        if not ret:
            return None
        
        landmarks = self.detector.detect(frame)
        if landmarks:
            result = self.analyzer.analyze(landmarks.landmark)
            return result
        return None
    
    async def run(self):
        """Main loop: capture, analyze, send."""
        self.running = True
        self.open_camera()
        
        # Start WebSocket server
        asyncio.create_task(self.ws_server.start())
        await asyncio.sleep(1)  # Let server start
        
        print("Posture tracking started...")
        
        while self.running:
            result = await self.process_frame()
            if result:
                await self.ws_server.send(result)
            await asyncio.sleep(0.1)  # 10 FPS
        
        self.cap.release()
        self.detector.close()

if __name__ == "__main__":
    service = PostureService()
    try:
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("\nStopping...")
```

---

## Step 2: Setup .NET Desktop App

### 2.1 Create WinUI3 Project
```bat
cd dotnet-app
dotnet new wasdk -n NeatBack
cd NeatBack
dotnet add package Microsoft.Toolkit.Uwp.Notifications
dotnet build
```

### 2.2 Create Data Model

**File: `Models/PostureData.cs`**

```csharp
public class PostureData
{
    public bool is_good { get; set; }
    public double neck_angle { get; set; }
}
```

### 2.3 WebSocket Client

**File: `Services/WebSocketClient.cs`**

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class WebSocketClient
{
    private ClientWebSocket _ws;
    private readonly string _uri = "ws://localhost:8765";
    
    public event EventHandler<PostureData> DataReceived;
    
    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(_uri), CancellationToken.None);
        _ = Task.Run(ReceiveLoop);
    }
    
    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024];
        
        while (_ws.State == WebSocketState.Open)
        {
            var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var data = JsonSerializer.Deserialize<PostureData>(json);
            DataReceived?.Invoke(this, data);
        }
    }
}
```

### 2.4 Notification Service

**File: `Services/NotificationService.cs`**

```csharp
using Microsoft.Toolkit.Uwp.Notifications;

public class NotificationService
{
    private DateTime _lastNotification = DateTime.MinValue;
    
    public void ShowAlert(string message)
    {
        // Only show notification every 30 seconds
        if ((DateTime.Now - _lastNotification).TotalSeconds < 30)
            return;
        
        new ToastContentBuilder()
            .AddText("Posture Alert")
            .AddText(message)
            .Show();
        
        _lastNotification = DateTime.Now;
    }
}
```

### 2.5 Main Window Logic

**File: `MainWindow.xaml.cs`**

```csharp
public sealed partial class MainWindow : Window
{
    private WebSocketClient _wsClient;
    private NotificationService _notificationService;
    private DateTime _badPostureStart;
    private bool _inBadPosture = false;
    
    public MainWindow()
    {
        InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        _wsClient.DataReceived += OnPostureDataReceived;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await _wsClient.ConnectAsync();
        StatusText.Text = "Monitoring...";
    }
    
    private void OnPostureDataReceived(object sender, PostureData data)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AngleText.Text = $"Neck Angle: {data.neck_angle}°";
            StatusText.Text = data.is_good ? "Good Posture ✓" : "Bad Posture ✗";
            
            // Track bad posture duration
            if (!data.is_good)
            {
                if (!_inBadPosture)
                {
                    _badPostureStart = DateTime.Now;
                    _inBadPosture = true;
                }
                else
                {
                    var duration = (DateTime.Now - _badPostureStart).TotalSeconds;
                    if (duration > 30)
                    {
                        _notificationService.ShowAlert("Fix your posture!");
                    }
                }
            }
            else
            {
                _inBadPosture = false;
            }
        });
    }
}
```

### 2.6 Basic UI

**File: `MainWindow.xaml`**

```xml
<Window x:Class="NeatBack.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    
    <StackPanel Padding="20" Spacing="15">
        <TextBlock Text="NeatBack - Posture Tracker" 
                   FontSize="24" FontWeight="Bold"/>
        
        <Button x:Name="StartButton" Content="Start Monitoring" 
                Click="StartButton_Click"/>
        
        <TextBlock x:Name="StatusText" Text="Not monitoring" 
                   FontSize="18"/>
        
        <TextBlock x:Name="AngleText" Text="Neck Angle: --" 
                   FontSize="16"/>
    </StackPanel>
</Window>
```

---

## Step 3: Run & Test

### 3.1 Start Python Service
```bat
cd python-service
call venv\Scripts\activate
python src\main.py
```

### 3.2 Run .NET App
```bat
cd dotnet-app\NeatBack
dotnet run
```

### 3.3 Test
1. Click "Start Monitoring" in the app
2. Sit with good posture - should show "Good Posture ✓"
3. Slouch forward - after 30 seconds, you'll get a notification

---

## What's Next (Optional Improvements)

Once the MVP works, you can add:
- Settings to adjust sensitivity
- System tray icon
- Better UI design
- Posture history/statistics
- Multiple notification levels

But for now, focus on getting the basic tracking and notifications working!
