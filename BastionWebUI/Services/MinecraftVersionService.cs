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
                    return await GetPaperVersionsAsync();
                case "vanilla":
                    return await GetVanillaVersionsAsync();
                default:
                    return new List<string>();
            }
        }

        private async Task<List<string>> GetPaperVersionsAsync()
        {
            var response = await _http.GetStringAsync("https://api.papermc.io/v2/projects/paper");
            var versionsObj = JObject.Parse(response);
            var versionsArray = versionsObj.Value<JArray>("versions");
            return versionsArray?.ToObject<List<string>>() ?? new List<string>();
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
