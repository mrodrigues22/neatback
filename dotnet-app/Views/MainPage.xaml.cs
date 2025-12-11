using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NeatBack.Models;
using NeatBack.Services;
using System;
using System.Threading;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;

namespace NeatBack.Views;

public sealed partial class MainPage : Page
{
    private WebSocketClient? _wsClient;
    private NotificationService? _notificationService;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private bool _isMonitoring = false;
    private bool _isProcessingFrame = false;
    private bool _isUpdatingPreview = false;
    private Timer? _frameTimer;
    private SoftwareBitmap? _latestBitmap;
    private SoftwareBitmapSource? _bitmapSource;
    
    public MainPage()
    {
        InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        _bitmapSource = new SoftwareBitmapSource();
        
        // Subscribe to events
        _wsClient.PostureDataReceived += OnPostureDataReceived;
        _wsClient.PostureSaved += OnPostureSaved;
        _wsClient.ThresholdsUpdated += OnThresholdsUpdated;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isMonitoring)
            {
                // Start monitoring
                StatusText.Text = "Initializing camera...";
                
                // Initialize camera
                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });
                
                System.Diagnostics.Debug.WriteLine($"Camera initialized. Found {_mediaCapture.FrameSources.Count} frame sources");
                
                // List all frame sources
                foreach (var source in _mediaCapture.FrameSources)
                {
                    System.Diagnostics.Debug.WriteLine($"  Source {source.Key}: {source.Value.Info.SourceKind}, {source.Value.Info.MediaStreamType}");
                }
                
                // Set up frame reader - prefer VideoRecord format
                var frameSource = _mediaCapture.FrameSources.Values
                    .FirstOrDefault(source => source.Info.SourceKind == MediaFrameSourceKind.Color);
                
                if (frameSource == null)
                {
                    // Try any source
                    frameSource = _mediaCapture.FrameSources.Values.FirstOrDefault();
                }
                
                if (frameSource != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Using frame source: {frameSource.Info.SourceKind}, Format: {frameSource.CurrentFormat.Subtype}");
                    
                    // Create frame reader with specific format
                    _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, frameSource.CurrentFormat.Subtype);
                    _frameReader.FrameArrived += OnFrameArrived;
                    
                    var status = await _frameReader.StartAsync();
                    System.Diagnostics.Debug.WriteLine($"Frame reader started with status: {status}");
                    
                    if (status != MediaFrameReaderStartStatus.Success)
                    {
                        StatusText.Text = $"Error: Frame reader failed to start: {status}";
                        return;
                    }
                }
                else
                {
                    StatusText.Text = "Error: No camera frame source found";
                    return;
                }
                
                // Connect to WebSocket
                await _wsClient!.ConnectAsync();
                
                // Start periodic frame sending (every 1 second)
                _frameTimer = new Timer(SendFrameCallback, null, 1000, 1000);
                
                _isMonitoring = true;
                StartButton.Content = "Stop Monitoring";
                SavePostureButton.IsEnabled = true;
                StatusText.Text = "Monitoring started. Camera should appear above. Please save your good posture!";
                
                System.Diagnostics.Debug.WriteLine("Monitoring started successfully");
            }
            else
            {
                // Stop monitoring
                await StopMonitoring();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error starting monitoring: {ex}");
        }
    }
    
    private async Task StopMonitoring()
    {
        _isMonitoring = false;
        
        // Stop frame timer
        _frameTimer?.Dispose();
        _frameTimer = null;
        
        // Stop frame reader
        if (_frameReader != null)
        {
            await _frameReader.StopAsync();
            _frameReader.FrameArrived -= OnFrameArrived;
            _frameReader.Dispose();
            _frameReader = null;
        }
        
        // Stop camera
        if (_mediaCapture != null)
        {
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }
        
        // Disconnect WebSocket
        if (_wsClient != null)
        {
            await _wsClient.DisconnectAsync();
        }
        
        StartButton.Content = "Start Monitoring";
        SavePostureButton.IsEnabled = false;
        StatusText.Text = "Monitoring stopped";
        PitchText.Text = "--°";
        DistanceText.Text = "-- cm";
        BadDurationText.Text = "0 s";
        BadPostureProgress.Value = 0;
        BadPostureProgress.Visibility = Visibility.Collapsed;
    }
    
    private int _frameCount = 0;
    
    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // Throttle preview updates
        if (_isUpdatingPreview)
            return;

        var frame = sender.TryAcquireLatestFrame();
        if (frame?.VideoMediaFrame?.SoftwareBitmap == null)
        {
            frame?.Dispose();
            System.Diagnostics.Debug.WriteLine("Frame arrived but no bitmap");
            return;
        }

        _frameCount++;
        if (_frameCount % 30 == 1)  // Log every 30 frames
        {
            System.Diagnostics.Debug.WriteLine($"Frame {_frameCount} arrived");
        }

        var softwareBitmap = frame.VideoMediaFrame.SoftwareBitmap;
        
        // Convert to supported format
        SoftwareBitmap convertedBitmap;
        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            convertedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            System.Diagnostics.Debug.WriteLine($"Converted bitmap from {softwareBitmap.BitmapPixelFormat} to Bgra8");
        }
        else
        {
            convertedBitmap = SoftwareBitmap.Copy(softwareBitmap);
        }
        
        frame.Dispose();
        
        // Store for WebSocket processing
        var oldBitmap = Interlocked.Exchange(ref _latestBitmap, convertedBitmap);
        
        // Update preview on UI thread
        _isUpdatingPreview = true;
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Updating preview, bitmap size: {convertedBitmap.PixelWidth}x{convertedBitmap.PixelHeight}");
                if (_bitmapSource != null && convertedBitmap != null)
                {
                    await _bitmapSource.SetBitmapAsync(convertedBitmap);
                    CameraPreview.Source = _bitmapSource;
                    System.Diagnostics.Debug.WriteLine("Preview updated successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot update: _bitmapSource={_bitmapSource != null}, convertedBitmap={convertedBitmap != null}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating preview: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _isUpdatingPreview = false;
                oldBitmap?.Dispose();
            }
        });
    }
    
    private async void SendFrameCallback(object? state)
    {
        if (_isProcessingFrame || !_isMonitoring)
            return;
        
        var bitmap = _latestBitmap;
        if (bitmap == null)
            return;
        
        _isProcessingFrame = true;
        
        try
        {
            await _wsClient!.SendFrameAsync(bitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending frame: {ex.Message}");
        }
        finally
        {
            _isProcessingFrame = false;
        }
    }
    
    private async void SavePostureButton_Click(object sender, RoutedEventArgs e)
    {
        var bitmap = _latestBitmap;
        if (bitmap != null && _wsClient != null)
        {
            StatusText.Text = "Saving good posture...";
            await _wsClient.SaveGoodPostureAsync(bitmap);
        }
    }
    
    private void OnPostureDataReceived(object? sender, PostureData data)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Update metrics
            if (data.AdjustedPitch.HasValue)
            {
                PitchText.Text = $"{data.AdjustedPitch.Value:F1}°";
            }
            
            if (data.Distance.HasValue)
            {
                DistanceText.Text = $"{data.Distance.Value:F1} cm";
            }
            
            BadDurationText.Text = $"{data.BadDuration} s";
            
            // Update status
            if (!string.IsNullOrEmpty(data.Error))
            {
                StatusText.Text = data.Error;
            }
            else if (data.IsBad)
            {
                StatusText.Text = $"⚠️ {data.Message}";
                BadPostureProgress.Visibility = Visibility.Visible;
                BadPostureProgress.Value = Math.Min(data.BadDuration, 30);
            }
            else
            {
                StatusText.Text = "✅ Good posture";
                BadPostureProgress.Visibility = Visibility.Collapsed;
                BadPostureProgress.Value = 0;
            }
            
            // Send notification if needed
            if (data.ShouldWarn)
            {
                _notificationService?.ShowAlert($"Bad posture for {data.BadDuration} seconds! Please adjust.");
            }
        });
    }
    
    private void OnPostureSaved(object? sender, bool success)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (success)
            {
                StatusText.Text = "✅ Good posture saved! Monitoring...";
            }
            else
            {
                StatusText.Text = "❌ Failed to save posture. Make sure your face is visible.";
            }
        });
    }
    
    private void OnThresholdsUpdated(object? sender, bool success)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (success)
            {
                StatusText.Text = "Thresholds updated";
            }
        });
    }
    
    private async void ThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isMonitoring)
            return;
        
        var pitchThreshold = PitchThresholdSlider.Value;
        var distanceThreshold = DistanceThresholdSlider.Value;
        
        PitchThresholdValue.Text = $"{pitchThreshold:F0}°";
        DistanceThresholdValue.Text = $"{distanceThreshold:F0} cm";
        
        if (_wsClient != null)
        {
            await _wsClient.SetThresholdsAsync(pitchThreshold, distanceThreshold);
        }
    }
}
