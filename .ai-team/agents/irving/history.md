# Irving — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server project (ASP.NET + SignalR backbone, features as modular units with own data/threads), Client project (Blazor WASM), Shared project (data models + utilities)
- **Key classes:** GIFBot instance (core Twitch connection + feature access), GIFBotHub (SignalR hub), Feature modules
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** .NET 8 → .NET 10, consolidate Blazor WASM into Blazor Server, maintain SignalR functionality

## Learnings

---

📌 **Team update (2026-02-13):** Architecture review identified critical dependencies: Telerik.DataSource usage in GIFBotHub requires server-side refactoring during component migration, old hosting pattern blocks .NET 10 upgrade, Blazor Server consolidation requires direct service injection pattern replacement for admin pages. Sequencing priority: Phase 1 (.NET 10 + hosting modernization) must complete before Phase 2 (WASM → Server consolidation). — Mark

---

### StreamDeck Plugin Removal (2026-02-13)
- Branch `feature/modernization` created from main
- GIFBotStreamDeckPlugin was NOT in GIFBot.sln — it had its own standalone .sln inside its directory. No solution file edit needed.
- Only code reference outside the plugin directory: `Client/Pages/About.razor` line 41 (feature list bullet). Removed.
- Installer project had zero StreamDeck references — left untouched.
- `.ai-team` docs reference StreamDeck in decisions/history but those are documentation, not build artifacts.
- Build attempted: fails on Telerik.UI.for.Blazor NuGet restore (proprietary feed not configured in this environment). This is a pre-existing issue unrelated to StreamDeck removal.
- 26 files changed, 1973 lines deleted. Committed on `feature/modernization`.
