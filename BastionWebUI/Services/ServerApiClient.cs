using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BastionWebUI.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BastionWebUI.Services;

public sealed class ServerApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ServerApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<List<ServerInfo>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => new(HttpMethod.Get, "profile/servers"), cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Could not load the server list.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<ServerInfo>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ServerMetrics?> GetMetricsAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "servers/metrics", new { id }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ServerMetrics>(JsonOptions, cancellationToken);
    }

    public Task<ApiResult> StartAsync(string id) => MutateAsync("servers/start", new { id });
    public Task<ApiResult> StopAsync(string id) => MutateAsync("servers/stop", new { id });
    public Task<ApiResult> RestartAsync(string id) => MutateAsync("servers/restart", new { id });
    public Task<ApiResult> DeleteServerAsync(string id) => MutateAsync("servers/delete", new { id });
    public Task<ApiResult> CreateServerAsync(CreateServerRequest request) => MutateAsync("servers/create", request);
    public Task<ApiResult> SendCommandAsync(string id, string command, CancellationToken token = default) =>
        MutateAsync("servers/console/command", new { id, command }, token);

    public async Task<List<FileEntry>> ListFilesAsync(string id, string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var uri = $"servers/files/list?id={Uri.EscapeDataString(id)}&path={Uri.EscapeDataString(path)}&recursive={recursive.ToString().ToLowerInvariant()}";
        using var response = await SendAsync(() => new(HttpMethod.Get, uri), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("entries").Deserialize<List<FileEntry>>(JsonOptions) ?? [];
    }

    public async Task<string> ReadFileAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var uri = $"servers/files/read?id={Uri.EscapeDataString(id)}&path={Uri.EscapeDataString(path)}";
        using var response = await SendAsync(() => new(HttpMethod.Get, uri), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("content").GetString() ?? "";
    }

    public Task<ApiResult> WriteFileAsync(string id, string path, string content) =>
        MutateAsync("servers/files/write", new { id, path, content });
    public Task<ApiResult> DeleteFileAsync(string id, string path) =>
        MutateAsync("servers/files/delete", new { id, path });
    public Task<ApiResult> CreateDirectoryAsync(string id, string path) =>
        MutateAsync("servers/files/mkdir", new { id, path });
    public Task<ApiResult> MoveFileAsync(string id, string from, string to, bool overwrite = false) =>
        MutateAsync("servers/files/move", new { id, from, to, overwrite });

    public async Task<ApiResult> UploadFileAsync(string id, string folder, IBrowserFile file,
        bool overwrite, CancellationToken cancellationToken = default)
    {
        const long maxUploadBytes = 64L * 1024 * 1024;
        await using var source = file.OpenReadStream(maxUploadBytes, cancellationToken);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var target = string.IsNullOrEmpty(folder) ? "/" : folder.TrimEnd('/', '\\') + "/";

        using var response = await SendAsync(() =>
        {
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(id), "id");
            content.Add(new StringContent(target), "path");
            content.Add(new StringContent(overwrite.ToString().ToLowerInvariant()), "overwrite");
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            return new HttpRequestMessage(HttpMethod.Post, "servers/files/upload") { Content = content };
        }, cancellationToken);
        return await ReadResultAsync(response, cancellationToken);
    }

    public async Task<DownloadedFile> DownloadFileAsync(string id, string path,
        CancellationToken cancellationToken = default)
    {
        var uri = $"servers/files/download?id={Uri.EscapeDataString(id)}&path={Uri.EscapeDataString(path)}";
        using var response = await SendAsync(() => new(HttpMethod.Get, uri), cancellationToken);
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? Path.GetFileName(path);
        return new(fileName, contentType, await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    public async Task<HttpResponseMessage> OpenConsoleStreamAsync(string id, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync() ?? throw new UnauthorizedAccessException();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"servers/console/stream?id={Uri.EscapeDataString(id)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.SetBrowserResponseStreamingEnabled(true);
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<ApiResult> MutateAsync(string path, object body, CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, path, body, cancellationToken);
        return await ReadResultAsync(response, cancellationToken);
    }

    private static async Task<ApiResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            return new(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? null : response.ReasonPhrase);
        try
        {
            using var json = JsonDocument.Parse(text);
            var success = json.RootElement.TryGetProperty("success", out var value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.String && value.GetString() == "true");
            var error = json.RootElement.TryGetProperty("error", out var errorValue) ? errorValue.GetString() : null;
            return new(response.IsSuccessStatusCode && success, error);
        }
        catch { return new(response.IsSuccessStatusCode, response.ReasonPhrase); }
    }

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object body, CancellationToken token) =>
        SendAsync(() => new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }, token);

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        if (!await _auth.IsAuthenticatedAsync())
            throw new UnauthorizedAccessException();

        var response = await SendOnceAsync(requestFactory, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        if (!await _auth.RefreshAsync(force: true))
            throw new UnauthorizedAccessException();
        return await SendOnceAsync(requestFactory, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> requestFactory, CancellationToken token)
    {
        var accessToken = await _auth.GetAccessTokenAsync() ?? throw new UnauthorizedAccessException();
        var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _http.SendAsync(request, token);
    }
}
