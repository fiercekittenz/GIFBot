# Dylan — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server + Client + Shared projects, SignalR for real-time communication, Feature-based module system
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Validate modernization migration — ensure .NET 10 works, Blazor Server consolidation is stable, MudBlazor replacements are functionally equivalent

## Learnings

---

📌 **Team update (2026-02-13):** Validation responsibilities span 4 milestones: baseline smoke test creation (M0.3), build/startup/SignalR validation (M1.5), Blazor Server consolidation smoke test (M2.10), full regression testing for all 400+ components (M3.19), visual regression + responsive testing (M4.7). No test projects currently exist; all validation is manual against checklist-driven smoke tests. — Milchick

### Baseline Build Assessment (main branch, pre-migration)
- **Build result:** FAILURE — `dotnet build GIFBot.sln` fails at NuGet restore with 2 errors
- **Root cause:** `Telerik.UI.for.Blazor` (v6.0.2) requires a private NuGet feed (commercial license). No `NuGet.config` with the Telerik feed exists in the repo.
- **Error details:** NU1101 on both Client and Server projects (Server fails transitively via ProjectReference to Client)
- **Pre-existing suppressed warnings in Server.csproj:** 1701, 1702, CA1416 (platform compatibility)
- **Implication:** Baseline build cannot be validated without Telerik NuGet feed credentials. This is a known commercial dependency, not a code defect.

### Test Infrastructure Status
- Zero test projects in the solution (confirmed: no `*Test*` or `*Tests*` csproj files)
- No xUnit, NUnit, or MSTest references anywhere
- All validation is manual; smoke test checklist created at `GIFBot/SMOKE-TESTS.md`
- Recommended framework for future automated tests: **xUnit** (standard for modern .NET, good Blazor component testing story via bUnit)

### Key Feature Areas Discovered
- **13 navigable pages** via sidebar: Dashboard, Bot Settings, Animations, Giveaway, Goal Bar, Greeter, Stickers, Regurgitator, Backdrop, Countdown, Help, Charity, About
- **6 browser source overlay pages** with HTML landing stubs: animations, stickers, secondarystickers, goalbar, countdowntimer, backdrop
- **11 server feature modules:** Animations, Backdrop, ChannelPoints, CountdownTimer, Giveaway, GoalBar, Greeter, Regurgitator, Stickers, StreamElements, Tiltify
- **5 API controllers:** PingController, RemoteController, StreamlabsController, UploadController, UtilityController
- **1 SignalR hub** (GIFBotHub.cs) — monolithic, ~130 public methods
- **Telerik server-side dependency:** DataSourceRequest/DataSourceResult used in GIFBotHub.cs for Regurgitator grid operations (also referenced in GiveawayManager.cs)
- **Test.razor** exists at `/test` — a Telerik TreeList demo page, likely a dev scratchpad (candidate for removal)
