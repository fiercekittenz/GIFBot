# Helly — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (→.NET 10), Blazor WASM (→Blazor Server), SignalR, Telerik (→MudBlazor)
- **Goal:** UI modernization: replace Telerik with MudBlazor, redesign navigation, clean legacy CSS/JS

## Completed Work

📌 **M3: Telerik + Radzen → MudBlazor component migration**
- Migrated ~490+ component instances across 28+ files
- Replaced all Telerik/Radzen UI with MudBlazor equivalents (TextBox→MudTextField, Grid→MudDataGrid, etc.)
- TODOs: InputFile upload handlers, DialogFactory→IDialogService, DataGrid column mapping refinements

📌 **M4 Tasks 4.1–4.6: UI redesign + styling**
- Task 4.1: Custom dark theme with GIFBot purple (#7e57c2 primary, #b39ddb secondary)
- Task 4.2: Navigation redesigned with MudLayout/MudDrawer/MudAppBar, Open Iconic→Material Design icons
- Tasks 4.3–4.4: Typography pass (22 .razor files), replaced Bootstrap grid with MudGrid + utility classes
- Task 4.5: Deleted Telerik theme (1MB), minimized app.css, removed CDN scripts (jQuery/Popper/Bootstrap JS no longer used)
- Task 4.6: Added 4 CSS utility classes for consistent background colors, replaced 65 inline style instances

## Key Learnings
- Regex lazy quantifiers (`.+?`) with nested HTML are fragile — use structural parsing instead
- GIFBot's purple identity scattered across files; standardized on single hex for MudBlazor
- Browser source HTML files (OBS) have separate jQuery; CDN load was redundant
- 18 files still use Bootstrap `btn` classes and `oi-*` icons — blocking full Bootstrap/Open Iconic removal
- Inline background color patterns fall into 4 tiers; centralizing into CSS classes enables easy theme updates

— Helly
