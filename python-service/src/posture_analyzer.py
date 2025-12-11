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
