# Head and Body Tilt Detection - Implementation Plan

## Executive Summary

This document provides a comprehensive plan to add **lateral tilt detection** (left/right tilting of the head and body) to the Slouti posture monitoring application. The system will detect when users tilt their head or shoulders sideways while facing the camera and warn them similar to existing pitch angle and distance warnings.

## Current System Analysis

### Existing Detection Capabilities

The application currently monitors:
1. **Head Pitch Angle** - Forward/backward head tilt (looking up/down)
2. **Distance from Camera** - How close the user is leaning toward the screen

### Current Technology Stack
- **MediaPipe Face Landmarker** - Detects 478 facial landmarks in 3D
- **OpenCV PnP (Perspective-n-Point)** - Calculates 3D head pose from 2D landmarks
- **Real-time Analysis** - Processes frames at ~10 FPS
- **Warning System** - Alerts at 5 seconds, then every 20 seconds

### Data Flow Architecture
```
Camera Frame → MediaPipe Detection → Landmark Extraction → 
PnP Algorithm → Head Pose Calculation → Posture Analysis → 
Warning System → WebSocket → .NET UI → Notifications
```

## What is Lateral Tilt?

**Lateral tilt** refers to tilting the head or body to the left or right side while facing forward (toward the camera). This is different from:
- **Pitch** - Looking up/down (already tracked)
- **Yaw** - Turning head left/right (not tracked, assumed user faces camera)
- **Roll** - The sideways tilt we want to detect ✓

### Why Detect Lateral Tilt?

Sustained lateral tilt causes:
- Neck strain and muscle imbalance
- Spine misalignment
- Shoulder asymmetry
- Long-term postural issues

## Technical Approach

### 1. Head Roll Angle Detection

**Concept**: Calculate the rotation around the Z-axis (depth axis) using the existing rotation matrix from PnP.

#### Implementation Details

**Current State**: The `pose_detector.py` already computes a 3D rotation matrix using `cv2.solvePnP()` but only extracts the pitch angle.

**New Extraction**: Add roll angle extraction from the same rotation matrix.

```python
def _rotation_matrix_to_euler_angles(self, R):
    """
    Convert rotation matrix to Euler angles (pitch, yaw, roll).
    
    Returns:
        tuple: (pitch, yaw, roll) in degrees
    """
    sy = np.sqrt(R[0, 0] ** 2 + R[1, 0] ** 2)
    singular = sy < 1e-6
    
    if not singular:
        pitch = np.arctan2(R[2, 1], R[2, 2])
        yaw = np.arctan2(-R[2, 0], sy)
        roll = np.arctan2(R[1, 0], R[0, 0])
    else:
        pitch = np.arctan2(-R[1, 2], R[1, 1])
        yaw = np.arctan2(-R[2, 0], sy)
        roll = 0
    
    # Convert radians to degrees
    return (np.degrees(pitch), np.degrees(yaw), np.degrees(roll))
```

**Key Landmarks for Head Roll**:
- **Left Eye Outer Corner** (landmark 33)
- **Right Eye Outer Corner** (landmark 263)
- The horizontal alignment of these points indicates head tilt

**Validation Method**: The roll angle from eyes should match the roll angle from the rotation matrix.

### 2. Shoulder Tilt Detection

**Concept**: Use shoulder landmarks to detect body tilt, which indicates poor posture even if the head is level.

#### Why Shoulders Matter

Users may compensate for body tilt by tilting their head the opposite direction, appearing to have a level head while their body is tilted. Shoulder detection catches this.

#### MediaPipe Limitations

**Problem**: MediaPipe Face Landmarker only detects facial landmarks, NOT body/shoulder landmarks.

**Solution**: Upgrade to **MediaPipe Pose Landmarker** or run both models simultaneously.

#### MediaPipe Pose Landmarker

MediaPipe Pose provides 33 body landmarks including:
- **Left Shoulder** (landmark 11)
- **Right Shoulder** (landmark 12)
- **Left Hip** (landmark 23)
- **Right Hip** (landmark 24)

**Shoulder Alignment Calculation**:
```python
def calculate_shoulder_tilt(self, pose_landmarks, frame_shape):
    """
    Calculate shoulder tilt angle from horizontal.
    
    Returns:
        float: Angle in degrees (positive = right shoulder higher)
    """
    height, width = frame_shape[:2]
    
    # Get shoulder landmarks
    left_shoulder = pose_landmarks[11]
    right_shoulder = pose_landmarks[12]
    
    # Convert to pixel coordinates
    left_x = left_shoulder.x * width
    left_y = left_shoulder.y * height
    right_x = right_shoulder.x * width
    right_y = right_shoulder.y * height
    
    # Calculate angle from horizontal
    delta_y = right_y - left_y
    delta_x = right_x - left_x
    
    angle = np.degrees(np.arctan2(delta_y, delta_x))
    
    return angle
```

### 3. Baseline Calibration Extension

**Current Calibration**: Saves good head pitch angle and distance.

**New Calibration**: Also save baseline head roll and shoulder tilt angles.

```python
def save_good_posture(self, frame, timestamp_ms):
    """Capture current posture as good posture baseline."""
    face_landmarks = self.detect_face_landmarks(frame, timestamp_ms)
    pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
    
    if not face_landmarks:
        return False
    
    pitch, yaw, roll = self.calculate_head_angles(face_landmarks, frame.shape)
    distance = self.calculate_distance(face_landmarks, frame.shape)
    shoulder_tilt = None
    
    if pose_landmarks:
        shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
    
    if pitch is not None and distance is not None:
        self.good_head_pitch = pitch
        self.good_head_roll = roll
        self.good_head_distance = distance
        self.good_shoulder_tilt = shoulder_tilt if shoulder_tilt is not None else 0
        return True
    
    return False
```

### 4. Posture Evaluation Logic

**New Threshold Parameters**:
- `head_roll_threshold`: Acceptable head roll deviation (default: ±15°)
- `shoulder_tilt_threshold`: Acceptable shoulder tilt deviation (default: ±10°)

**Updated Bad Posture Check**:
```python
def _is_posture_bad(self, adjusted_pitch, adjusted_roll, adjusted_shoulder_tilt, 
                    current_distance, good_distance):
    """
    Determine if current posture is bad based on all thresholds.
    
    Returns:
        tuple: (is_bad, reasons)
    """
    reasons = []
    
    # Check pitch (looking down)
    if adjusted_pitch < self.pitch_threshold:
        reasons.append('head_pitch')
    
    # Check distance (leaning forward)
    if (good_distance - current_distance) > self.distance_threshold:
        reasons.append('distance')
    
    # Check head roll (head tilted sideways)
    if abs(adjusted_roll) > self.head_roll_threshold:
        reasons.append('head_roll')
    
    # Check shoulder tilt (body tilted sideways)
    if abs(adjusted_shoulder_tilt) > self.shoulder_tilt_threshold:
        reasons.append('shoulder_tilt')
    
    is_bad = len(reasons) > 0
    return is_bad, reasons
```

### 5. Enhanced Warning Messages

**Current Message**: Generic "Bad posture for X seconds"

**New Messages**: Specific feedback about tilt type

```python
def generate_warning_message(self, reasons, durations):
    """Generate specific warning based on posture issues."""
    messages = []
    
    if 'head_pitch' in reasons:
        messages.append("head tilted down")
    if 'distance' in reasons:
        messages.append("leaning too close")
    if 'head_roll' in reasons:
        messages.append("head tilted sideways")
    if 'shoulder_tilt' in reasons:
        messages.append("shoulders uneven")
    
    if len(messages) == 1:
        return f"Bad posture: {messages[0]}"
    elif len(messages) == 2:
        return f"Bad posture: {messages[0]} and {messages[1]}"
    else:
        return f"Bad posture: {', '.join(messages[:-1])}, and {messages[-1]}"
```

## Detailed Implementation Steps

### Phase 1: Core Detection Infrastructure

#### Step 1.1: Add MediaPipe Pose Landmarker
**File**: `python-service/src/pose_detector.py`

**Changes**:
1. Import MediaPipe Pose modules
2. Initialize Pose Landmarker alongside Face Landmarker
3. Create `detect_pose_landmarks()` method
4. Download and add `pose_landmarker.task` model file

**Code Addition**:
```python
def __init__(self):
    # Existing Face Landmarker initialization...
    
    # Initialize MediaPipe Pose Landmarker
    self.PoseLandmarker = mp.tasks.vision.PoseLandmarker
    self.PoseLandmarkerOptions = mp.tasks.vision.PoseLandmarkerOptions
    
    # Get path to pose model
    pose_model_path = os.path.join(script_dir, 'pose_landmarker.task')
    
    # Configure pose landmarker
    pose_options = self.PoseLandmarkerOptions(
        base_options=self.BaseOptions(model_asset_path=pose_model_path),
        running_mode=self.VisionRunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=0.5,
        min_tracking_confidence=0.5
    )
    
    self.pose_landmarker = self.PoseLandmarker.create_from_options(pose_options)
    
    # Add new baseline values
    self.good_head_roll = None
    self.good_shoulder_tilt = None
    
    # Add new thresholds
    self.head_roll_threshold = 15  # degrees
    self.shoulder_tilt_threshold = 10  # degrees
```

#### Step 1.2: Extract Roll Angle from Rotation Matrix
**File**: `python-service/src/pose_detector.py`

**Changes**:
1. Replace `_rotation_matrix_to_pitch()` with `_rotation_matrix_to_euler_angles()`
2. Update `calculate_pitch_angle()` to return all three angles
3. Rename method to `calculate_head_angles()`

**Refactor**:
```python
def calculate_head_angles(self, landmarks, frame_shape):
    """
    Calculate head orientation angles using PnP algorithm.
    
    Returns:
        tuple: (pitch, yaw, roll) in degrees or (None, None, None)
    """
    height, width = frame_shape[:2]
    
    # Get 2D coordinates for key landmarks
    face_2d = self.get_2d_landmarks(landmarks, frame_shape, self.landmark_indices)
    
    # Create camera matrix
    focal_length = width
    camera_matrix = np.array([
        [focal_length, 0, height / 2],
        [0, focal_length, width / 2],
        [0, 0, 1]
    ], dtype=np.float64)
    
    dist_coeffs = np.zeros((4, 1))
    
    # Solve PnP
    success, rot_vec, trans_vec = cv2.solvePnP(
        self.face_3d_model,
        face_2d,
        camera_matrix,
        dist_coeffs,
        flags=cv2.SOLVEPNP_ITERATIVE
    )
    
    if not success:
        return None, None, None
    
    # Convert rotation vector to rotation matrix
    rot_matrix, _ = cv2.Rodrigues(rot_vec)
    
    # Extract all Euler angles
    pitch, yaw, roll = self._rotation_matrix_to_euler_angles(rot_matrix)
    
    # Normalize angles
    if pitch > 0:
        pitch = 180 - pitch
    else:
        pitch = -180 - pitch
    
    return pitch, yaw, roll
```

#### Step 1.3: Add Shoulder Tilt Detection
**File**: `python-service/src/pose_detector.py`

**New Method**:
```python
def detect_pose_landmarks(self, frame, timestamp_ms):
    """Detect body landmarks using MediaPipe Pose Landmarker."""
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)
    
    detection_result = self.pose_landmarker.detect_for_video(mp_image, timestamp_ms)
    
    if detection_result.pose_landmarks:
        return detection_result.pose_landmarks[0]
    return None

def calculate_shoulder_tilt(self, pose_landmarks, frame_shape):
    """
    Calculate shoulder tilt angle from horizontal.
    
    Returns:
        float: Angle in degrees (positive = right shoulder higher)
    """
    if not pose_landmarks or len(pose_landmarks) < 13:
        return None
    
    height, width = frame_shape[:2]
    
    # Get shoulder landmarks (11 = left, 12 = right)
    left_shoulder = pose_landmarks[11]
    right_shoulder = pose_landmarks[12]
    
    # Convert to pixel coordinates
    left_x = left_shoulder.x * width
    left_y = left_shoulder.y * height
    right_x = right_shoulder.x * width
    right_y = right_shoulder.y * height
    
    # Calculate angle from horizontal
    delta_y = right_y - left_y
    delta_x = right_x - left_x
    
    # Angle in degrees (0 = level, positive = right shoulder higher)
    angle = np.degrees(np.arctan2(delta_y, delta_x))
    
    return angle
```

### Phase 2: Update Data Models

#### Step 2.1: Extend PostureData Model
**File**: `dotnet-app/Models/PostureData.cs`

**Add Properties**:
```csharp
[JsonPropertyName("roll_angle")]
public double? RollAngle { get; set; }

[JsonPropertyName("adjusted_roll")]
public double? AdjustedRoll { get; set; }

[JsonPropertyName("shoulder_tilt")]
public double? ShoulderTilt { get; set; }

[JsonPropertyName("adjusted_shoulder_tilt")]
public double? AdjustedShoulderTilt { get; set; }

[JsonPropertyName("posture_issues")]
public List<string>? PostureIssues { get; set; }
```

#### Step 2.2: Update Python Posture Status Dictionary
**File**: `python-service/src/pose_detector.py`

**Update `check_posture()` Return Value**:
```python
def check_posture(self, frame, timestamp_ms):
    """
    Analyze frame and return posture status.
    
    Returns:
        dict: {
            'is_bad': bool,
            'pitch_angle': float,
            'roll_angle': float,
            'shoulder_tilt': float,
            'distance': float,
            'adjusted_pitch': float,
            'adjusted_roll': float,
            'adjusted_shoulder_tilt': float,
            'posture_issues': list,
            'error': str (optional)
        }
    """
    face_landmarks = self.detect_landmarks(frame, timestamp_ms)
    
    if not face_landmarks:
        return {
            'is_bad': False,
            'pitch_angle': None,
            'roll_angle': None,
            'shoulder_tilt': None,
            'distance': None,
            'adjusted_pitch': None,
            'adjusted_roll': None,
            'adjusted_shoulder_tilt': None,
            'posture_issues': [],
            'error': 'No face detected'
        }
    
    # Calculate face metrics
    pitch, yaw, roll = self.calculate_head_angles(face_landmarks, frame.shape)
    distance = self.calculate_distance(face_landmarks, frame.shape)
    
    # Calculate body metrics
    pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
    shoulder_tilt = None
    if pose_landmarks:
        shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
    
    # If no baseline, can't determine bad posture
    if self.good_head_pitch is None:
        return {
            'is_bad': False,
            'pitch_angle': pitch,
            'roll_angle': roll,
            'shoulder_tilt': shoulder_tilt,
            'distance': distance,
            'adjusted_pitch': None,
            'adjusted_roll': None,
            'adjusted_shoulder_tilt': None,
            'posture_issues': [],
            'error': 'No baseline posture saved'
        }
    
    # Calculate adjusted values
    adjusted_pitch = pitch - self.good_head_pitch
    adjusted_roll = roll - self.good_head_roll if self.good_head_roll is not None else 0
    adjusted_shoulder_tilt = shoulder_tilt - self.good_shoulder_tilt if shoulder_tilt is not None and self.good_shoulder_tilt is not None else 0
    
    # Determine if posture is bad
    is_bad, issues = self._is_posture_bad(
        adjusted_pitch, 
        adjusted_roll, 
        adjusted_shoulder_tilt,
        distance, 
        self.good_head_distance
    )
    
    return {
        'is_bad': is_bad,
        'pitch_angle': round(pitch, 2) if pitch else None,
        'roll_angle': round(roll, 2) if roll else None,
        'shoulder_tilt': round(shoulder_tilt, 2) if shoulder_tilt else None,
        'distance': round(distance, 2) if distance else None,
        'adjusted_pitch': round(adjusted_pitch, 2) if adjusted_pitch else None,
        'adjusted_roll': round(adjusted_roll, 2) if adjusted_roll else None,
        'adjusted_shoulder_tilt': round(adjusted_shoulder_tilt, 2) if adjusted_shoulder_tilt else None,
        'posture_issues': issues
    }
```

### Phase 3: Update Analyzer and Warning System

#### Step 3.1: Enhanced Warning Messages
**File**: `python-service/src/posture_analyzer.py`

**Add Method**:
```python
def _generate_warning_message(self, issues, duration):
    """Generate specific warning message based on posture issues."""
    if not issues:
        return "Good posture"
    
    issue_descriptions = {
        'head_pitch': 'head tilted down',
        'distance': 'leaning too close',
        'head_roll': 'head tilted sideways',
        'shoulder_tilt': 'shoulders uneven'
    }
    
    messages = [issue_descriptions[issue] for issue in issues if issue in issue_descriptions]
    
    if len(messages) == 0:
        return f"Bad posture for {duration} seconds"
    elif len(messages) == 1:
        return f"Bad posture: {messages[0]} ({duration}s)"
    elif len(messages) == 2:
        return f"Bad posture: {messages[0]} and {messages[1]} ({duration}s)"
    else:
        return f"Bad posture: {', '.join(messages[:-1])}, and {messages[-1]} ({duration}s)"
```

**Update `update()` Method**:
```python
def update(self, posture_status):
    """
    Update analyzer with new posture status.
    """
    current_time = time.time()
    is_bad = posture_status.get('is_bad', False)
    issues = posture_status.get('posture_issues', [])
    
    if is_bad:
        # Bad posture detected
        if self.bad_posture_start is None:
            self.bad_posture_start = current_time
            good_duration = current_time - self.good_posture_start
            if good_duration > self.longest_good_streak:
                self.longest_good_streak = good_duration
            self.warning_sent_at.clear()
        
        self.bad_posture_duration = int(current_time - self.bad_posture_start)
        should_warn = self._should_send_warning(self.bad_posture_duration)
        
        return {
            'should_warn': should_warn,
            'bad_duration': self.bad_posture_duration,
            'pitch': posture_status.get('adjusted_pitch'),
            'roll': posture_status.get('adjusted_roll'),
            'shoulder_tilt': posture_status.get('adjusted_shoulder_tilt'),
            'distance': posture_status.get('distance'),
            'posture_issues': issues,
            'message': self._generate_warning_message(issues, self.bad_posture_duration)
        }
    else:
        # Good posture
        if self.bad_posture_start is not None:
            self.total_bad_duration += self.bad_posture_duration
            if self.bad_posture_duration > self.longest_bad_streak:
                self.longest_bad_streak = self.bad_posture_duration
            self.bad_posture_start = None
            self.bad_posture_duration = 0
            self.warning_sent_at.clear()
            self.good_posture_start = current_time
        
        return {
            'should_warn': False,
            'bad_duration': 0,
            'pitch': posture_status.get('adjusted_pitch'),
            'roll': posture_status.get('adjusted_roll'),
            'shoulder_tilt': posture_status.get('adjusted_shoulder_tilt'),
            'distance': posture_status.get('distance'),
            'posture_issues': [],
            'message': "Good posture"
        }
```

### Phase 4: Update UI Display

#### Step 4.1: Add UI Elements
**File**: `dotnet-app/Views/MainPage.xaml`

**New TextBlocks**:
```xml
<TextBlock Text="Head Roll:" FontSize="14"/>
<TextBlock x:Name="RollText" Text="--°" FontSize="14" FontWeight="SemiBold"/>

<TextBlock Text="Shoulder Tilt:" FontSize="14"/>
<TextBlock x:Name="ShoulderTiltText" Text="--°" FontSize="14" FontWeight="SemiBold"/>
```

**New Threshold Sliders**:
```xml
<TextBlock Text="Head Roll Threshold:" FontSize="14"/>
<Slider x:Name="HeadRollThresholdSlider" 
        Minimum="5" Maximum="30" Value="15" StepFrequency="1"
        ValueChanged="ThresholdSlider_ValueChanged"/>
<TextBlock x:Name="HeadRollThresholdValue" Text="15°" FontSize="12"/>

<TextBlock Text="Shoulder Tilt Threshold:" FontSize="14"/>
<Slider x:Name="ShoulderTiltThresholdSlider" 
        Minimum="5" Maximum="20" Value="10" StepFrequency="1"
        ValueChanged="ThresholdSlider_ValueChanged"/>
<TextBlock x:Name="ShoulderTiltThresholdValue" Text="10°" FontSize="12"/>
```

#### Step 4.2: Update UI Code-Behind
**File**: `dotnet-app/Views/MainPage.xaml.cs`

**Add Fields**:
```csharp
private TextBlock? _rollText;
private TextBlock? _shoulderTiltText;
private Slider? _headRollThresholdSlider;
private Slider? _shoulderTiltThresholdSlider;
private TextBlock? _headRollThresholdValue;
private TextBlock? _shoulderTiltThresholdValue;
```

**Initialize in Constructor**:
```csharp
_rollText = FindName("RollText") as TextBlock;
_shoulderTiltText = FindName("ShoulderTiltText") as TextBlock;
_headRollThresholdSlider = FindName("HeadRollThresholdSlider") as Slider;
_shoulderTiltThresholdSlider = FindName("ShoulderTiltThresholdSlider") as Slider;
_headRollThresholdValue = FindName("HeadRollThresholdValue") as TextBlock;
_shoulderTiltThresholdValue = FindName("ShoulderTiltThresholdValue") as TextBlock;
```

**Update Display**:
```csharp
// In OnPostureDataReceived method
if (data.AdjustedRoll.HasValue)
{
    if (_rollText != null) 
        _rollText.Text = $"{data.AdjustedRoll.Value:F1}°";
}

if (data.AdjustedShoulderTilt.HasValue)
{
    if (_shoulderTiltText != null) 
        _shoulderTiltText.Text = $"{data.AdjustedShoulderTilt.Value:F1}°";
}
```

#### Step 4.3: Update WebSocket Client
**File**: `dotnet-app/Services/WebSocketClient.cs`

**Update `SetThresholdsAsync()` Method**:
```csharp
public async Task SetThresholdsAsync(double pitchThreshold, double distanceThreshold, 
                                     double headRollThreshold, double shoulderTiltThreshold)
{
    var message = new
    {
        type = "set_thresholds",
        pitch_threshold = pitchThreshold,
        distance_threshold = distanceThreshold,
        head_roll_threshold = headRollThreshold,
        shoulder_tilt_threshold = shoulderTiltThreshold
    };
    
    await SendAsync(JsonSerializer.Serialize(message));
}
```

### Phase 5: Update WebSocket Communication

#### Step 5.1: Handle New Threshold Messages
**File**: `python-service/src/websocket_server.py`

**Update `process_message()`**:
```python
elif msg_type == 'set_thresholds':
    if self.detector:
        self.detector.pitch_threshold = data.get('pitch_threshold', -10)
        self.detector.distance_threshold = data.get('distance_threshold', 10)
        self.detector.head_roll_threshold = data.get('head_roll_threshold', 15)
        self.detector.shoulder_tilt_threshold = data.get('shoulder_tilt_threshold', 10)
        await websocket.send(json.dumps({
            'type': 'thresholds_updated',
            'success': True
        }))
```

#### Step 5.2: Include New Data in Monitoring Loop
**File**: `python-service/src/websocket_server.py`

**Update Data Transmission**:
```python
await self.send({
    'type': 'posture_result',
    'data': {
        'is_bad': posture_status['is_bad'],
        'pitch_angle': posture_status['pitch_angle'],
        'roll_angle': posture_status['roll_angle'],
        'shoulder_tilt': posture_status['shoulder_tilt'],
        'adjusted_pitch': posture_status['adjusted_pitch'],
        'adjusted_roll': posture_status['adjusted_roll'],
        'adjusted_shoulder_tilt': posture_status['adjusted_shoulder_tilt'],
        'distance': posture_status['distance'],
        'bad_duration': analysis['bad_duration'],
        'should_warn': analysis['should_warn'],
        'message': analysis['message'],
        'posture_issues': posture_status['posture_issues'],
        'error': posture_status.get('error'),
        'frame': frame_base64
    }
})
```

## Testing Strategy

### Unit Testing

#### Test 1: Roll Angle Calculation
**File**: Create `python-service/tests/test_roll_detection.py`

```python
import unittest
import numpy as np
from src.pose_detector import PostureDetector

class TestRollDetection(unittest.TestCase):
    def test_perfect_level_head(self):
        """Test that level head returns 0 roll angle"""
        # Create mock rotation matrix for level head
        R = np.eye(3)  # Identity matrix = no rotation
        detector = PostureDetector()
        pitch, yaw, roll = detector._rotation_matrix_to_euler_angles(R)
        self.assertAlmostEqual(roll, 0, places=1)
    
    def test_head_tilted_right(self):
        """Test that right tilt returns positive angle"""
        # Create rotation matrix for 15° right tilt
        angle = np.radians(15)
        R = np.array([
            [np.cos(angle), -np.sin(angle), 0],
            [np.sin(angle), np.cos(angle), 0],
            [0, 0, 1]
        ])
        detector = PostureDetector()
        pitch, yaw, roll = detector._rotation_matrix_to_euler_angles(R)
        self.assertAlmostEqual(roll, 15, places=1)
    
    def test_head_tilted_left(self):
        """Test that left tilt returns negative angle"""
        # Similar test for left tilt
        pass
```

#### Test 2: Shoulder Tilt Calculation
```python
def test_level_shoulders(self):
    """Test that level shoulders return 0° tilt"""
    # Create mock landmarks with equal Y coordinates
    pass

def test_right_shoulder_higher(self):
    """Test detection of right shoulder being higher"""
    # Create mock landmarks with right shoulder higher
    pass
```

### Integration Testing

#### Test 3: End-to-End Tilt Detection
1. Run application with monitoring
2. Manually tilt head to right at various angles (5°, 10°, 15°, 20°)
3. Verify correct angle measurement displayed
4. Verify warning triggers at threshold (15°)
5. Repeat for left tilt
6. Repeat for shoulder tilt

#### Test 4: Combined Posture Issues
1. Test simultaneous pitch + roll issues
2. Verify warning message includes both issues
3. Test all four issues simultaneously
4. Verify accurate issue list in warning

### Performance Testing

#### Test 5: Frame Processing Speed
- Measure FPS before and after adding pose detection
- Target: Maintain ~10 FPS
- Acceptable: Down to 8 FPS
- If below 8 FPS, optimize by:
  - Reducing resolution
  - Processing every other frame for pose
  - Using lighter pose model

#### Test 6: Memory Usage
- Monitor memory consumption over 30-minute session
- Verify no memory leaks from dual model usage

## Edge Cases and Considerations

### 1. User Turning Sideways
**Issue**: If user turns body sideways (not facing camera), shoulder landmarks may not be visible.

**Solution**: 
- Check landmark visibility scores
- If shoulders not detected, only use head roll
- Display message: "Please face the camera"

### 2. Poor Lighting
**Issue**: Landmarks may be unreliable in low light.

**Solution**:
- Check detection confidence scores
- If confidence < 0.5, show warning
- Temporarily disable tilt detection

### 3. Camera Angle
**Issue**: If camera is mounted at an angle, baseline may be skewed.

**Solution**:
- Baseline calibration accounts for camera angle
- Use relative measurements (adjusted angles)
- User calibrates in their actual working position

### 4. Temporary Movements
**Issue**: Brief movements (reaching for something) shouldn't trigger warnings.

**Solution**:
- Existing 5-second threshold handles this
- Consider adding "sustained tilt" check (tilt for 3 consecutive seconds)

### 5. Physical Limitations
**Issue**: Some users may have physical conditions requiring asymmetric posture.

**Solution**:
- Make thresholds highly configurable
- Allow disabling specific checks
- Add "custom baseline" for individual needs

## Model File Requirements

### Download MediaPipe Pose Model

**Model**: `pose_landmarker.task`

**Download Source**: https://developers.google.com/mediapipe/solutions/vision/pose_landmarker#models

**Recommended Model**: 
- **pose_landmarker_heavy.task** - Best accuracy (5.2 MB)
- Alternative: **pose_landmarker_lite.task** - Faster, less accurate (3.6 MB)

**Installation**:
1. Download model file
2. Place in `python-service/src/`
3. Verify path in `pose_detector.py`

**Model Size Comparison**:
- Face Landmarker: ~10 MB
- Pose Landmarker (heavy): ~5 MB
- Total: ~15 MB (acceptable for local app)

## Performance Considerations

### Dual Model Impact

**Current**: Single Face Landmarker at ~30 FPS
**New**: Face + Pose Landmarkers

**Expected Performance**:
- Face detection: ~15ms per frame
- Pose detection: ~20ms per frame
- Total: ~35ms per frame = ~28 FPS (theoretical)
- Practical: ~15-20 FPS after processing overhead

**Optimization Options** (if needed):
1. **Skip Frames**: Process pose every 2-3 frames
2. **Lower Resolution**: Downscale to 640x480 before detection
3. **Lite Model**: Use pose_landmarker_lite.task
4. **ROI Processing**: Only process upper body region

### Memory Considerations

**Additional Memory Usage**:
- Pose model: ~5 MB
- Pose landmarks: ~1 KB per frame
- Negligible impact on modern systems

## User Experience Enhancements

### Visual Feedback

**Option 1: Color-Coded Metrics**
- Green: Within threshold
- Yellow: Approaching threshold
- Red: Exceeding threshold

**Option 2: Tilt Indicator**
- Visual representation of head/body tilt
- Arrow showing tilt direction
- Degree indicator

**Option 3: 3D Avatar**
- Small 3D model showing user's posture
- Real-time orientation matching
- Advanced but engaging

### Settings Panel

**New Settings UI**:
```
Tilt Detection Settings
-----------------------
□ Enable Head Roll Detection
  Threshold: [====|====] 15°
  
□ Enable Shoulder Tilt Detection
  Threshold: [===|=====] 10°
  
Warning Preferences
-------------------
□ Warn for head tilt
□ Warn for shoulder tilt
□ Detailed warning messages
```

## Alternative Approaches (Considered but Not Recommended)

### Approach 1: Eye Alignment Only
**Method**: Use horizontal alignment of eyes without full 3D pose

**Pros**: 
- Simpler, no additional model needed
- Faster processing

**Cons**: 
- Less accurate than 3D rotation matrix
- Sensitive to face perspective
- Doesn't detect body tilt
- **Not recommended**

### Approach 2: Full Body Tracking
**Method**: Track entire body including legs, feet

**Pros**: 
- Complete posture assessment
- Detect slouching, leg position

**Cons**: 
- Requires full-body camera view
- Most users only have face/upper body visible
- Overkill for seated desk work
- **Not recommended for v1**

### Approach 3: Accelerometer/Gyroscope
**Method**: Use phone/wearable sensors for tilt

**Pros**: 
- Very accurate
- No camera needed

**Cons**: 
- Requires additional hardware
- Not integrated with existing system
- User must wear device
- **Not applicable to current design**

## Documentation Updates Required

### User-Facing Documentation

#### README.md Updates
- Add tilt detection to feature list
- Update screenshots with new UI
- Add calibration instructions for tilt

#### QUICKSTART.md Updates
```markdown
## New Feature: Head and Body Tilt Detection

Slouti now detects lateral tilting (sideways leaning):

1. **Head Roll**: Detects when your head tilts left or right
2. **Shoulder Tilt**: Detects when one shoulder is higher than the other

### Calibration
Sit with BOTH good posture AND level head/shoulders:
- Face camera directly
- Keep head level (not tilted)
- Keep shoulders even
- Click "Save Good Posture"

### Thresholds
- **Head Roll**: Default 15° (adjust if needed)
- **Shoulder Tilt**: Default 10° (adjust if needed)
```

### Developer Documentation

#### TECHNICAL_GUIDE.md Updates
- Document new MediaPipe Pose integration
- Explain Euler angle extraction
- Add shoulder tilt calculation algorithm
- Include coordinate system diagrams

#### API Documentation
- Update WebSocket message schemas
- Document new threshold parameters
- Add example JSON messages

## Implementation Timeline

### Week 1: Core Detection
- **Day 1-2**: Implement MediaPipe Pose Landmarker integration
- **Day 3**: Add roll angle extraction from rotation matrix
- **Day 4**: Implement shoulder tilt calculation
- **Day 5**: Testing and debugging core detection

### Week 2: Integration
- **Day 1**: Update data models (Python and C#)
- **Day 2**: Extend posture analysis logic
- **Day 3**: Update WebSocket communication
- **Day 4**: Add enhanced warning messages
- **Day 5**: Integration testing

### Week 3: UI and Polish
- **Day 1-2**: Update UI with new metrics and sliders
- **Day 3**: Visual enhancements and feedback
- **Day 4**: User testing and adjustments
- **Day 5**: Documentation updates

### Week 4: Testing and Deployment
- **Day 1-2**: Comprehensive testing (unit, integration, performance)
- **Day 3**: Bug fixes and optimization
- **Day 4**: User acceptance testing
- **Day 5**: Final deployment and documentation

## Success Criteria

### Functional Requirements
✓ Detect head roll angle within ±2° accuracy
✓ Detect shoulder tilt angle within ±2° accuracy  
✓ Warn when head roll exceeds 15° (configurable)
✓ Warn when shoulder tilt exceeds 10° (configurable)
✓ Provide specific warning messages for each issue type
✓ Allow independent threshold configuration
✓ Maintain baseline calibration for tilt angles

### Performance Requirements
✓ Maintain minimum 8 FPS frame processing
✓ Detection latency < 150ms
✓ Memory usage < 500 MB total
✓ No memory leaks over 1-hour session

### User Experience Requirements
✓ Clear visual display of tilt metrics
✓ Intuitive threshold adjustment
✓ Helpful warning messages
✓ No false positives during normal movement
✓ Easy to understand calibration process

## Risks and Mitigations

### Risk 1: Performance Degradation
**Impact**: HIGH - User experience suffers if FPS drops too low

**Mitigation**:
- Profile performance early
- Implement optimization strategies proactively
- Use lite pose model if needed
- Add frame skipping option

### Risk 2: Inaccurate Shoulder Detection
**Impact**: MEDIUM - Body tilt warnings may be unreliable

**Mitigation**:
- Higher confidence thresholds for pose landmarks
- Fallback to head-only detection if pose unavailable
- Make shoulder detection optional
- Clear user guidance on camera positioning

### Risk 3: False Positives
**Impact**: MEDIUM - Annoying warnings during legitimate movements

**Mitigation**:
- Tune thresholds based on user feedback
- Add "sustained tilt" requirement (3+ seconds)
- Allow easy threshold adjustment
- Option to disable specific checks

### Risk 4: Model Compatibility
**Impact**: LOW - Pose model may not work on all systems

**Mitigation**:
- Test on multiple system configurations
- Provide clear error messages
- Fallback to face-only detection
- Document system requirements

## Future Enhancements (Post-MVP)

### Enhancement 1: Machine Learning Personalization
- Learn user's typical posture patterns
- Adjust thresholds automatically
- Predict posture degradation

### Enhancement 2: Posture History Analytics
- Track tilt frequency over time
- Generate daily/weekly reports
- Identify problematic times of day
- Suggest desk ergonomics improvements

### Enhancement 3: Advanced Visualizations
- 3D skeleton overlay on camera feed
- Heat map of problem areas
- Posture comparison (current vs. ideal)

### Enhancement 4: Integration with Smart Devices
- Control desk height automatically
- Adjust monitor position
- Smart reminders on phone/watch

### Enhancement 5: Gamification
- Posture score and streaks
- Achievements and badges
- Friendly competitions
- Rewards for good posture

## Conclusion

This implementation plan provides a comprehensive roadmap for adding head and body tilt detection to the Slouti posture monitoring application. By leveraging MediaPipe Pose Landmarker alongside the existing Face Landmarker and extracting roll angles from the existing PnP rotation matrix, we can detect lateral tilting with minimal performance impact.

The phased approach ensures systematic implementation with testing at each stage, while the detailed technical specifications provide clear guidance for developers. The plan accounts for edge cases, performance considerations, and user experience, setting the foundation for a robust feature that significantly enhances posture monitoring capabilities.

**Key Advantages of This Approach:**
1. ✅ Uses proven MediaPipe technology (same as existing detection)
2. ✅ Minimal code changes to existing architecture
3. ✅ Maintains real-time performance
4. ✅ Provides specific, actionable feedback to users
5. ✅ Highly configurable for individual needs
6. ✅ Backwards compatible with existing calibration system

**Next Steps:**
1. Review and approve this implementation plan
2. Download MediaPipe Pose model file
3. Begin Phase 1 implementation (Core Detection Infrastructure)
4. Set up testing framework
5. Iterate based on testing results

---

**Document Version**: 1.0  
**Created**: December 11, 2025  
**Status**: Ready for Implementation  
**Estimated Implementation Time**: 3-4 weeks (1 developer)
