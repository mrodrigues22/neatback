using NeatBack.Models;
using NeatBack.Services;

namespace NeatBack.Views;

public sealed partial class MainPage : Page
{
    private WebSocketClient? _wsClient;
    private NotificationService? _notificationService;
    private DateTime _badPostureStart;
    private bool _inBadPosture = false;
    
    public MainPage()
    {
        InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        _wsClient.DataReceived += OnPostureDataReceived;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_wsClient != null)
            {
                await _wsClient.ConnectAsync();
                StatusText.Text = "Monitoring...";
                StartButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }
    
    private void OnPostureDataReceived(object? sender, PostureData data)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AngleText.Text = $"Neck Angle: {data.neck_angle}°";
            StatusText.Text = data.is_good ? "Good Posture ✓" : "Bad Posture ✗";
            
            // Track bad posture duration
            if (!data.is_good)
            {
                if (!_inBadPosture)
                {
                    _badPostureStart = DateTime.Now;
                    _inBadPosture = true;
                }
                else
                {
                    var duration = (DateTime.Now - _badPostureStart).TotalSeconds;
                    if (duration > 30)
                    {
                        _notificationService?.ShowAlert("Fix your posture!");
                    }
                }
            }
            else
            {
                _inBadPosture = false;
            }
        });
    }
}
