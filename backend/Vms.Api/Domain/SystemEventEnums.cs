using System.Text.Json.Serialization;

namespace Vms.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<SystemEventType>))]
public enum SystemEventType
{
    UserLogin,
    UserLogout,
    CameraOffline,
    CameraReconnected,
    MotionDetected,
    RecordingStarted,
    RecordingStopped,
    RecordingFailure
}

[JsonConverter(typeof(JsonStringEnumConverter<EventSeverity>))]
public enum EventSeverity
{
    Information,
    Warning,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter<EventStatus>))]
public enum EventStatus
{
    Open,
    Closed
}
