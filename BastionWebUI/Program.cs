using BastionWebUI;
using BastionWebUI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ServerApiClient>();
builder.Services.AddScoped<PublicApiClient>();
builder.Services.AddScoped<MinecraftVersionService>();


// Add MudBlazor services to DI container
builder.Services.AddMudServices();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["BackendUrl"] ?? "http://localhost:5678/")
});

await builder.Build().RunAsync();
