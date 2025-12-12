# Python Service Setup Guide

## Prerequisites

- Python 3.8 or higher
- pip (Python package installer)

## Installation Steps

### 1. Install Python Dependencies

Navigate to the `python-service` directory and install the required packages:

```bash
cd python-service
pip install -r requirements.txt
```

### 2. Download MediaPipe Face Landmarker Model

The posture detection system requires the MediaPipe Face Landmarker model file.

**Download URL:** https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task

**Option A: Manual Download**
1. Download the `face_landmarker.task` file from the URL above
2. Place it in the `python-service/src/` directory
3. The file should be located at: `python-service/src/face_landmarker.task`

**Option B: Using PowerShell (Windows)**
```powershell
cd python-service\src
Invoke-WebRequest -Uri "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task" -OutFile "face_landmarker.task"
```

**Option C: Using curl (Linux/Mac/Windows with curl)**
```bash
cd python-service/src
curl -L -o face_landmarker.task "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"
```

### 3. Verify Setup

After downloading the model, your directory structure should look like:

```
python-service/
├── src/
│   ├── face_landmarker.task  ← Model file should be here
│   ├── main.py
│   ├── pose_detector.py
│   ├── posture_analyzer.py
│   └── websocket_server.py
├── requirements.txt
└── SETUP.md
```

### 4. Run the Service

Start the WebSocket server:

```bash
cd python-service/src
python main.py
```

You should see:
```
============================================================
Slouti Posture Analysis Service
============================================================
Starting WebSocket server...
The service will capture frames directly from your webcam.

Waiting for client connection...
============================================================
WebSocket server starting on ws://localhost:8765
```

## Troubleshooting

### "No module named 'mediapipe'"
- Run: `pip install -r requirements.txt`

### "face_landmarker.task not found"
- Make sure the model file is in the `python-service/src/` directory
- Verify the filename is exactly `face_landmarker.task`

### "Address already in use" error
- Another process is using port 8765
- Stop any existing Python services or change the port in `websocket_server.py`

### Camera/Face Detection Issues
- Ensure good lighting conditions
- Face should be clearly visible to the camera
- Try adjusting camera angle or distance

## Model Information

- **Model**: MediaPipe Face Landmarker (Float16)
- **Version**: 1
- **Size**: ~10 MB
- **Landmarks**: 478 facial landmarks
- **License**: Apache 2.0

## Configuration

Default thresholds can be adjusted in the UI or programmatically:
- **Pitch Threshold**: -10 degrees (how much you look down)
- **Distance Threshold**: 10 cm (how close you lean forward)

These thresholds determine when posture is considered "bad".
