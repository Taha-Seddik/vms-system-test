using System.Text.Json.Serialization;

namespace Vms.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<CameraConnectionStatus>))]
public enum CameraConnectionStatus
{
    Unknown,
    Online,
    Offline,
    Disabled
}

[JsonConverter(typeof(JsonStringEnumConverter<CameraRecordingStatus>))]
public enum CameraRecordingStatus
{
    NotRecording,
    Recording
}
