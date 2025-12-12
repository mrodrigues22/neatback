import cv2
import mediapipe as mp
import numpy as np
import os
from mediapipe import solutions
from mediapipe.framework.formats import landmark_pb2

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
        
        # Configurable thresholds
        self.pitch_threshold = -10  # degrees (negative = looking down)
        self.distance_threshold = 10  # cm (closer than baseline)
        self.head_roll_threshold = 15  # degrees
        self.shoulder_tilt_threshold = 15  # degrees (use abs value, so catches both directions)
        
        # Compensation detection settings
        self.compensation_detection_enabled = True
        self.min_tilt_for_compensation = 5  # degrees
        self.compensation_ratio_threshold = 0.7  # 70% match indicates compensation
        
        # Track last compensation description for messaging
        self._last_compensation_desc = None
        
        # 3D face model coordinates (in mm)
        self.face_3d_model = np.array([
            [-165.0, 170.0, -135.0],   # Left eye outer corner (index 33)
            [165.0, 170.0, -135.0],    # Right eye outer corner (index 263)
            [0.0, 0.0, 0.0],           # Nose tip (index 1)
            [-150.0, -150.0, -125.0],  # Left mouth corner (index 61)
            [150.0, -150.0, -125.0],   # Right mouth corner (index 291)
            [0.0, -330.0, -65.0]       # Chin (index 199)
        ], dtype=np.float64)
        
        # Landmark indices for PnP
        self.landmark_indices = [33, 263, 1, 61, 291, 199]
        
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
        return (np.degrees(pitch), np.degrees(yaw), np.degrees(roll))
    
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
    
    def save_good_posture(self, frame, timestamp_ms):
        """Capture current posture as good posture baseline."""
        face_landmarks = self.detect_landmarks(frame, timestamp_ms)
        
        if not face_landmarks:
            return False
        
        pitch, yaw, roll = self.calculate_head_angles(face_landmarks, frame.shape)
        distance = self.calculate_distance(face_landmarks, frame.shape)
        
        # Try to get shoulder tilt
        pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
        shoulder_tilt = None
        if pose_landmarks:
            shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
        
        if pitch is not None and distance is not None:
            self.good_head_pitch_angle = pitch
            self.good_head_roll = roll if roll is not None else 0
            self.good_head_distance = distance
            self.good_shoulder_tilt = shoulder_tilt if shoulder_tilt is not None else 0
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
        
        # Calculate body metrics
        pose_landmarks = self.detect_pose_landmarks(frame, timestamp_ms)
        shoulder_tilt = None
        if pose_landmarks:
            shoulder_tilt = self.calculate_shoulder_tilt(pose_landmarks, frame.shape)
        
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
        
        # Calculate adjusted values relative to baseline
        adjusted_pitch = pitch - self.good_head_pitch_angle
        adjusted_roll = roll - self.good_head_roll if roll is not None and self.good_head_roll is not None else 0
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
            'posture_issues': issues,
            'shoulder_detection_active': pose_landmarks is not None,
            'shoulder_detection_confidence': self._get_shoulder_confidence(pose_landmarks),
            'compensation_description': self._last_compensation_desc,
            'face_bbox': face_bbox
        }
    
    def _is_posture_bad(self, adjusted_pitch, adjusted_roll, adjusted_shoulder_tilt, current_distance, good_distance):
        """Determine if current posture is bad based on thresholds.
        
        Returns:
            tuple: (is_bad, reasons)
        """
        reasons = []
        
        # Check pitch (looking down) - use abs() to handle both directions
        if adjusted_pitch is not None and abs(adjusted_pitch) > abs(self.pitch_threshold):
            reasons.append('head_pitch')
        
        # Check distance (leaning forward)
        if good_distance is not None and current_distance is not None:
            if (good_distance - current_distance) > self.distance_threshold:
                reasons.append('distance')
        
        # Check for compensation pattern FIRST (before individual checks)
        is_compensating, compensation_desc = self._detect_compensation(
            adjusted_roll, adjusted_shoulder_tilt
        )
        
        if is_compensating:
            reasons.append('body_compensation')
            # Store compensation description for warning message
            self._last_compensation_desc = compensation_desc
        else:
            # Only check individual thresholds if no compensation detected
            # Check head roll (head tilted sideways)
            if adjusted_roll is not None and abs(adjusted_roll) > self.head_roll_threshold:
                reasons.append('head_roll')
            
            # Check shoulder tilt (body tilted sideways)
            if adjusted_shoulder_tilt is not None and abs(adjusted_shoulder_tilt) > self.shoulder_tilt_threshold:
                reasons.append('shoulder_tilt')
        
        is_bad = len(reasons) > 0
        return is_bad, reasons
    
    def close(self):
        """Clean up resources."""
        self.face_landmarker.close()
        if self.pose_landmarker is not None:
            self.pose_landmarker.close()
