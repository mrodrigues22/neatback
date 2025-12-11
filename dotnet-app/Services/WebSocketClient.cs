using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NeatBack.Models;

namespace NeatBack.Services;

public class WebSocketClient
{
    private ClientWebSocket? _ws;
    private readonly string _uri = "ws://localhost:8765";
    
    public event EventHandler<PostureData>? DataReceived;
    
    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(_uri), CancellationToken.None);
        _ = Task.Run(ReceiveLoop);
    }
    
    private async Task ReceiveLoop()
    {
        if (_ws == null) return;
        
        var buffer = new byte[1024];
        
        while (_ws.State == WebSocketState.Open)
        {
            var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var data = JsonSerializer.Deserialize<PostureData>(json);
            if (data != null)
            {
                DataReceived?.Invoke(this, data);
            }
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
}
