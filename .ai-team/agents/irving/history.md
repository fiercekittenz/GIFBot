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

---

### Telerik/Radzen Removal + MudBlazor Addition (2026-02-13)
- Milestone plan resequenced: Telerik+Radzen removal now happens BEFORE .NET 10 upgrade because Telerik NuGet feed is unavailable.
- **Client.csproj**: Removed `Telerik.UI.for.Blazor` (6.0.2) and `Radzen.Blazor` (4.32.6), added `MudBlazor` (8.5.0).
- **Server.csproj**: No direct Telerik packages — Telerik.DataSource came transitively via Client project reference.
- **Program.cs**: Replaced `AddTelerikBlazor()` + Radzen scoped services with `AddMudServices()`.
- **_Imports.razor**: Replaced 6 Telerik/Radzen `@using` lines with single `@using MudBlazor`.
- **index.html**: Removed Telerik CSS (kendo-font-icons), Telerik JS (telerik-blazor.js), Radzen CSS (dark-base.css), Radzen JS (Radzen.Blazor.js). Added MudBlazor CSS (Roboto font + MudBlazor.min.css) and JS (MudBlazor.min.js).
- **GIFBotHub.cs**: Removed `using Telerik.DataSource` and `Telerik.DataSource.Extensions`. Replaced `DataSourceRequest`/`DataSourceResult`/`ToDataSourceResultAsync()` with manual LINQ paging using new `PagedRequest` model. Method `GetRegurgitatorEntries` now takes `PagedRequest` instead of `DataSourceRequest`.
- **Created `Shared/Models/Visualization/PagedRequest.cs`**: Simple paging DTO with `Page` and `PageSize` properties. Existing `DataEnvelope<T>` was already Telerik-free — kept as-is.
- **GiveawayManager.cs**: Removed unused `using Telerik.SvgIcons;`.
- Did NOT touch .razor component files — those are Helly's domain.
- **Helly coordination note**: `Regurgitator.razor.cs` line 203 still invokes `GetRegurgitatorEntries` with old Telerik `args.Request`. Helly needs to update that call to pass a `PagedRequest` instead.
- 7 files changed, 26 insertions, 28 deletions. Committed on `feature/modernization`.

---

### Blazor Server Dispatcher Threading Fix (2026-02-13)
- After WASM → Server consolidation (M3), SignalR `HubConnection.On(...)` callbacks execute on non-UI threads. Under WASM this was fine (single-threaded), but Blazor Server requires `StateHasChanged()` and `JSRuntime` calls to run on the Dispatcher thread.
- Fixed 11 files total (8 originally scoped + 3 additional code-behind files discovered via grep):
  - `Index.razor` — 3 callbacks (LogMessage, UpdateBonkersModeState, UpdateStreamerOnlyModeState)
  - `Animations.razor` — 5 callbacks (PlayAnimation, UpdateTestVisual, StopTestVisual, StopAnimation, SendStreamlabsAuthToken)
  - `Stickers.razor` — 6 callbacks (UpdateTestVisual, StopTestVisual, SendAllPlacedStickers, PlaceSticker, RemoveSticker, ClearAllStickers; UpdateStickerAudioSettings skipped — no StateHasChanged/JSRuntime)
  - `SecondaryStickers.razor` — 6 callbacks (same pattern as Stickers)
  - `Backdrop.razor` — 2 callbacks (HangBackdrop, TakeDownBackdrop)
  - `CountdownTimer.razor` — 2 callbacks (UpdateTime, HideTimer)
  - `GoalBar.razor` — 2 callbacks (UpdateGoal, GoalBarDataUpdated)
  - `GoalBarEditor.razor` — 1 callback (GoalBarDataUpdated)
  - `EditAnimation.razor.cs` — 1 callback (UpdatePosition → UpdateVisualPosition calls StateHasChanged)
  - `GiveawayEditor.razor.cs` — 2 callbacks (SendNewGiveawayEntrant, SendGiveawayWinner)
  - `StickersEditor.Razor.cs` — 1 callback (UpdatePosition → UpdateStickerPosition calls StateHasChanged)
- Pattern: `StateHasChanged()` → `await InvokeAsync(StateHasChanged)`; `JSRuntime.InvokeVoidAsync(...)` → `await InvokeAsync(async () => await JSRuntime.InvokeVoidAsync(...))`. For callbacks calling helper methods that internally use StateHasChanged/JSRuntime, wrapped the helper call with `await InvokeAsync(async () => await HelperMethod(...))`.
- Did NOT touch `StateHasChanged()` in lifecycle methods (OnInitializedAsync), event handlers, or non-hub-callback code.
- Build: 0 errors. 11 files changed, 56 insertions, 55 deletions. Committed on `feature/modernization`.

---

### Prerendering Disabled for Local Desktop App (2026-02-13)
- GIFBot is a local desktop app with no benefit from SSR/prerendering.
- `App.razor` had `@rendermode="RenderMode.InteractiveServer"` on both `<HeadOutlet>` and `<Routes>`, which enables prerendering by default.
- Prerendering caused two issues: (1) JS interop crash (`InvalidOperationException`) during static render phase when `Index.razor` line 162 called `JSRuntime.InvokeVoidAsync("UpdateScroll")` inside a hub callback in `OnInitializedAsync`, and (2) two browser tabs opening on startup (server prerender + Blazor circuit connect = double navigation).
- Fix: Changed both to `new InteractiveServerRenderMode(prerender: false)` — 2-line change in `GIFBot\Server\Components\App.razor`.
- With prerendering disabled, the Blazor circuit is immediately interactive — no static render phase means JS interop is available from the start, hub callbacks only fire once, and only one tab opens.
- Build: 0 errors. 1 file changed. Committed on `feature/modernization`.

---

📌 **Team update (2026-02-13):** Decisions merged from session log: StreamDeck plugin removed from feature/modernization branch (committed), prerendering disabled for local desktop app (App.razor InteractiveServerRenderMode), Blazor Server Dispatcher threading pattern established (InvokeAsync StateHasChanged requirement). These unblock the modernization workflow. — Scribe
