using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NeatBack.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace NeatBack.Services;

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
                System.Diagnostics.Debug.WriteLine($"Error receiving data: {ex.Message}");
            }
        }
    }
    
    private void HandleMessage(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<WebSocketMessage>(json);
            if (message == null) return;
            
            switch (message.type)
            {
                case "posture_result":
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
                
                case "error":
                    System.Diagnostics.Debug.WriteLine($"Server error: {message.message}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
        }
    }
    
    public async Task SendFrameAsync(SoftwareBitmap bitmap)
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var base64 = await ConvertBitmapToBase64(bitmap);
        
        var message = JsonSerializer.Serialize(new
        {
            type = "frame",
            frame = base64,
            timestamp_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task SaveGoodPostureAsync(SoftwareBitmap bitmap)
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var base64 = await ConvertBitmapToBase64(bitmap);
        
        var message = JsonSerializer.Serialize(new
        {
            type = "save_good_posture",
            frame = base64,
            timestamp_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    public async Task SetThresholdsAsync(double pitchThreshold, double distanceThreshold)
    {
        if (_ws?.State != WebSocketState.Open)
            return;
        
        var message = JsonSerializer.Serialize(new
        {
            type = "set_thresholds",
            pitch_threshold = pitchThreshold,
            distance_threshold = distanceThreshold
        });
        
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
        
        var message = JsonSerializer.Serialize(new { type = "get_statistics" });
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
    
    private async Task<string> ConvertBitmapToBase64(SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        
        var encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.JpegEncoderId,
            stream
        );
        
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.ScaledWidth = 640;  // Resize for efficiency
        encoder.BitmapTransform.ScaledHeight = 480;
        
        await encoder.FlushAsync();
        
        var bytes = new byte[stream.Size];
        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        
        return Convert.ToBase64String(bytes);
    }
    
    public async Task DisconnectAsync()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
}
