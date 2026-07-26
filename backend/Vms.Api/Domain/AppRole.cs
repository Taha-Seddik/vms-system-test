using System.Text.Json.Serialization;

namespace Vms.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<AppRole>))]
public enum AppRole
{
    Administrator,
    Operator,
    Viewer
}
