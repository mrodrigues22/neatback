using System.Text.Json.Serialization;

namespace NeatBack.Models;

public class PostureData
{
    [JsonPropertyName("is_bad")]
    public bool IsBad { get; set; }
    
    [JsonPropertyName("pitch_angle")]
    public double? PitchAngle { get; set; }
    
    [JsonPropertyName("adjusted_pitch")]
    public double? AdjustedPitch { get; set; }
    
    [JsonPropertyName("distance")]
    public double? Distance { get; set; }
    
    [JsonPropertyName("bad_duration")]
    public int BadDuration { get; set; }
    
    [JsonPropertyName("should_warn")]
    public bool ShouldWarn { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("frame")]
    public string? Frame { get; set; }
}
