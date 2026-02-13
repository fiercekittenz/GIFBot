# Milchick — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server project (ASP.NET + SignalR), Client project (Blazor WASM), Shared project (models/utilities), Stream Deck plugin, Installer
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Modernize — .NET 8 → .NET 10, Blazor WASM → Blazor Server, Telerik → MudBlazor, UI refresh
- **Team:** Mark (Lead), Helly (Frontend), Irving (Backend), Dylan (Tester)

## Learnings

### Solution Structure
- Solution file: `GIFBot.sln` contains 3 projects: Server, Client, Shared
- `GIFBotStreamDeckPlugin` (.csproj exists at `GIFBot/GIFBotStreamDeckPlugin/`) but is NOT in the .sln — targets netcoreapp3.1
- `GIFBot/Installer/GIFBot-R.iss` — Inno Setup installer script

### Project Details
- **GIFBot.Server** (`GIFBot/Server/GIFBot.Server.csproj`): net8.0, SDK `Microsoft.NET.Sdk.Web`, AssemblyName `GIFBotR`, RuntimeIdentifier `win-x64`
  - Uses old Startup.cs + Program.cs hosting pattern (NOT minimal hosting)
  - Key packages: `Microsoft.AspNetCore.Components.WebAssembly.Server 8.0.6`, `TwitchLib 3.5.3`, `Microsoft.CognitiveServices.Speech 1.37.0`, `Newtonsoft.Json 13.0.*`, `System.Drawing.Common 8.0.6`, `System.Speech 8.0.0`
  - References Client and Shared projects
  - GIFBotHub.cs is 2,238 lines — main SignalR hub
  - 5 controllers: Ping, Remote, Streamlabs, Upload, Utility
  - 11 feature manager dirs: Animations, Backdrop, ChannelPoints, CountdownTimer, Giveaway, GoalBar, Greeter, Regurgitator, Stickers, StreamElements, Tiltify

- **GIFBot.Client** (`GIFBot/Client/GIFBot.Client.csproj`): net8.0, SDK `Microsoft.NET.Sdk.BlazorWebAssembly`
  - Has PWA service worker
  - Key packages: `Telerik.UI.for.Blazor 6.0.2`, `Radzen.Blazor 4.32.6`, `bootstrap 5.3.3`, `Microsoft.AspNetCore.SignalR.Client 8.0.6`
  - References Shared project only
  - 40 .razor files total, 17 .cs code-behind files

- **GIFBot.Shared** (`GIFBot/Shared/GIFBot.Shared.csproj`): net8.0, SDK `Microsoft.NET.Sdk`
  - Key packages: `Newtonsoft.Json`, `TwitchLib 3.5.3`, `System.Drawing.Common 8.0.6`, `System.ComponentModel.Annotations 5.0.0`
  - Contains: Models (Animation, Features, GIFBot, Tiltify, Twitch, StreamElements, Visualization, Base), Utility, Interfaces, Versioning

### Telerik Usage Scope
- 21 .razor files reference Telerik, 9 .cs files reference Telerik
- ~241 Telerik component instances total
- Component types: TelerikCheckBox (62), TelerikNumericTextBox (62), TelerikTextBox (55), TelerikWindow (21), TelerikTabStrip (17), TelerikGrid (11), TelerikDropDownList (5), TelerikTreeList (3), TelerikRootComponent (2), TelerikTooltip (2), TelerikListView (1)
- Server-side: GIFBotHub.cs uses `Telerik.DataSource` + `Telerik.DataSource.Extensions` for Regurgitator grid paging (lines 21-22, method at ~line 1150)
- Server-side: GiveawayManager.cs has unused `using Telerik.SvgIcons` (line 12)
- Shared: AnimationTreeItem.cs has only a comment reference (line 29)

### Radzen Usage Scope (also present — must be replaced)
- 18 .razor files reference Radzen, 9 .cs files reference Radzen
- ~250+ Radzen component instances total
- Component types: RadzenRadioButtonListItem (143), RadzenFieldset (33), RadzenRadioButtonList (19), RadzenProgressBar (12), RadzenTextBox (12), RadzenUpload (12), RadzenDropDown (5), RadzenLabel (5), RadzenPassword (4), RadzenNotification (2), RadzenNumeric (2), RadzenTree/TreeItem (3)
- Client Program.cs registers Radzen services: NotificationService, DialogService

### Frontend Architecture
- `_Imports.razor` imports: Radzen, Radzen.Blazor, Telerik.Blazor, Telerik.Blazor.Components, Telerik.FontIcons, Telerik.SvgIcons
- `MainLayout.razor` wraps body in `<TelerikRootComponent>`, includes `<RadzenNotification />`
- `index.html` loads: Telerik CSS (kendo-font-icons) + JS (telerik-blazor.js), Radzen CSS (dark-base.css) + JS (Radzen.Blazor.js), Bootstrap, jQuery 3.3.1 + jQuery UI 1.12.1, custom GIFBotPurple22 theme
- Browser source pages (standalone HTML): animations.html, stickers.html, secondarystickers.html, goalbar.html, backdrop.html, countdowntimer.html
- Custom JS: CORSRequest.js, AnimationHelpers.js, Streamlabs.js, ElementDrag.js

### Key Risks Identified
- No test projects exist in the solution
- GIFBotHub.cs at 2,238 lines is the most complex file
- Dual UI library (Telerik + Radzen) means ~490+ component instances to migrate
- `Telerik.DataSource` leaks into server-side code (GIFBotHub)
- Browser source pages are external HTML files that must remain independently accessible

---

📌 **Team update (2026-02-13):** Modernization plan decomposed into 5 milestones and 44 tasks with detailed sizing and dependencies. Critical insights: OBS browser source survivability is #1 risk, monolithic hub requires refactoring strategy, Telerik removal must include server-side DataSource replacement, Stream Deck plugin deferrable out-of-scope. Recommended phasing ensures prerequisite work completes before dependent tasks. — Milchick
