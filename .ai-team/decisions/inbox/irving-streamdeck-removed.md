### 2026-02-13: StreamDeck plugin removed
**By:** Irving
**What:** Deleted `GIFBot/GIFBotStreamDeckPlugin/` directory and removed the StreamDeck feature mention from `Client/Pages/About.razor`. Committed on `feature/modernization` branch.
**Why:** Per Georgia's directive and team consensus — the plugin was on netcoreapp3.1, not in the main solution, and out of scope for modernization.
**Impact:** No other projects referenced StreamDeck. The Installer project is clean. Solution structure unchanged (plugin was never in GIFBot.sln).
**Note:** `dotnet build` fails on Telerik.UI.for.Blazor NuGet restore — pre-existing, not caused by this change. Team needs the Telerik private feed configured to validate builds.
