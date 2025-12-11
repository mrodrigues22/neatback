"""
Download MediaPipe Pose Landmarker model.
This script downloads the pose_landmarker_heavy.task model from Google's servers.
"""

import urllib.request
import os

def download_pose_model():
    """Download the MediaPipe Pose Landmarker model."""
    # Model URL
    model_url = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/1/pose_landmarker_heavy.task"
    
    # Destination path
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_dir, 'src', 'pose_landmarker.task')
    
    print(f"Downloading MediaPipe Pose Landmarker model...")
    print(f"URL: {model_url}")
    print(f"Destination: {model_path}")
    
    try:
        # Download the file
        urllib.request.urlretrieve(model_url, model_path)
        
        # Check file size
        file_size = os.path.getsize(model_path) / (1024 * 1024)  # Size in MB
        print(f"\n✅ Download successful!")
        print(f"File size: {file_size:.2f} MB")
        print(f"Model saved to: {model_path}")
        
    except Exception as e:
        print(f"\n❌ Download failed: {e}")
        print("\nYou can manually download the model from:")
        print("https://developers.google.com/mediapipe/solutions/vision/pose_landmarker")
        print(f"Save it as: {model_path}")

if __name__ == "__main__":
    download_pose_model()
