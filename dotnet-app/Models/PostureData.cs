using System.Text.Json.Serialization;

namespace Slouti.Models;

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
    
    [JsonPropertyName("roll_angle")]
    public double? RollAngle { get; set; }
    
    [JsonPropertyName("adjusted_roll")]
    public double? AdjustedRoll { get; set; }
    
    [JsonPropertyName("shoulder_tilt")]
    public double? ShoulderTilt { get; set; }
    
    [JsonPropertyName("adjusted_shoulder_tilt")]
    public double? AdjustedShoulderTilt { get; set; }
    
    [JsonPropertyName("body_lean_offset")]
    public double? BodyLeanOffset { get; set; }
    
    [JsonPropertyName("adjusted_body_lean")]
    public double? AdjustedBodyLean { get; set; }
    
    [JsonPropertyName("posture_issues")]
    public List<string>? PostureIssues { get; set; }
    
    [JsonPropertyName("shoulder_detection_active")]
    public bool? ShoulderDetectionActive { get; set; }
    
    [JsonPropertyName("shoulder_detection_confidence")]
    public double? ShoulderDetectionConfidence { get; set; }
    
    [JsonPropertyName("compensation_description")]
    public string? CompensationDescription { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("frame")]
    public string? Frame { get; set; }
}
