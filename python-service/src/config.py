# Pose Detection Stability Configuration

# Smoothing Filter Settings
SMOOTHING_WINDOW_SIZE = 5  # Number of frames to average (5 = ~0.17s at 30 FPS)

# State Debouncer Settings
GOOD_TO_BAD_FRAMES = 2     # Frames needed to detect bad posture
BAD_TO_GOOD_FRAMES = 3     # Frames needed to return to good posture

# Sensitivity Scale Functions (1.0-5.0 continuous scale: 1=Low/Lenient, 5=High/Strict)
def _interpolate_threshold(scale: float, mapping: dict) -> tuple[float, float]:
    """Interpolate threshold values between discrete scale points"""
    # Clamp scale to valid range
    scale = max(1.0, min(5.0, scale))
    
    # If scale is exactly an integer, return the exact mapping
    if scale == int(scale):
        return mapping[int(scale)]
    
    # Interpolate between two adjacent integer values
    lower = int(scale)
    upper = lower + 1
    fraction = scale - lower
    
    lower_enter, lower_exit = mapping[lower]
    upper_enter, upper_exit = mapping[upper]
    
    # Linear interpolation
    enter_bad = lower_enter + (upper_enter - lower_enter) * fraction
    exit_bad = lower_exit + (upper_exit - lower_exit) * fraction
    
    return (enter_bad, exit_bad)

def scale_to_pitch_threshold(scale: float) -> tuple[float, float]:
    """Convert 1.0-5.0 continuous scale to pitch threshold (enter_bad, exit_bad)"""
    mapping = {
        1: (-20, -16),  # Very lenient - rarely triggers
        2: (-15, -12),  # Lenient - only severe forward head
        3: (-10, -8),   # Medium - balanced (default)
        4: (-7, -5),    # Strict - triggers easily
        5: (-5, -3)     # Very strict - very sensitive
    }
    return _interpolate_threshold(scale, mapping)

def scale_to_distance_threshold(scale: float) -> tuple[float, float]:
    """Convert 1.0-5.0 continuous scale to distance threshold in cm (enter_bad, exit_bad)"""
    mapping = {
        1: (15, 12),    # Very lenient - must lean far forward
        2: (12, 10),    # Lenient
        3: (10, 8),     # Medium - balanced (default)
        4: (8, 6),      # Strict
        5: (6, 4)       # Very strict - very sensitive
    }
    return _interpolate_threshold(scale, mapping)

def scale_to_head_roll_threshold(scale: float) -> tuple[float, float]:
    """Convert 1.0-5.0 continuous scale to head roll threshold in degrees (enter_bad, exit_bad)"""
    mapping = {
        1: (25, 20),    # Very lenient - only extreme tilts
        2: (20, 16),    # Lenient
        3: (15, 12),    # Medium - balanced (default)
        4: (12, 9),     # Strict
        5: (1, 0.5)     # Very strict - very sensitive (triggers at ±1°)
    }
    return _interpolate_threshold(scale, mapping)

def scale_to_shoulder_tilt_threshold(scale: float) -> tuple[float, float]:
    """Convert 1.0-5.0 continuous scale to shoulder tilt threshold in degrees (enter_bad, exit_bad)"""
    mapping = {
        1: (10, 7),     # Very lenient - only major imbalances
        2: (7, 5),      # Lenient
        3: (5, 3),      # Medium - balanced (default)
        4: (4, 2),      # Strict
        5: (1, 0.5)     # Very strict - very sensitive (triggers at ±1°)
    }
    return _interpolate_threshold(scale, mapping)

# Default sensitivity scales (1-5, where 3 is medium/default)
DEFAULT_SCALES = {
    'pitch': 3,
    'distance': 3,
    'head_roll': 3,
    'shoulder_tilt': 3
}

# Hysteresis Thresholds (degrees or cm) - initialized with defaults
THRESHOLDS = {
    'pitch': {
        'enter_bad': -10,
        'exit_bad': -8
    },
    'distance': {
        'enter_bad': 10,
        'exit_bad': 8
    },
    'head_roll': {
        'enter_bad': 15,
        'exit_bad': 12
    },
    'shoulder_tilt': {
        'enter_bad': 5,
        'exit_bad': 3
    },
    'body_lean': {
        'enter_bad': 3.0,    # Shoulder offset > 3% of frame width to start bad
        'exit_bad': 2.0      # Must be < 2% to exit bad
    }
}

# Update thresholds from scale function
def update_thresholds_from_scales(pitch_scale: float = 3.0, distance_scale: float = 3.0, 
                                   head_roll_scale: float = 3.0, shoulder_tilt_scale: float = 3.0):
    """Update THRESHOLDS dict based on sensitivity scales (accepts float values 1.0-5.0)"""
    pitch_enter, pitch_exit = scale_to_pitch_threshold(pitch_scale)
    dist_enter, dist_exit = scale_to_distance_threshold(distance_scale)
    roll_enter, roll_exit = scale_to_head_roll_threshold(head_roll_scale)
    shoulder_enter, shoulder_exit = scale_to_shoulder_tilt_threshold(shoulder_tilt_scale)
    
    THRESHOLDS['pitch']['enter_bad'] = pitch_enter
    THRESHOLDS['pitch']['exit_bad'] = pitch_exit
    THRESHOLDS['distance']['enter_bad'] = dist_enter
    THRESHOLDS['distance']['exit_bad'] = dist_exit
    THRESHOLDS['head_roll']['enter_bad'] = roll_enter
    THRESHOLDS['head_roll']['exit_bad'] = roll_exit
    THRESHOLDS['shoulder_tilt']['enter_bad'] = shoulder_enter
    THRESHOLDS['shoulder_tilt']['exit_bad'] = shoulder_exit

# Confidence Filtering
MIN_DETECTION_CONFIDENCE = 0.4  # Minimum confidence to trust detection (0.0-1.0)

# Warning Timing
INITIAL_WARNING_SECONDS = 10    # First warning after 10s
REPEAT_WARNING_INTERVAL = 20    # Then warn every 20s

# Processing
TARGET_FPS = 30                  # Target frame processing rate
