# Burt — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot with ASP.NET backend (SignalR) + Blazor Server frontend
- **Stack:** C#, .NET 10, Blazor Server, MudBlazor 8.5.0, SignalR
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Modernize UI: migrated from Telerik → MudBlazor, .NET 8 → .NET 10, Blazor WASM → Blazor Server
- **Nature:** LOCAL desktop app — launches browser to localhost:5000. Not a public website.
- **OBS integration:** Browser source pages use BasicLayout — must NOT be affected by theme changes

## Project Learnings (from import)
- Unified color palette around deep blue/purple (PaletteDark: Primary #7e57c2, Background #1a1a2e, Surface #22223a)
- CSS panel hierarchy: .gifbot-page-header (#2d2b52), .gifbot-content-panel (#1e1e38), .gifbot-section-panel (#161630), .gifbot-form-panel (#1c1c34)
- Global input fill: .mud-input-control .mud-input { background-color: #2a2a48 !important }
- Dialog styling: .mud-dialog { min-width: 450px, border: 1px solid #2d2b52, bg: #1c1c34 }, title bar: #2d2b52
- Typography: GitHub font stack (-apple-system, BlinkMacSystemFont, "Segoe UI", etc.)
- Georgia likes the deep blue/purple identity and wants uniform styling across all pages
- Georgia prefers fixing problems in code rather than covering them up
- Bootstrap buttons and HTML tables are being progressively replaced with MudButton/MudGrid

## Learnings
- **Bootstrap button remnants:** Found ~85 instances of `btn btn-secondary` / `btn btn-primary` across 12 pages. Dashboard (Index.razor) Quick Actions navbar is highest-impact target.
- **open-iconic icons:** ~42 instances of `oi-*` classes remain, primarily in page headers and DataGrid action buttons. All have direct MudBlazor Material Icon equivalents (see ux-audit.md mapping table).
- **Font selector anti-pattern:** CountdownTimer.razor and GoalBarEditor.razor use 24+ MudRadio buttons for font selection. Should be MudSelect dropdown with live preview.
- **Button bar placement:** Some pages (CountdownTimer, BackdropEditor) have Save/Cancel at both top AND bottom. Standard should be bottom-right only with gap-3 spacing.
- **Custom button CSS classes work with MudButton:** `.gifbot-action-btn`, `.gifbot-secondary-btn` etc. can be applied via `Class` parameter on `<MudButton>` without modification.
- **NavMenu already uses Material Icons:** Provides precedent for icon migration. Icons match the MudBlazor palette secondary color (#b39ddb).
- **Settings User Groups tab:** Well-implemented with MudDialog, MudDataGrid, proper field descriptions — good reference for other pages.
- **StickersEditor dialog issue:** Uses Bootstrap navbar INSIDE MudDialog for Cancel/Update buttons. Should use `<DialogActions>` component.
- **GreeterEditor legacy CSS:** Contains embedded `<style>` block with Telerik `.k-*` classes that can be removed.
- **Setup wizard (Setup.razor):** Best-implemented page — centered layout, consistent MudButton usage, clean step progression. Use as reference.
