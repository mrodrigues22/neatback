import cv2
import mediapipe as mp
import numpy as np
import os
from mediapipe import solutions
from mediapipe.framework.formats import landmark_pb2
from smoothing_filter import SmoothingFilter
from config import SMOOTHING_WINDOW_SIZE, THRESHOLDS

class PostureDetector:
    def __init__(self):
        # Initialize MediaPipe Face Landmarker
        self.BaseOptions = mp.tasks.BaseOptions
        self.FaceLandmarker = mp.tasks.vision.FaceLandmarker
        self.FaceLandmarkerOptions = mp.tasks.vision.FaceLandmarkerOptions
        self.VisionRunningMode = mp.tasks.vision.RunningMode
        
        # Get the path to the model file (in the same directory as this script)
        script_dir = os.path.dirname(os.path.abspath(__file__))
        model_path = os.path.join(script_dir, 'face_landmarker.task')
        
        # Configure face landmarker
        options = self.FaceLandmarkerOptions(
            base_options=self.BaseOptions(model_asset_path=model_path),
            running_mode=self.VisionRunningMode.VIDEO,
            num_faces=1,
            min_face_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )
        
        self.face_landmarker = self.FaceLandmarker.create_from_options(options)
        
        # Initialize MediaPipe Pose Landmarker
        self.PoseLandmarker = mp.tasks.vision.PoseLandmarker
        self.PoseLandmarkerOptions = mp.tasks.vision.PoseLandmarkerOptions
        
        # Get path to pose model
        pose_model_path = os.path.join(script_dir, 'pose_landmarker.task')
        
        # Configure pose landmarker (only if model file exists)
        self.pose_landmarker = None
        if os.path.exists(pose_model_path):
            try:
                pose_options = self.PoseLandmarkerOptions(
                    base_options=self.BaseOptions(model_asset_path=pose_model_path),
                    running_mode=self.VisionRunningMode.VIDEO,
                    num_poses=1,
                    min_pose_detection_confidence=0.5,
                    min_tracking_confidence=0.5
                )
                self.pose_landmarker = self.PoseLandmarker.create_from_options(pose_options)
            except Exception as e:
                print(f"Warning: Could not initialize Pose Landmarker: {e}")
        else:
            print(f"Warning: Pose model not found at {pose_model_path}. Shoulder tilt detection will be disabled.")
        
        # Good posture baseline (None until calibrated)
        self.good_head_pitch_angle = None
        self.good_head_distance = None
        self.good_head_roll = None
        self.good_shoulder_tilt = None
        
        # Add smoothing filter (window of 5 frames = ~0.25s at 20 FPS)
        self.smoothing_filter = SmoothingFilter(window_size=SMOOTHING_WINDOW_SIZE)
        
        # Track current state for hysteresis
        self.is_currently_bad = False
        
        # Hysteresis thresholds
        self.thresholds = THRESHOLDS
        
        # Configurable thresholds
        self.pitch_threshold = -15  # degrees (negative = looking down)
        self.distance_threshold = 10  # cm (closer than baseline)
        self.head_roll_threshold = 15  # degrees
        self.shoulder_tilt_threshold = 15  # degrees (use abs value, so catches both directions)
        self.yaw_threshold = 30  # degrees - ignore roll detection if head rotated beyond this
        
        # Compensation detection settings
        self.compensation_detection_enabled = True
        self.min_tilt_for_compensation = 2  # degrees
        self.compensation_ratio_threshold = 0.7  # 70% match indicates compensation
        
        # Track last compensation description for messaging
        self._last_compensation_desc = None
        
        # 3D face model coordinates (in mm)
        # Using stable landmarks that don't move with facial expressions (smiling, etc.)
        self.face_3d_model = np.array([
            [-165.0, 170.0, -135.0],   # Left eye outer corner (index 33)
            [165.0, 170.0, -135.0],    # Right eye outer corner (index 263)
            [0.0, 0.0, 0.0],           # Nose tip (index 1)
            [-150.0, 170.0, -125.0],   # Left eye inner corner (index 133)
            [150.0, 170.0, -125.0],    # Right eye inner corner (index 362)
            [0.0, 200.0, -80.0]        # Nose bridge/forehead (index 168)
        ], dtype=np.float64)
        
        # Landmark indices for PnP - using stable points that don't move when smiling
        self.landmark_indices = [33, 263, 1, 133, 362, 168]
        
    def detect_landmarks(self, frame, timestamp_ms):
        """Detect facial landmarks using MediaPipe Face Landmarker."""
        # Convert BGR to RGB
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        
        # Create MediaPipe Image
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)
        
        # Detect landmarks
        detection_result = self.face_landmarker.detect_for_video(mp_image, timestamp_ms)
        
        if detection_result.face_landmarks:
            return detection_result.face_landmarks[0]
        return None

    def get_face_bbox(self, landmarks, frame_shape, padding_ratio=0.05):
        """Compute a face bounding box from landmarks.

        Args:
            landmarks: list of MediaPipe landmarks for a single face.
            frame_shape: tuple (height, width, channels) of the frame.
            padding_ratio: extra padding around bbox as fraction of min(width, height).

        Returns:
            tuple: (x1, y1, x2, y2) in pixel coordinates, or None if landmarks invalid.
        """
        if not landmarks or len(landmarks) == 0:
            return None
        height, width = frame_shape[:2]
        xs = [int(lm.x * width) for lm in landmarks]
        ys = [int(lm.y * height) for lm in landmarks]
        x1, x2 = max(min(xs), 0), min(max(xs), width - 1)
        y1, y2 = max(min(ys), 0), min(max(ys), height - 1)

        # Apply padding
        pad = int(min(width, height) * padding_ratio)
        x1 = max(x1 - pad, 0)
        y1 = max(y1 - pad, 0)
        x2 = min(x2 + pad, width - 1)
        y2 = min(y2 + pad, height - 1)
        return (x1, y1, x2, y2)
    
    def get_2d_landmarks(self, landmarks, frame_shape, indices):
        """Extract 2D coordinates for specific landmark indices."""
        height, width = frame_shape[:2]
        coords_2d = []
        
        for idx in indices:
            landmark = landmarks[idx]
            x = landmark.x * width
            y = landmark.y * height
            coords_2d.append([x, y])
        
        return np.array(coords_2d, dtype=np.float64)
    
    def calculate_head_angles(self, landmarks, frame_shape):
        """Calculate head orientation angles (pitch, yaw, roll) using PnP algorithm."""
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
        
        # No lens distortion assumed
        dist_coeffs = np.zeros((4, 1))
        
        # Solve PnP to get rotation and translation vectors
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
        
        # Normalize pitch angle to proper range
        if pitch > 0:
            pitch = 180 - pitch
        else:
            pitch = -180 - pitch
        
        return pitch, yaw, roll
    
    def calculate_pitch_angle(self, landmarks, frame_shape):
        """Calculate head pitch angle (backward compatibility)."""
        pitch, _, _ = self.calculate_head_angles(landmarks, frame_shape)
        return pitch
    
    def _detect_compensation(self, adjusted_roll, adjusted_shoulder_tilt):
        """
        Detect if user is compensating body tilt with head tilt.
        
        Returns:
            tuple: (is_compensating: bool, description: str or None)
        """
        if not self.compensation_detection_enabled:
            return False, None
        
        # Skip if either measurement is missing
        if adjusted_roll is None or adjusted_shoulder_tilt is None:
            return False, None
        
        # Check if both have significant tilt (>5°)
        has_head_tilt = abs(adjusted_roll) > self.min_tilt_for_compensation
        has_shoulder_tilt = abs(adjusted_shoulder_tilt) > self.min_tilt_for_compensation
        
        if not (has_head_tilt or has_shoulder_tilt):
            return False, None
        
        # Check if tilts are in opposite directions
        opposite_directions = (adjusted_roll * adjusted_shoulder_tilt) < 0
        
        if opposite_directions:
            # Calculate compensation ratio
            smaller = min(abs(adjusted_roll), abs(adjusted_shoulder_tilt))
            larger = max(abs(adjusted_roll), abs(adjusted_shoulder_tilt))
            ratio = smaller / larger if larger > 0 else 0
            
            if ratio > self.compensation_ratio_threshold:
                # Determine pattern
                if adjusted_shoulder_tilt > 0:
                    description = "Body leaning right, head compensating left"
                else:
                    description = "Body leaning left, head compensating right"
                
                return True, description
        
        return False, None
    
    def _get_shoulder_confidence(self, pose_landmarks):
        """Get average visibility/confidence of shoulder landmarks."""
        if not pose_landmarks or len(pose_landmarks) < 13:
            return 0.0
        
        left_shoulder = pose_landmarks[11]
        right_shoulder = pose_landmarks[12]
        
        # MediaPipe provides visibility score for each landmark
        avg_visibility = (left_shoulder.visibility + right_shoulder.visibility) / 2
        return round(avg_visibility, 2)
    
    def _rotation_matrix_to_euler_angles(self, R):
        """Convert rotation matrix to Euler angles (pitch, yaw, roll) in degrees."""
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
        pitch_deg = np.degrees(pitch)
        yaw_deg = np.degrees(yaw)
        roll_deg = np.degrees(roll)
        
        # Normalize roll to -90 to 90 range (deviation from vertical)
        # If angle is close to 180 or -180, we want the smallest angle from vertical (0 or ±180)
        if roll_deg > 90:
            roll_deg = roll_deg - 180
        elif roll_deg < -90:
            roll_deg = roll_deg + 180
        
        return (pitch_deg, yaw_deg, roll_deg)
    
    def calculate_eye_roll_angle(self, landmarks, frame_shape):
        """Calculate head roll angle directly from eye positions.
        This is more reliable than Euler angles when head is rotated (yaw).
        
        Returns:
            float: Roll angle in degrees (positive = head tilted right)
        """
        height, width = frame_shape[:2]
        
        # Use eye corner landmarks for more stable roll calculation
        left_eye_idx = 33   # Left eye outer corner
        right_eye_idx = 263  # Right eye outer corner
        
        left_eye = landmarks[left_eye_idx]
        right_eye = landmarks[right_eye_idx]
        
        # Convert to pixel coordinates
        left_x = left_eye.x * width
        left_y = left_eye.y * height
        right_x = right_eye.x * width
        right_y = right_eye.y * height
        
        # Calculate angle of line between eyes relative to horizontal
        delta_y = right_y - left_y
        delta_x = right_x - left_x
        
        # Angle in degrees (positive = right eye lower = head tilted right)
        roll_angle = np.degrees(np.arctan2(delta_y, delta_x))
        
        return roll_angle
    
    def calculate_distance(self, landmarks, frame_shape):
        """Calculate head-to-camera distance using interpupillary distance (IPD) method."""
        height, width = frame_shape[:2]
        
        # Pupil landmark indices
        left_pupil_idx = 473
        right_pupil_idx = 468
        
        # Get pupil coordinates
        left_pupil = landmarks[left_pupil_idx]
        right_pupil = landmarks[right_pupil_idx]
        
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
        
        # Average human interpupillary distance in cm
        avg_ipd = 6.3
        
        # Calculate distance using similar triangles
        focal_length = width
        distance = (focal_length / pixel_distance) * avg_ipd
        
        return distance
    
    def detect_pose_landmarks(self, frame, timestamp_ms):
        """Detect body landmarks using MediaPipe Pose Landmarker."""
        if self.pose_landmarker is None:
            return None
        
        try:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)
            
            detection_result = self.pose_landmarker.detect_for_video(mp_image, timestamp_ms)
            
            if detection_result.pose_landmarks:
                return detection_result.pose_landmarks[0]
        except Exception as e:
            print(f"Error detecting pose landmarks: {e}")
        
        return None
    
    def calculate_shoulder_tilt(self, pose_landmarks, frame_shape):
        """Calculate shoulder tilt angle from horizontal.
        
        Returns:
            float: Angle in degrees (positive = right shoulder higher, negative = left shoulder higher)
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
        
        # Angle in degrees using arctan2 (returns -180 to 180)
        angle = np.degrees(np.arctan2(delta_y, delta_x))
        
        # Normalize to -90 to 90 range (deviation from horizontal)
        # If angle is close to 180 or -180, it means shoulders are nearly level but facing left
        # We want the smallest angle from horizontal (0 or ±180)
        if angle > 90:
            angle = angle - 180
        elif angle < -90:
            angle = angle + 180
        
        return angle
    
    def save_good_posture(self, frame, timestamp_ms):
        """Capture current posture as good posture baseline."""
        face_landmarks = self.detect_landmarks(frame, timestamp_ms)
        
        if not face_landmarks:
            return False
        
        pitch, yaw, roll = self.calculate_head_angles(face_landmarks, frame.shape)
        distance = self.calculate_distance(face_landmarks, frame.shape)
        
        # Use eye-based roll for baseline (more reliable)
        eye_roll = self.calculate_eye_roll_angle(face_landmarks, frame.shape)
        
        # Try to get shoulder tilt
        pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
        shoulder_tilt = None
        if pose_landmarks:
            shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
        
        if pitch is not None and distance is not None:
            self.good_head_pitch_angle = pitch
            self.good_head_roll = eye_roll if eye_roll is not None else 0
            self.good_head_distance = distance
            self.good_shoulder_tilt = shoulder_tilt if shoulder_tilt is not None else 0
            
            # Reset smoothing filter to start fresh
            self.smoothing_filter.reset()
            # Reset hysteresis state
            self.is_currently_bad = False
            
            return True
        
        return False
    
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
        
        # Use eye-based roll calculation (more reliable than Euler angles)
        eye_roll = self.calculate_eye_roll_angle(face_landmarks, frame.shape)
        
        # Calculate body metrics
        pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
        shoulder_tilt = None
        if pose_landmarks:
            shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
        
        # Add to smoothing filter
        self.smoothing_filter.add_measurement(pitch, eye_roll, shoulder_tilt, distance)
        
        # Get smoothed values
        smoothed = self.smoothing_filter.get_smoothed_values()
        
        # Use smoothed values for detection if available, otherwise raw
        pitch_smoothed = smoothed['pitch'] if smoothed['pitch'] is not None else pitch
        eye_roll_smoothed = smoothed['roll'] if smoothed['roll'] is not None else eye_roll
        shoulder_tilt_smoothed = smoothed['shoulder_tilt'] if smoothed['shoulder_tilt'] is not None else shoulder_tilt
        distance_smoothed = smoothed['distance'] if smoothed['distance'] is not None else distance
        
        # Compute face bbox for drawing
        face_bbox = self.get_face_bbox(face_landmarks, frame.shape)

        # If no baseline saved, can't determine bad posture
        if self.good_head_pitch_angle is None:
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
                'face_bbox': face_bbox,
                'error': 'No baseline posture saved'
            }
        
        # Calculate adjusted values relative to baseline (using smoothed values)
        adjusted_pitch = pitch_smoothed - self.good_head_pitch_angle
        adjusted_roll = eye_roll_smoothed - self.good_head_roll if eye_roll_smoothed is not None and self.good_head_roll is not None else 0
        adjusted_shoulder_tilt = shoulder_tilt_smoothed - self.good_shoulder_tilt if shoulder_tilt_smoothed is not None and self.good_shoulder_tilt is not None else 0
        
        # Determine if posture is bad (using smoothed measurements)
        is_bad, issues = self._is_posture_bad(
            adjusted_pitch, 
            adjusted_roll, 
            adjusted_shoulder_tilt,
            distance_smoothed, 
            self.good_head_distance,
            yaw  # Pass yaw to check if head is rotated
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
            'posture_issues': issues,
            'shoulder_detection_active': pose_landmarks is not None,
            'shoulder_detection_confidence': self._get_shoulder_confidence(pose_landmarks),
            'compensation_description': self._last_compensation_desc,
            'face_bbox': face_bbox
        }
    
    def _check_threshold_with_hysteresis(self, value, threshold_config, is_lower_bad=True):
        """
        Check if value violates threshold with hysteresis.
        
        Args:
            value: Current measurement value
            threshold_config: Dict with 'enter_bad' and 'exit_bad' thresholds
            is_lower_bad: True if lower values are bad (like pitch), 
                          False if higher values are bad (like distance deviation)
        
        Returns:
            bool: True if threshold is violated
        """
        enter_threshold = threshold_config['enter_bad']
        exit_threshold = threshold_config['exit_bad']
        
        if self.is_currently_bad:
            # Already in bad state, use exit threshold (more lenient)
            if is_lower_bad:
                return value < exit_threshold
            else:
                return abs(value) > exit_threshold
        else:
            # In good state, use enter threshold (stricter)
            if is_lower_bad:
                return value < enter_threshold
            else:
                return abs(value) > enter_threshold
    
    def _is_posture_bad(self, adjusted_pitch, adjusted_roll, adjusted_shoulder_tilt, current_distance, good_distance, yaw=None):
        """Determine if current posture is bad based on thresholds with hysteresis.
        
        Returns:
            tuple: (is_bad, reasons)
        """
        reasons = []
        
        # Check pitch with hysteresis (looking down)
        if adjusted_pitch is not None:
            if self._check_threshold_with_hysteresis(
                adjusted_pitch, 
                self.thresholds['pitch'], 
                is_lower_bad=True
            ):
                reasons.append('head_pitch')
        
        # Check distance with hysteresis (leaning forward)
        if good_distance is not None and current_distance is not None:
            distance_deviation = good_distance - current_distance
            if self._check_threshold_with_hysteresis(
                distance_deviation,
                self.thresholds['distance'],
                is_lower_bad=False
            ):
                reasons.append('distance')
        
        # Only check roll/tilt if head is not rotated significantly
        # This prevents false positives when user is looking left/right
        head_is_facing_forward = yaw is None or abs(yaw) < self.yaw_threshold
        
        if head_is_facing_forward:
            # Check head roll with hysteresis (head tilted sideways)
            if adjusted_roll is not None:
                if self._check_threshold_with_hysteresis(
                    adjusted_roll,
                    self.thresholds['head_roll'],
                    is_lower_bad=False
                ):
                    reasons.append('head_roll')
            
            # Check shoulder tilt with hysteresis (body tilted sideways)
            if adjusted_shoulder_tilt is not None:
                if self._check_threshold_with_hysteresis(
                    adjusted_shoulder_tilt,
                    self.thresholds['shoulder_tilt'],
                    is_lower_bad=False
                ):
                    reasons.append('shoulder_tilt')
            
            # Check for compensation pattern (for informational purposes)
            is_compensating, compensation_desc = self._detect_compensation(
                adjusted_roll, adjusted_shoulder_tilt
            )
            
            if is_compensating:
                # Store compensation description for warning message
                self._last_compensation_desc = compensation_desc
            else:
                self._last_compensation_desc = None
        
        is_bad = len(reasons) > 0
        
        # Update hysteresis state for next check
        self.is_currently_bad = is_bad
        
        return is_bad, reasons
    
    def close(self):
        """Clean up resources."""
        self.face_landmarker.close()
        if self.pose_landmarker is not None:
            self.pose_landmarker.close()
