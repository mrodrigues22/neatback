from collections import deque
import numpy as np

class SmoothingFilter:
    """
    Applies moving average smoothing to pose measurements.
    Reduces jitter and noise in pose detection.
    """
    def __init__(self, window_size=5):
        """
        Args:
            window_size: Number of frames to average (default: 5)
        """
        self.window_size = window_size
        self.pitch_buffer = deque(maxlen=window_size)
        self.roll_buffer = deque(maxlen=window_size)
        self.shoulder_buffer = deque(maxlen=window_size)
        self.distance_buffer = deque(maxlen=window_size)
    
    def add_measurement(self, pitch, roll, shoulder_tilt, distance):
        """Add new measurements to the buffers."""
        if pitch is not None:
            self.pitch_buffer.append(pitch)
        if roll is not None:
            self.roll_buffer.append(roll)
        if shoulder_tilt is not None:
            self.shoulder_buffer.append(shoulder_tilt)
        if distance is not None:
            self.distance_buffer.append(distance)
    
    def get_smoothed_values(self):
        """
        Get smoothed values using moving average.
        Returns None for values that don't have enough history yet.
        """
        def smooth(buffer):
            if len(buffer) == 0:
                return None
            # Use median for more robust outlier rejection
            return float(np.median(list(buffer)))
        
        return {
            'pitch': smooth(self.pitch_buffer),
            'roll': smooth(self.roll_buffer),
            'shoulder_tilt': smooth(self.shoulder_buffer),
            'distance': smooth(self.distance_buffer)
        }
    
    def is_ready(self):
        """Check if we have enough measurements for reliable smoothing."""
        min_required = max(1, self.window_size // 2)  # At least half the window
        return (len(self.pitch_buffer) >= min_required or 
                len(self.distance_buffer) >= min_required)
    
    def reset(self):
        """Clear all buffers."""
        self.pitch_buffer.clear()
        self.roll_buffer.clear()
        self.shoulder_buffer.clear()
        self.distance_buffer.clear()
