# Session: 2026-02-13 Modernization Plan & Risk Analysis

**Requested by:** Georgia Nelson

## Summary

Milchick decomposed the GIFBot modernization into 5 milestones and 44 tasks:
- **M0:** Pre-migration setup & baseline (3 tasks)
- **M1:** .NET 8 → .NET 10 TFM upgrade (5 tasks)
- **M2:** Blazor WASM → Blazor Server consolidation (10 tasks)
- **M3:** Telerik + Radzen → MudBlazor migration (19 tasks)
- **M4:** UI polish & theming — stretch goal (7 tasks)

Mark analyzed the architecture and identified critical risks:
- **Dual UI library dependency:** Project uses BOTH Telerik AND Radzen (250+ Radzen instances + 241 Telerik instances), doubles migration scope
- **Hosting model debt:** Server uses deprecated Startup.cs pattern; must modernize to .NET 10 minimal hosting first
- **OBS browser source survivability:** CRITICAL PATH. Static HTML landing pages → ping redirect → Blazor WASM. Under Blazor Server, circuit loss = blank overlays. Requires reconnection strategy.
- **Monolithic hub:** GIFBotHub.cs is 2,238 lines with 130+ public methods. Telerik DataSource leakage: server-side uses `Telerik.DataSource` for Regurgitator grid paging.
- **HttpClient self-call pattern:** Every admin page injects HttpClient and calls `http://localhost:5000/...`. Under Blazor Server, server calls itself; must refactor to direct service injection.

## Key Findings

1. Both Telerik AND Radzen present in codebase (~490+ total component instances)
2. No test projects exist; all validation is manual
3. Browser source pages (6 total) are standalone HTML files with jQuery + polling
4. StreamDeck plugin (netcoreapp3.1) is out-of-scope, not in solution
5. Recommended sequence: Phase 1 (.NET 10 + hosting modernization) → Phase 2 (Blazor Server) → Phase 3 (MudBlazor)
