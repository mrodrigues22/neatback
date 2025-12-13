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
    
    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(_uri), CancellationToken.None);
        _ = Task.Run(ReceiveLoop);
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
    
    public async Task SetThresholdsAsync(double pitchThreshold, double distanceThreshold, double headRollThreshold = 15, double shoulderTiltThreshold = 10)
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new SetThresholdsMessage
        {
            pitch_threshold = pitchThreshold,
            distance_threshold = distanceThreshold,
            head_roll_threshold = headRollThreshold,
            shoulder_tilt_threshold = shoulderTiltThreshold
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
