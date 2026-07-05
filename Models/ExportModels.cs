using System.Text.Json.Serialization;

namespace PasswordManager.Models;

public class ExportFile
{
    [JsonPropertyName("version")]    public string Version    { get; set; } = "1.0";
    [JsonPropertyName("exportedAt")] public string ExportedAt { get; set; } = "";
    [JsonPropertyName("entryCount")] public int    EntryCount { get; set; }
    [JsonPropertyName("salt")]       public string Salt       { get; set; } = "";
    [JsonPropertyName("data")]       public string Data       { get; set; } = "";
}

public class ExportPayload
{
    [JsonPropertyName("entries")] public List<ExportEntry> Entries { get; set; } = [];
}

public class ExportEntry
{
    [JsonPropertyName("serviceName")] public string  ServiceName { get; set; } = "";
    [JsonPropertyName("password")]    public string  Password    { get; set; } = "";
    [JsonPropertyName("username")]    public string? Username    { get; set; }
    [JsonPropertyName("email")]       public string? Email       { get; set; }
    [JsonPropertyName("phone")]       public string? Phone       { get; set; }
    [JsonPropertyName("createdAt")]   public string  CreatedAt   { get; set; } = "";
    [JsonPropertyName("updatedAt")]   public string  UpdatedAt   { get; set; } = "";
}
