# NeatBack - Posture Tracking App Implementation Plan

## Project Overview
A hybrid Windows desktop application combining:
- **Python (MediaPipe)**: Real-time pose estimation and landmark detection
- **.NET (WinUI3)**: Modern Windows UI, notifications, and user experience

**Key Principle**: Privacy-first, local-only processing. No cloud required.

---

## Technology Stack

### Python Backend
- **Python 3.10+**
- **MediaPipe** (pose estimation)
- **OpenCV** (webcam capture)
- **WebSockets** (communication layer)
- **NumPy** (numerical processing)

### .NET Frontend
- **WinUI3** with Windows App SDK
- **C# 11+**
- **.NET 8.0**
- **System.Net.WebSockets** (client)
- **Microsoft.Toolkit.Uwp.Notifications** (toast notifications)
- **CommunityToolkit.Mvvm** (MVVM pattern)

### Communication Protocol
- **WebSocket** (JSON messages at 15-30 FPS)
- **localhost:8765** (default port)

---

## Phase 1: Project Setup & Architecture (Days 1-2)

### Step 1.1: Create Project Structure
```
neatback/
├── python-service/          # Python AI service
│   ├── src/
│   │   ├── pose_detector.py
│   │   ├── landmark_processor.py
│   │   ├── websocket_server.py
│   │   └── config.py
│   ├── requirements.txt
│   └── main.py
├── dotnet-app/              # WinUI3 application
│   ├── NeatBack.sln
│   └── NeatBack/
│       ├── Views/
│       ├── ViewModels/
│       ├── Models/
│       ├── Services/
│       └── Assets/
└── docs/
    └── architecture.md
```

**Action Items:**
- [ ] Create root directory structure
- [ ] Initialize Git repository
- [ ] Create .gitignore for Python and .NET
 - [ ] Add `README.md` with quick-start
 - [ ] Add `docs/development.md` with setup notes
 - [ ] Add `.editorconfig` for consistent formatting

**Suggested .gitignore entries:**
```
# Python
python-service/venv/
python-service/__pycache__/
python-service/*.spec
python-service/dist/
python-service/build/

# VS/VS Code
.vs/
.vscode/

# .NET
dotnet-app/**/bin/
dotnet-app/**/obj/
*.user
*.snupkg
```

### Step 1.2: Set Up Python Environment (Windows `cmd.exe`)
```bat
cd python-service
py -3 -m venv venv
call venv\Scripts\activate
python -m pip install --upgrade pip
pip install mediapipe opencv-python numpy websockets
pip freeze > requirements.txt
```

If MediaPipe installation fails on Windows, try:
```bat
pip install mediapipe==0.10.11
```

**Action Items:**
- [ ] Create virtual environment
- [ ] Install dependencies
- [ ] Test MediaPipe import
- [ ] Verify webcam access with OpenCV

### Step 1.3: Create WinUI3 Project (Windows `cmd.exe`)
```bat
cd dotnet-app
dotnet new wasdk -n NeatBack
cd NeatBack
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Toolkit.Uwp.Notifications
dotnet build -c Debug
```

**Action Items:**
- [ ] Create WinUI3 project via Visual Studio or CLI
- [ ] Add NuGet packages
- [ ] Configure project for x64 (required for MediaPipe)
- [ ] Test build and run empty app
 - [ ] Enable app capability: `webcam` (for MSIX packaging)

---

## Phase 2: Python ML Service (Days 3-7)

### Step 2.1: Implement Pose Detection Module

**File: `python-service/src/pose_detector.py`**

```python
import cv2
import mediapipe as mp
import numpy as np

class PoseDetector:
    def __init__(self):
        self.mp_pose = mp.solutions.pose
        self.pose = self.mp_pose.Pose(
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5,
            model_complexity=1
        )
        
    def detect(self, frame):
        # Convert BGR to RGB
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = self.pose.process(rgb_frame)
        return results
```

**Action Items:**
- [ ] Create PoseDetector class
- [ ] Configure MediaPipe parameters (experiment with model_complexity: 0, 1, 2)
- [ ] Add error handling for pose detection failures
- [ ] Test with sample webcam frames

### Step 2.2: Implement Landmark Processing

**File: `python-service/src/landmark_processor.py`**

```python
import numpy as np
from typing import List, Dict, Optional

class LandmarkProcessor:
    def __init__(self, smoothing_alpha=0.2):
        self.alpha = smoothing_alpha
        self.smoothed_landmarks = None
        
    def smooth_landmarks(self, landmarks):
        """Apply exponential moving average to reduce jitter"""
        if self.smoothed_landmarks is None:
            self.smoothed_landmarks = landmarks
        else:
            self.smoothed_landmarks = (
                self.alpha * landmarks + 
                (1 - self.alpha) * self.smoothed_landmarks
            )
        return self.smoothed_landmarks
    
    def calculate_angle(self, p1, p2, p3):
        """Calculate angle between three points"""
        v1 = np.array([p1.x - p2.x, p1.y - p2.y])
        v2 = np.array([p3.x - p2.x, p3.y - p2.y])
        
        cos_angle = np.dot(v1, v2) / (np.linalg.norm(v1) * np.linalg.norm(v2))
        angle = np.arccos(np.clip(cos_angle, -1.0, 1.0))
        return np.degrees(angle)
    
    def compute_posture_metrics(self, landmarks) -> Dict:
        """Compute key posture metrics"""
        # Key landmark indices (MediaPipe Pose)
        NOSE = 0
        LEFT_SHOULDER = 11
        RIGHT_SHOULDER = 12
        LEFT_EAR = 7
        RIGHT_EAR = 8
        LEFT_HIP = 23
        RIGHT_HIP = 24
        
        # Neck angle (ear-shoulder-hip alignment)
        left_neck_angle = self.calculate_angle(
            landmarks[LEFT_EAR],
            landmarks[LEFT_SHOULDER],
            landmarks[LEFT_HIP]
        )
        
        # Shoulder slope (left vs right shoulder height difference)
        shoulder_slope = abs(
            landmarks[LEFT_SHOULDER].y - landmarks[RIGHT_SHOULDER].y
        ) * 100  # Convert to percentage
        
        # Forward head distance (nose relative to shoulder midpoint)
        shoulder_mid_x = (landmarks[LEFT_SHOULDER].x + landmarks[RIGHT_SHOULDER].x) / 2
        forward_head = abs(landmarks[NOSE].x - shoulder_mid_x) * 100
        
        return {
            "neck_angle": left_neck_angle,
            "shoulder_slope": shoulder_slope,
            "forward_head_distance": forward_head
        }
```

**Action Items:**
- [ ] Implement smoothing algorithm
- [ ] Create angle calculation utility
- [ ] Implement neck angle calculation
- [ ] Implement shoulder slope detection
- [ ] Implement forward head measurement
- [ ] Test with recorded video samples

### Step 2.3: Create WebSocket Server

**File: `python-service/src/websocket_server.py`**

```python
import asyncio
import websockets
import json
import time
from typing import Optional

class PostureWebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        self.frame_counter = 0
        
    async def register(self, websocket):
        self.clients.add(websocket)
        print(f"Client connected. Total clients: {len(self.clients)}")
        
    async def unregister(self, websocket):
        self.clients.remove(websocket)
        print(f"Client disconnected. Total clients: {len(self.clients)}")
        
    async def send_posture_data(self, data: dict):
        """Broadcast posture data to all connected clients.
        Implements simple backpressure: skip send if any client is not ready."""
        if not self.clients:
            return
        message = json.dumps(data, separators=(",", ":"))
        send_tasks = []
        for client in list(self.clients):
            try:
                if client.closed:
                    await self.unregister(client)
                    continue
                send_tasks.append(client.send(message))
            except Exception:
                # Don't break the loop; unregister problematic client
                await self.unregister(client)
        if send_tasks:
            await asyncio.gather(*send_tasks, return_exceptions=True)
    
    async def handler(self, websocket, path):
        await self.register(websocket)
        try:
            async for message in websocket:
                # Handle incoming messages (e.g., commands, config)
                # Example: update server FPS target or enable debug
                # (define your schema in docs)
                await asyncio.sleep(0)
        finally:
            await self.unregister(websocket)
    
    async def start(self):
        print(f"Starting WebSocket server on ws://{self.host}:{self.port}")
        async with websockets.serve(self.handler, self.host, self.port, max_size=2**20):
            await asyncio.Future()  # run forever
```

**Action Items:**
- [ ] Create WebSocket server class
- [ ] Implement client connection management
- [ ] Add JSON serialization for landmark data
- [ ] Test with WebSocket client tool (e.g., websocat)

### Step 2.4: Main Service Integration

**File: `python-service/main.py`**

```python
import cv2
import asyncio
import time
from src.pose_detector import PoseDetector
from src.landmark_processor import LandmarkProcessor
from src.websocket_server import PostureWebSocketServer

class PostureService:
    def __init__(self):
        self.detector = PoseDetector()
        self.processor = LandmarkProcessor()
        self.ws_server = PostureWebSocketServer()
        self.cap = None
        self.running = False
        self.target_fps = 30
        self._frame_interval = 1 / self.target_fps
        
    def initialize_camera(self, camera_index=0):
        self.cap = cv2.VideoCapture(camera_index)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        self.cap.set(cv2.CAP_PROP_FPS, 30)
        
    async def process_frame(self):
        """Process single frame and send data"""
        ret, frame = self.cap.read()
        if not ret:
            return None
            
        # Detect pose
        results = self.detector.detect(frame)
        
        if results.pose_landmarks:
            # Extract landmarks
            landmarks = results.pose_landmarks.landmark
            
            # Smooth landmarks
            smoothed = self.processor.smooth_landmarks(landmarks)
            
            # Compute metrics
            metrics = self.processor.compute_posture_metrics(landmarks)
            
            # Prepare message
            message = {
                "timestamp": int(time.time() * 1000),
                "landmarks": [
                    {
                        "id": idx,
                        "x": lm.x,
                        "y": lm.y,
                        "z": lm.z,
                        "visibility": lm.visibility
                    }
                    for idx, lm in enumerate(landmarks)
                ],
                "metrics": metrics
            }
            
            return message
        return None
    
    async def run(self):
        """Main service loop"""
        self.running = True
        self.initialize_camera()
        
        # Start WebSocket server in background
        asyncio.create_task(self.ws_server.start())
        
        print("Posture service started. Processing frames...")
        
        while self.running:
            try:
                message = await self.process_frame()
                if message:
                    await self.ws_server.send_posture_data(message)
                
                # Control frame rate
                await asyncio.sleep(self._frame_interval)
                
            except Exception as e:
                print(f"Error processing frame: {e}")
        
        self.cap.release()

if __name__ == "__main__":
    service = PostureService()
    try:
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("\nShutting down...")
```

**Action Items:**
- [ ] Integrate all Python modules
- [ ] Add FPS control (target 30 FPS)
- [ ] Implement graceful shutdown
- [ ] Add command-line arguments for config
- [ ] Test end-to-end: camera → detection → WebSocket

### Step 2.5: Testing & Optimization

**Action Items:**
- [ ] Measure FPS and CPU usage
- [ ] Test with different lighting conditions
- [ ] Test with user wearing glasses/hat
- [ ] Profile memory usage
- [ ] Optimize frame skipping if FPS drops
 - [ ] Add unit tests for `LandmarkProcessor` calculations
 - [ ] Add integration test using a sample video file
 - [ ] Validate WebSocket message schema against JSON Schema
 - [ ] Fuzz WebSocket client with malformed messages

---

## Phase 3: .NET Desktop Application (Days 8-21)

### Step 3.1: Project Architecture Setup

**Action Items:**
- [ ] Create folder structure (Views, ViewModels, Models, Services)
- [ ] Set up dependency injection in App.xaml.cs
- [ ] Configure MVVM toolkit
- [ ] Create base classes (ViewModelBase, etc.)
 - [ ] Add `IOptions` pattern for thresholds and settings

### Step 3.2: Create Data Models

**File: `Models/PostureLandmark.cs`**

```csharp
public class PostureLandmark
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Visibility { get; set; }
}

public class PostureMetrics
{
    public double NeckAngle { get; set; }
    public double ShoulderSlope { get; set; }
    public double ForwardHeadDistance { get; set; }
}

public class PostureData
{
    public long Timestamp { get; set; }
    public List<PostureLandmark> Landmarks { get; set; }
    public PostureMetrics Metrics { get; set; }
}
```

**Action Items:**
- [ ] Create model classes
- [ ] Add JSON serialization attributes
- [ ] Implement validation logic

### Step 3.3: WebSocket Service

**File: `Services/PostureWebSocketService.cs`**

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class PostureWebSocketService
{
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts;
    private readonly string _serverUri = "ws://localhost:8765";
    
    public event EventHandler<PostureData> DataReceived;
    public event EventHandler<string> ConnectionStatusChanged;
    
    public async Task ConnectAsync()
    {
        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        
        try
        {
            await _webSocket.ConnectAsync(new Uri(_serverUri), _cts.Token);
            ConnectionStatusChanged?.Invoke(this, "Connected");
            
            // Start receiving loop
            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception ex)
        {
            ConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
        }
    }
    
    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 4];
        
        while (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    _cts.Token
                );
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var data = JsonSerializer.Deserialize<PostureData>(json);
                    
                    DataReceived?.Invoke(this, data);
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
                break;
            }
        }
    }
    
    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, 
                "Closing", 
                CancellationToken.None
            );
        }
    }
}
```

**Action Items:**
- [ ] Implement WebSocket client
- [ ] Add reconnection logic (exponential backoff)
- [ ] Handle connection errors gracefully
- [ ] Test connection/disconnection scenarios
 - [ ] Validate incoming payload size and schema

### Step 3.4: Posture Analysis Service

**File: `Services/PostureAnalysisService.cs`**

```csharp
public class PostureAnalysisService
{
    private PostureData _baselinePosture;
    private DateTime _badPostureStartTime;
    private bool _inBadPosture = false;
    
    // Configurable thresholds
    public double NeckAngleThreshold { get; set; } = 20.0;
    public double ShoulderSlopeThreshold { get; set; } = 15.0;
    public double ForwardHeadThreshold { get; set; } = 5.0;
    
    public event EventHandler<PostureAlert> AlertTriggered;
    
    public void SetBaseline(PostureData baseline)
    {
        _baselinePosture = baseline;
    }
    
    public PostureStatus AnalyzePosture(PostureData current)
    {
        var status = new PostureStatus();
        
        // Check neck angle
        if (Math.Abs(current.Metrics.NeckAngle - 90) > NeckAngleThreshold)
        {
            status.Issues.Add($"Neck angle: {current.Metrics.NeckAngle:F1}°");
            status.IsGood = false;
        }
        
        // Check shoulder slope
        if (current.Metrics.ShoulderSlope > ShoulderSlopeThreshold)
        {
            status.Issues.Add($"Uneven shoulders: {current.Metrics.ShoulderSlope:F1}%");
            status.IsGood = false;
        }
        
        // Check forward head
        if (current.Metrics.ForwardHeadDistance > ForwardHeadThreshold)
        {
            status.Issues.Add($"Head too forward: {current.Metrics.ForwardHeadDistance:F1}");
            status.IsGood = false;
        }
        
        // Track bad posture duration
        if (!status.IsGood)
        {
            if (!_inBadPosture)
            {
                _badPostureStartTime = DateTime.Now;
                _inBadPosture = true;
            }
            else
            {
                var duration = (DateTime.Now - _badPostureStartTime).TotalSeconds;
                status.BadPostureDuration = duration;
                
                // Trigger alerts based on duration
                if (duration > 30 && duration < 31)
                {
                    AlertTriggered?.Invoke(this, new PostureAlert
                    {
                        Severity = AlertSeverity.Mild,
                        Message = "Check your posture"
                    });
                }
                else if (duration > 120 && duration < 121)
                {
                    AlertTriggered?.Invoke(this, new PostureAlert
                    {
                        Severity = AlertSeverity.Strong,
                        Message = "Poor posture for 2 minutes!"
                    });
                }
            }
        }
        else
        {
            _inBadPosture = false;
        }
        
        return status;
    }
}

public class PostureStatus
{
    public bool IsGood { get; set; } = true;
    public List<string> Issues { get; set; } = new();
    public double BadPostureDuration { get; set; }
}

public class PostureAlert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; }
}

public enum AlertSeverity { Mild, Strong, Critical }
```

**Action Items:**
- [ ] Implement threshold-based analysis
- [ ] Add configurable settings
- [ ] Create alert system with severity levels
- [ ] Add timer tracking for bad posture duration
 - [ ] Persist alert snooze state and quiet hours

### Step 3.5: Notification Service

**File: `Services/NotificationService.cs`**

```csharp
using Microsoft.Toolkit.Uwp.Notifications;

public class NotificationService
{
    public void ShowPostureAlert(PostureAlert alert)
    {
        var builder = new ToastContentBuilder()
            .AddText("NeatBack Posture Alert")
            .AddText(alert.Message)
            .AddButton(new ToastButton()
                .SetContent("OK")
                .AddArgument("action", "dismiss"))
            .AddButton(new ToastButton()
                .SetContent("Snooze 10 min")
                .AddArgument("action", "snooze"));
        
        if (alert.Severity == AlertSeverity.Strong)
        {
            builder.AddAudio(new ToastAudio() { Src = new Uri("ms-winsoundevent:Notification.Default") });
        }
        
        builder.Show();
    }
}
```

**Action Items:**
- [ ] Implement toast notifications
- [ ] Add snooze functionality
- [ ] Configure notification sounds
- [ ] Test notification permissions
 - [ ] Add global mute/quiet hours integration

### Step 3.6: Main ViewModel

**File: `ViewModels/MainViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    private readonly PostureWebSocketService _wsService;
    private readonly PostureAnalysisService _analysisService;
    private readonly NotificationService _notificationService;
    
    [ObservableProperty]
    private string _connectionStatus = "Disconnected";
    
    [ObservableProperty]
    private PostureMetrics _currentMetrics;
    
    [ObservableProperty]
    private PostureStatus _postureStatus;
    
    [ObservableProperty]
    private bool _isMonitoring = false;
    
    public MainViewModel(
        PostureWebSocketService wsService,
        PostureAnalysisService analysisService,
        NotificationService notificationService)
    {
        _wsService = wsService;
        _analysisService = analysisService;
        _notificationService = notificationService;
        
        _wsService.DataReceived += OnDataReceived;
        _wsService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _analysisService.AlertTriggered += OnAlertTriggered;
    }
    
    [RelayCommand]
    private async Task StartMonitoring()
    {
        await _wsService.ConnectAsync();
        IsMonitoring = true;
    }
    
    [RelayCommand]
    private async Task StopMonitoring()
    {
        await _wsService.DisconnectAsync();
        IsMonitoring = false;
    }
    
    [RelayCommand]
    private void CalibrateBaseline()
    {
        // Set current posture as baseline
        if (CurrentMetrics != null)
        {
            _analysisService.SetBaseline(new PostureData
            {
                Metrics = CurrentMetrics
            });
        }
    }
    
    private void OnDataReceived(object sender, PostureData data)
    {
        CurrentMetrics = data.Metrics;
        PostureStatus = _analysisService.AnalyzePosture(data);
    }
    
    private void OnConnectionStatusChanged(object sender, string status)
    {
        ConnectionStatus = status;
    }
    
    private void OnAlertTriggered(object sender, PostureAlert alert)
    {
        _notificationService.ShowPostureAlert(alert);
    }
}
```

**Action Items:**
- [ ] Implement main ViewModel
- [ ] Wire up all services
- [ ] Add command handlers
- [ ] Implement property change notifications
 - [ ] Add validation and error banners for connection issues

### Step 3.7: Main UI View

**File: `Views/MainPage.xaml`**

```xml
<Page x:Class="NeatBack.Views.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <Grid Padding="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <StackPanel Grid.Row="0" Spacing="8">
            <TextBlock Text="NeatBack" Style="{StaticResource TitleTextBlockStyle}"/>
            <TextBlock Text="{x:Bind ViewModel.ConnectionStatus, Mode=OneWay}"
                       Foreground="{ThemeResource SystemAccentColor}"/>
        </StackPanel>
        
        <!-- Metrics Display -->
        <StackPanel Grid.Row="1" VerticalAlignment="Center" Spacing="16">
            
            <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    CornerRadius="8" Padding="16">
                <StackPanel Spacing="12">
                    <TextBlock Text="Current Metrics" 
                               Style="{StaticResource SubtitleTextBlockStyle}"/>
                    
                    <StackPanel Spacing="8">
                        <TextBlock>
                            <Run Text="Neck Angle: "/>
                            <Run Text="{x:Bind ViewModel.CurrentMetrics.NeckAngle, Mode=OneWay}"
                                 FontWeight="Bold"/>
                            <Run Text="°"/>
                        </TextBlock>
                        
                        <TextBlock>
                            <Run Text="Shoulder Slope: "/>
                            <Run Text="{x:Bind ViewModel.CurrentMetrics.ShoulderSlope, Mode=OneWay}"
                                 FontWeight="Bold"/>
                            <Run Text="%"/>
                        </TextBlock>
                        
                        <TextBlock>
                            <Run Text="Forward Head: "/>
                            <Run Text="{x:Bind ViewModel.CurrentMetrics.ForwardHeadDistance, Mode=OneWay}"
                                 FontWeight="Bold"/>
                        </TextBlock>
                    </StackPanel>
                </StackPanel>
            </Border>
            
            <!-- Posture Status -->
            <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    CornerRadius="8" Padding="16">
                <StackPanel Spacing="8">
                    <TextBlock Text="Posture Status" 
                               Style="{StaticResource SubtitleTextBlockStyle}"/>
                    <TextBlock Text="{x:Bind ViewModel.PostureStatus.IsGood, Mode=OneWay}"
                               Foreground="{ThemeResource SystemFillColorSuccessBrush}"/>
                    
                    <ItemsRepeater ItemsSource="{x:Bind ViewModel.PostureStatus.Issues, Mode=OneWay}">
                        <ItemsRepeater.ItemTemplate>
                            <DataTemplate x:DataType="x:String">
                                <TextBlock Text="{x:Bind}" Foreground="Orange"/>
                            </DataTemplate>
                        </ItemsRepeater.ItemTemplate>
                    </ItemsRepeater>
                </StackPanel>
            </Border>
            
        </StackPanel>
        
        <!-- Controls -->
        <StackPanel Grid.Row="2" Spacing="8" Orientation="Horizontal">
            <Button Content="Start Monitoring"
                    Command="{x:Bind ViewModel.StartMonitoringCommand}"
                    IsEnabled="{x:Bind ViewModel.IsMonitoring, Mode=OneWay, Converter={StaticResource InverseBoolConverter}}"/>
            
            <Button Content="Stop"
                    Command="{x:Bind ViewModel.StopMonitoringCommand}"
                    IsEnabled="{x:Bind ViewModel.IsMonitoring, Mode=OneWay}"/>
            
            <Button Content="Calibrate"
                    Command="{x:Bind ViewModel.CalibrateBaselineCommand}"/>
        </StackPanel>
        
    </Grid>
</Page>
```

**Action Items:**
- [ ] Create main UI layout
- [ ] Design metrics display cards
- [ ] Add control buttons
- [ ] Implement data binding
- [ ] Add visual feedback for posture status
 - [ ] Add live connection indicator and FPS display

### Step 3.8: Settings Page

**Action Items:**
- [ ] Create settings page UI
- [ ] Add threshold configuration sliders
- [ ] Implement quiet hours setting
- [ ] Add sensitivity controls
- [ ] Save settings to local storage (ApplicationData)

---

## Phase 4: Integration & Process Management (Days 22-28)

### Step 4.1: Python Service Launcher

**File: `Services/PythonServiceManager.cs`**

```csharp
using System.Diagnostics;

public class PythonServiceManager
{
    private Process _pythonProcess;
    private readonly string _pythonScriptPath;
    private readonly string _workingDirectory;
    private readonly string _pythonExecutable;
    
    public PythonServiceManager(string scriptPath, string workingDirectory = null, string pythonExecutable = "python")
    {
        _pythonScriptPath = scriptPath;
        _workingDirectory = workingDirectory ?? Path.GetDirectoryName(scriptPath);
        _pythonExecutable = pythonExecutable;
    }
    
    public async Task StartServiceAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExecutable,
            Arguments = _pythonScriptPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.WorkingDirectory = _workingDirectory;
        
        _pythonProcess = new Process { StartInfo = startInfo };
        
        _pythonProcess.OutputDataReceived += (s, e) => Debug.WriteLine($"Python: {e.Data}");
        _pythonProcess.ErrorDataReceived += (s, e) => Debug.WriteLine($"Python Error: {e.Data}");
        
        _pythonProcess.Start();
        _pythonProcess.BeginOutputReadLine();
        _pythonProcess.BeginErrorReadLine();
        
        // Wait for service to be ready
        await Task.Delay(2000);
    }
    
    public void StopService()
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            _pythonProcess.Kill();
            _pythonProcess.Dispose();
        }
    }
}
```

**Action Items:**
- [ ] Implement Python process launcher
- [ ] Add process monitoring and auto-restart
- [ ] Handle Python crashes gracefully
- [ ] Add logging for Python stdout/stderr
 - [ ] Allow configuring Python path (e.g., bundled exe)

### Step 4.2: Bundle Python with PyInstaller

**Create build script: `python-service/build.py`**

```python
import PyInstaller.__main__

PyInstaller.__main__.run([
    'main.py',
    '--onefile',
    '--name=NeatBackService',
    '--hidden-import=mediapipe',
    '--hidden-import=cv2',
    '--noconsole',
])
```

**Action Items:**
- [ ] Install PyInstaller: `pip install pyinstaller`
- [ ] Create build script
- [ ] Test standalone executable
- [ ] Bundle with .NET app in deployment
 - [ ] Include data files (e.g., `requirements.txt`, config) if needed
 - [ ] Verify antivirus false positives and code signing

### Step 4.3: Startup & Shutdown Flow

**Action Items:**
- [ ] Start Python service on app launch
- [ ] Wait for WebSocket server ready
- [ ] Connect .NET client
- [ ] Gracefully shutdown Python on app close
- [ ] Handle abnormal terminations
 - [ ] Implement health check endpoint or heartbeat messages

---

## Phase 5: Advanced Features (Days 29-42)

### Step 5.1: Posture History & Analytics

**Action Items:**
- [ ] Create SQLite database for history
- [ ] Store posture snapshots every minute
- [ ] Create charts (Line chart for posture score over time)
- [ ] Add daily/weekly reports
- [ ] Export to CSV functionality

### Step 5.2: Calibration Wizard

**Action Items:**
- [ ] Create first-run calibration wizard
- [ ] Guide user to sit correctly
- [ ] Capture baseline posture
- [ ] Save as user's "good posture" reference
- [ ] Allow recalibration from settings

### Step 5.3: Advanced Alerts

**Action Items:**
- [ ] Implement alert frequency limiting (no spam)
- [ ] Add snooze timer (10 min, 30 min, 1 hour)
- [ ] Create escalating alert system
- [ ] Add sound options (gentle chime, notification sound, silent)

### Step 5.4: Performance Optimizations

**Action Items:**
- [ ] Implement adaptive FPS (lower when idle)
- [ ] Add GPU acceleration option (MediaPipe supports it)
- [ ] Optimize memory usage
- [ ] Profile and fix bottlenecks
- [ ] Add battery-saving mode (5 FPS)

### Step 5.5: Privacy Features

**Action Items:**
- [ ] Add "pause monitoring" hotkey
- [ ] Privacy indicator (tray icon changes color)
- [ ] Option to disable preview camera feed
- [ ] Clear privacy policy in about page
- [ ] Add "no data leaves your device" messaging
 - [ ] Make telemetry opt-in with explicit consent (default off)

---

## Phase 6: Polish & Testing (Days 43-56)

### Step 6.1: UI/UX Polish

**Action Items:**
- [ ] Create app icon and logo
- [ ] Design splash screen
- [ ] Add animations (smooth transitions)
- [ ] Implement dark/light theme support
- [ ] Add accessibility features (screen reader support)

### Step 6.2: System Tray Integration

**Action Items:**
- [ ] Add system tray icon
- [ ] Create tray menu (Start/Stop, Settings, Exit)
- [ ] Show posture status in tray tooltip
- [ ] Minimize to tray option

### Step 6.3: Comprehensive Testing

**Action Items:**
- [ ] Test on different Windows versions (10, 11)
- [ ] Test with various webcams (USB, built-in)
- [ ] Test in different lighting conditions
- [ ] Test with multiple monitors
- [ ] Stress test (8+ hour sessions)
- [ ] Test all alert scenarios
- [ ] User acceptance testing with 5-10 people

### Step 6.4: Error Handling

**Action Items:**
- [ ] Add comprehensive try-catch blocks
- [ ] Implement error logging (file-based)
- [ ] Create user-friendly error messages
- [ ] Add diagnostic info collection
- [ ] Test recovery from failures
 - [ ] Redact sensitive data in logs by default

---

## Phase 7: Deployment (Days 57-63)

### Step 7.1: Create Installer

**Option A: MSIX Package (Recommended)**

**Action Items:**
- [ ] Configure MSIX packaging in Visual Studio
- [ ] Include Python executable in package
- [ ] Set up app capabilities (webcam)
- [ ] Create signing certificate
- [ ] Test MSIX installation
 - [ ] Validate MSIX Capabilities: `internetClient`, `webcam`, `backgroundTasks` (if used)

**Option B: Traditional Installer (Inno Setup)**

```
[Setup]
AppName=NeatBack
AppVersion=1.0.0
DefaultDirName={pf}\NeatBack
OutputDir=installer
```

**Action Items:**
- [ ] Download Inno Setup
- [ ] Create installer script
- [ ] Bundle all dependencies
- [ ] Create uninstaller
- [ ] Test full install/uninstall cycle

### Step 7.2: Microsoft Store Submission

**Action Items:**
- [ ] Create Microsoft Partner Center account
- [ ] Prepare store listing (screenshots, description)
- [ ] Submit MSIX package
- [ ] Pass certification tests
- [ ] Publish

### Step 7.3: Documentation

**Action Items:**
- [ ] Create user guide
- [ ] Write troubleshooting FAQ
- [ ] Create video tutorial
- [ ] Document privacy policy
- [ ] Add in-app help
 - [ ] Add architecture diagram and data flow

---

## Phase 8: Monetization & Growth (Optional)

### Free Tier Features
- Basic posture tracking
- Real-time alerts
- Simple metrics
- 7-day history

### Pro Tier Features ($4.99/month or $29.99/year)
- Unlimited history & analytics
- Advanced metrics (detailed reports)
- Customizable thresholds
- PDF export of reports
- Priority support
- Cloud sync (future)

### Implementation Tasks
- [ ] Implement license validation
- [ ] Add in-app purchase (Microsoft Store)
- [ ] Create upgrade prompts
- [ ] Build subscription management page
- [ ] Add trial period (14 days)
 - [ ] Add offline grace period and fallback behavior

---

## Testing Checklist

### Functional Testing
- [ ] Pose detection accuracy across body types
- [ ] WebSocket connection stability
- [ ] Alert triggering at correct thresholds
- [ ] Settings persistence
- [ ] Calibration accuracy
- [ ] Notification delivery

### Performance Testing
- [ ] CPU usage < 10% average
- [ ] Memory usage < 200MB
- [ ] FPS stability (28-30 FPS)
- [ ] No memory leaks after 8 hours
- [ ] Startup time < 5 seconds
 - [ ] End-to-end latency < 100ms (camera → UI)

### Compatibility Testing
- [ ] Windows 10 (21H2+)
- [ ] Windows 11
- [ ] Different webcams (5+ models)
- [ ] Different screen resolutions
- [ ] Laptop vs desktop

### Security Testing
- [ ] No unauthorized network access
- [ ] Camera permissions handled correctly
- [ ] Local-only processing verified
- [ ] No data exfiltration
 - [ ] Logs do not contain PII
 - [ ] WebSocket rejects oversized/malformed frames
 - [ ] MSIX capabilities limited to least privilege

---

## Security & Privacy

- Local-only design: no cloud endpoints by default.
- Minimize data retention: no raw frames stored unless user opts in.
- Logs: rotate and redact; store under `%LOCALAPPDATA%/NeatBack/logs`.
- Permissions: request webcam capability via MSIX; show clear rationale.
- Threats: unauthorized access to WebSocket, elevation via Python process, DLL injection; mitigate with localhost binding, input validation, code signing.
- Crash handling: supervised Python process with auto-restart and backoff.

---

## JSON Message Schema (Draft)

Top-level fields:
- `timestamp`: `int` (ms since epoch)
- `landmarks`: `array` of 33 items `{ id, x, y, z, visibility }` where coordinates are normalized (0..1)
- `metrics`: `{ neck_angle, shoulder_slope, forward_head_distance }`

Notes:
- Use compact JSON with `separators` to reduce bandwidth.
- Limit message size to ~10–20KB per frame.
- Consider binary framing if needed (e.g., MessagePack) later.

---

## Risks & Mitigations

- MediaPipe install issues on Windows → pin versions, document fallback.
- Performance variability across webcams → adaptive FPS and resolution.
- False alerts → calibration wizard and per-user thresholds.
- Packaging Python → verify antivirus and sign binaries.
- Long sessions stability → memory profiling and leak detection.

---

## Development Timeline Summary

| Phase | Duration | Key Deliverable |
|-------|----------|----------------|
| 1. Setup | 2 days | Working dev environment |
| 2. Python Service | 5 days | Real-time pose detection |
| 3. .NET App | 14 days | Full UI and logic |
| 4. Integration | 7 days | Seamless Python/.NET communication |
| 5. Advanced Features | 14 days | Analytics, calibration, optimizations |
| 6. Polish & Testing | 14 days | Production-ready app |
| 7. Deployment | 7 days | Installer & store submission |
| **Total** | **63 days** | **Shippable product** |

---

## Key Success Metrics

### Technical
- ✅ 30 FPS pose detection
- ✅ < 100ms latency between detection and UI update
- ✅ < 10% CPU usage on modern hardware
- ✅ Zero crashes in 24-hour test

### User Experience
- ✅ 95%+ posture detection accuracy
- ✅ Calibration completes in < 2 minutes
- ✅ Alerts are helpful, not annoying
- ✅ App is intuitive (no manual needed)

### Business
- ✅ 1000+ downloads in first month
- ✅ 4.5+ stars average rating
- ✅ 15%+ conversion to Pro tier

---

## Resources & References

### Documentation
- [MediaPipe Pose](https://google.github.io/mediapipe/solutions/pose.html)
- [WinUI 3 Docs](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [Windows Notifications](https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/adaptive-interactive-toasts)

### Libraries
- Python: `mediapipe`, `opencv-python`, `websockets`, `numpy`
- .NET: `CommunityToolkit.Mvvm`, `Microsoft.Toolkit.Uwp.Notifications`

### Tools
- **Visual Studio 2022** (with WinUI3 workload)
- **VS Code** (for Python development)
- **PyInstaller** (for bundling Python)
- **Inno Setup** or **MSIX Packaging Tool**

---

## Next Steps

1. **TODAY**: Set up project structure and dev environment
2. **Week 1**: Complete Python service with basic pose detection
3. **Week 2-3**: Build .NET UI and WebSocket integration
4. **Week 4-6**: Add advanced features and testing
5. **Week 7-8**: Polish, test, and prepare for release
6. **Week 9**: Deploy and launch!

---

## Notes

- **Privacy First**: Emphasize that all processing is local
- **Start Simple**: Get MVP working before adding advanced features
- **User Testing**: Get feedback early and iterate
- **Performance**: Monitor resource usage continuously
- **Documentation**: Keep README and docs updated

---

*Last Updated: 2025-12-11*
*Version: 1.0*
