# NeatBack - Quick Start Guide

## 🚀 Get Started in 5 Minutes

### Step 1: Setup Python Service (One-Time)

Open PowerShell in the project root and run:

```powershell
.\setup-python-service.ps1
```

This will automatically install dependencies and download the required model file.

### Step 2: Start the Python Service

```powershell
cd python-service\src
python main.py
```

**Keep this window open!** You should see:
```
============================================================
NeatBack Posture Analysis Service
============================================================
WebSocket server starting on ws://localhost:8765
```

### Step 3: Build and Run the WinUI App

Open a new PowerShell window:

```powershell
cd dotnet-app
dotnet build
dotnet run
```

### Step 4: Use the Application

1. Click **"Start Monitoring"** - allows camera access if prompted
2. Position yourself in **good posture** (sit up straight)
3. Click **"Save Good Posture"** - this saves your baseline
4. Continue working normally
5. You'll receive alerts if your posture gets bad

## 📊 What You'll See

- **Head Pitch**: Shows how much you're tilting your head (negative = looking down)
- **Distance**: How far you are from the camera in centimeters
- **Bad Duration**: Timer showing how long you've been slouching
- **Status Messages**: Real-time feedback on your posture

## ⚙️ Adjusting Settings

Use the sliders in the app to customize:
- **Pitch Threshold**: How sensitive to head tilt (-30° to 0°, default: -10°)
- **Distance Threshold**: How sensitive to leaning forward (5-20 cm, default: 10 cm)

## 🔔 Notifications

- First warning: **5 seconds** of bad posture
- Repeat warnings: Every **20 seconds** thereafter
- Windows notifications appear in bottom-right corner

## ❓ Common Issues

**"face_landmarker.task not found"**
→ Run `.\setup-python-service.ps1` to download the model

**"Can't connect to Python service"**
→ Make sure Python service is running first (Step 2)

**"No face detected"**
→ Improve lighting and ensure your face is clearly visible

**Camera permission denied**
→ Check Windows Settings → Privacy → Camera

## 💡 Tips for Best Results

- Sit 50-70 cm from camera
- Use good, even lighting
- Position camera at eye level
- Keep face clearly visible (no obstructions)
- Calibrate in your actual working posture

## 🎯 Understanding Bad Posture

The app detects slouching in two ways:

1. **Looking Down**: Head tilted more than 10° below your baseline
2. **Leaning Forward**: Moving more than 10 cm closer to camera

Both thresholds can be adjusted to your preference!

## 🛑 Stopping the App

1. Click "Stop Monitoring" in the WinUI app
2. Close the app window
3. Press `Ctrl+C` in the Python service terminal

## 📚 Need More Help?

- Read the full [README_NEW.md](README_NEW.md)
- Check [POSTURE_ANALYSIS_IMPLEMENTATION_GUIDE.md](POSTURE_ANALYSIS_IMPLEMENTATION_GUIDE.md)
- Review [python-service/SETUP.md](python-service/SETUP.md)

---

**Enjoy better posture! 🙂**
