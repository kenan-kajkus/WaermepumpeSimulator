using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WaermepumpeSimulator;
using WaermepumpeSimulator.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<WeatherDataService>();
builder.Services.AddScoped<HeatPumpPresetService>();
builder.Services.AddSingleton<SimulationEngine>();

await builder.Build().RunAsync();
