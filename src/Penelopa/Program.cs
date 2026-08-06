using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Atlas.Blazor.DependencyInjection;
using Penelopa;
using Penelopa.Core.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Atlas.Blazor workspace services (workspace factory, content routing, history).
builder.Services.AddAtlasWorkspace();

// Primitive store and selection shared by the editor panels.
builder.Services.AddSingleton<IPrimitiveService, PrimitiveService>();

await builder.Build().RunAsync();
