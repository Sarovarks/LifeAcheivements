using LifeAchievements.Web;
using LifeAchievements.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Where the API lives. wwwroot/appsettings.json makes this changeable without a rebuild.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5100";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/") });
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthEvents>();
builder.Services.AddScoped<GameApiClient>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, TokenAuthStateProvider>();

await builder.Build().RunAsync();
