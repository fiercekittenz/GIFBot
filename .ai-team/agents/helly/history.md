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
- NoNavMenuLayout needs its own MudTheme definition (duplicated from MainLayout) since the theme is a private static field — extracting to a shared static class would be cleaner if more layouts appear
- Setup wizard panels use `max-width: 700px; width: 100%` with `d-flex justify-center` parent for responsive centering
- `html, body` background set to `#121218` globally in app.css; browser source pages are unaffected since they set their own backgrounds via inline styles
- Deprecated `<center>` tags replaced with MudBlazor utility classes (`d-flex justify-center`)
- Bare `<button>` elements replaced with `<MudButton Variant="Variant.Filled" Color="Color.Primary">` for consistent theming
- Color scheme unified around Georgia's preferred deep blue/purple (#1a1a2e) — all neutral grays replaced with blue-tinted equivalents
- Unified palette key values: Background #0d0d1a, Surface #22223a, BackgroundGray #161628, AppbarBackground/DrawerBackground #1a1a2e (anchor)
- CSS panel classes shifted from maroon/pink tones to blue/purple: page-header #2d2b52, content-panel #1e1e38, section-panel #161630, form-panel #1c1c34
- Text readability improved: TextPrimary bumped from #ffffffb3 (70%) to #ffffffde (87%), LinesInputs from #ffffff4d to #ffffff80 for visible text field borders
- Typography FontFamily updated from Roboto to match the system font stack already declared in app.css

- Added `.mud-input-control .mud-input` CSS rule in app.css with `background-color: #2a2a48`, `border-radius: 4px`, and horizontal padding — gives all MudTextField/MudSelect inputs a visible filled background against dark panels
- Removed legacy inline `Style="background-color: #2c2241; color: …"` from 20 .razor files (Server + Client) — CSS override now handles input appearance uniformly
- Also removed `Style="color: #232323"` (dark text on dark bg) from Index, AddCategory, AnimationTutorial MudTextField/MudNumericField elements
- Changed `Background` palette value from `#0d0d1a` to `#1a1a2e` in both MainLayout.razor and NoNavMenuLayout.razor — eliminates visible dark gray area behind page content
- Updated `html, body` background in app.css from `#0d0d1a` to `#1a1a2e` to match the new palette Background value

- **Container height fix:** Added `.mud-main-content .mud-container { min-height: calc(100vh - 64px); }` — the 64px accounts for MudAppBar's default height, making the container background extend to the bottom of the viewport instead of cropping at the content boundary
- **Bulk inline color cleanup (42 files):** Removed ~500 inline `background-color` styles across Server Pages, Server UI components, Client Pages, Client Components, and Client Shared layouts. Each hardcoded hex was replaced with a reusable CSS class in app.css:
  - `.gifbot-quick-actions` (#1e1e38) — navbar backgrounds (was #1f0c24)
  - `.gifbot-action-btn` (#3d3566) — primary action buttons (was #5c4872)
  - `.gifbot-log-panel` (#12122a) — output log panels (was #1e1e1e)
  - `.gifbot-secondary-btn` (#3a2d56) — secondary/icon buttons (was #6c4872)
  - `.gifbot-primary-btn` (#6b2fa0) — submit/primary buttons (was #8f269e)
  - `.gifbot-danger-btn` (#4a2d3d) — delete/danger buttons (was #724859)
  - `.gifbot-special-btn` (#2d3566) — special action buttons (was #484d72/#485472)
  - `.gifbot-neutral-btn` (#3a3a5c) — neutral grey buttons (was #646464)
  - `.gifbot-layout-dark` (#0e0e1e) — layout wrapper backgrounds (was #101010/#121218)
- Reused existing classes: `.gifbot-page-header` for jumbotrons (#36173e), `.gifbot-content-panel` for containers (#211126), `.gifbot-form-panel` for form wrappers (#1d161f)
- InputFile elements kept inline styles but shifted from grey #1e1e1e to blue-tinted #16162e
- Exceptions preserved: #230000 warning reds, transparent video backgrounds, canvas editor (#dedede/#45356f), BrowserSource pages

- **MudDialog patterns used:** Inline `<MudDialog @bind-Visible>` with `<TitleContent>`, `<DialogContent>`, and `<DialogActions>` sections for Add Group, Add User, Delete Group, and Rename Group dialogs. Cancel always on LEFT, action on RIGHT. Validation via `Disabled` prop with computed properties.
- **Bootstrap→MudBlazor replacements on User Groups tab:** All `<button class="btn ...">` replaced with `<MudButton Variant/Color>`. Trash icon `<span class="oi oi-trash">` replaced with `<MudIconButton Icon="@Icons.Material.Filled.Delete">`. Cancel/Save page buttons also migrated to MudButton.
- **HTML table→MudBlazor layout:** Nested `<table>` elements replaced with `<MudGrid>`/`<MudItem>` for the select+button row, and `<MudStack Row="true">` for grouping action buttons (Rename, Clone, Delete).
- **MudSelect population fix:** Added `@foreach` over `mUserGroupNames` with `<MudSelectItem>` children — the dropdown was rendering empty before.
- **CSS:** Added `.gifbot-bordered-table` with `border: 1px solid #2d2b52; border-radius: 4px` for the DataGrid wrapper.
- **Dialog validation:** `IsAddGroupDisabled` checks empty + case-insensitive duplicate group name. `IsAddUserDisabled` checks empty + user already in current group.

— Helly

---

📌 **Team update (2026-02-13):** Decisions merged from session log: User Groups tab redesigned with MudDialog popups for CRUD (Add/Delete with validation), replaced Bootstrap buttons with MudButton, HTML tables with MudGrid, fixed MudSelect population. Theme decision consolidated: custom MudTheme with #7e57c2 deep purple, dark surfaces; NoNavMenuLayout kept in sync with MainLayout theme (duplicated, future refactor candidate). — Scribe
