# Pose Detection Stability Configuration

# Smoothing Filter Settings
SMOOTHING_WINDOW_SIZE = 5  # Number of frames to average (5 = ~0.25s at 20 FPS)

# State Debouncer Settings
GOOD_TO_BAD_FRAMES = 2     # Frames needed to detect bad posture
BAD_TO_GOOD_FRAMES = 3     # Frames needed to return to good posture

# Hysteresis Thresholds (degrees or cm)
THRESHOLDS = {
    'pitch': {
        'enter_bad': -10,    # Stricter: must go below -10° to start bad
        'exit_bad': -8       # Lenient: must go above -8° to exit bad
    },
    'distance': {
        'enter_bad': 10,     # Must lean 10cm closer to start bad
        'exit_bad': 8        # Only need to be 8cm close to stay bad
    },
    'head_roll': {
        'enter_bad': 15,     # Must tilt >15° to start bad
        'exit_bad': 12       # Must be <12° to exit bad
    },
    'shoulder_tilt': {
        'enter_bad': 10,     # Must tilt >10° to start bad
        'exit_bad': 8        # Must be <8° to exit bad
    }
}

# Confidence Filtering
MIN_DETECTION_CONFIDENCE = 0.4  # Minimum confidence to trust detection (0.0-1.0)

# Warning Timing
INITIAL_WARNING_SECONDS = 10    # First warning after 10s
REPEAT_WARNING_INTERVAL = 20    # Then warn every 20s

# Processing
TARGET_FPS = 20                  # Target frame processing rate
