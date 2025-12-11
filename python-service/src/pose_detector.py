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
        
        # Good posture baseline (None until calibrated)
        self.good_head_pitch_angle = None
        self.good_head_distance = None
        
        # Configurable thresholds
        self.pitch_threshold = -10  # degrees (negative = looking down)
        self.distance_threshold = 10  # cm (closer than baseline)
        
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
    
    def calculate_pitch_angle(self, landmarks, frame_shape):
        """Calculate head pitch angle using PnP algorithm."""
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
            return None
        
        # Convert rotation vector to rotation matrix
        rot_matrix, _ = cv2.Rodrigues(rot_vec)
        
        # Extract pitch angle from rotation matrix
        pitch_angle = self._rotation_matrix_to_pitch(rot_matrix)
        
        # Normalize angle to proper range
        if pitch_angle > 0:
            pitch_angle = 180 - pitch_angle
        else:
            pitch_angle = -180 - pitch_angle
        
        return pitch_angle
    
    def _rotation_matrix_to_pitch(self, R):
        """Convert rotation matrix to pitch angle in degrees."""
        sy = np.sqrt(R[0, 0] ** 2 + R[1, 0] ** 2)
        singular = sy < 1e-6
        
        if not singular:
            pitch = np.arctan2(R[2, 1], R[2, 2])
        else:
            pitch = np.arctan2(-R[1, 2], R[1, 1])
        
        # Convert radians to degrees
        return np.degrees(pitch)
    
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
    
    def save_good_posture(self, frame, timestamp_ms):
        """Capture current posture as good posture baseline."""
        landmarks = self.detect_landmarks(frame, timestamp_ms)
        
        if not landmarks:
            return False
        
        pitch_angle = self.calculate_pitch_angle(landmarks, frame.shape)
        distance = self.calculate_distance(landmarks, frame.shape)
        
        if pitch_angle is not None and distance is not None:
            self.good_head_pitch_angle = pitch_angle
            self.good_head_distance = distance
            return True
        
        return False
    
    def check_posture(self, frame, timestamp_ms):
        """
        Analyze frame and return posture status.
        
        Returns:
            dict: {
                'is_bad': bool,
                'pitch_angle': float,
                'distance': float,
                'adjusted_pitch': float,
                'error': str (optional)
            }
        """
        landmarks = self.detect_landmarks(frame, timestamp_ms)
        
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
        
        # Calculate adjusted pitch relative to baseline
        adjusted_pitch = pitch_angle - self.good_head_pitch_angle
        
        # Determine if posture is bad
        is_bad = self._is_posture_bad(adjusted_pitch, distance, self.good_head_distance)
        
        return {
            'is_bad': is_bad,
            'pitch_angle': round(pitch_angle, 2) if pitch_angle else None,
            'distance': round(distance, 2) if distance else None,
            'adjusted_pitch': round(adjusted_pitch, 2) if adjusted_pitch else None
        }
    
    def _is_posture_bad(self, adjusted_pitch, current_distance, good_distance):
        """Determine if current posture is bad based on thresholds."""
        # Bad if looking down too much (negative pitch)
        if adjusted_pitch < self.pitch_threshold:
            return True
        
        # Bad if too close to camera (leaning forward)
        if (good_distance - current_distance) > self.distance_threshold:
            return True
        
        return False
    
    def close(self):
        """Clean up resources."""
        self.face_landmarker.close()
