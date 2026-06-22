namespace BastionWebUI.Services
{
    using Newtonsoft.Json.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;

    public class MinecraftVersionService
    {
        private readonly HttpClient _http;

        public MinecraftVersionService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<string>> GetVersionsAsync(string software)
        {
            switch (software.ToLower())
            {
                case "paper":
                case "velocity":
                    return await GetPaperProjectVersionsAsync(software.ToLowerInvariant());
                case "vanilla":
                    return await GetVanillaVersionsAsync();
                default:
                    return new List<string>();
            }
        }

        public async Task<List<string>> GetBuildsAsync(string software, string version)
        {
            var response = await _http.GetStringAsync($"https://api.papermc.io/v2/projects/{software.ToLowerInvariant()}/versions/{version}");
            var versionsObj = JObject.Parse(response);
            return versionsObj.Value<JArray>("builds")?.Select(x => x.ToString()).Reverse().ToList() ?? new();
        }

        private async Task<List<string>> GetPaperProjectVersionsAsync(string project)
        {
            var response = await _http.GetStringAsync($"https://api.papermc.io/v2/projects/{project}");
            var versionsObj = JObject.Parse(response);
            return versionsObj.Value<JArray>("versions")?.Select(x => x.ToString()).Reverse().ToList() ?? new();
        }

        private async Task<List<string>> GetVanillaVersionsAsync()
        {
            var response = await _http.GetFromJsonAsync<JsonElement>("https://piston-meta.mojang.com/mc/game/version_manifest.json");
            var versions = response.GetProperty("versions")
                .EnumerateArray()
                .Select(v => v.GetProperty("id").GetString()!)
                .ToList();
            return versions;
        }
    }
}
