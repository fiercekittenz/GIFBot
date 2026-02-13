# Mark — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server project (ASP.NET + SignalR), Client project (Blazor WASM), Shared project (models/utilities), Stream Deck plugin, Installer
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Modernize — .NET 8 → .NET 10, Blazor WASM → Blazor Server, Telerik → MudBlazor, UI refresh

## Learnings

### Architecture Layout
- Solution has 3 core projects (Server, Client, Shared) + StreamDeckPlugin on netcoreapp3.1
- Server references Client (hosts WASM via `UseBlazorFrameworkFiles()`) and Shared
- Client references Shared only
- Server uses pre-.NET 6 hosting: `Startup.cs` + `Host.CreateDefaultBuilder` in `Program.cs`
- Server entry point: `GIFBot\Server\Program.cs` and `GIFBot\Server\Startup.cs`
- Client entry point: `GIFBot\Client\Program.cs` (WebAssemblyHostBuilder)
- All projects target `net8.0`; StreamDeckPlugin targets `netcoreapp3.1`

### SignalR
- Single hub: `GIFBot\Server\Hubs\GIFBotHub.cs` — ~130 public methods, ~72KB monolith
- Hub imports `Telerik.DataSource` for server-side grid operations (4 usages)
- All client pages connect to `/gifbothub` using `HttpTransportType.LongPolling`
- `GIFBotService` is an `IHostedService` that holds `IHubContext<GIFBotHub>` for server-initiated pushes
- GIFBot singleton is registered in DI; manages Twitch connection and all features

### OBS Browser Source Pattern (Critical)
- Static HTML files in `Client\wwwroot\`: `animations.html`, `stickers.html`, `goalbar.html`, `countdowntimer.html`, `backdrop.html`, `secondarystickers.html`
- Each polls `http://localhost:5000/ping/pong` (PingController) every 1s via jQuery CORS
- On success, redirects to Blazor route (e.g., `/animations`)
- Blazor browser source pages use `@layout BasicLayout` (bare layout, no nav chrome)
- 6 browser source Blazor pages in `Client\Pages\BrowserSource\`
- This pattern exists so OBS doesn't show errors if GIFBot isn't running when OBS starts

### UI Libraries
- Telerik Blazor 6.0.2 — primary UI library, 18+ files, 250+ component instances
- Radzen Blazor 4.32.6 — secondary, used for NotificationService, DialogService, RadzenNotification
- MainLayout wraps in `<TelerikRootComponent>` and includes `<RadzenNotification />`
- `_Imports.razor` globally imports both Telerik and Radzen namespaces
- Custom theme: `GIFBotPurple22.css`
- Both Telerik JS and Radzen JS loaded in `index.html`

### Serialization
- Newtonsoft.Json used pervasively (50+ files) for all hub communication and data persistence

### Risk Areas
- OBS browser source survivability under Blazor Server (circuit loss = blank overlay)
- Telerik.DataSource in GIFBotHub means Telerik removal touches server-side code
- HttpClient self-call pattern in every admin page will break under Blazor Server
- Double SignalR connections under Blazor Server (framework circuit + app hub)
- JS interop latency for audio/video playback in OBS overlays under Blazor Server
- Radzen must also be removed alongside Telerik (avoid three UI libraries)

---

📌 **Team update (2026-02-13):** Comprehensive architecture review and risk analysis completed. Key findings: dual UI library dependency (Telerik + Radzen ~490+ instances), monolithic SignalR hub (2,238 lines), OBS browser source survivability CRITICAL, hosting pattern modernization prerequisite, Telerik.DataSource leakage into server. Recommended phased migration: .NET 10 + hosting → Blazor Server + OBS strategy → MudBlazor migration. — Mark
