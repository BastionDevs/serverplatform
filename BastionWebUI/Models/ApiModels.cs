using System.Text.Json.Serialization;

namespace BastionWebUI.Models;

public sealed class ServerInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Software { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    [JsonIgnore] public ServerMetrics? Metrics { get; set; }
    [JsonIgnore] public bool IsBusy { get; set; }
    [JsonIgnore] public bool IsOnline => Metrics is not null && (Metrics.Running ?? true);
}

public sealed class ServerMetrics
{
    public bool? Running { get; set; }
    public double Cpu { get; set; }
    public long Memory { get; set; }
    public long MemoryMB { get; set; }
}

public sealed class FileEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
}

public sealed record ApiResult(bool Success, string? Error = null);

public sealed class EndpointInfo
{
    public string Server { get; set; } = "";
    public string Version { get; set; } = "";
}

public sealed record DownloadedFile(string FileName, string ContentType, byte[] Content);

public sealed class CreateServerRequest
{
    public string ServerName { get; set; } = "";
    public string ServerDesc { get; set; } = "";
    public string Software { get; set; } = "";
    public string Version { get; set; } = "";
    public string MinRam { get; set; } = "1024";
    public string MaxRam { get; set; } = "4096";
    public string JavaVer { get; set; } = "21";
    public string JavaVendor { get; set; } = "Temurin";
    public string JavaType { get; set; } = "jre";
}
