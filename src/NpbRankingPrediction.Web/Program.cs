using ApexCharts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NpbRankingPrediction.Web;
using NpbRankingPrediction.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<SeasonDataService>();
builder.Services.AddApexCharts();
builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync();
