import time
from state_debouncer import StateDebouncer
from config import GOOD_TO_BAD_FRAMES, BAD_TO_GOOD_FRAMES, INITIAL_WARNING_SECONDS, REPEAT_WARNING_INTERVAL

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
        
        # Warning thresholds (in seconds) - from config
        self.initial_warning_seconds = INITIAL_WARNING_SECONDS
        self.repeat_warning_interval = REPEAT_WARNING_INTERVAL
        
        # Add state debouncer
        # - Need 2 consecutive bad frames to start bad posture
        # - Need 3 consecutive good frames to end bad posture
        self.debouncer = StateDebouncer(
            bad_to_good_frames=BAD_TO_GOOD_FRAMES,
            good_to_bad_frames=GOOD_TO_BAD_FRAMES
        )
    
    def _generate_warning_message(self, issues, duration):
        """Generate specific warning message based on posture issues."""
        if not issues:
            return "Good posture"
        
        # Special case: If compensation detected, make it primary message
        if 'body_compensation' in issues:
            return "⚠️ Bad posture: compensating body tilt with head tilt"
        
        issue_descriptions = {
            'head_pitch': 'head tilted down',
            'distance': 'leaning too close',
            'head_roll': 'head tilted sideways',
            'shoulder_tilt': 'shoulders uneven',
            'body_lean': 'body leaning to the side'
        }
        
        messages = [issue_descriptions[issue] for issue in issues if issue in issue_descriptions]
        
        if len(messages) == 0:
            return "Bad posture detected"
        elif len(messages) == 1:
            return f"Bad posture: {messages[0]}"
        elif len(messages) == 2:
            return f"Bad posture: {messages[0]} and {messages[1]}"
        else:
            return f"Bad posture: {', '.join(messages[:-1])}, and {messages[-1]}"
        
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
                'roll': float,
                'shoulder_tilt': float,
                'distance': float,
                'posture_issues': list,
                'message': str
            }
        """
        current_time = time.time()
        
        # Get raw detection
        detected_is_bad = posture_status.get('is_bad', False)
        
        # Apply debouncing to get stable state
        is_bad = self.debouncer.update(detected_is_bad)
        
        issues = posture_status.get('posture_issues', [])
        
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
                'roll': posture_status.get('adjusted_roll'),
                'shoulder_tilt': posture_status.get('adjusted_shoulder_tilt'),
                'distance': posture_status.get('distance'),
                'posture_issues': issues,
                'message': self._generate_warning_message(issues, self.bad_posture_duration)
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
                'roll': posture_status.get('adjusted_roll'),
                'shoulder_tilt': posture_status.get('adjusted_shoulder_tilt'),
                'distance': posture_status.get('distance'),
                'posture_issues': [],
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
        self.debouncer.reset()
