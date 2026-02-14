# Helly — History

📌 Team update (2026-02-14): Page style audit methodology established; 16 pages audited against Settings/GiveawayEditor standard with 14 work items decomposed across 6 priority tiers — decided by Burt, Milchick


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

- **Global MudDialog CSS styling:** Added rules in app.css targeting `.mud-dialog`, `.mud-dialog .mud-dialog-title`, `.mud-dialog .mud-dialog-content`, and `.mud-dialog .mud-dialog-actions`. Key properties: `min-width: 450px` (wider dialogs), `background-color: #1c1c34` (matches `.gifbot-form-panel`), title bar gets `background-color: #2d2b52` (matches `.gifbot-page-header`), `border: 1px solid #2d2b52`, `border-radius: 8px`. These are global — every MudDialog across the app benefits automatically.
- **Dialog field title+description pattern:** Replaced `<label><b>Name:</b></label>` + `<small class="form-text">` with `<MudText Typo="Typo.subtitle1"><b>Title</b></MudText>` + `<MudText Typo="Typo.caption" Class="text-white-50 mb-2">description</MudText>` above each input field. Applied to 10 files: Settings (4 dialogs), AnimationsEditor (6 dialogs), EditAnimation (1), BackdropEditor (2), CountdownTimer (2), GiveawayEditor (1), GoalBarEditor (1), GreeterEditor (2), StickersEditor (4), Index (1). Confirmation dialogs get `<MudText Typo="Typo.subtitle1"><b>Confirm Deletion</b></MudText>` subtitle. Bootstrap `<button>` + `<center>` replaced with `<DialogActions>` + `<MudButton>` in dialog action areas.
- Page headers standardized with `MudStack` + `MudIcon` + `MudText` to replace open-iconic icons.
- Editor actions standardized with `MudIconButton` for row actions and bottom-right Save/Cancel bars using outlined Cancel + primary Save.
- **Page header icon standard:** Wrap headers in `<MudStack>` with a `MudIcon` + `MudText` inside `.gifbot-page-header` for Material icon consistency.
- **Dashboard quick actions pattern:** Use `<MudPaper>` + `<MudStack Row="true" Wrap="Wrap.Wrap">` with `MudButton Variant="Variant.Filled"` and `.gifbot-action-btn` for action bars.
- **Font selector pattern:** Replace large font radio groups with `MudSelect` dropdowns for compact form layouts.
- **Form action placement:** Standardize Save/Cancel actions in a bottom-right MudButton bar with outlined Cancel and filled Primary Save.
- Applied MudBlazor button/icon swaps in nested components, using Variant="Filled" MudButton/MudIconButton and Material icons (Folder, PlayArrow, Close, Delete, Stop).
- Remaining legacy references: open-iconic `oi-cog` spans in `Server/Components/Pages/Setup/Setup.razor`; no `btn btn-*` matches left under `Server/Components`.

— Helly

---

📌 **Team update (2026-02-13):** Decisions merged from session log: User Groups tab redesigned with MudDialog popups for CRUD (Add/Delete with validation), replaced Bootstrap buttons with MudButton, HTML tables with MudGrid, fixed MudSelect population. Theme decision consolidated: custom MudTheme with #7e57c2 deep purple, dark surfaces; NoNavMenuLayout kept in sync with MainLayout theme (duplicated, future refactor candidate). — Scribe

---

📌 **Team update (2026-02-14):** UX audit completed by Burt. Three implementation decisions added: UI Component Migration Priority Order (Dashboard first, then DataGrid buttons, headers, font selectors, button bars), Page Header Icon Standard (use Material Icons instead of open-iconic, eliminate ~15KB CSS dependency), Button Bar Position Standard (Save/Cancel buttons at bottom-right only, not duplicated at top). These guide your Bootstrap cleanup work. — Scribe

---

📌 **Editor icon mapping decision (2026-02-14):** Icon mapping for editor cleanup — map remaining open-iconic action icons to Material Design equivalents (`oi-x` → `Icons.Material.Filled.Close`, `oi-delete` → `Icons.Material.Filled.Delete`), continue existing edit/play mappings. — Scribe

## Learnings
- Added MudBlazor page header + content panel framing on the remaining test/error pages for consistent layout polish.
- Standardized MudCheckBox labels with inline Label usage and added inline-flex styling to keep labels aligned.
- Restyled GiveawayEditor.razor to match Settings.razor panel structure: wrapped MudTabs in `gifbot-form-panel`, all tab content sections in `gifbot-section-panel` > `p-2` divs, moved Cancel/Save buttons outside tabs but inside form panel. Pattern: outer `gifbot-form-panel` → `MudTabs` → each tab's content in `gifbot-section-panel`. This is the standard for all editor pages going forward.
- **AnimationSelectorComponent GUID fix:** Added `Placeholder="None"` and `ToStringFunc` to MudSelect so `Guid.Empty` renders as "None" instead of `00000000-...`. Also added an explicit "None" `MudSelectItem` for clearing selection. Pattern: any MudSelect with a GUID type should use a `ToStringFunc` to resolve display names.
- **Banned Users tab restyled to mirror User Groups tab:** Removed search bar from `ToolBarContent`, added description paragraph, wrapped DataGrid in `.gifbot-bordered-table`, set `Dense="true"`, delete button uses `Color="Color.Error"` instead of `.gifbot-neutral-btn`, "Add Banned User" button placed below grid in `d-flex justify-end` div. Added delete confirmation dialog (`mIsDeleteBannedUserDialogVisible` + `mPendingDeleteBannedUser`) matching the User Groups delete pattern. Add dialog now has `Immediate="true"` on the text field and `Disabled` validation preventing empty or duplicate entries.
- **CountdownTimer page restructured** (`GIFBot\Server\Components\Pages\Features\CountdownTimer.razor` + `.razor.cs`): Applied standard panel hierarchy — MudTabs wrapped in `gifbot-form-panel`, General and Timer Actions tab content wrapped in `gifbot-section-panel` > `p-2`. Cancel/Save buttons moved outside MudTabs but inside form-panel. Added delete confirmation dialog (`mIsDeleteActionDialogVisible` + `mPendingDeleteAction`) matching GiveawayEditor pattern. Fixed clipboard copy to use `navigator.clipboard.writeText` instead of custom JS `CopyToClipboard`. Removed `Variant="Variant.Filled"` from font MudSelect (global CSS handles it). Changed `gifbot-content-panel` to `gifbot-section-panel` on action buttons bar.
- **GoalBarEditor page restructured** (`GIFBot\Server\Components\Pages\Features\GoalBarEditor.razor`): Applied standard panel hierarchy — `gifbot-form-panel` now wraps MudTabs (was inverted, inside Settings tab). All four tabs wrapped in `gifbot-section-panel` > `p-2`. Cancel/Save buttons inside form-panel after MudTabs. Added delete confirmation dialog (`mIsDeleteGoalDialogVisible` + `mPendingDeleteGoalId`) for goals. Fixed clipboard copy to use `navigator.clipboard.writeText` instead of `CopyToClipboard`. Replaced bare `<table>` with flex layout (`d-flex align-center gap-2`). Removed `Variant="Variant.Filled"` from font MudSelect. Intro paragraph placed above section-panel inside the Settings tab.
- **BackdropEditor page restructured** (`GIFBot\Server\Components\Pages\Features\BackdropEditor.razor` + `.razor.cs`): Applied standard panel hierarchy — MudTabs wrapped in `gifbot-form-panel`, General and Backdrops tab content wrapped in `gifbot-section-panel` > `p-2` (Browsersource URL tab already had it). Cancel/Save buttons moved from inside General tab to after `</MudTabs>` but inside form-panel. Added delete confirmation dialog (`mIsDeleteDialogVisible` + `mPendingDeleteBackdrop`) matching GiveawayEditor pattern. Fixed clipboard copy to use `navigator.clipboard.writeText` with actual URL value instead of element name. Pattern confirmed: all editor pages follow outer `gifbot-form-panel` → `MudTabs` → each tab's content in `gifbot-section-panel` → Cancel/Save after tabs.
- **Regurgitator page restructured** (`GIFBot\Server\Components\Pages\Features\Regurgitator.razor` + `.razor.cs`): P2.1 page style audit fix. Replaced Bootstrap navbar (`navbar navbar-expand-sm`) with `MudPaper` + `MudStack` toolbar for package selector and Add/Delete buttons. Applied standard panel hierarchy — `gifbot-form-panel` wraps MudTabs (was inverted, inside Settings tab). All three tabs wrapped in `gifbot-section-panel` > `p-2` (Trigger Costs already had it). Cancel/Save buttons moved outside MudTabs but inside form-panel. Added delete confirmation dialogs: `mIsDeletePackageDialogVisible` + `mPendingDeletePackageName` for package deletion, `mIsClearListDialogVisible` for clearing all entries in Data Entries tab. Replaced bare `<table>` layout with flex (`d-flex align-center gap-2`). Removed deprecated `<center>` tag from MudSlider. Entry deletion remains immediate (no dialog) since entries are small list items, but Clear List gets confirmation since it deletes everything.

