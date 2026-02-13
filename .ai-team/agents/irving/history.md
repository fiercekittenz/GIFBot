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
