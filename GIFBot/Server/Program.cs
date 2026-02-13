using GIFBot.Server.GIFBot;
using GIFBot.Server.Hubs;
using GIFBot.Server.Components;
using GIFBot.Server.Components.Utility;
using GIFBot.Shared;
using MudBlazor.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

// Services
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowedOrigins", policy =>
    {
        policy.AllowAnyOrigin().WithMethods("GET").AllowAnyHeader();
    });
});

builder.Services.AddHttpClient(Common.skHttpClientName);

// Register HttpClient for Blazor Server components (self-call to localhost)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5000/") });
builder.Services.AddScoped<ClientAppData>();

builder.Services.AddSingleton<GIFBot.Server.GIFBot.GIFBot>();
builder.Services.AddHostedService<GIFBotService>();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddMudServices();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream", "application/json" });
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0";
        context.Context.Response.Headers["Expires"] = "-1";
    }
});

app.UseRouting();
app.UseCors("AllowedOrigins");
app.UseAntiforgery();

app.MapRazorPages();
app.MapControllers();
app.MapHub<GIFBotHub>("/gifbothub");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (!app.Environment.IsDevelopment())
{
    var ps = new ProcessStartInfo($"http://localhost:5000?{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}")
    {
        UseShellExecute = true,
        Verb = "open"
    };
    Process.Start(ps);
}

app.Run();
