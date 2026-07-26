using System.Text.Json.Serialization;

namespace Vms.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<RecordingMode>))]
public enum RecordingMode
{
    Manual,
    Continuous,
    Event
}

[JsonConverter(typeof(JsonStringEnumConverter<RecordingState>))]
public enum RecordingState
{
    Recording,
    Completed,
    Failed
}
