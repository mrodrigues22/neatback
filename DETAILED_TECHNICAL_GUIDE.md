# NeatBack - Detailed Technical Guide

## Architecture Overview

NeatBack uses a **client-server architecture** with asynchronous communication:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           PYTHON SERVICE (Server)                        │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ┌──────────────┐    ┌─────────────────┐    ┌───────────────────┐     │
│  │   OpenCV     │───►│  PoseDetector   │───►│ PostureAnalyzer   │     │
│  │  (Camera)    │    │  (MediaPipe)    │    │  (Angle Calc)     │     │
│  └──────────────┘    └─────────────────┘    └───────────────────┘     │
│         │                     │                        │                 │
│         └─────────────────────┴────────────────────────┘                │
│                              │                                           │
│                              ▼                                           │
│                    ┌──────────────────┐                                 │
│                    │ WebSocketServer  │                                 │
│                    │ (Port 8765)      │                                 │
│                    └──────────────────┘                                 │
│                              │                                           │
└──────────────────────────────┼───────────────────────────────────────────┘
                               │ WebSocket (JSON)
                               │ ws://localhost:8765
┌──────────────────────────────┼───────────────────────────────────────────┐
│                              ▼                                           │
│                    ┌──────────────────┐                                 │
│                    │ WebSocketClient  │                                 │
│                    └──────────────────┘                                 │
│                              │                                           │
│  ┌──────────────┐    ┌───────┴────────┐    ┌──────────────────┐       │
│  │   MainPage   │◄───┤  PostureData   │───►│ NotificationSvc  │       │
│  │   (UI)       │    │   (Model)      │    │  (Alerts)        │       │
│  └──────────────┘    └────────────────┘    └──────────────────┘       │
│                                                                           │
│                        .NET DESKTOP APP (Client)                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part 1: Python Service (Backend)

### Entry Point: `main.py`

**Purpose**: Orchestrates the entire posture detection pipeline.

```python
class PostureService:
    def __init__(self):
        self.detector = PoseDetector()       # Handles MediaPipe
        self.analyzer = PostureAnalyzer()     # Calculates angles
        self.ws_server = WebSocketServer()    # Manages connections
        self.cap = None                       # OpenCV camera
        self.running = False                  # Control flag
```

#### Key Methods:

**1. `open_camera()`**
- Initializes OpenCV VideoCapture on device 0 (default webcam)
- Sets resolution to 640x480 for optimal performance
- Lower resolution = faster processing, but less accurate pose detection

**2. `process_frame()` - async**
- Captures a single frame from the webcam
- Passes frame to PoseDetector
- If landmarks are detected, passes them to PostureAnalyzer
- Returns analysis result (dict with `is_good` and `neck_angle`)

**3. `run()` - async**
- Main event loop that runs continuously
- Creates WebSocket server task
- Processes frames at 10 FPS (0.1 second delay)
- Sends results to all connected clients
- Handles cleanup on exit

**Flow**:
```
Camera → Frame → PoseDetector → Landmarks → PostureAnalyzer → Result → WebSocket
```

---

### Pose Detection: `pose_detector.py`

**Purpose**: Uses MediaPipe to detect 33 body landmarks from video frames.

```python
class PoseDetector:
    def __init__(self):
        self.mp_pose = mp.solutions.pose
        self.pose = self.mp_pose.Pose(
            min_detection_confidence=0.5,  # 50% confidence for initial detection
            min_tracking_confidence=0.5    # 50% confidence for tracking
        )
```

#### MediaPipe Landmarks

MediaPipe detects 33 points on the human body:

```
     0: Nose
   7: Left Ear  ◄── We use this
  11: Left Shoulder  ◄── We use this
  23: Left Hip  ◄── We use this
  ... (and 30 others)
```

#### Key Method: `detect(frame)`

**Input**: BGR image frame from OpenCV
**Process**:
1. Converts BGR → RGB (MediaPipe requires RGB)
2. Runs pose estimation model
3. Returns landmark coordinates (x, y, z, visibility)

**Output**: `pose_landmarks` object or `None` if no person detected

**Coordinates**: All landmarks are normalized (0.0 - 1.0)
- `x`: Horizontal position (0 = left, 1 = right)
- `y`: Vertical position (0 = top, 1 = bottom)
- `z`: Depth (relative to hip midpoint)
- `visibility`: Confidence that landmark is visible

---

### Posture Analysis: `posture_analyzer.py`

**Purpose**: Analyzes landmark geometry to determine posture quality.

#### Method: `calculate_angle(p1, p2, p3)`

Calculates the angle at point `p2` formed by the three points.

**Mathematical approach**:
1. Create vectors from p2 to p1 and p2 to p3
2. Use dot product formula: cos(θ) = (v1 · v2) / (|v1| × |v2|)
3. Apply arccos to get angle in radians
4. Convert to degrees

```python
v1 = [p1.x - p2.x, p1.y - p2.y]
v2 = [p3.x - p2.x, p3.y - p2.y]
angle = arccos(dot(v1, v2) / (norm(v1) * norm(v2)))
```

#### Method: `analyze(landmarks)`

**Process**:
1. Extracts specific landmarks (indices 7, 11, 23)
   - `LEFT_EAR = 7`
   - `LEFT_SHOULDER = 11`
   - `LEFT_HIP = 23`

2. Calculates neck angle with shoulder as vertex

3. Determines posture quality:
   - **Good**: 80° ≤ angle ≤ 100°
   - **Bad**: angle < 80° (leaning forward) or angle > 100° (leaning back)

**Return value**:
```json
{
  "is_good": true,
  "neck_angle": 87.3
}
```

**Why this angle?**
- When sitting upright, ear-shoulder-hip forms ~90°
- Slouching/leaning forward reduces this angle
- The 80-100° range gives some tolerance for natural movement

---

### WebSocket Server: `websocket_server.py`

**Purpose**: Provides real-time communication with the .NET client.

```python
class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()  # Track all connected clients
```

#### Key Methods:

**1. `register(websocket)` - async**
- Adds new client to the set
- Called when .NET app connects

**2. `unregister(websocket)` - async**
- Removes client from the set
- Called when connection closes

**3. `send(data)` - async**
- Converts Python dict to JSON string
- Sends to ALL connected clients simultaneously using `asyncio.gather()`
- Handles exceptions (e.g., if a client disconnected)

**4. `handler(websocket)` - async**
- Manages individual client connection lifecycle
- Registers client on connect
- Waits for connection to close
- Unregisters client on disconnect

**5. `start()` - async**
- Creates WebSocket server on specified host:port
- Runs forever waiting for connections
- Uses `websockets.serve()` context manager

**Protocol**: WebSocket (RFC 6455)
**Data format**: JSON strings
**Example message**:
```json
{"is_good": false, "neck_angle": 65.2}
```

---

## Part 2: .NET Desktop App (Frontend)

### Application Entry: `App.xaml.cs`

**Purpose**: Application initialization and navigation setup.

#### Key Components:

**1. Application Lifecycle**
- `App()` constructor: Initializes WinUI3 components
- `OnLaunched()`: Called when app starts
  - Creates main window
  - Sets up navigation frame
  - Navigates to MainPage
  - Activates (shows) the window

**2. Navigation Framework**
```csharp
Frame rootFrame = new Frame();
rootFrame.Navigate(typeof(MainPage), e.Arguments);
window.Content = rootFrame;
```

This sets up the navigation container that can switch between pages.

---

### User Interface: `MainPage.xaml` & `MainPage.xaml.cs`

#### XAML Structure (`MainPage.xaml`)

```xml
<StackPanel Padding="20" Spacing="15">
    <TextBlock Text="NeatBack - Posture Tracker" /> <!-- Title -->
    <Button x:Name="StartButton" 
            Content="Start Monitoring" 
            Click="StartButton_Click" /> <!-- Action button -->
    <TextBlock x:Name="StatusText" 
               Text="Not monitoring" />     <!-- Status display -->
    <TextBlock x:Name="AngleText" 
               Text="Neck Angle: --" />     <!-- Angle display -->
</StackPanel>
```

**Layout**: Vertical stack with 20px padding and 15px spacing between elements.

#### Code-Behind (`MainPage.xaml.cs`)

**Class Fields**:
```csharp
private WebSocketClient? _wsClient;           // Connection to Python service
private NotificationService? _notificationService;  // Manages toast alerts
private DateTime _badPostureStart;            // When bad posture began
private bool _inBadPosture = false;           // Current state tracking
```

**Constructor**:
```csharp
public MainPage()
{
    InitializeComponent();  // Loads XAML UI
    _wsClient = new WebSocketClient();
    _notificationService = new NotificationService();
    _wsClient.DataReceived += OnPostureDataReceived;  // Subscribe to data events
}
```

**Event Handler: `StartButton_Click`**
- Attempts to connect to Python WebSocket server
- Updates UI to show "Monitoring..."
- Disables the Start button
- Catches and displays any connection errors

**Event Handler: `OnPostureDataReceived`**

This is where the magic happens! Called every ~100ms with new posture data.

**UI Thread Marshalling**:
```csharp
DispatcherQueue.TryEnqueue(() => { ... });
```
WebSocket data arrives on a background thread, but UI updates MUST happen on the UI thread. `DispatcherQueue` safely queues the update.

**Bad Posture Tracking Logic**:
```
IF posture is bad:
    IF not already tracking bad posture:
        Start timer (record current time)
        Mark as "in bad posture"
    ELSE:
        Calculate duration
        IF duration > 30 seconds:
            Send notification
ELSE (posture is good):
    Reset tracking flag
```

This prevents spamming notifications and only alerts after sustained bad posture.

---

### WebSocket Client: `WebSocketClient.cs`

**Purpose**: Connects to Python service and receives posture data.

#### Key Components:

**1. Connection Management**
```csharp
private ClientWebSocket? _ws;
private readonly string _uri = "ws://localhost:8765";

public async Task ConnectAsync()
{
    _ws = new ClientWebSocket();
    await _ws.ConnectAsync(new Uri(_uri), CancellationToken.None);
    _ = Task.Run(ReceiveLoop);  // Start receiving in background
}
```

**2. Receive Loop**
```csharp
private async Task ReceiveLoop()
{
    var buffer = new byte[1024];  // 1KB buffer for messages
    
    while (_ws.State == WebSocketState.Open)
    {
        var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var data = JsonSerializer.Deserialize<PostureData>(json);
        
        DataReceived?.Invoke(this, data);  // Fire event
    }
}
```

**Flow**:
1. Wait for WebSocket message (async, non-blocking)
2. Convert bytes → UTF-8 string → JSON → C# object
3. Fire `DataReceived` event
4. Subscribers (MainPage) handle the data
5. Loop continues until connection closes

**3. Event Pattern**
```csharp
public event EventHandler<PostureData>? DataReceived;
```

This allows MainPage to react to new data without polling. It's a **push model**.

---

### Notifications: `NotificationService.cs`

**Purpose**: Shows Windows toast notifications.

```csharp
public class NotificationService
{
    private DateTime _lastNotification = DateTime.MinValue;
    
    public void ShowAlert(string message)
    {
        // Cooldown check: only notify every 30 seconds
        if ((DateTime.Now - _lastNotification).TotalSeconds < 30)
            return;
        
        // Create and show toast
        new ToastContentBuilder()
            .AddText("Posture Alert")
            .AddText(message)
            .Show();
        
        _lastNotification = DateTime.Now;
    }
}
```

**How it works**:
1. Checks if 30 seconds have passed since last notification
2. Uses `Microsoft.Toolkit.Uwp.Notifications` library
3. Builds a toast with title and message
4. Shows notification via Windows Action Center
5. Records timestamp to prevent spam

**Toast Notification Lifecycle**:
- Appears in bottom-right corner
- Stays for ~5 seconds
- Moves to Action Center if not dismissed
- Can be clicked (though we don't handle clicks currently)

---

### Data Model: `PostureData.cs`

**Purpose**: Defines the data structure shared between Python and .NET.

```csharp
public class PostureData
{
    public bool is_good { get; set; }
    public double neck_angle { get; set; }
}
```

**Why lowercase property names?**
Python uses `snake_case` by default. To avoid manual mapping, we match Python's naming convention. C# deserializer automatically maps JSON keys to property names.

**JSON mapping**:
```json
{
  "is_good": true,      → PostureData.is_good = true
  "neck_angle": 87.3    → PostureData.neck_angle = 87.3
}
```

---

## Data Flow: Complete Journey

Let's trace one complete cycle from camera to notification:

### Step 1: Camera Capture (Python)
```python
ret, frame = self.cap.read()  # 640x480 BGR image
```

### Step 2: Pose Detection (Python)
```python
rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
results = self.pose.process(rgb_frame)
landmarks = results.pose_landmarks  # 33 points with x,y,z coordinates
```

### Step 3: Angle Calculation (Python)
```python
ear = landmarks[7]       # (x=0.42, y=0.15)
shoulder = landmarks[11] # (x=0.38, y=0.35)
hip = landmarks[23]      # (x=0.36, y=0.68)

neck_angle = calculate_angle(ear, shoulder, hip)  # 65.2°
is_good = 80 <= neck_angle <= 100  # False
```

### Step 4: WebSocket Send (Python)
```python
data = {"is_good": False, "neck_angle": 65.2}
message = json.dumps(data)  # '{"is_good": false, "neck_angle": 65.2}'
await websocket.send(message)
```

### Step 5: WebSocket Receive (.NET)
```csharp
var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
// json = '{"is_good": false, "neck_angle": 65.2}'
```

### Step 6: Deserialization (.NET)
```csharp
var data = JsonSerializer.Deserialize<PostureData>(json);
// data.is_good = false
// data.neck_angle = 65.2
```

### Step 7: Event Dispatch (.NET)
```csharp
DataReceived?.Invoke(this, data);
// MainPage.OnPostureDataReceived() is called
```

### Step 8: UI Update (.NET)
```csharp
DispatcherQueue.TryEnqueue(() => {
    AngleText.Text = "Neck Angle: 65.2°";
    StatusText.Text = "Bad Posture ✗";
});
```

### Step 9: Duration Tracking (.NET)
```csharp
if (!_inBadPosture) {
    _badPostureStart = DateTime.Now;  // Start timer
    _inBadPosture = true;
}
```

### Step 10: Notification (After 30 seconds) (.NET)
```csharp
var duration = (DateTime.Now - _badPostureStart).TotalSeconds;
if (duration > 30) {
    _notificationService?.ShowAlert("Fix your posture!");
}
```

**Total latency**: ~100-200ms from camera capture to UI update

---

## Threading and Concurrency

### Python Service (Asyncio)

**Event Loop**: Single-threaded asynchronous execution
```python
asyncio.run(service.run())  # Starts event loop
```

**Concurrent Tasks**:
- WebSocket server (listening for connections)
- Frame processing (10 FPS loop)
- Multiple client message sends (parallel with `asyncio.gather()`)

**No traditional threads**: Everything uses cooperative multitasking with `async/await`

### .NET App (Multi-threaded)

**UI Thread**: Handles all UI updates and events
**WebSocket Thread**: Background task for `ReceiveLoop()`

**Thread Safety**:
```csharp
DispatcherQueue.TryEnqueue(() => { /* UI update */ });
```
This queues work to the UI thread from the WebSocket background thread.

---

## Error Handling

### Python Service

**Camera Errors**:
```python
ret, frame = self.cap.read()
if not ret:
    return None  # Skip this frame
```

**No Pose Detected**:
```python
landmarks = self.detector.detect(frame)
if landmarks:
    result = self.analyzer.analyze(landmarks.landmark)
else:
    return None  # No person in frame
```

**WebSocket Send Failures**:
```python
await asyncio.gather(*[client.send(message) for client in self.clients],
                     return_exceptions=True)  # Don't crash on client disconnect
```

### .NET App

**Connection Errors**:
```csharp
try {
    await _wsClient.ConnectAsync();
} catch (Exception ex) {
    StatusText.Text = $"Error: {ex.Message}";
}
```

**Null Safety**:
```csharp
private WebSocketClient? _wsClient;  // Nullable reference type
if (_wsClient != null) { ... }       // Null check before use
```

---

## Performance Considerations

### Frame Rate: 10 FPS
```python
await asyncio.sleep(0.1)  # 100ms delay = 10 frames per second
```
**Why 10 FPS?**
- Pose detection is computationally expensive
- Posture changes slowly (no need for 30+ FPS)
- Reduces CPU/GPU usage
- Battery friendly for laptops

### WebSocket Buffer: 1KB
```csharp
var buffer = new byte[1024];
```
**Why 1KB?**
- Our JSON messages are ~50 bytes
- Plenty of headroom
- Small enough to be cache-friendly

### Camera Resolution: 640x480
```python
self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
```
**Why 640x480?**
- MediaPipe works well at this resolution
- Lower = faster processing
- Higher = more accurate but slower

---

## Configuration and Constants

### Posture Thresholds
```python
# posture_analyzer.py
is_good_posture = 80 <= neck_angle <= 100
```
Could be made configurable for different body types.

### Bad Posture Duration
```csharp
// MainPage.xaml.cs
if (duration > 30)  // 30 seconds
```
Time before first alert.

### Notification Cooldown
```csharp
// NotificationService.cs
if ((DateTime.Now - _lastNotification).TotalSeconds < 30)
```
Minimum time between notifications.

### WebSocket Port
```python
# websocket_server.py
port=8765
```
```csharp
// WebSocketClient.cs
private readonly string _uri = "ws://localhost:8765";
```

---

## Debugging Tips

### Python Service

**Check if camera is working**:
```python
ret, frame = self.cap.read()
cv2.imshow('Debug', frame)  # Show what camera sees
cv2.waitKey(1)
```

**Verify pose detection**:
```python
print(f"Detected landmarks: {len(landmarks.landmark) if landmarks else 0}")
```

**Monitor WebSocket**:
```python
print(f"Connected clients: {len(self.clients)}")
print(f"Sending: {json.dumps(result)}")
```

### .NET App

**Check WebSocket state**:
```csharp
Console.WriteLine($"WebSocket state: {_ws?.State}");
```

**Log received data**:
```csharp
System.Diagnostics.Debug.WriteLine($"Received: {json}");
```

**Verify thread context**:
```csharp
var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
System.Diagnostics.Debug.WriteLine($"Thread ID: {threadId}");
```

---

## Potential Issues and Solutions

### Issue: "Python service won't start"
**Cause**: MediaPipe not installed or wrong Python version
**Solution**: Ensure Python 3.11, reinstall: `pip install mediapipe`

### Issue: ".NET app shows 'Error: Connection refused'"
**Cause**: Python service not running
**Solution**: Start Python service first: `python src\main.py`

### Issue: "No pose detected"
**Cause**: Poor lighting or not in frame
**Solution**: Ensure face/upper body visible, improve lighting

### Issue: "Notifications not appearing"
**Cause**: Windows notifications disabled
**Solution**: Check Windows Settings → Notifications

### Issue: "Angle seems wrong"
**Cause**: Using right side when algorithm expects left
**Solution**: Sit with left side facing camera, or modify code to use right landmarks

---

## Extension Ideas

### 1. Configuration File
```json
{
  "posture_threshold": {"min": 80, "max": 100},
  "bad_posture_duration": 30,
  "notification_cooldown": 30,
  "frame_rate": 10
}
```

### 2. Calibration Mode
Let users set their personal "good posture" baseline by recording their angle when sitting correctly.

### 3. Posture History
```csharp
List<PostureRecord> history = new();
history.Add(new PostureRecord { 
    Timestamp = DateTime.Now, 
    Angle = data.neck_angle, 
    IsGood = data.is_good 
});
```

### 4. System Tray Integration
```csharp
NotifyIcon trayIcon = new();
trayIcon.Icon = SystemIcons.Application;
trayIcon.Visible = true;
```

### 5. Auto-start with Windows
Add registry entry or use Windows Task Scheduler.

---

## Summary

NeatBack is a well-architected application that demonstrates:
- **Separation of concerns**: AI/CV logic in Python, UI in .NET
- **Asynchronous programming**: Non-blocking I/O throughout
- **Real-time communication**: WebSocket for low-latency data transfer
- **Event-driven architecture**: UI reacts to data events
- **Modern Windows development**: WinUI3 with native notifications

The modular design makes it easy to extend, debug, and maintain.
