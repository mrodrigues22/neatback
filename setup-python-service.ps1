# Slouti Python Service Setup Script
# This script installs dependencies and downloads the required MediaPipe model

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Slouti Python Service Setup" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Check if Python 3.11 is installed
Write-Host "Checking Python 3.11 installation..." -ForegroundColor Yellow
try {
    $pythonVersion = py -3.11 --version 2>&1
    Write-Host "Found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "Python 3.11 not found! MediaPipe requires Python 3.11 or 3.12." -ForegroundColor Red
    Write-Host "  Download Python 3.11 from: https://www.python.org/downloads/" -ForegroundColor Yellow
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

# Create virtual environment with Python 3.11 if it doesn't exist
if (-not (Test-Path "venv")) {
    Write-Host "Creating virtual environment with Python 3.11..." -ForegroundColor Yellow
    py -3.11 -m venv venv
    Write-Host "Virtual environment created" -ForegroundColor Green
}

# Activate virtual environment
Write-Host "Activating virtual environment..." -ForegroundColor Yellow
& ".\venv\Scripts\Activate.ps1"

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
