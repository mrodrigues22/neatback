import time

class StateDebouncer:
    """
    Prevents rapid state transitions by requiring consistent
    state detection over multiple frames before changing state.
    """
    def __init__(self, 
                 bad_to_good_frames=3,    # Frames needed to transition bad → good
                 good_to_bad_frames=2):    # Frames needed to transition good → bad
        """
        Args:
            bad_to_good_frames: Consecutive good frames needed to exit bad posture
            good_to_bad_frames: Consecutive bad frames needed to enter bad posture
        """
        self.bad_to_good_frames = bad_to_good_frames
        self.good_to_bad_frames = good_to_bad_frames
        
        # Current stable state
        self.current_state = 'good'  # 'good' or 'bad'
        
        # Transition tracking
        self.consecutive_good_frames = 0
        self.consecutive_bad_frames = 0
        
        # Timestamp tracking
        self.last_update = time.time()
    
    def update(self, detected_is_bad):
        """
        Update debouncer with new detection.
        
        Args:
            detected_is_bad: bool - what the current frame detected
            
        Returns:
            bool: The stable debounced state (is_bad)
        """
        current_time = time.time()
        
        # Reset counters if too much time has passed (>1 second gap)
        if current_time - self.last_update > 1.0:
            self.consecutive_good_frames = 0
            self.consecutive_bad_frames = 0
        
        self.last_update = current_time
        
        # Update consecutive frame counts
        if detected_is_bad:
            self.consecutive_bad_frames += 1
            self.consecutive_good_frames = 0
        else:
            self.consecutive_good_frames += 1
            self.consecutive_bad_frames = 0
        
        # State transition logic
        if self.current_state == 'good':
            # Need consecutive bad frames to transition to bad
            if self.consecutive_bad_frames >= self.good_to_bad_frames:
                self.current_state = 'bad'
        else:  # current_state == 'bad'
            # Need consecutive good frames to transition to good
            if self.consecutive_good_frames >= self.bad_to_good_frames:
                self.current_state = 'good'
        
        return self.current_state == 'bad'
    
    def get_transition_progress(self):
        """Get current progress toward state transition (for debugging/UI)."""
        if self.current_state == 'good':
            return {
                'current_state': 'good',
                'transitioning_to': 'bad' if self.consecutive_bad_frames > 0 else None,
                'progress': self.consecutive_bad_frames,
                'required': self.good_to_bad_frames
            }
        else:
            return {
                'current_state': 'bad',
                'transitioning_to': 'good' if self.consecutive_good_frames > 0 else None,
                'progress': self.consecutive_good_frames,
                'required': self.bad_to_good_frames
            }
    
    def reset(self):
        """Reset to initial state."""
        self.current_state = 'good'
        self.consecutive_good_frames = 0
        self.consecutive_bad_frames = 0
    
    def force_state(self, is_bad):
        """Force a specific state (used when user saves good posture)."""
        self.current_state = 'bad' if is_bad else 'good'
        self.consecutive_good_frames = 0
        self.consecutive_bad_frames = 0
