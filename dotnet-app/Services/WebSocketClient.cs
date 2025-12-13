using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Slouti.Models;

namespace Slouti.Services;

public class WebSocketMessage
{
    public string type { get; set; } = string.Empty;
    public PostureData? data { get; set; }
    public bool success { get; set; }
    public double? good_pitch { get; set; }
    public double? good_distance { get; set; }
    public string? message { get; set; }
}

public class WebSocketClient
{
    private ClientWebSocket? _ws;
    private readonly string _uri = "ws://localhost:8765";
    
    public event EventHandler<PostureData>? PostureDataReceived;
    public event EventHandler<bool>? PostureSaved;
    public event EventHandler<bool>? ThresholdsUpdated;
    public event EventHandler? MonitoringStarted;
    
    public async Task ConnectAsync()
    {
        // Retry connection with exponential backoff to allow Python service to start
        const int maxRetries = 10;
        int retryDelayMs = 500;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(_uri), CancellationToken.None);
                _ = Task.Run(ReceiveLoop);
                return; // Success!
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    // Last attempt failed, throw the exception
                    throw new Exception("Unable to start the webcam. Please try restarting the app.", ex);
                }
                
                // Wait before retrying (exponential backoff)
                await Task.Delay(retryDelayMs);
                retryDelayMs = Math.Min(retryDelayMs * 2, 5000); // Cap at 5 seconds
            }
        }
    }
    
    private async Task ReceiveLoop()
    {
        if (_ws == null) return;
        
        var buffer = new byte[1024 * 1024]; // 1MB buffer
        
        while (_ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
                
                string json;
                if (result.EndOfMessage)
                {
                    json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }
                else
                {
                    // Message is larger than buffer, handle in chunks
                    using var ms = new MemoryStream();
                    ms.Write(buffer, 0, result.Count);
                    
                    while (!result.EndOfMessage)
                    {
                        result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer, 0, result.Count);
                    }
                    
                    json = Encoding.UTF8.GetString(ms.ToArray());
                }
                
                HandleMessage(json);
            }
            catch (Exception ex)
            {
                // Error receiving data
            }
        }
    }
    
    private void HandleMessage(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize(json, JsonContext.Default.WebSocketMessage);
            if (message == null) return;
            
            switch (message.type)
            {
                case "posture_result":
                    if (message.data != null)
                    {
                        PostureDataReceived?.Invoke(this, message.data);
                    }
                    break;
                
                case "preview_frame":
                    // Handle preview-only frames (no posture data, just video)
                    if (message.data != null)
                    {
                        PostureDataReceived?.Invoke(this, message.data);
                    }
                    break;
                
                case "posture_saved":
                    PostureSaved?.Invoke(this, message.success);
                    break;
                
                case "thresholds_updated":
                    ThresholdsUpdated?.Invoke(this, message.success);
                    break;
                
                case "monitoring_started":
                    MonitoringStarted?.Invoke(this, EventArgs.Empty);
                    break;
                
                case "monitoring_stopped":
                    break;
                
                case "error":
                    break;
            }
        }
        catch (Exception ex)
        {
            // Error handling message
        }
    }
    
    public async Task StartMonitoringAsync()
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new StartMonitoringMessage(), JsonContext.Default.StartMonitoringMessage);
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task StopMonitoringAsync()
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new StopMonitoringMessage(), JsonContext.Default.StopMonitoringMessage);
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task SaveGoodPostureAsync()
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new SavePostureMessage(), JsonContext.Default.SavePostureMessage);
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task SetThresholdsAsync(double pitchScale, double distanceScale, double headRollScale = 3.0, double shoulderTiltScale = 3.0)
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new SetThresholdsMessage
        {
            pitch_scale = pitchScale,
            distance_scale = distanceScale,
            head_roll_scale = headRollScale,
            shoulder_tilt_scale = shoulderTiltScale
        }, JsonContext.Default.SetThresholdsMessage);
        
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task GetStatisticsAsync()
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new GetStatisticsMessage(), JsonContext.Default.GetStatisticsMessage);
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    

    
    public async Task DisconnectAsync()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
}
