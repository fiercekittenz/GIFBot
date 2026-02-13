# Decision: Baseline Build Requires Telerik NuGet Feed

**By:** Dylan (Tester)
**Date:** 2026-02-14
**Status:** Informational

## What

The baseline `dotnet build GIFBot.sln` on main fails at NuGet restore because `Telerik.UI.for.Blazor` v6.0.2 is a commercial package requiring a private feed. No `NuGet.config` with the Telerik source exists in the repo.

## Impact

- No team member can build the solution without Telerik credentials configured locally
- CI/CD would also fail without the feed
- This blocks baseline build validation (M0.3 gate)

## Recommendation

Georgia should confirm whether a local Telerik NuGet feed is configured on her machine (typically at `%APPDATA%\NuGet\NuGet.Config` or via Visual Studio). If so, the build likely succeeds locally for her. For the team, we need one of:

1. A repo-level `NuGet.config` pointing to the Telerik feed (with credentials handled via environment variables, not committed)
2. Or acceptance that baseline build validation happens only after M3 (MudBlazor replacement removes the Telerik dependency entirely)

Option 2 is pragmatic — we're removing Telerik anyway. Validate build at M1 after Irving's branch removes the StreamDeck project and before Telerik replacement begins.
