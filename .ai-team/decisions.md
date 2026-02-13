# Decisions

This file is the shared brain for the GIFBot squad. All team decisions are recorded here.

---

### 2026-02-13: Migration Architecture Analysis & Risk Assessment
**By:** Mark
**What:** Architecture review of current GIFBot structure and risk analysis for the three planned migrations
**Why:** Must understand risks before sequencing work

---

## 1. Current Architecture Summary

GIFBot is a three-project solution with a companion Stream Deck plugin:

| Project | SDK | TFM | Role |
|---|---|---|---|
| **GIFBot.Server** | `Microsoft.NET.Sdk.Web` | `net8.0` | ASP.NET host, SignalR hub, Twitch connection, feature managers, serves WASM client |
| **GIFBot.Client** | `Microsoft.NET.Sdk.BlazorWebAssembly` | `net8.0` | Blazor WASM app — admin UI + OBS browser source pages |
| **GIFBot.Shared** | `Microsoft.NET.Sdk` | `net8.0` | Models, utilities, constants shared between Server and Client |
| **GIFBotStreamDeckPlugin** | `Microsoft.NET.Sdk` | `netcoreapp3.1` | Stream Deck integration — **ancient TFM, out of scope but noted** |

**Project references:**
- Server → Client (hosts the WASM app via `UseBlazorFrameworkFiles()`)
- Server → Shared
- Client → Shared

The Server uses the **Startup.cs / Program.cs** split (pre-.NET 6 hosting pattern). It registers `GIFBot` as a singleton and `GIFBotService` as an `IHostedService`. The startup pattern (`Host.CreateDefaultBuilder` + `ConfigureWebHostDefaults` + `UseStartup<Startup>`) is deprecated — the migration to .NET 10 should adopt minimal hosting.

**Key entry point detail:** In non-dev mode, `Startup.Configure` launches a browser to `http://localhost:5000`. This is a desktop app deployed via an installer — GIFBot runs locally, not in the cloud.

---

## 2. SignalR Architecture

**Hub:** `GIFBotHub.cs` — a single, massive hub file (~130 public methods, ~72KB). It handles everything: bot settings CRUD, animation management, sticker operations, giveaways, goal bars, countdowns, greeter config, regurgitator settings, backdrop management, channel points, Tiltify integration, Stream Elements, throttled users, and more.

**Client connections:** Every Blazor page (admin UI pages and browser source pages alike) establishes its own `HubConnection` to `/gifbothub` using `HubConnectionBuilder` with **`HttpTransportType.LongPolling`**. This is significant.

**Server-side Telerik dependency:** The hub imports `Telerik.DataSource` and `Telerik.DataSource.Extensions` — there are 4 usages of `DataSourceRequest`/`DataSourceResult` in the hub for server-side grid operations. This means the Telerik → MudBlazor migration reaches into the server project, not just the client.

**Serialization:** All hub communication uses `Newtonsoft.Json` for manual serialization/deserialization. This is pervasive (50+ files across the solution). NOT using System.Text.Json.

---

## 3. OBS Browser Source Architecture — CRITICAL PATH

This is the most architecturally sensitive piece. The pattern is:

1. **Static HTML landing pages** in `wwwroot/` (e.g., `animations.html`, `stickers.html`, `goalbar.html`, `countdowntimer.html`, `backdrop.html`, `secondarystickers.html`)
2. Each HTML page uses jQuery + `CORSRequest.js` to **poll `http://localhost:5000/ping/pong`** every 1 second
3. When the ping succeeds, the page redirects to the corresponding **Blazor route** (e.g., `/animations`, `/stickers`)
4. The Blazor page connects to SignalR via LongPolling and renders the visual overlay
5. **`PingController`** is a simple MVC controller that returns 200 OK

**Why this exists:** OBS browser sources are loaded once. If GIFBot isn't running when OBS starts, the browser source would show an error and require manual cache refresh. The HTML landing page gracefully waits for the server, then hands off to Blazor.

**Browser source pages use `@layout BasicLayout`** — a minimal layout with no nav/chrome, just `@Body`. These pages are transparent overlays rendered in OBS.

**Browser source Blazor pages (6 total):**
- `/animations` — Animations.razor
- `/stickers` — Stickers.razor
- `/secondarystickers` — SecondaryStickers.razor
- `/goalbar` — GoalBar.razor
- `/countdowntimer` — CountdownTimer.razor
- `/backdrop` — Backdrop.razor

---

## 4. UI Component Landscape

**Telerik usage** is extensive — 18+ files with Telerik components. By raw match count, the heaviest usage is in:
- `StickersEditor.razor` (61 component instances)
- `AnimationComponent.razor` (48)
- `Settings.razor` (26)
- `GoalBarEditor.razor` (23)
- `CountdownTimer.razor` (21)
- `AnimationsEditor.razor` (20)

Components observed: `TelerikGrid`, `TelerikTreeView`, `TelerikTreeList`, `TelerikDialog`, `TelerikWindow`, `TelerikButton`, `TelerikDropDown`, `TelerikNumericTextBox`, `TelerikTextBox`, `TelerikTabStrip`, `TelerikColorPicker`, `TelerikSwitch`, `TelerikUpload`, `TelerikCheckBox`, `TelerikComboBox`, `TelerikAutoComplete`, `TelerikRadioGroup`.

**Radzen** is also in the project — used for `NotificationService`, `DialogService`, and `RadzenNotification` in the main layout. It's a secondary component library, not a primary one. The Radzen package (`Radzen.Blazor 4.32.6`) plus its CSS and JS are loaded in `index.html`.

**`_Imports.razor`** globally imports both `Telerik.Blazor`, `Telerik.Blazor.Components`, `Telerik.FontIcons`, `Telerik.SvgIcons`, and `Radzen`/`Radzen.Blazor`.

**MainLayout.razor** wraps everything in `<TelerikRootComponent>` and includes `<RadzenNotification />`.

---

## 5. Risk Analysis

### 5A. .NET 8 → .NET 10

| Risk | Severity | Detail |
|---|---|---|
| **Hosting model migration** | HIGH | Server uses deprecated `Startup.cs` + `Host.CreateDefaultBuilder` pattern. Must migrate to minimal hosting (`WebApplication.CreateBuilder`). This touches DI, middleware pipeline, and the Kestrel configuration. |
| **`System.Drawing.Common` on non-Windows** | LOW | Used in Server and Shared. On Windows-only deployment this is fine, but worth noting. In .NET 8+ it already requires a runtime config switch on non-Windows. |
| **`System.Speech` dependency** | MEDIUM | Windows-only API. Works today, but .NET 10 may tighten platform warnings. Already suppressed via `CA1416` NoWarn. |
| **Newtonsoft.Json everywhere** | LOW | Not deprecated, but the ecosystem increasingly defaults to System.Text.Json. No urgency to switch, but it's tech debt. |
| **TwitchLib 3.5.3** | MEDIUM | Need to verify .NET 10 compatibility. TwitchLib has had breaking changes between major versions. |
| **Stream Deck plugin on netcoreapp3.1** | OUT OF SCOPE | Ancient TFM, won't even restore on modern SDKs. Flag for future but don't block migration. |
| **`Microsoft.AspNetCore.Components.WebAssembly.Server` package** | HIGH | This package is used for hosting WASM and will be removed when we move to Blazor Server. It's version-locked to 8.0.6. |
| **`BlazorCacheBootResources` / `BlazorWebAssemblyEnableLinking`** | LOW | WASM-specific MSBuild properties in Server.csproj. Must be cleaned up in migration. |

### 5B. Blazor WASM → Blazor Server

| Risk | Severity | Detail |
|---|---|---|
| **OBS browser source rendering model** | CRITICAL | Today, OBS loads the static HTML landing page, which redirects to a Blazor WASM page. WASM runs entirely in the browser — the OBS browser source is self-contained once loaded. With Blazor Server, every render requires an active SignalR circuit back to the server. **If the server restarts, ALL OBS browser sources lose their circuits and go blank.** The current reconnection logic in `ConnectToHub()` handles SignalR *client* hub reconnection, but Blazor Server circuit loss is a different beast — the entire component tree is destroyed. We must design a reconnection/reload strategy for OBS sources under Blazor Server. |
| **Double SignalR** | HIGH | Today the client manually creates `HubConnection` instances to `/gifbothub`. Under Blazor Server, the framework itself maintains a SignalR circuit for rendering. The app would have TWO SignalR connections per page — one for Blazor Server rendering, one for the custom GIFBotHub. This is wasteful but may be acceptable short-term. Long-term, the hub methods should be refactored into injectable services that pages call directly (since Blazor Server runs on the server, it has direct access to `GIFBot` singleton). |
| **`HttpClient` injection pattern** | HIGH | Every page injects `HttpClient` and uses it to hit the server's own API (e.g., `/ping/pong`, file uploads). Under Blazor Server, this pattern breaks — the component runs on the server, so `HttpClient` calls to `localhost` would be the server calling itself. These must be replaced with direct service injection. |
| **JS Interop execution context** | MEDIUM | Browser source pages heavily use `IJSRuntime` for playing audio (`PlaySound`), playing video (`PlayVideo`), Streamlabs integration, and element dragging. Under Blazor Server, JS interop calls are remoted over the circuit. Latency increases. For OBS overlays that need frame-precise animation timing, this could introduce visible jank. |
| **Service Worker** | LOW | The client registers a service worker (`service-worker.js`). This is a WASM concept and irrelevant under Blazor Server. Remove it. |
| **`WebAssemblyHostBuilder` → server-side DI** | HIGH | `Client/Program.cs` uses `WebAssemblyHostBuilder` with `AddTelerikBlazor()`, Radzen services, and `ClientAppData` singleton. All of this moves into the server's DI container. |
| **Static file serving** | MEDIUM | `UseBlazorFrameworkFiles()` serves the WASM payload. Under Blazor Server, this is replaced by `MapBlazorHub()` + `<script src="_framework/blazor.server.js">`. The `index.html` is replaced by a `_Host.cshtml` or `App.razor` host page. |
| **LongPolling transport** | LOW | All pages use `HttpTransportType.LongPolling` for SignalR client connections. Under the "double SignalR" scenario, these connections remain and still use LongPolling. Fine for localhost, but wasteful. |

### 5C. Telerik → MudBlazor

| Risk | Severity | Detail |
|---|---|---|
| **Server-side `Telerik.DataSource` in the Hub** | HIGH | `GIFBotHub.cs` imports `Telerik.DataSource` and uses `DataSourceRequest`/`DataSourceResult` for server-side data operations. This means removing Telerik isn't just a client-side job — the hub must be refactored too. |
| **Component count** | HIGH | 18+ files with Telerik components, 250+ total component instances. The StickersEditor alone has 61. This is a significant manual effort. |
| **`TelerikTreeView` / `TelerikTreeList`** | HIGH | MudBlazor has `MudTreeView` but its API is different (uses `MudTreeViewItem` children vs. Telerik's data-binding model). Animation categories use a tree structure — this mapping needs careful work. |
| **`TelerikGrid` → `MudDataGrid`** | MEDIUM | API differences in column definitions, filtering, sorting, editing, and selection. Telerik grids use `DataSourceRequest` for server-side ops; MudBlazor uses its own `ServerData` delegate pattern. |
| **`TelerikUpload`** | MEDIUM | File upload component. MudBlazor has `MudFileUpload` but the API and event model differ. The `UploadController` on the server side should be unaffected. |
| **`TelerikWindow` / `TelerikDialog`** | MEDIUM | Used for modal editing. MudBlazor has `MudDialog` with a `DialogService` pattern. The Radzen `DialogService` already in the project could be a bridge, but we should go all-in on MudBlazor. |
| **`TelerikColorPicker`** | LOW | MudBlazor has `MudColorPicker`. Straightforward swap. |
| **`TelerikRootComponent` in MainLayout** | LOW | Must be replaced with `<MudThemeProvider>`, `<MudPopoverProvider>`, `<MudDialogProvider>`, and `<MudSnackbarProvider>`. |
| **Radzen removal** | MEDIUM | Radzen is also in the project (notification/dialog services, CSS, JS). When migrating to MudBlazor, we should remove Radzen too — don't want three UI libraries. But this means migrating Radzen's notification/dialog usage as well. |
| **CSS/theming** | MEDIUM | `GIFBotPurple22.css` is a custom theme. Telerik's CSS and Radzen's `dark-base.css` are loaded in `index.html`. MudBlazor uses a completely different theming system (`MudTheme`). The custom purple theme will need to be recreated. |
| **Telerik license removal** | POSITIVE | Eliminating the Telerik dependency removes the commercial license requirement noted in the README's Contributing section. This is a clear win for open-source contributors. |

---

## 6. Gotchas for Milchick (TPM)

1. **OBS browser source survivability is the #1 risk.** The static HTML → ping → redirect pattern MUST be preserved or replaced with an equivalent. Under Blazor Server, circuit loss = blank overlay. We need a strategy — likely keeping the static HTML landing pages and having them reload the Blazor page on circuit loss. This needs prototyping before full migration.

2. **The hub is a monolith.** 130+ public methods, 72KB. Migrating to Blazor Server gives us the opportunity to bypass the hub for admin UI pages (direct service injection). But browser source pages still need real-time push — those should keep using SignalR or a lighter notification mechanism. Splitting the hub should be sequenced as a prerequisite or parallel workstream.

3. **Telerik is in the server project, not just the client.** The `Telerik.DataSource` usage in `GIFBotHub.cs` means the Telerik removal must include hub refactoring. Don't let anyone treat this as a "just swap components" task.

4. **Two UI libraries to remove, not one.** Radzen is also present. The migration to MudBlazor should include Radzen removal to avoid a three-library situation.

5. **`HttpClient` self-call pattern will break.** Every admin page uses `@inject HttpClient` to call the server's own endpoints. Under Blazor Server, this is the server calling itself over HTTP — it works but is wasteful and fragile. These should be refactored to direct service calls. This is a LOT of pages.

6. **Hosting model modernization is a prerequisite.** The `Startup.cs` pattern should be migrated to minimal hosting FIRST, before tackling WASM → Server. It's cleaner to do the .NET 10 TFM bump + hosting modernization as step 1.

7. **Stream Deck plugin is on netcoreapp3.1.** It won't build with modern SDKs. It communicates with GIFBot via its own mechanism (not SignalR). Flag it but don't block the main migration.

8. **JS Interop latency for browser sources.** Audio/video playback in OBS overlays uses JS interop. Under Blazor Server, these calls go over the wire. For localhost this is probably sub-millisecond, but test it. If there's perceivable audio/animation sync drift, we may need to push more logic into JS.

9. **Sequence suggestion:**
   - **Phase 1:** .NET 8 → .NET 10 TFM bump + minimal hosting migration (Server/Program.cs + Startup.cs consolidation). Keep WASM. Keep Telerik. Validate everything still works.
   - **Phase 2:** Blazor WASM → Blazor Server. Prototype OBS browser source reconnection strategy first. Refactor HttpClient patterns.
   - **Phase 3:** Telerik + Radzen → MudBlazor. Component-by-component replacement. Remove Telerik.DataSource from hub.
   - Phases 2 and 3 can overlap if different people work on them, but Phase 1 must complete first.

---

### 2026-02-13: GIFBot Modernization — Milestone & Task Breakdown
**By:** Milchick
**What:** Full project decomposition for .NET 10 migration, Blazor Server consolidation, and Telerik → MudBlazor replacement
**Why:** Georgia requested a structured plan before beginning work

---

## Project Inventory (Discovery Summary)

| Item | Detail |
|------|--------|
| **Solution projects** | GIFBot.Server (net8.0), GIFBot.Client (net8.0, Blazor WASM), GIFBot.Shared (net8.0) |
| **Out-of-solution** | GIFBotStreamDeckPlugin (netcoreapp3.1) — not in .sln, likely out of scope |
| **Razor/Blazor files** | 40 total (7 components, 6 layouts/shared, 18 pages, 6 browser source pages, 3 editor pages) |
| **Telerik component instances** | ~241 across 21 .razor files + 9 .cs files |
| **Telerik component types** | TelerikCheckBox (62), TelerikNumericTextBox (62), TelerikTextBox (55), TelerikWindow (21), TelerikTabStrip (17), TelerikGrid (11), TelerikDropDownList (5), TelerikTreeList (3), TelerikRootComponent (2), TelerikTooltip (2), TelerikListView (1) |
| **Radzen component instances** | ~250+ across 18 .razor files + 9 .cs files |
| **Radzen component types** | RadzenRadioButtonListItem (143), RadzenFieldset (33), RadzenRadioButtonList (19), RadzenProgressBar (12), RadzenTextBox (12), RadzenUpload (12), RadzenDropDown (5), RadzenLabel (5), RadzenPassword (4), RadzenNotification (2), RadzenNumeric (2), RadzenTree/TreeItem (3) |
| **Server-side Telerik leakage** | GIFBotHub.cs uses `Telerik.DataSource` for Regurgitator grid paging; GiveawayManager.cs has unused `using Telerik.SvgIcons` |
| **Server architecture** | Old Startup.cs pattern, GIFBotHub.cs is 2,238 lines, 5 controllers, 11 feature managers |
| **Frontend dependencies** | Telerik.UI.for.Blazor 6.0.2, Radzen.Blazor 4.32.6, Bootstrap 5.3.3, jQuery 3.3.1 + jQuery UI 1.12.1 |
| **Test projects** | None exist |
| **PWA** | Yes — service worker registered in index.html |

> **⚠️ Key Finding:** The project uses BOTH Telerik AND Radzen component libraries. Both must be replaced with MudBlazor to achieve a clean migration. This roughly doubles the UI migration scope versus Telerik-only.

> **⚠️ Key Finding:** The Server project uses old-style `Startup.cs` + `Program.cs` pattern (not minimal hosting). This must be modernized as part of the .NET 10 upgrade.

> **⚠️ Key Finding:** No test projects exist. Dylan will need to create validation tests from scratch rather than verifying existing ones pass.

---

## Milestone 0: Pre-Migration Setup & Baseline
**Goal:** Ensure we have a clean starting point and can verify nothing is broken at each step.
**Owner:** Irving + Dylan

| # | Task | Assignee | Size | Dependencies | Description |
|---|------|----------|------|-------------|-------------|
| 0.1 | Create migration branch | Irving | S | — | Create `feature/modernization` branch from main. All work targets this branch. |
| 0.2 | Verify baseline build | Dylan | S | 0.1 | Run `dotnet build GIFBot.sln` on current net8.0 code. Document any existing warnings/errors. Capture baseline state. |
| 0.3 | Create smoke test checklist | Dylan | M | 0.2 | Document manual smoke test steps: app starts, dashboard loads, navigation works, SignalR connects, animations page loads, file upload works. No test framework yet — this is a manual checklist for validation gates. |

---

## Milestone 1: .NET 10 Target Framework Upgrade
**Goal:** All three solution projects compile and run on .NET 10. No feature changes.
**Owner:** Irving
**Gate:** `dotnet build` succeeds, app starts, SignalR connects, smoke tests pass.

| # | Task | Assignee | Size | Dependencies | Description |
|---|------|----------|------|-------------|-------------|
| 1.1 | Update TargetFramework in all .csproj files | Irving | S | 0.1 | Change `net8.0` → `net10.0` in Server, Client, and Shared .csproj files. Update `RuntimeIdentifier` if needed. |
| 1.2 | Update NuGet packages to .NET 10 versions | Irving | M | 1.1 | Update all `Microsoft.AspNetCore.*` packages from 8.0.x to 10.0.x. Update `System.Drawing.Common`, `System.Speech`, `System.ComponentModel.Annotations` to 10.0.x. Update `TwitchLib`, `Newtonsoft.Json` to latest compatible versions. Keep Telerik/Radzen versions as-is for now (they'll be removed in M3). |
| 1.3 | Modernize Server hosting pattern | Irving | M | 1.2 | Replace `Startup.cs` + old `Program.cs` with .NET 10 minimal hosting pattern (`WebApplication.CreateBuilder`). Migrate all service registrations and middleware from `ConfigureServices`/`Configure` to the new `Program.cs`. Keep `Startup.cs` around but empty until verified. |
| 1.4 | Fix breaking API changes | Irving | M | 1.3 | Address any .NET 10 breaking changes: deprecated APIs, namespace changes, new required configurations. This is a catch-all task for anything that surfaces during build. |
| 1.5 | Milestone 1 validation | Dylan | S | 1.4 | Run smoke test checklist from 0.3. Verify build, app launch, SignalR, navigation, all pages load. |

---

## Milestone 2: Blazor WASM → Blazor Server Consolidation
**Goal:** Merge Client project into Server. App runs as Blazor Server (server-side rendering + SignalR circuit). Single deployable project.
**Owner:** Irving (architecture) + Helly (Razor file moves)
**Gate:** Single project builds, app runs as Blazor Server, all pages render, real-time updates work.

| # | Task | Assignee | Size | Dependencies | Description |
|---|------|----------|------|-------------|-------------|
| 2.1 | Architecture plan for consolidation | Mark | S | M1 done | Mark reviews and approves the consolidation approach: where Razor files go, how DI changes (HttpClient → direct service injection), what happens to the service worker/PWA, how wwwroot merges. Write decision to decisions.md. |
| 2.2 | Update Server .csproj for Blazor Server | Irving | M | 2.1 | Change Server SDK to support Blazor Server rendering. Add `Microsoft.AspNetCore.Components.Server` package. Remove `Microsoft.AspNetCore.Components.WebAssembly.Server`. Add MudBlazor-related packages (prep for M3). Configure interactive server-side rendering. |
| 2.3 | Create server-side App.razor and _Host | Irving | M | 2.2 | Replace Client's index.html + App.razor with server-side `_Host.cshtml` (or App.razor with `@rendermode InteractiveServer`). Move `<head>` content (CSS/JS refs) into server-hosted page. Add `blazor.server.js` script. Remove `blazor.webassembly.js` and service worker registration. |
| 2.4 | Move Razor components to Server project | Helly | L | 2.3 | Move all 40 .razor files + .razor.cs code-behinds from Client to Server. Preserve folder structure (Pages/, Components/, Shared/). Update namespaces from `GIFBot.Client.*` to `GIFBot.Server.*` (or keep in a `UI/` subfolder). |
| 2.5 | Move Client wwwroot assets to Server | Helly | S | 2.3 | Merge Client/wwwroot into Server/wwwroot. Includes CSS, JS, images, static HTML browser source pages (animations.html, stickers.html, etc.). Resolve any path conflicts. |
| 2.6 | Migrate Client DI and services | Irving | M | 2.4 | Replace `WebAssemblyHostBuilder` service registrations with server-side equivalents in Program.cs. Replace `HttpClient` usage in components with direct service injection (components can now access `GIFBot.GIFBot` singleton directly instead of going through SignalR from client). Migrate `ClientAppData` singleton. |
| 2.7 | Update SignalR connectivity model | Irving | L | 2.6 | In Blazor Server, components don't need a SignalR *client* connection — they run on the server. Evaluate which hub methods can become direct service calls vs. which must remain as hub methods (browser source pages still connect as external clients). This is the most complex task in M2. |
| 2.8 | Remove Client project from solution | Irving | S | 2.7 | Remove GIFBot.Client.csproj from GIFBot.sln. Remove Server's `<ProjectReference>` to Client. Delete or archive the Client folder. Update any remaining references. |
| 2.9 | Update _Imports.razor | Helly | S | 2.4 | Update namespace imports: remove `Microsoft.AspNetCore.Components.WebAssembly.Http`, update `GIFBot.Client` → new namespace. Keep Telerik/Radzen imports for now (removed in M3). |
| 2.10 | Milestone 2 validation | Dylan | M | 2.8, 2.9 | Full smoke test. Verify: app starts as Blazor Server, all navigation works, pages render correctly, real-time data flows work, browser source pages still accessible externally, file uploads function. |

---

## Milestone 3: Telerik + Radzen → MudBlazor Component Migration
**Goal:** Remove all Telerik and Radzen dependencies. Replace with MudBlazor equivalents. App is fully functional on MudBlazor.
**Owner:** Helly (components) + Irving (server-side Telerik removal)
**Gate:** Zero Telerik/Radzen references remain. All pages render correctly. All interactive features work.

| # | Task | Assignee | Size | Dependencies | Description |
|---|------|----------|------|-------------|-------------|
| 3.1 | Install and configure MudBlazor | Helly | S | M2 done | Add `MudBlazor` NuGet package to Server. Add `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider` to MainLayout. Update _Imports.razor with MudBlazor namespaces. Add MudBlazor CSS/JS to `_Host`. |
| 3.2 | Replace TelerikRootComponent with MudBlazor layout | Helly | S | 3.1 | Remove `<TelerikRootComponent>` from MainLayout.razor and NoNavMenuLayout.razor. Replace with MudBlazor layout components (`MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`). |
| 3.3 | Migrate TelerikTextBox → MudTextField | Helly | M | 3.1 | Replace 55 instances across all pages. Map `Value`/`ValueChanged` bindings. MudBlazor equivalent: `<MudTextField>`. |
| 3.4 | Migrate TelerikNumericTextBox → MudNumericField | Helly | M | 3.1 | Replace 62 instances. Map `Value`, `Min`, `Max`, `Step`, `Format` properties. MudBlazor equivalent: `<MudNumericField>`. |
| 3.5 | Migrate TelerikCheckBox → MudCheckBox | Helly | M | 3.1 | Replace 62 instances. Map `Value`/`ValueChanged` → `Checked`/`CheckedChanged`. MudBlazor equivalent: `<MudCheckBox>`. |
| 3.6 | Migrate TelerikWindow → MudDialog | Helly | L | 3.1 | Replace 21 instances. TelerikWindow is a positioned modal; MudDialog is the closest equivalent. May need `MudOverlay` for some use cases. Map `Visible`/`VisibleChanged`, title, size, actions. |
| 3.7 | Migrate TelerikTabStrip → MudTabs | Helly | M | 3.1 | Replace 17 instances. Map `TelerikTabStrip`/`TabStripTab` → `MudTabs`/`MudTabPanel`. Preserve tab content and selection logic. |
| 3.8 | Migrate TelerikGrid → MudDataGrid | Helly | L | 3.1 | Replace 11 instances. Map columns, sorting, paging, selection. TelerikGrid uses `DataSourceRequest` for server-side ops — must coordinate with 3.14 (server-side Telerik DataSource removal). MudBlazor equivalent: `<MudDataGrid>` with `ServerData` callback. |
| 3.9 | Migrate TelerikTreeList → MudTreeView | Helly | M | 3.1 | Replace 3 instances (AnimationsEditor primarily). Map hierarchical data binding, expand/collapse, selection. Used for animation category/item tree. |
| 3.10 | Migrate TelerikDropDownList → MudSelect | Helly | S | 3.1 | Replace 5 instances. Map `Data`, `Value`, `ValueChanged`, `TextField`, `ValueField`. |
| 3.11 | Migrate remaining Telerik components | Helly | S | 3.1 | Replace TelerikTooltip (2) → `MudTooltip`, TelerikListView (1) → `MudList`. |
| 3.12 | Migrate RadzenRadioButtonList → MudRadioGroup | Helly | L | 3.1 | Replace 19 RadioButtonList instances with 143 RadioButtonListItem entries → `MudRadioGroup`/`MudRadio`. This is the highest-count Radzen component. |
| 3.13 | Migrate remaining Radzen components | Helly | L | 3.1 | Replace: RadzenFieldset (33) → `MudPaper`/`MudExpansionPanel`, RadzenProgressBar (12) → `MudProgressLinear`, RadzenTextBox (12) → `MudTextField`, RadzenUpload (12) → `MudFileUpload`, RadzenDropDown (5) → `MudSelect`, RadzenLabel (5) → `MudText`, RadzenPassword (4) → `MudTextField` with `InputType.Password`, RadzenNumeric (2) → `MudNumericField`, RadzenNotification (2) → `MudSnackbar`, RadzenTree (3) → `MudTreeView`. |
| 3.14 | Remove Telerik DataSource from server-side code | Irving | M | 3.8 | Replace `Telerik.DataSource.DataSourceRequest` and `ToDataSourceResultAsync()` in GIFBotHub.cs with a custom paging/filtering model or MudBlazor's `GridState<T>`. Remove unused `using Telerik.SvgIcons` from GiveawayManager.cs. Update `DataEnvelope<T>` if needed. |
| 3.15 | Update Client Program.cs service registrations | Irving | S | 3.1 | Remove `AddTelerikBlazor()` and Radzen `NotificationService`/`DialogService` registrations. Add MudBlazor service registration (`AddMudServices()`). |
| 3.16 | Clean up CSS/JS references | Helly | S | 3.12, 3.13 | Remove Telerik CSS/JS (`kendo-font-icons`, `telerik-blazor.js`), Radzen CSS/JS (`dark-base.css`, `Radzen.Blazor.js`) from host page. Add MudBlazor CSS/JS. Remove old Bootstrap JS CDN links (MudBlazor includes its own styling). |
| 3.17 | Remove Telerik and Radzen NuGet packages | Irving | S | 3.14, 3.16 | Remove `Telerik.UI.for.Blazor` and `Radzen.Blazor` from .csproj. Remove `bootstrap` package if MudBlazor replaces it. Run `dotnet restore` to verify clean dependency graph. |
| 3.18 | Update _Imports.razor | Helly | S | 3.17 | Remove all `@using Telerik.*` and `@using Radzen*` lines. Ensure `@using MudBlazor` is present. |
| 3.19 | Milestone 3 validation | Dylan | L | 3.17, 3.18 | Full regression test of every page and component. Verify: all form inputs work (text, numeric, checkbox, radio, dropdown), all dialogs open/close, grids paginate and sort, tree views expand/collapse, file uploads succeed, notifications appear, tab navigation works. Zero Telerik/Radzen references in codebase. |

---

## Milestone 4: UI Polish & Theming (Stretch Goal)
**Goal:** Improve visual quality. Adopt MudBlazor design language: clean dark theme, modern typography, responsive layout.
**Owner:** Helly
**Gate:** Georgia approves the look and feel.

| # | Task | Assignee | Size | Dependencies | Description |
|---|------|----------|------|-------------|-------------|
| 4.1 | Design MudBlazor theme | Helly | M | M3 done | Create a custom `MudTheme` using MudBlazor's theming system. Reference MudBlazor website for color palette (deep purple primary, dark surfaces). Define `Palette.Primary`, `Palette.Surface`, `Palette.Background`, typography, border radius. Replace GIFBotPurple22 custom CSS. |
| 4.2 | Redesign navigation (sidebar/drawer) | Helly | M | 4.1 | Replace the current `<ul class="nav">` sidebar with `MudNavMenu`/`MudNavLink`. Add icons (MudBlazor uses Material Design icons). Make responsive with `MudDrawer` breakpoint behavior. |
| 4.3 | Improve page layouts with MudBlazor grid | Helly | L | 4.1 | Audit all pages for responsive layout. Replace raw HTML `<div class="row">` patterns with `MudGrid`/`MudItem`. Ensure consistent spacing, alignment, and mobile-friendly breakpoints. |
| 4.4 | Typography and spacing pass | Helly | M | 4.1 | Replace `<h3>`, `<b>`, `<font>` tags with `MudText` using proper `Typo` variants. Standardize padding/margins using MudBlazor spacing utilities. Remove inline styles where MudBlazor classes suffice. |
| 4.5 | Clean up legacy CSS and JS | Helly | S | 4.3 | Remove GIFBotPurple22.css, GIFBotPurple22.scss, GIFBotPurple22.json. Remove jQuery and jQuery UI if no longer needed (check browser source pages). Remove bootstrap.min.css if fully replaced by MudBlazor. Minimize app.css. |
| 4.6 | Final UI review and polish | Helly | M | 4.5 | Screenshot every page, compare against MudBlazor website aesthetic. Fix any visual inconsistencies, broken layouts, or missing styles. Ensure dark theme works end-to-end. |
| 4.7 | Milestone 4 validation | Dylan | M | 4.6 | Visual regression check of all pages. Test responsive behavior at multiple viewport sizes. Verify all interactions still work after CSS/JS cleanup. Get Georgia's sign-off. |

---

## Dependency Graph (Critical Path)

```
M0 (Setup) → M1 (NET 10) → M2 (Blazor Server) → M3 (MudBlazor) → M4 (Polish)
```

All milestones are sequential. Within each milestone, tasks can be parallelized where dependencies allow — e.g., Helly can work on Razor file moves (2.4) while Irving handles DI migration (2.6), but both must wait for 2.3.

## Sizing Summary

| Size | Count | Assignee Breakdown |
|------|-------|--------------------|
| **S** | 15 | Irving: 6, Helly: 5, Dylan: 2, Mark: 1 |
| **M** | 16 | Helly: 8, Irving: 5, Dylan: 2, Mark: 0 |
| **L** | 6 | Helly: 4, Irving: 1, Dylan: 1 |

## Risk Notes

1. **Telerik DataSource on the server** — GIFBotHub.cs uses `Telerik.DataSource` for server-side grid operations (Regurgitator feature). This creates a cross-cutting dependency that must be resolved in M3 before Telerik packages can be removed.
2. **Dual UI library (Telerik + Radzen)** — The project uses BOTH libraries, roughly doubling the component migration scope. ~490+ component instances total.
3. **No existing tests** — There are no unit or integration test projects. All validation must be manual or Dylan must create tests as part of the effort.
4. **GIFBotHub is 2,238 lines** — The main SignalR hub is massive. The Blazor Server consolidation (M2) will significantly change how components interact with it. This is the highest-risk task.
5. **Browser source pages** (animations.html, stickers.html, etc.) are standalone HTML files that connect as external SignalR clients. These must continue working after the Blazor Server migration.
6. **StreamDeck plugin** is on netcoreapp3.1 and NOT in the solution. Recommend deferring to a separate initiative unless Georgia wants it included.
7. **jQuery dependency** — Several JS files (ElementDrag.js, AnimationHelpers.js) and browser source pages may depend on jQuery. Must verify before removing in M4.

