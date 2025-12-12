# Posture Analysis Implementation Guide

> **Note**: This guide describes the original implementation plan. The actual implementation differs in that the Python service now captures frames directly from the webcam using OpenCV, rather than receiving frames from the .NET UI. See the actual code in [python-service/src/websocket_server.py](python-service/src/websocket_server.py) for the current implementation.

## Overview
This guide explains how to implement a new posture analysis logic into Slouti application. The extension uses MediaPipe Face Landmarker and OpenCV to detect slouching in real-time.

## Core Technologies Used

### 1. **MediaPipe Face Landmarker**
- Used for detecting 478 facial landmarks in real-time
- Runs in VIDEO mode for continuous frame processing
- Configuration:
  - `numFaces: 1` (single face detection)
  - `minFaceDetectionConfidence: 0.5`
  - `minTrackingConfidence: 0.5`

### 2. **OpenCV.js**
- Used for 3D head pose estimation using PnP (Perspective-n-Point) algorithm
- Performs rotation matrix calculations and Euler angle conversions
- Calculates pitch angle from rotation matrix

## Posture Detection Algorithm

### Step 1: Facial Landmark Detection
The system extracts specific facial landmarks using MediaPipe:
- **Index 33**: Left eye outer corner
- **Index 263**: Right eye outer corner  
- **Index 1**: Nose tip
- **Index 61**: Left mouth corner
- **Index 291**: Right mouth corner
- **Index 199**: Chin
- **Index 473**: Left eye pupil (for distance calculation)
- **Index 468**: Right eye pupil (for distance calculation)

### Step 2: Head Pitch Angle Calculation

The head pitch angle is calculated using the **PnP (Perspective-n-Point) algorithm**:

#### 2.1 Define 3D Face Model (in mm)
```javascript
const face3dCoordMatrix = [
    [-165.0, 170.0, -135.0],   // Left eye outer corner
    [165.0, 170.0, -135.0],    // Right eye outer corner
    [0.0, 0.0, 0.0],           // Nose tip (origin)
    [-150.0, -150.0, -125.0],  // Left mouth corner
    [150.0, -150.0, -125.0],   // Right mouth corner
    [0.0, -330.0, -65.0]       // Chin
];
```

#### 2.2 Extract 2D Face Coordinates
Extract the corresponding 2D coordinates from the detected facial landmarks in the camera frame.

#### 2.3 Create Camera Matrix
```javascript
const focalLength = frameWidth;
const cameraMatrix = [
    [focalLength, 0, frameHeight / 2],
    [0, focalLength, frameWidth / 2],
    [0, 0, 1]
];
```

#### 2.4 Solve PnP
Use OpenCV's `solvePnP()` to find rotation and translation vectors that map the 3D model to the 2D image coordinates.

#### 2.5 Convert to Pitch Angle
```javascript
// Convert rotation vector to rotation matrix using Rodrigues
cv.Rodrigues(rotVec, rotationMatrix);

// Extract pitch from rotation matrix
let pitchAngle = rotationMatrixToEulerAngles(rotationMatrix);

// Normalize angle to proper range
if (pitchAngle > 0) {
    pitchAngle = 180 - pitchAngle;
} else {
    pitchAngle = -180 - pitchAngle;
}
```

#### Euler Angle Extraction Function
```javascript
function rotationMatrixToEulerAngles(R) {
    let sy = Math.sqrt(R[0][0] * R[0][0] + R[1][0] * R[1][0]);
    let singular = sy < 1e-6; // Check for singularity

    let pitchAngle;
    if (!singular) {
        pitchAngle = Math.atan2(R[2][1], R[2][2]);
    } else {
        pitchAngle = Math.atan2(-R[1][2], R[1][1]);
    }
    
    // Convert radians to degrees
    pitchAngle = pitchAngle * (180 / Math.PI);
    return pitchAngle;
}
```

### Step 3: Head-to-Webcam Distance Calculation

Uses the interpupillary distance (IPD) method:

```javascript
function estimateHeadWebcamDistance(faceLandmarks, frameHeight, frameWidth) {
    const leftEyePupil = 473;   // MediaPipe landmark index
    const rightEyePupil = 468;  // MediaPipe landmark index
    const averagePupillaryDistance = 6.3;  // cm (average human IPD)
    
    const leftEye = faceLandmarks[leftEyePupil];
    const rightEye = faceLandmarks[rightEyePupil];
    
    // Convert normalized coordinates to pixel coordinates
    const leftEyeX = leftEye.x * frameWidth;
    const leftEyeY = leftEye.y * frameHeight;
    const rightEyeX = rightEye.x * frameWidth;
    const rightEyeY = rightEye.y * frameHeight;
    
    // Calculate distance between pupils in pixels
    const imageEyeDistance = Math.sqrt(
        Math.pow(rightEyeX - leftEyeX, 2) + 
        Math.pow(rightEyeY - leftEyeY, 2)
    );
    
    const focalLength = frameWidth;
    
    // Calculate distance using similar triangles
    return (focalLength / imageEyeDistance) * averagePupillaryDistance;
}
```

### Step 4: Good Posture Calibration

The system requires users to save their "good posture" baseline:

1. User maintains best posture and clicks "Save Good Posture"
2. System captures:
   - `goodHeadPitchAngle`: Current pitch angle
   - `goodHeadWebcamDistance`: Current distance from camera

3. All subsequent measurements are compared against this baseline:
```javascript
adjustedHeadPitchAngle = currentHeadPitchAngle - goodHeadPitchAngle;
```

### Step 5: Bad Posture Detection

The system determines bad posture using two thresholds:

```javascript
function sittingPostureIsBad(headPitchAngle, headDistance, goodHeadDistance) {
    // Default thresholds (configurable by user)
    const headPitchAngleThreshold = -10;        // degrees
    const headWebcamDistanceThreshold = 10;     // cm
    
    // Bad posture if:
    // 1. Head is tilted down too much (negative pitch)
    // 2. User is too close to the camera
    if (headPitchAngle < headPitchAngleThreshold || 
        (goodHeadDistance - headDistance) > headWebcamDistanceThreshold) {
        return true;
    }
    return false;
}
```

**Key Insight**: 
- **Negative pitch angle** = looking down (slouching)
- **Distance reduction** = leaning forward (slouching)

### Step 6: Bad Posture Duration Tracking

The system continuously monitors bad posture duration:

```javascript
if (sittingPostureIsBad(...)) {
    if (!countingBadPostureDuration) {
        countingBadPostureDuration = true;
        startTime = performance.now();
    } else {
        badPostureDuration = Math.round((performance.now() - startTime) / 1000);
        warnUser(badPostureDuration);
    }
} else if (countingBadPostureDuration) {
    // Good posture resumed
    countingBadPostureDuration = false;
    badPostureDuration = 0;
    warnedUser = false;
}
```

### Step 7: User Warnings

Warnings are triggered based on bad posture duration:

```javascript
function warnUser(badPostureDuration) {
    // Warn at 5 seconds, then every 20 seconds after
    if (5 <= badPostureDuration && ((badPostureDuration - 5) % 20) === 0) {
        sendNotification("Please check your posture!");
    }
}
```

## Processing Pipeline

### Frame Processing Flow

```
1. Capture video frame from webcam
   ↓
2. Send frame to processing thread/sandbox
   ↓
3. MediaPipe detects facial landmarks
   ↓
4. Extract key landmark coordinates (2D)
   ↓
5. Calculate head pitch angle using OpenCV PnP
   ↓
6. Calculate head-to-camera distance using IPD
   ↓
7. Compare with saved good posture baseline
   ↓
8. Determine if posture is bad
   ↓
9. Track duration of bad posture
   ↓
10. Send warning notification if threshold exceeded
```

### Processing Speeds

The extension offers three processing speeds:
- **Fast**: 1000ms (1 second between frames)
- **Medium**: 2500ms (2.5 seconds)
- **Slow**: 5000ms (5 seconds)

## Implementation Steps for Slouti

### 1. Install Required Dependencies

For your Windows app (WinUI/C#), you'll need:

```xml
<!-- Slouti.csproj -->
<ItemGroup>
    <!-- For MediaPipe -->
    <PackageReference Include="MediaPipe.Net" Version="0.x.x" />
    <!-- OR use native MediaPipe via P/Invoke -->
    
    <!-- For OpenCV -->
    <PackageReference Include="OpenCvSharp4" Version="4.x.x" />
    <PackageReference Include="OpenCvSharp4.runtime.win" Version="4.x.x" />
</ItemGroup>
```

For your Python service (if keeping it):
```bash
pip install mediapipe opencv-python numpy
```

### 2. Update Python Service (Recommended Approach)

Since you already have a Python service, implement the logic there:

**File: `python-service/src/pose_detector.py`**

```python
import mediapipe as mp
import cv2
import numpy as np

class PostureDetector:
    def __init__(self):
        self.mp_face_mesh = mp.solutions.face_mesh
        self.face_mesh = self.mp_face_mesh.FaceLandmarker.create_from_options(
            mp.tasks.vision.FaceLandmarkerOptions(
                base_options=mp.tasks.BaseOptions(
                    model_asset_path='face_landmarker.task'
                ),
                running_mode=mp.tasks.vision.RunningMode.VIDEO,
                num_faces=1,
                min_face_detection_confidence=0.5,
                min_tracking_confidence=0.5
            )
        )
        
        # Good posture baseline
        self.good_head_pitch_angle = None
        self.good_head_distance = None
        
        # Thresholds
        self.pitch_threshold = -10  # degrees
        self.distance_threshold = 10  # cm
        
        # 3D face model (in mm)
        self.face_3d_model = np.array([
            [-165.0, 170.0, -135.0],   # Left eye outer
            [165.0, 170.0, -135.0],    # Right eye outer
            [0.0, 0.0, 0.0],           # Nose tip
            [-150.0, -150.0, -125.0],  # Left mouth
            [150.0, -150.0, -125.0],   # Right mouth
            [0.0, -330.0, -65.0]       # Chin
        ], dtype=np.float64)
        
    def save_good_posture(self, frame):
        """Capture current posture as baseline"""
        results = self.detect_landmarks(frame)
        if results:
            self.good_head_pitch_angle = self.calculate_pitch_angle(results, frame.shape)
            self.good_head_distance = self.calculate_distance(results, frame.shape)
            return True
        return False
    
    def detect_landmarks(self, frame):
        """Detect facial landmarks using MediaPipe"""
        # Convert frame to RGB
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        
        # Process frame
        results = self.face_mesh.detect_for_video(
            mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame),
            timestamp_ms=int(cv2.getTickCount() / cv2.getTickFrequency() * 1000)
        )
        
        if results.face_landmarks:
            return results.face_landmarks[0]
        return None
    
    def get_2d_landmarks(self, landmarks, frame_shape, indices):
        """Extract 2D coordinates for specific landmark indices"""
        height, width = frame_shape[:2]
        coords_2d = []
        
        for idx in indices:
            landmark = landmarks[idx]
            x = landmark.x * width
            y = landmark.y * height
            coords_2d.append([x, y])
        
        return np.array(coords_2d, dtype=np.float64)
    
    def calculate_pitch_angle(self, landmarks, frame_shape):
        """Calculate head pitch angle using PnP algorithm"""
        height, width = frame_shape[:2]
        
        # Key landmark indices
        landmark_indices = [33, 263, 1, 61, 291, 199]
        
        # Get 2D coordinates
        face_2d = self.get_2d_landmarks(landmarks, frame_shape, landmark_indices)
        
        # Camera matrix
        focal_length = width
        camera_matrix = np.array([
            [focal_length, 0, height / 2],
            [0, focal_length, width / 2],
            [0, 0, 1]
        ], dtype=np.float64)
        
        # Distortion coefficients (assuming no distortion)
        dist_coeffs = np.zeros((4, 1))
        
        # Solve PnP
        success, rot_vec, trans_vec = cv2.solvePnP(
            self.face_3d_model,
            face_2d,
            camera_matrix,
            dist_coeffs
        )
        
        if not success:
            return None
        
        # Convert rotation vector to rotation matrix
        rot_matrix, _ = cv2.Rodrigues(rot_vec)
        
        # Extract pitch angle
        pitch_angle = self.rotation_matrix_to_pitch(rot_matrix)
        
        # Normalize angle
        if pitch_angle > 0:
            pitch_angle = 180 - pitch_angle
        else:
            pitch_angle = -180 - pitch_angle
        
        return pitch_angle
    
    def rotation_matrix_to_pitch(self, R):
        """Convert rotation matrix to pitch angle in degrees"""
        sy = np.sqrt(R[0, 0] ** 2 + R[1, 0] ** 2)
        singular = sy < 1e-6
        
        if not singular:
            pitch = np.arctan2(R[2, 1], R[2, 2])
        else:
            pitch = np.arctan2(-R[1, 2], R[1, 1])
        
        # Convert to degrees
        return np.degrees(pitch)
    
    def calculate_distance(self, landmarks, frame_shape):
        """Calculate head-to-camera distance using IPD method"""
        height, width = frame_shape[:2]
        
        # Pupil landmarks
        left_pupil = landmarks[473]
        right_pupil = landmarks[468]
        
        # Convert to pixel coordinates
        left_x = left_pupil.x * width
        left_y = left_pupil.y * height
        right_x = right_pupil.x * width
        right_y = right_pupil.y * height
        
        # Calculate pixel distance between pupils
        pixel_distance = np.sqrt(
            (right_x - left_x) ** 2 + 
            (right_y - left_y) ** 2
        )
        
        # Average human IPD in cm
        avg_ipd = 6.3
        
        # Calculate distance using similar triangles
        focal_length = width
        distance = (focal_length / pixel_distance) * avg_ipd
        
        return distance
    
    def check_posture(self, frame):
        """
        Analyze frame and return posture status
        
        Returns:
            dict: {
                'is_bad': bool,
                'pitch_angle': float,
                'distance': float,
                'adjusted_pitch': float
            }
        """
        landmarks = self.detect_landmarks(frame)
        
        if not landmarks:
            return {
                'is_bad': False,
                'pitch_angle': None,
                'distance': None,
                'adjusted_pitch': None,
                'error': 'No face detected'
            }
        
        # Calculate metrics
        pitch_angle = self.calculate_pitch_angle(landmarks, frame.shape)
        distance = self.calculate_distance(landmarks, frame.shape)
        
        # If no baseline saved, can't determine bad posture
        if self.good_head_pitch_angle is None:
            return {
                'is_bad': False,
                'pitch_angle': pitch_angle,
                'distance': distance,
                'adjusted_pitch': None,
                'error': 'No baseline posture saved'
            }
        
        # Calculate adjusted pitch
        adjusted_pitch = pitch_angle - self.good_head_pitch_angle
        
        # Determine if posture is bad
        is_bad = self.is_posture_bad(
            adjusted_pitch,
            distance,
            self.good_head_distance
        )
        
        return {
            'is_bad': is_bad,
            'pitch_angle': round(pitch_angle, 2),
            'distance': round(distance, 2),
            'adjusted_pitch': round(adjusted_pitch, 2)
        }
    
    def is_posture_bad(self, pitch_angle, current_distance, good_distance):
        """Determine if current posture is bad"""
        # Bad if looking down too much
        if pitch_angle < self.pitch_threshold:
            return True
        
        # Bad if too close to camera
        if (good_distance - current_distance) > self.distance_threshold:
            return True
        
        return False
```

**File: `python-service/src/posture_analyzer.py`**

```python
import time
from collections import deque

class PostureAnalyzer:
    def __init__(self):
        self.bad_posture_start = None
        self.bad_posture_duration = 0
        self.warning_sent = False
        
        # Statistics tracking
        self.total_bad_duration = 0
        self.consecutive_bad_duration = 0
        self.longest_good_duration = 0
        self.good_posture_start = time.time()
        
        # Warning thresholds
        self.initial_warning_seconds = 5
        self.repeat_warning_interval = 20
        
    def update(self, posture_status):
        """
        Update analyzer with new posture status
        
        Args:
            posture_status: dict from PostureDetector.check_posture()
            
        Returns:
            dict: {
                'should_warn': bool,
                'bad_duration': int (seconds),
                'message': str
            }
        """
        current_time = time.time()
        is_bad = posture_status.get('is_bad', False)
        
        if is_bad:
            # Bad posture detected
            if self.bad_posture_start is None:
                # Bad posture just started
                self.bad_posture_start = current_time
                
                # Update good posture duration
                good_duration = current_time - self.good_posture_start
                if good_duration > self.longest_good_duration:
                    self.longest_good_duration = good_duration
                
                self.warning_sent = False
            
            # Calculate duration
            self.bad_posture_duration = int(current_time - self.bad_posture_start)
            
            # Check if should warn
            should_warn = self.should_send_warning(self.bad_posture_duration)
            
            return {
                'should_warn': should_warn,
                'bad_duration': self.bad_posture_duration,
                'pitch': posture_status.get('adjusted_pitch'),
                'distance': posture_status.get('distance'),
                'message': f"Bad posture for {self.bad_posture_duration} seconds"
            }
        else:
            # Good posture
            if self.bad_posture_start is not None:
                # Good posture just resumed
                self.total_bad_duration += self.bad_posture_duration
                self.consecutive_bad_duration = 0
                self.bad_posture_start = None
                self.bad_posture_duration = 0
                self.warning_sent = False
                self.good_posture_start = current_time
            
            return {
                'should_warn': False,
                'bad_duration': 0,
                'pitch': posture_status.get('adjusted_pitch'),
                'distance': posture_status.get('distance'),
                'message': "Good posture"
            }
    
    def should_send_warning(self, duration):
        """Determine if warning should be sent"""
        # First warning at 5 seconds
        if duration >= self.initial_warning_seconds:
            # Repeat every 20 seconds after initial warning
            if duration == self.initial_warning_seconds:
                return True
            elif (duration - self.initial_warning_seconds) % self.repeat_warning_interval == 0:
                return True
        return False
    
    def get_statistics(self):
        """Get current session statistics"""
        return {
            'total_bad_duration': int(self.total_bad_duration),
            'current_bad_duration': self.bad_posture_duration,
            'longest_good_duration': int(self.longest_good_duration)
        }
```

### 3. Update WebSocket Server

**File: `python-service/src/websocket_server.py`**

```python
import asyncio
import websockets
import json
import cv2
import base64
import numpy as np
from pose_detector import PostureDetector
from posture_analyzer import PostureAnalyzer

class PostureWebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.detector = PostureDetector()
        self.analyzer = PostureAnalyzer()
        self.clients = set()
        
    async def handle_client(self, websocket, path):
        """Handle WebSocket client connection"""
        self.clients.add(websocket)
        print(f"Client connected. Total clients: {len(self.clients)}")
        
        try:
            async for message in websocket:
                await self.process_message(websocket, message)
        except websockets.exceptions.ConnectionClosed:
            print("Client disconnected")
        finally:
            self.clients.remove(websocket)
    
    async def process_message(self, websocket, message):
        """Process incoming message from client"""
        try:
            data = json.loads(message)
            msg_type = data.get('type')
            
            if msg_type == 'frame':
                # Process video frame
                await self.handle_frame(websocket, data)
            
            elif msg_type == 'save_good_posture':
                # Save baseline posture
                await self.handle_save_posture(websocket, data)
            
            elif msg_type == 'get_statistics':
                # Return statistics
                stats = self.analyzer.get_statistics()
                await websocket.send(json.dumps({
                    'type': 'statistics',
                    'data': stats
                }))
            
            elif msg_type == 'set_thresholds':
                # Update thresholds
                self.detector.pitch_threshold = data.get('pitch_threshold', -10)
                self.detector.distance_threshold = data.get('distance_threshold', 10)
                await websocket.send(json.dumps({
                    'type': 'thresholds_updated',
                    'success': True
                }))
        
        except Exception as e:
            print(f"Error processing message: {e}")
            await websocket.send(json.dumps({
                'type': 'error',
                'message': str(e)
            }))
    
    async def handle_frame(self, websocket, data):
        """Process video frame and return posture analysis"""
        # Decode base64 image
        img_data = base64.b64decode(data['frame'])
        nparr = np.frombuffer(img_data, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        # Analyze posture
        posture_status = self.detector.check_posture(frame)
        
        # Update analyzer
        analysis = self.analyzer.update(posture_status)
        
        # Send response
        await websocket.send(json.dumps({
            'type': 'posture_result',
            'data': {
                'is_bad': posture_status['is_bad'],
                'pitch_angle': posture_status['pitch_angle'],
                'adjusted_pitch': posture_status['adjusted_pitch'],
                'distance': posture_status['distance'],
                'bad_duration': analysis['bad_duration'],
                'should_warn': analysis['should_warn'],
                'message': analysis['message']
            }
        }))
    
    async def handle_save_posture(self, websocket, data):
        """Save good posture baseline"""
        # Decode image
        img_data = base64.b64decode(data['frame'])
        nparr = np.frombuffer(img_data, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        # Save baseline
        success = self.detector.save_good_posture(frame)
        
        await websocket.send(json.dumps({
            'type': 'posture_saved',
            'success': success,
            'good_pitch': self.detector.good_head_pitch_angle,
            'good_distance': self.detector.good_head_distance
        }))
    
    async def start(self):
        """Start the WebSocket server"""
        print(f"Starting WebSocket server on {self.host}:{self.port}")
        async with websockets.serve(self.handle_client, self.host, self.port):
            await asyncio.Future()  # Run forever

if __name__ == '__main__':
    server = PostureWebSocketServer()
    asyncio.run(server.start())
```

### 4. Update C# WinUI App

**File: `dotnet-app/Services/WebSocketClient.cs`**

Update to send frames and receive posture analysis:

```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Slouti.Services
{
    public class PostureData
    {
        public bool IsBad { get; set; }
        public double? PitchAngle { get; set; }
        public double? AdjustedPitch { get; set; }
        public double? Distance { get; set; }
        public int BadDuration { get; set; }
        public bool ShouldWarn { get; set; }
        public string Message { get; set; }
    }

    public class WebSocketClient
    {
        private ClientWebSocket _webSocket;
        private readonly string _serverUri;
        private CancellationTokenSource _cancellationTokenSource;
        
        public event EventHandler<PostureData> PostureDataReceived;
        public event EventHandler<bool> PostureSaved;
        
        public WebSocketClient(string serverUri = "ws://localhost:8765")
        {
            _serverUri = serverUri;
        }
        
        public async Task ConnectAsync()
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            await _webSocket.ConnectAsync(new Uri(_serverUri), _cancellationTokenSource.Token);
            
            // Start listening for messages
            _ = Task.Run(ListenForMessages);
        }
        
        public async Task SendFrameAsync(SoftwareBitmap bitmap)
        {
            if (_webSocket?.State != WebSocketState.Open)
                return;
            
            // Convert bitmap to base64
            var base64 = await ConvertBitmapToBase64(bitmap);
            
            var message = JsonSerializer.Serialize(new
            {
                type = "frame",
                frame = base64
            });
            
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        
        public async Task SaveGoodPostureAsync(SoftwareBitmap bitmap)
        {
            if (_webSocket?.State != WebSocketState.Open)
                return;
            
            var base64 = await ConvertBitmapToBase64(bitmap);
            
            var message = JsonSerializer.Serialize(new
            {
                type = "save_good_posture",
                frame = base64
            });
            
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        
        public async Task SetThresholdsAsync(double pitchThreshold, double distanceThreshold)
        {
            if (_webSocket?.State != WebSocketState.Open)
                return;
            
            var message = JsonSerializer.Serialize(new
            {
                type = "set_thresholds",
                pitch_threshold = pitchThreshold,
                distance_threshold = distanceThreshold
            });
            
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        
        private async Task ListenForMessages()
        {
            var buffer = new byte[8192];
            
            while (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token
                    );
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving message: {ex.Message}");
                    break;
                }
            }
        }
        
        private void HandleMessage(string message)
        {
            try
            {
                var doc = JsonDocument.Parse(message);
                var type = doc.RootElement.GetProperty("type").GetString();
                
                switch (type)
                {
                    case "posture_result":
                        var data = doc.RootElement.GetProperty("data");
                        var postureData = new PostureData
                        {
                            IsBad = data.GetProperty("is_bad").GetBoolean(),
                            PitchAngle = GetNullableDouble(data, "pitch_angle"),
                            AdjustedPitch = GetNullableDouble(data, "adjusted_pitch"),
                            Distance = GetNullableDouble(data, "distance"),
                            BadDuration = data.GetProperty("bad_duration").GetInt32(),
                            ShouldWarn = data.GetProperty("should_warn").GetBoolean(),
                            Message = data.GetProperty("message").GetString()
                        };
                        
                        PostureDataReceived?.Invoke(this, postureData);
                        break;
                    
                    case "posture_saved":
                        var success = doc.RootElement.GetProperty("success").GetBoolean();
                        PostureSaved?.Invoke(this, success);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }
        
        private double? GetNullableDouble(JsonElement element, string propertyName)
        {
            var prop = element.GetProperty(propertyName);
            return prop.ValueKind == JsonValueKind.Null ? null : prop.GetDouble();
        }
        
        private async Task<string> ConvertBitmapToBase64(SoftwareBitmap bitmap)
        {
            using var stream = new InMemoryRandomAccessStream();
            
            var encoder = await BitmapEncoder.CreateAsync(
                BitmapEncoder.JpegEncoderId,
                stream
            );
            
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            
            var bytes = new byte[stream.Size];
            var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            
            return Convert.ToBase64String(bytes);
        }
        
        public async Task DisconnectAsync()
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None
                );
            }
            
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
        }
    }
}
```

### 5. Update MainPage to Use Posture Analysis

**File: `dotnet-app/Views/MainPage.xaml.cs`**

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using System;
using System.Threading.Tasks;
using Slouti.Services;

namespace Slouti.Views
{
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture;
        private MediaFrameReader _frameReader;
        private WebSocketClient _wsClient;
        private NotificationService _notificationService;
        private bool _isProcessing = false;
        
        public MainPage()
        {
            this.InitializeComponent();
            _wsClient = new WebSocketClient();
            _notificationService = new NotificationService();
            
            // Subscribe to events
            _wsClient.PostureDataReceived += OnPostureDataReceived;
            _wsClient.PostureSaved += OnPostureSaved;
        }
        
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPostureMonitoring();
        }
        
        private async Task StartPostureMonitoring()
        {
            try
            {
                // Connect to WebSocket server
                await _wsClient.ConnectAsync();
                
                // Initialize camera
                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync();
                
                // Set up frame reader
                var frameSource = _mediaCapture.FrameSources.Values.FirstOrDefault();
                if (frameSource != null)
                {
                    _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource);
                    _frameReader.FrameArrived += OnFrameArrived;
                    await _frameReader.StartAsync();
                }
                
                StatusText.Text = "Monitoring started";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }
        
        private async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            // Throttle processing (e.g., every 1 second)
            if (_isProcessing)
                return;
            
            _isProcessing = true;
            
            try
            {
                using var frame = sender.TryAcquireLatestFrame();
                if (frame?.VideoMediaFrame?.SoftwareBitmap != null)
                {
                    var bitmap = frame.VideoMediaFrame.SoftwareBitmap;
                    await _wsClient.SendFrameAsync(bitmap);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frame processing error: {ex.Message}");
            }
            finally
            {
                // Wait 1 second before processing next frame
                await Task.Delay(1000);
                _isProcessing = false;
            }
        }
        
        private void OnPostureDataReceived(object sender, PostureData data)
        {
            // Update UI on UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                PitchText.Text = $"Pitch: {data.AdjustedPitch:F1}°";
                DistanceText.Text = $"Distance: {data.Distance:F1} cm";
                StatusText.Text = data.Message;
                
                if (data.ShouldWarn)
                {
                    _notificationService.ShowNotification(
                        "Posture Warning",
                        $"Bad posture detected for {data.BadDuration} seconds!"
                    );
                }
            });
        }
        
        private void OnPostureSaved(object sender, bool success)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (success)
                {
                    StatusText.Text = "Good posture saved!";
                }
                else
                {
                    StatusText.Text = "Failed to save posture. Try again.";
                }
            });
        }
        
        private async void SavePostureButton_Click(object sender, RoutedEventArgs e)
        {
            using var frame = _frameReader?.TryAcquireLatestFrame();
            if (frame?.VideoMediaFrame?.SoftwareBitmap != null)
            {
                var bitmap = frame.VideoMediaFrame.SoftwareBitmap;
                await _wsClient.SaveGoodPostureAsync(bitmap);
            }
        }
        
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await _frameReader?.StopAsync();
            _frameReader?.Dispose();
            _mediaCapture?.Dispose();
            await _wsClient.DisconnectAsync();
            
            StatusText.Text = "Monitoring stopped";
        }
    }
}
```

## Key Implementation Notes

### 1. **Architecture Choice**
You have two options:
- **Python Service (Recommended)**: Easier to implement since MediaPipe and OpenCV have excellent Python support
- **Pure C#/.NET**: More integrated but requires P/Invoke or limited .NET bindings

### 2. **Performance Considerations**
- Process frames every 1-2.5 seconds (not every frame)
- Run processing in background thread/service
- Use hardware acceleration if available (GPU)

### 3. **Calibration is Critical**
- Users MUST save their good posture baseline
- Without baseline, the system cannot accurately detect slouching
- Provide clear UI guidance for calibration

### 4. **Thresholds**
Default values work well for most users:
- **Pitch threshold**: -10 degrees (looking down)
- **Distance threshold**: 10 cm (moving forward)

Allow users to adjust these based on their preferences.

### 5. **Warning Strategy**
- First warning at 5 seconds of bad posture
- Repeat warnings every 20 seconds
- Don't spam notifications excessively

## Testing Checklist

- [ ] MediaPipe face detection works reliably
- [ ] OpenCV PnP calculation is accurate
- [ ] Good posture calibration saves correctly
- [ ] Bad posture detection triggers appropriately
- [ ] Warnings appear at correct intervals
- [ ] Distance calculation is reasonably accurate
- [ ] Pitch angle calculations make sense
- [ ] System performs well (no lag)
- [ ] Works with different lighting conditions
- [ ] Handles missing face detection gracefully

## Dependencies Summary

### Python
```
mediapipe>=0.10.0
opencv-python>=4.8.0
numpy>=1.24.0
websockets>=12.0
```

### C# (.NET)
```xml
<PackageReference Include="OpenCvSharp4" Version="4.8.1" />
<PackageReference Include="OpenCvSharp4.runtime.win" Version="4.8.1" />
```

## References

- MediaPipe Face Landmarker: https://developers.google.com/mediapipe/solutions/vision/face_landmarker
- OpenCV PnP: https://docs.opencv.org/4.x/d9/d0c/group__calib3d.html#ga549c2075fac14829ff4a58bc931c033d

