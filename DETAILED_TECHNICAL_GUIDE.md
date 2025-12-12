# Slouti - Detailed Technical Guide

## Architecture Overview

Slouti uses a **client-server architecture** with asynchronous communication:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        .NET DESKTOP APP (Client)                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ┌──────────────┐    ┌─────────────────┐    ┌──────────────────┐       │
│  │ WebSocket    │───►│  PostureData    │───►│  MainPage UI     │       │
│  │  Client      │    │  (Model)        │    │  (Display)       │       │
│  └──────────────┘    └─────────────────┘    └──────────────────┘       │
│         │                     │                        │                 │
│         └─────────────────────┴────────────────────────┘                │
│                              ▲ Posture Results & Frame Previews          │
│  ┌──────────────┐    ┌───────┴────────┐    ┌──────────────────┐       │
│  │   Controls   │───►│  WebSocket     │───►│ NotificationSvc  │       │
│  │   (Buttons)  │    │   Messages     │    │  (Alerts)        │       │
│  └──────────────┘    └────────────────┘    └──────────────────┘       │
│                              │ Control Messages (JSON)                  │
└──────────────────────────────┼───────────────────────────────────────────┘
                               │ WebSocket (JSON)
                               │ ws://localhost:8765
┌──────────────────────────────┼───────────────────────────────────────────┐
│                              │                                           │
│                    ┌──────────────────┐                                 │
│                    │ WebSocketServer  │                                 │
│                    │ (Port 8765)      │                                 │
│                    └──────────────────┘                                 │
│                              │                                           │
│  ┌──────────────┐    ┌───────┴────────┐    ┌───────────────────┐     │
│  │ OpenCV       │───►│PostureDetector │───►│ PostureAnalyzer   │     │
│  │ (Camera      │    │ (MediaPipe     │    │ (Pitch/Distance   │     │
│  │  Capture)    │    │  Face Detect)  │    │  Analysis)        │     │
│  └──────────────┘    └────────────────┘    └───────────────────┘     │
│                                                                           │
│                           PYTHON SERVICE (Server)                        │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Part 1: Python Service (Backend)

### Entry Point: `main.py`

**Purpose**: Orchestrates the WebSocket server and posture analysis pipeline.

```python
class PostureService:
    def __init__(self):
        self.detector = PostureDetector()       # Handles MediaPipe Face Landmarker
        self.analyzer = PostureAnalyzer()       # Tracks posture duration & stats
        self.ws_server = WebSocketServer()      # Manages connections
        
        # Link detector and analyzer to WebSocket server
        self.ws_server.detector = self.detector
        self.ws_server.analyzer = self.analyzer
        
        self.running = False                    # Control flag
```

#### Key Method:

**`run()` - async**
- Main entry point that starts the WebSocket server
- Waits for client connections
- Server processes incoming frames from .NET client
- Returns posture analysis results back to client
- Handles cleanup on exit (closes detector)

**Flow**:
```
OpenCV Camera → Capture Frame → Face Detection → Pitch/Distance Calc → 
Posture Analysis → Result + Frame → WebSocket → .NET Client Display
```

---

### Face Detection & Pose Estimation: `pose_detector.py`

**Purpose**: Uses MediaPipe Face Landmarker to detect facial landmarks and calculate head pose.

```python
class PostureDetector:
    def __init__(self):
        # Initialize MediaPipe Face Landmarker
        options = self.FaceLandmarkerOptions(
            base_options=self.BaseOptions(model_asset_path='face_landmarker.task'),
            running_mode=self.VisionRunningMode.VIDEO,
            num_faces=1,
            min_face_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )
        self.face_landmarker = self.FaceLandmarker.create_from_options(options)
        
        # Good posture baseline (set via calibration)
        self.good_head_pitch_angle = None
        self.good_head_distance = None
        
        # Configurable thresholds
        self.pitch_threshold = -10  # degrees
        self.distance_threshold = 10  # cm
```

#### MediaPipe Face Landmarks

MediaPipe Face Landmarker detects 478 facial landmarks. We use 6 key points for 3D pose estimation:

```
  33: Left eye outer corner  ◄── We use this
 263: Right eye outer corner ◄── We use this
   1: Nose tip               ◄── We use this
  61: Left mouth corner      ◄── We use this
 291: Right mouth corner     ◄── We use this
 199: Chin                   ◄── We use this
```

#### Key Methods:

**1. `detect_landmarks(frame, timestamp_ms)`**

**Input**: BGR image frame from OpenCV, timestamp in milliseconds
**Process**:
1. Converts BGR → RGB (MediaPipe requires RGB)
2. Creates MediaPipe Image object
3. Runs face landmarker model for video mode
4. Returns first face's landmarks

**Output**: List of landmark objects with normalized (x, y, z) coordinates or `None`

**Coordinates**: All landmarks are normalized (0.0 - 1.0)
- `x`: Horizontal position (0 = left, 1 = right)
- `y`: Vertical position (0 = top, 1 = bottom)
- `z`: Depth relative to face center

**2. `calculate_pitch_angle(landmarks, frame_shape)`**

**Purpose**: Calculates head pitch angle using 3D Perspective-n-Point (PnP) algorithm.

**Mathematical approach**:
1. Extract 2D coordinates of 6 key facial landmarks from frame
2. Use pre-defined 3D face model coordinates (in mm)
3. Create camera intrinsic matrix (focal length, optical center)
4. Solve PnP problem to get rotation and translation vectors
5. Convert rotation vector to rotation matrix
6. Extract Euler angles (pitch, yaw, roll)
7. Return pitch angle in degrees

**Why PnP?**
- Estimates 3D head pose from 2D image points
- More accurate than simple angle calculation
- Accounts for camera perspective and distance

**3. `calculate_distance(landmarks, frame_shape)`**

**Purpose**: Estimates distance from camera based on face size.

**Approach**:
1. Calculate distance between eye corners in pixels
2. Use known average eye distance (~6.3 cm)
3. Apply similar triangles: `distance = (focal_length × real_size) / pixel_size`
4. Returns distance in centimeters

**4. `check_posture(landmarks, frame_shape, timestamp_ms)`**

**Main method that combines all measurements:**

**Process**:
1. Calculate current pitch angle
2. Calculate current distance from camera
3. If baseline is set (calibrated):
   - Calculate adjusted pitch (difference from baseline)
   - Check if outside thresholds
4. Return comprehensive posture status

**Output**:
```python
{
    'is_bad': False,
    'pitch_angle': -5.2,
    'adjusted_pitch': -3.1,  # Relative to baseline
    'distance': 55.3,
    'reasons': []  # List of threshold violations
}
```

---

### Posture Duration Tracking: `posture_analyzer.py`

**Purpose**: Tracks bad posture duration, manages warnings, and maintains statistics.

```python
class PostureAnalyzer:
    def __init__(self):
        self.bad_posture_start = None           # When bad posture began
        self.bad_posture_duration = 0           # Current bad duration
        self.warning_sent_at = set()            # Track warning timestamps
        
        # Statistics tracking
        self.total_bad_duration = 0
        self.longest_bad_streak = 0
        self.longest_good_streak = 0
        self.good_posture_start = time.time()
        
        # Warning thresholds (configurable)
        self.initial_warning_seconds = 5        # First warning
        self.repeat_warning_interval = 20       # Subsequent warnings
```

#### Key Method: `update(posture_status)`

**Purpose**: Updates analyzer with new posture status and determines if warning is needed.

**Input**: Posture status dict from `PostureDetector.check_posture()`

**Process**:

1. **If bad posture detected**:
   - If just started: Record start time, update good streak
   - If continuing: Calculate duration since start
   - Check if warning threshold reached:
     - Initial warning at 5 seconds
     - Repeat warnings every 20 seconds
   - Track which warnings have been sent to avoid duplicates

2. **If good posture**:
   - If coming from bad: Update statistics (total bad duration, longest streak)
   - Reset bad posture tracking
   - Start tracking good posture duration

**Return value**:
```python
{
    'should_warn': True,              # Should send notification?
    'bad_duration': 25,               # Seconds of bad posture
    'pitch': -12.5,                   # Current pitch angle
    'adjusted_pitch': -10.3,          # Relative to baseline
    'distance': 45.2,                 # Distance from camera
    'message': 'Bad posture for 25s'  # Notification message
}
```

**Warning Logic**:
- First warning at 5 seconds of continuous bad posture
- Then warn every 20 seconds (25s, 45s, 65s, etc.)
- Resets when posture becomes good
- Prevents duplicate warnings using `warning_sent_at` set

#### Method: `get_statistics()`

**Returns current session statistics**:
```python
{
    'total_bad_duration': 180,      # Total seconds of bad posture
    'longest_bad_streak': 45,       # Longest continuous bad period
    'longest_good_streak': 1200,    # Longest continuous good period
    'current_bad_duration': 15      # Current streak (if bad)
}
```

---

### WebSocket Server: `websocket_server.py`

**Purpose**: Provides bidirectional real-time communication with the .NET client.

```python
class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()              # Track all connected clients
        self.on_client_change = None      # Callback for connection events
        self.detector = None              # Injected PostureDetector
        self.analyzer = None              # Injected PostureAnalyzer
```

#### Key Methods:

**1. `register(websocket)` - async**
- Adds new client to the set
- Logs connection count
- Triggers client change callback

**2. `unregister(websocket)` - async**
- Removes client from the set
- Logs remaining client count
- Triggers client change callback

**3. `handler(websocket)` - async**
- Manages individual client connection lifecycle
- Registers client on connect
- Listens for incoming messages (frames, commands)
- Processes each message via `process_message()`
- Handles disconnection gracefully

**4. `process_message(websocket, message)` - async**

**Handles different message types**:

- **`type: 'frame'`**: 
  - Receives base64-encoded PNG frame
  - Decodes to numpy array
  - Runs face detection and posture analysis
  - Sends back posture status
  
- **`type: 'save_good_posture'`**:
  - Receives current posture as baseline
  - Saves to detector for calibration
  - Confirms success to client
  
- **`type: 'get_statistics'`**:
  - Retrieves statistics from analyzer
  - Sends back to client
  
- **`type: 'reset_statistics'`**:
  - Resets analyzer statistics
  - Confirms reset to client
  
- **`type: 'set_thresholds'`**:
  - Updates pitch/distance thresholds
  - Confirms update to client

**5. `handle_frame(websocket, data)` - async**

**Frame processing pipeline**:
```python
1. Extract base64 PNG from message
2. Decode base64 → bytes
3. Convert bytes → numpy array → OpenCV BGR image
4. Generate timestamp (ms since start)
5. Detect facial landmarks
6. If face detected:
   - Check posture (pitch, distance, thresholds)
   - Update analyzer (duration tracking)
   - Send result to client
7. If no face: Send no_face status
```

**6. `start()` - async**
- Creates WebSocket server on specified host:port
- Runs forever waiting for connections
- Uses `websockets.serve()` context manager

**Protocol**: WebSocket (RFC 6455)
**Data format**: JSON strings

**Example messages**:

**Client → Server (frame)**:
```json
{
  "type": "frame",
  "data": "iVBORw0KGgoAAAANSUhEUg...",  // base64 PNG
  "width": 640,
  "height": 480
}
```

**Server → Client (posture result)**:
```json
{
  "type": "posture_result",
  "is_bad": true,
  "pitch_angle": -12.5,
  "adjusted_pitch": -10.3,
  "distance": 45.2,
  "bad_duration": 8,
  "should_warn": true,
  "message": "Head tilted down too much"
}
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
    <TextBlock Text="Slouti - Posture Tracker" /> <!-- Title -->
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
private WebSocketClient? _wsClient;              // Connection to Python service
private NotificationService? _notificationService;  // Manages toast alerts
private MediaCapture? _mediaCapture;             // Windows camera API
private MediaFrameReader? _frameReader;          // Reads frames from camera
private bool _isMonitoring = false;              // Monitoring state
private bool _isProcessingFrame = false;         // Prevent concurrent processing
private Timer? _frameTimer;                      // Controls frame send rate
private SoftwareBitmap? _latestBitmap;          // Latest camera frame
private SoftwareBitmapSource? _bitmapSource;    // For UI preview
```

**Constructor**:
```csharp
public MainPage()
{
    InitializeComponent();  // Loads XAML UI
    _wsClient = new WebSocketClient();
    _notificationService = new NotificationService();
    _bitmapSource = new SoftwareBitmapSource();
    
    // Subscribe to WebSocket events
    _wsClient.PostureDataReceived += OnPostureDataReceived;
    _wsClient.PostureSaved += OnPostureSaved;
    _wsClient.ThresholdsUpdated += OnThresholdsUpdated;
    
    // Bind XAML controls using FindName
    _statusText = FindName("StatusText") as TextBlock;
    _cameraPreview = FindName("CameraPreview") as Image;
    // ... other controls
}
```

**Event Handler: `StartButton_Click`**

**Startup sequence**:
1. Initialize `MediaCapture` with default camera
2. Find best video format (640x480 or 1280x720)
3. Create `MediaFrameReader` for that format
4. Set up frame arrival callback
5. Start frame reader
6. Connect to Python WebSocket server
7. Start timer to send frames periodically (~15 FPS)
8. Update UI to show monitoring state

**Camera initialization**:
```csharp
_mediaCapture = new MediaCapture();
await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
{
    VideoDeviceId = devices[0].Id,
    StreamingCaptureMode = StreamingCaptureMode.Video
});
```

**Frame processing**:
```csharp
private async void FrameReader_FrameArrived(sender, args)
{
    using var frame = _frameReader.TryAcquireLatestFrame();
    if (frame?.VideoMediaFrame?.SoftwareBitmap != null)
    {
        // Convert to BGRA8 format
        _latestBitmap = SoftwareBitmap.Convert(
            frame.VideoMediaFrame.SoftwareBitmap,
            BitmapPixelFormat.Bgra8
        );
        
        // Update UI preview asynchronously
        await UpdatePreviewAsync(_latestBitmap);
    }
}
```

**Event Handler: `OnPostureDataReceived`**

Called when Python service sends posture analysis.

**UI Thread Marshalling**:
```csharp
DispatcherQueue.TryEnqueue(() => { ... });
```

**Updates UI with**:
- Posture status (Good ✓ / Bad ✗)
- Pitch angle display
- Distance from screen
- Bad posture duration
- Progress bar (fills as bad posture continues)
- Warning messages

**Notification Logic**:
- Python service determines when to warn
- .NET app receives `should_warn` flag
- Shows toast notification if flagged
- Notification service has its own cooldown

**Event Handler: `SendFrame`**

**Frame sending pipeline**:
```csharp
1. Check if latest bitmap is available
2. Encode bitmap to PNG in memory
3. Convert PNG bytes to base64 string
4. Create JSON message with frame data
5. Send to Python service via WebSocket
6. Wait for analysis result
```

**Encoding process**:
```csharp
using var stream = new InMemoryRandomAccessStream();
var encoder = await BitmapEncoder.CreateAsync(
    BitmapEncoder.PngEncoderId, stream);
encoder.SetSoftwareBitmap(_latestBitmap);
await encoder.FlushAsync();

var bytes = new byte[stream.Size];
await stream.ReadAsync(bytes.AsBuffer(), ...);
var base64 = Convert.ToBase64String(bytes);
```

---

### WebSocket Client: `WebSocketClient.cs`

**Purpose**: Bidirectional WebSocket communication with Python service.

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

**2. Send Methods**

**`SendFrameAsync(base64Frame, width, height)`**
```csharp
public async Task SendFrameAsync(string base64Frame, int width, int height)
{
    var message = new
    {
        type = "frame",
        data = base64Frame,
        width = width,
        height = height
    };
    
    var json = JsonSerializer.Serialize(message);
    var bytes = Encoding.UTF8.GetBytes(json);
    await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ...);
}
```

**`SaveGoodPostureAsync(pitch, distance)`**
```csharp
public async Task SaveGoodPostureAsync(double pitch, double distance)
{
    var message = new
    {
        type = "save_good_posture",
        pitch_angle = pitch,
        distance = distance
    };
    await SendMessageAsync(message);
}
```

**`SetThresholdsAsync(pitchThreshold, distanceThreshold)`**
- Sends threshold configuration to Python service
- Allows runtime adjustment without restart

**3. Receive Loop**
```csharp
private async Task ReceiveLoop()
{
    var buffer = new byte[4096];  // Larger buffer for detailed messages
    
    while (_ws.State == WebSocketState.Open)
    {
        var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        
        // Parse message and route to appropriate handler
        var message = JsonSerializer.Deserialize<JsonElement>(json);
        var type = message.GetProperty("type").GetString();
        
        switch (type)
        {
            case "posture_result":
                var data = JsonSerializer.Deserialize<PostureData>(json);
                PostureDataReceived?.Invoke(this, data);
                break;
                
            case "posture_saved":
                PostureSaved?.Invoke(this, EventArgs.Empty);
                break;
                
            case "thresholds_updated":
                ThresholdsUpdated?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
```

**Flow**:
1. Wait for WebSocket message (async, non-blocking)
2. Convert bytes → UTF-8 string → JSON
3. Determine message type
4. Deserialize and fire appropriate event
5. Subscribers (MainPage) handle the data
6. Loop continues until connection closes

**4. Event Pattern**
```csharp
public event EventHandler<PostureData>? PostureDataReceived;
public event EventHandler? PostureSaved;
public event EventHandler? ThresholdsUpdated;
```

Multiple events allow MainPage to react to different server responses. It's a **push model** with typed events.

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

Slouti is a well-architected application that demonstrates:
- **Separation of concerns**: AI/CV logic in Python, UI in .NET
- **Asynchronous programming**: Non-blocking I/O throughout
- **Real-time communication**: WebSocket for low-latency data transfer
- **Event-driven architecture**: UI reacts to data events
- **Modern Windows development**: WinUI3 with native notifications

The modular design makes it easy to extend, debug, and maintain.
