# Helly — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server project (ASP.NET + SignalR), Client project (Blazor WASM), Shared project (models/utilities)
- **Frontend:** Blazor WASM with Telerik Blazor UI components, browser source pages for OBS, SignalR client for real-time updates
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Replace Telerik with MudBlazor, improve UI, target MudBlazor website aesthetics for color/fonts/responsiveness

## Learnings

---

📌 **Team update (2026-02-13):** Component migration scope is ~490+ instances across Telerik AND Radzen (not just Telerik). Milchick's plan includes 19 component migration tasks in Milestone 3. Radzen must be removed alongside Telerik to avoid three-library situation. Server-side Telerik.DataSource refactoring (task 3.14) is prerequisite before DataSource-dependent components can be fully migrated. — Milchick
