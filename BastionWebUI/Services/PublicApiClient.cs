using System.Net.Http.Json;
using System.Text.Json;
using BastionWebUI.Models;

namespace BastionWebUI.Services;

public sealed class PublicApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public PublicApiClient(HttpClient http) => _http = http;

    public async Task<EndpointInfo?> GetEndpointInfoAsync(CancellationToken token = default) =>
        await _http.GetFromJsonAsync<EndpointInfo>("endpointinfo", Options, token);
}
