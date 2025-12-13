using System.Text.Json.Serialization;
using Slouti.Models;

namespace Slouti.Services;

[JsonSerializable(typeof(WebSocketMessage))]
[JsonSerializable(typeof(PostureData))]
[JsonSerializable(typeof(StartMonitoringMessage))]
[JsonSerializable(typeof(StopMonitoringMessage))]
[JsonSerializable(typeof(SavePostureMessage))]
[JsonSerializable(typeof(SetThresholdsMessage))]
[JsonSerializable(typeof(GetStatisticsMessage))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class JsonContext : JsonSerializerContext
{
}

// Message types for serialization
public class StartMonitoringMessage
{
    public string type { get; set; } = "start_monitoring";
}

public class StopMonitoringMessage
{
    public string type { get; set; } = "stop_monitoring";
}

public class SavePostureMessage
{
    public string type { get; set; } = "save_good_posture";
}

public class SetThresholdsMessage
{
    public string type { get; set; } = "set_thresholds";
    public int pitch_scale { get; set; }
    public int distance_scale { get; set; }
    public int head_roll_scale { get; set; }
    public int shoulder_tilt_scale { get; set; }
}

public class GetStatisticsMessage
{
    public string type { get; set; } = "get_statistics";
}
