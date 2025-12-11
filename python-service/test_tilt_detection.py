"""
Test script to verify head and body tilt detection implementation.
Tests all new functionality including roll angle extraction and shoulder tilt detection.
"""

import sys
import os
import cv2
import numpy as np
import time

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src'))

from pose_detector import PostureDetector
from posture_analyzer import PostureAnalyzer

def test_detector_initialization():
    """Test that detector initializes with all new features."""
    print("=" * 60)
    print("TEST 1: Detector Initialization")
    print("=" * 60)
    
    detector = PostureDetector()
    
    # Check that new properties exist
    assert hasattr(detector, 'good_head_roll'), "Missing good_head_roll property"
    assert hasattr(detector, 'good_shoulder_tilt'), "Missing good_shoulder_tilt property"
    assert hasattr(detector, 'head_roll_threshold'), "Missing head_roll_threshold property"
    assert hasattr(detector, 'shoulder_tilt_threshold'), "Missing shoulder_tilt_threshold property"
    assert hasattr(detector, 'pose_landmarker'), "Missing pose_landmarker property"
    
    # Check default values
    assert detector.head_roll_threshold == 15, f"Expected head_roll_threshold=15, got {detector.head_roll_threshold}"
    assert detector.shoulder_tilt_threshold == 10, f"Expected shoulder_tilt_threshold=10, got {detector.shoulder_tilt_threshold}"
    
    print("✅ Detector has all new properties with correct defaults")
    print(f"   - good_head_roll: {detector.good_head_roll}")
    print(f"   - good_shoulder_tilt: {detector.good_shoulder_tilt}")
    print(f"   - head_roll_threshold: {detector.head_roll_threshold}°")
    print(f"   - shoulder_tilt_threshold: {detector.shoulder_tilt_threshold}°")
    print(f"   - pose_landmarker available: {detector.pose_landmarker is not None}")
    
    detector.close()
    print("\n")
    return True

def test_euler_angles_extraction():
    """Test Euler angles extraction from rotation matrix."""
    print("=" * 60)
    print("TEST 2: Euler Angles Extraction")
    print("=" * 60)
    
    detector = PostureDetector()
    
    # Test with identity matrix (no rotation)
    R_identity = np.eye(3)
    pitch, yaw, roll = detector._rotation_matrix_to_euler_angles(R_identity)
    
    print(f"Identity matrix (no rotation):")
    print(f"   Pitch: {pitch:.2f}°, Yaw: {yaw:.2f}°, Roll: {roll:.2f}°")
    assert abs(pitch) < 1, f"Expected pitch≈0, got {pitch}"
    assert abs(yaw) < 1, f"Expected yaw≈0, got {yaw}"
    assert abs(roll) < 1, f"Expected roll≈0, got {roll}"
    print("✅ Identity matrix returns ~0° for all angles")
    
    # Test with 15° roll (right tilt)
    angle_rad = np.radians(15)
    R_roll = np.array([
        [np.cos(angle_rad), -np.sin(angle_rad), 0],
        [np.sin(angle_rad), np.cos(angle_rad), 0],
        [0, 0, 1]
    ])
    pitch, yaw, roll = detector._rotation_matrix_to_euler_angles(R_roll)
    
    print(f"\n15° roll rotation matrix:")
    print(f"   Pitch: {pitch:.2f}°, Yaw: {yaw:.2f}°, Roll: {roll:.2f}°")
    assert abs(roll - 15) < 1, f"Expected roll≈15°, got {roll}"
    print("✅ 15° roll rotation detected correctly")
    
    detector.close()
    print("\n")
    return True

def test_check_posture_output():
    """Test that check_posture returns all new fields."""
    print("=" * 60)
    print("TEST 3: Check Posture Output Structure")
    print("=" * 60)
    
    detector = PostureDetector()
    
    # Create a dummy frame
    frame = np.zeros((480, 640, 3), dtype=np.uint8)
    timestamp_ms = int(time.time() * 1000)
    
    result = detector.check_posture(frame, timestamp_ms)
    
    # Check all required fields exist
    required_fields = [
        'is_bad', 'pitch_angle', 'roll_angle', 'shoulder_tilt',
        'distance', 'adjusted_pitch', 'adjusted_roll', 
        'adjusted_shoulder_tilt', 'posture_issues'
    ]
    
    for field in required_fields:
        assert field in result, f"Missing field: {field}"
        print(f"✅ Field '{field}' present: {result[field]}")
    
    # Check that posture_issues is a list
    assert isinstance(result['posture_issues'], list), "posture_issues should be a list"
    print(f"✅ posture_issues is a list: {result['posture_issues']}")
    
    detector.close()
    print("\n")
    return True

def test_posture_analyzer_messages():
    """Test enhanced warning message generation."""
    print("=" * 60)
    print("TEST 4: Enhanced Warning Messages")
    print("=" * 60)
    
    analyzer = PostureAnalyzer()
    
    # Test single issue
    message = analyzer._generate_warning_message(['head_pitch'], 5)
    print(f"Single issue: '{message}'")
    assert 'head tilted down' in message, "Should mention head tilted down"
    print("✅ Single issue message correct")
    
    # Test two issues
    message = analyzer._generate_warning_message(['head_pitch', 'head_roll'], 10)
    print(f"Two issues: '{message}'")
    assert 'head tilted down' in message and 'head tilted sideways' in message, "Should mention both issues"
    print("✅ Two issue message correct")
    
    # Test multiple issues
    message = analyzer._generate_warning_message(['head_pitch', 'distance', 'head_roll', 'shoulder_tilt'], 15)
    print(f"Four issues: '{message}'")
    assert all(word in message for word in ['down', 'close', 'sideways', 'uneven']), "Should mention all issues"
    print("✅ Multiple issue message correct")
    
    print("\n")
    return True

def test_posture_issues_detection():
    """Test that _is_posture_bad detects specific issues."""
    print("=" * 60)
    print("TEST 5: Posture Issues Detection")
    print("=" * 60)
    
    detector = PostureDetector()
    
    # Test head pitch issue
    is_bad, issues = detector._is_posture_bad(-15, 0, 0, 50, 60)
    assert 'head_pitch' in issues, "Should detect head_pitch issue"
    print(f"✅ Head pitch issue detected: {issues}")
    
    # Test distance issue
    is_bad, issues = detector._is_posture_bad(0, 0, 0, 40, 55)
    assert 'distance' in issues, "Should detect distance issue"
    print(f"✅ Distance issue detected: {issues}")
    
    # Test head roll issue
    is_bad, issues = detector._is_posture_bad(0, 20, 0, 50, 50)
    assert 'head_roll' in issues, "Should detect head_roll issue"
    print(f"✅ Head roll issue detected: {issues}")
    
    # Test shoulder tilt issue
    is_bad, issues = detector._is_posture_bad(0, 0, 15, 50, 50)
    assert 'shoulder_tilt' in issues, "Should detect shoulder_tilt issue"
    print(f"✅ Shoulder tilt issue detected: {issues}")
    
    # Test multiple issues
    is_bad, issues = detector._is_posture_bad(-15, 20, 15, 40, 55)
    assert len(issues) == 4, f"Should detect 4 issues, got {len(issues)}"
    print(f"✅ Multiple issues detected: {issues}")
    
    # Test no issues (good posture)
    is_bad, issues = detector._is_posture_bad(0, 0, 0, 50, 50)
    assert len(issues) == 0, "Should detect no issues"
    assert is_bad == False, "Should not be bad posture"
    print(f"✅ Good posture detected (no issues): {issues}")
    
    detector.close()
    print("\n")
    return True

def run_all_tests():
    """Run all tests."""
    print("\n" + "=" * 60)
    print("HEAD AND BODY TILT DETECTION - TEST SUITE")
    print("=" * 60)
    print("\n")
    
    tests = [
        test_detector_initialization,
        test_euler_angles_extraction,
        test_check_posture_output,
        test_posture_analyzer_messages,
        test_posture_issues_detection
    ]
    
    passed = 0
    failed = 0
    
    for test in tests:
        try:
            if test():
                passed += 1
        except Exception as e:
            print(f"❌ TEST FAILED: {e}")
            import traceback
            traceback.print_exc()
            failed += 1
            print("\n")
    
    print("=" * 60)
    print("TEST RESULTS")
    print("=" * 60)
    print(f"✅ Passed: {passed}/{len(tests)}")
    if failed > 0:
        print(f"❌ Failed: {failed}/{len(tests)}")
    else:
        print("🎉 All tests passed!")
    print("=" * 60)
    
    return failed == 0

if __name__ == "__main__":
    success = run_all_tests()
    sys.exit(0 if success else 1)
