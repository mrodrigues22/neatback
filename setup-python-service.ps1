# NeatBack Python Service Setup Script
# This script installs dependencies and downloads the required MediaPipe model

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "NeatBack Python Service Setup" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Check if Python is installed
Write-Host "Checking Python installation..." -ForegroundColor Yellow
try {
    $pythonVersion = python --version 2>&1
    Write-Host "Found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "Python not found! Please install Python 3.8 or higher." -ForegroundColor Red
    Write-Host "  Download from: https://www.python.org/downloads/" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Navigate to python-service directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$servicePath = Join-Path $scriptPath "python-service"

if (-not (Test-Path $servicePath)) {
    Write-Host "python-service directory not found!" -ForegroundColor Red
    exit 1
}

Set-Location $servicePath

# Install Python dependencies
Write-Host "Installing Python dependencies..." -ForegroundColor Yellow
Write-Host "Running: pip install -r requirements.txt" -ForegroundColor Gray
try {
    pip install -r requirements.txt
    Write-Host "Dependencies installed successfully" -ForegroundColor Green
} catch {
    Write-Host "Failed to install dependencies" -ForegroundColor Red
    Write-Host "  Try running: pip install -r requirements.txt manually" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Download MediaPipe model
$modelPath = Join-Path $servicePath "src\face_landmarker.task"
$modelUrl = "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"

if (Test-Path $modelPath) {
    Write-Host "MediaPipe model already exists at:" -ForegroundColor Green
    Write-Host "  $modelPath" -ForegroundColor Gray
    
    $response = Read-Host "Do you want to re-download it? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "Skipping model download." -ForegroundColor Yellow
    } else {
        Write-Host "Downloading MediaPipe Face Landmarker model..." -ForegroundColor Yellow
        try {
            Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath -UseBasicParsing
            Write-Host "Model downloaded successfully" -ForegroundColor Green
        } catch {
            Write-Host "Failed to download model" -ForegroundColor Red
            Write-Host "  You can download it manually from:" -ForegroundColor Yellow
            Write-Host "  $modelUrl" -ForegroundColor Gray
            exit 1
        }
    }
} else {
    Write-Host "Downloading MediaPipe Face Landmarker model..." -ForegroundColor Yellow
    Write-Host "URL: $modelUrl" -ForegroundColor Gray
    Write-Host "Destination: $modelPath" -ForegroundColor Gray
    
    try {
        Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath -UseBasicParsing
        $fileSize = (Get-Item $modelPath).Length / 1MB
        Write-Host "Model downloaded successfully (~$($fileSize.ToString('F1')) MB)" -ForegroundColor Green
    } catch {
        Write-Host "Failed to download model" -ForegroundColor Red
        Write-Host "  You can download it manually from:" -ForegroundColor Yellow
        Write-Host "  $modelUrl" -ForegroundColor Gray
        Write-Host "  Place it at: $modelPath" -ForegroundColor Gray
        exit 1
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To start the service, run:" -ForegroundColor Yellow
Write-Host "  cd python-service\src" -ForegroundColor White
Write-Host "  python main.py" -ForegroundColor White
Write-Host ""
Write-Host "The service will listen on ws://localhost:8765" -ForegroundColor Gray
Write-Host ""
