import time

class PostureAnalyzer:
    def __init__(self):
        self.bad_posture_start = None
        self.bad_posture_duration = 0
        self.warning_sent_at = set()  # Track which durations we've warned at
        
        # Statistics tracking
        self.total_bad_duration = 0
        self.longest_bad_streak = 0
        self.longest_good_streak = 0
        self.good_posture_start = time.time()
        
        # Warning thresholds (in seconds)
        self.initial_warning_seconds = 5
        self.repeat_warning_interval = 20
        
    def update(self, posture_status):
        """
        Update analyzer with new posture status.
        
        Args:
            posture_status: dict from PostureDetector.check_posture()
            
        Returns:
            dict: {
                'should_warn': bool,
                'bad_duration': int (seconds),
                'pitch': float,
                'distance': float,
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
                
                # Update good posture streak
                good_duration = current_time - self.good_posture_start
                if good_duration > self.longest_good_streak:
                    self.longest_good_streak = good_duration
                
                # Reset warning tracking for new bad posture session
                self.warning_sent_at.clear()
            
            # Calculate duration
            self.bad_posture_duration = int(current_time - self.bad_posture_start)
            
            # Check if should warn
            should_warn = self._should_send_warning(self.bad_posture_duration)
            
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
                # Bad posture session just ended
                self.total_bad_duration += self.bad_posture_duration
                
                # Update longest bad streak
                if self.bad_posture_duration > self.longest_bad_streak:
                    self.longest_bad_streak = self.bad_posture_duration
                
                # Reset bad posture tracking
                self.bad_posture_start = None
                self.bad_posture_duration = 0
                self.warning_sent_at.clear()
                self.good_posture_start = current_time
            
            return {
                'should_warn': False,
                'bad_duration': 0,
                'pitch': posture_status.get('adjusted_pitch'),
                'distance': posture_status.get('distance'),
                'message': "Good posture"
            }
    
    def _should_send_warning(self, duration):
        """
        Determine if warning should be sent based on duration.
        Warns at 5 seconds, then every 20 seconds after (25, 45, 65, etc.)
        """
        # First warning at initial threshold
        if duration == self.initial_warning_seconds and duration not in self.warning_sent_at:
            self.warning_sent_at.add(duration)
            return True
        
        # Subsequent warnings every repeat_warning_interval seconds after initial
        if duration > self.initial_warning_seconds:
            # Check if we're at a warning interval
            elapsed_since_initial = duration - self.initial_warning_seconds
            if elapsed_since_initial % self.repeat_warning_interval == 0:
                if duration not in self.warning_sent_at:
                    self.warning_sent_at.add(duration)
                    return True
        
        return False
    
    def get_statistics(self):
        """Get current session statistics."""
        current_good_duration = 0
        if self.bad_posture_start is None and self.good_posture_start:
            current_good_duration = int(time.time() - self.good_posture_start)
        
        return {
            'total_bad_duration': int(self.total_bad_duration),
            'current_bad_duration': self.bad_posture_duration,
            'longest_bad_streak': int(self.longest_bad_streak),
            'longest_good_streak': int(self.longest_good_streak),
            'current_good_duration': current_good_duration
        }
    
    def reset_statistics(self):
        """Reset all statistics."""
        self.total_bad_duration = 0
        self.longest_bad_streak = 0
        self.longest_good_streak = 0
        self.bad_posture_start = None
        self.bad_posture_duration = 0
        self.good_posture_start = time.time()
        self.warning_sent_at.clear()
