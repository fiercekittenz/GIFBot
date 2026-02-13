### 2026-02-13: User directive — Remove Telerik/Radzen before .NET 10 upgrade
**By:** Georgia Nelson (via Copilot)
**What:** Skip configuring the Telerik NuGet feed. Remove Telerik and Radzen packages first, replace with MudBlazor, get the build working. Then proceed with .NET 10 upgrade and Blazor Server consolidation.
**Why:** User request — the Telerik license/feed is unavailable. Resequences milestones: component migration (formerly M3) moves to M1.
