using System.Net.Http.Json;
using System.Text.Json;

namespace BastionWebUI.Services;

public sealed class AuthService
{
    private const string AccessKey = "accessToken";
    private const string RefreshKey = "refreshToken";
    private const string ExpiryKey = "accessTokenExpiresAt";
    private readonly HttpClient _http;
    private readonly StorageService _storage;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthService(HttpClient http, StorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("auth/login", new { username, password });
        if (!response.IsSuccessStatusCode)
            return (false, await ReadErrorAsync(response));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!IsSuccess(json.RootElement))
            return (false, ReadError(json.RootElement));

        await SaveTokensAsync(json.RootElement);
        return (true, null);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _storage.GetItemAsync(AccessKey);
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (await IsAccessTokenFreshAsync())
            return true;

        return await RefreshAsync();
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!await IsAuthenticatedAsync())
            return null;
        return await _storage.GetItemAsync(AccessKey);
    }

    public async Task<bool> RefreshAsync(bool force = false)
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (!force && await IsAccessTokenFreshAsync())
                return true;

            var refreshToken = await _storage.GetItemAsync(RefreshKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var response = await _http.PostAsJsonAsync("auth/refresh", new { refreshToken });
            if (!response.IsSuccessStatusCode)
            {
                await ClearAsync();
                return false;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!IsSuccess(json.RootElement))
            {
                await ClearAsync();
                return false;
            }

            await SaveTokensAsync(json.RootElement);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        var token = await _storage.GetItemAsync(AccessKey);
        var refreshToken = await _storage.GetItemAsync(RefreshKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout")
            {
                Content = JsonContent.Create(new { refreshToken })
            };
            request.Headers.Authorization = new("Bearer", token);
            try { await _http.SendAsync(request); } catch { /* Always clear the local session. */ }
        }
        await ClearAsync();
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(AccessKey);
        await _storage.RemoveItemAsync(RefreshKey);
        await _storage.RemoveItemAsync(ExpiryKey);
        await _storage.RemoveItemAsync("jwtToken");
    }

    private async Task<bool> IsAccessTokenFreshAsync()
    {
        var token = await _storage.GetItemAsync(AccessKey);
        var expiryText = await _storage.GetItemAsync(ExpiryKey);
        return !string.IsNullOrWhiteSpace(token)
            && DateTimeOffset.TryParse(expiryText, out var expiry)
            && expiry > DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private async Task SaveTokensAsync(JsonElement root)
    {
        var accessToken = root.GetProperty("accessToken").GetString() ?? root.GetProperty("token").GetString()!;
        var refreshToken = root.GetProperty("refreshToken").GetString()!;
        var expiresIn = root.TryGetProperty("expiresIn", out var expiry) ? expiry.GetInt32() : 3600;
        await _storage.SetItemAsync(AccessKey, accessToken);
        await _storage.SetItemAsync(RefreshKey, refreshToken);
        await _storage.SetItemAsync(ExpiryKey, DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("O"));
        await _storage.RemoveItemAsync("jwtToken");
    }

    private static bool IsSuccess(JsonElement root) =>
        root.TryGetProperty("success", out var success) &&
        (success.ValueKind == JsonValueKind.True ||
         success.ValueKind == JsonValueKind.String && bool.TryParse(success.GetString(), out var value) && value);

    private static string? ReadError(JsonElement root) =>
        root.TryGetProperty("error", out var error) ? error.GetString() : null;

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ReadError(json.RootElement);
        }
        catch { return response.ReasonPhrase; }
    }
}
