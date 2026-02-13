# Helly — History

## Project Context
- **Project:** GIFBot — Interactive Twitch bot
- **Stack:** C#, .NET 8 (migrating to .NET 10), Blazor WASM (migrating to Blazor Server), SignalR, Telerik (replacing with MudBlazor)
- **Architecture:** Server project (ASP.NET + SignalR), Client project (Blazor WASM), Shared project (models/utilities)
- **Frontend:** Blazor WASM with Telerik Blazor UI components, browser source pages for OBS, SignalR client for real-time updates
- **User:** Georgia Nelson (gnelson@microsoft.com)
- **Goal:** Replace Telerik with MudBlazor, improve UI, target MudBlazor website aesthetics for color/fonts/responsiveness

## Learnings

---

📌 **Team update (2026-02-13):** Component migration scope is ~490+ instances across Telerik AND Radzen (not just Telerik). Milchick's plan includes 19 component migration tasks in Milestone 3. Radzen must be removed alongside Telerik to avoid three-library situation. Server-side Telerik.DataSource refactoring (task 3.14) is prerequisite before DataSource-dependent components can be fully migrated. — Milchick

📌 **Completed: Telerik + Radzen → MudBlazor component migration (all .razor and .razor.cs files)**

### What was done
- Migrated **~490+ component instances** across **28+ files** in `Client/Pages/`, `Client/Shared/`, and `Client/Components/`
- Replaced all Telerik and Radzen UI components with MudBlazor equivalents
- Replaced `Radzen.NotificationService` injection and calls with `ISnackbar`/`Snackbar.Add()` in all files
- Commented out `DialogFactory` cascading parameters with TODO notes (needs `IDialogService` wiring)
- Replaced all `Telerik.DataSource` imports and type references (EventArgs substituted for Telerik event types)
- Converted `RadzenUpload` to `<InputFile>` with TODO comments for handler wiring

### Component mapping summary
| Old | New |
|---|---|
| TelerikTextBox | MudTextField T="string" |
| TelerikNumericTextBox | MudNumericField T="int/double" |
| TelerikCheckBox | MudCheckBox T="bool" |
| TelerikTabStrip / TabStripTab | MudTabs / MudTabPanel |
| TelerikWindow / WindowTitle / WindowContent | MudDialog / TitleContent / DialogContent |
| TelerikGrid / GridColumns / GridColumn | MudDataGrid / Columns / PropertyColumn |
| TelerikTreeList | MudDataGrid (simplified) |
| TelerikDropDownList | MudSelect + MudSelectItem |
| TelerikTooltip | MudTooltip (comment) |
| TelerikListView | MudList |
| RadzenRadioButtonList + Items | MudRadioGroup + MudRadio |
| RadzenFieldset | MudExpansionPanel |
| RadzenProgressBar | MudProgressLinear |
| RadzenTextBox | MudTextField T="string" |
| RadzenPassword | MudTextField T="string" InputType="InputType.Password" |
| RadzenNumeric | MudNumericField T="int" |
| RadzenUpload | InputFile (with TODO) |
| RadzenDropDown | MudSelect |
| RadzenLabel | MudText |
| RadzenTree / RadzenTreeItem | MudTreeView / MudTreeViewItem |
| RadzenNotification | Removed (ISnackbar is service-based) |
| TelerikRootComponent | MudThemeProvider + MudPopoverProvider + MudDialogProvider + MudSnackbarProvider |

### Known TODOs left for follow-up
1. **InputFile upload handlers** — `RadzenUpload` was replaced with `<InputFile>` but the event handlers (`OnImportVisualFileProgress`, `OnImportVisualFileComplete`, error handlers) still need rewiring to use `IBrowserFile` and manual HTTP upload
2. **DialogFactory → IDialogService** — All `Dialogs.ConfirmAsync`/`PromptAsync` calls are commented out with `Task.FromResult` placeholders. Needs proper MudBlazor dialog components.
3. **MudDataGrid column mapping** — Grid columns use `PropertyColumn`/`TemplateColumn` but some `Field=` attributes may not map 1:1 to MudBlazor's `Property=` expression syntax
4. **TreeList → MudDataGrid** — TelerikTreeList was simplified to MudDataGrid; hierarchical display is lost. Consider MudTreeView for full tree UI.
5. **MudSelect items** — Some selects may need `ToStringFunc` or additional item rendering
6. **Event handler signatures** — Some handlers still have `EventArgs` where MudBlazor may use different types (e.g., grid command buttons)

### Learnings
- Regex `[^>]*` breaks on `>` inside Blazor lambda expressions like `@(args => Handler(args))`. Use `.*?` with `re.DOTALL` instead.
- `RadzenRadioButtonListItem` was being partially matched by `RadzenRadioButtonList` regex — always process more-specific patterns first.
- `@using Telerik.DataSource` regex also matches the start of `@using Telerik.DataSource.Extensions`, leaving `.Extensions` orphaned — process longer patterns first.
- StickersEditor has capital R: `StickersEditor.Razor.cs` — Python's `endswith('.razor.cs')` is case-sensitive even on Windows.
- MudSelect doesn't have `Data=` attribute — items must be rendered as child `<MudSelectItem>` elements.

— Helly

---

📌 **Completed: M4 Task 4.1 — Custom MudBlazor dark theme**

### What was done
- Added a full custom `MudTheme` to `MainLayout.razor` with `IsDarkMode = true`
- Configured `PaletteDark` with GIFBot's identity purple (`#7e57c2` primary, `#b39ddb` secondary) sourced from the original Telerik theme's accent color (`#5d3e9c`) and App.razor's slider thumb (`#651298`)
- Dark surface/background colors (`#1e1e2d` / `#121218`) with subtle purple tinting to match the MudBlazor website aesthetic
- Drawer/appbar use `#1a1a2e` — coordinated with the existing sidebar gradient (`rgb(47,39,103)` → `#22003d`)
- Set `PaletteLight` with basic purple colors as fallback, but dark mode is the default
- Typography set to Roboto (already loaded in App.razor `<head>`)
- All four MudBlazor providers already existed — updated `MudThemeProvider` with `Theme` and `IsDarkMode` bindings

### Key file paths
- `GIFBot/Server/Components/Layout/MainLayout.razor` — theme definition lives here in `@code` block
- `GIFBot/Server/wwwroot/css/GIFBotPurple22.css` — Telerik/Kendo theme (1MB), identity purple is `#5d3e9c`, base dark is `#2a1538`
- `GIFBot/Server/wwwroot/css/app.css` — sidebar gradient uses `rgb(47,39,103)` → `#22003d`
- `GIFBot/Server/Components/App.razor` — slider thumb uses `#651298` (another GIFBot purple reference)

### Learnings
- MudBlazor 8.5.0 uses `PaletteLight` and `PaletteDark` (not the older `Palette` class)
- GIFBot's purple identity is spread across multiple files with slightly different hex values — standardized on `#7e57c2` (deep purple 400) for MudBlazor primary
- Existing layout structure (sidebar div + main div) was left intact — nav restructuring is task 4.2
- Existing CSS not removed — cleanup is task 4.5

— Helly

---

📌 **Completed: M4 Task 4.2 — Redesign navigation sidebar with MudBlazor**

### What was done
- Replaced `NavMenu.razor`: removed Bootstrap `<ul>/<li>/<NavLink>` list and Open Iconic icons, replaced with `<MudNavMenu>` + `<MudNavLink>` items using Material Design icons
- Replaced `MainLayout.razor`: removed raw `<div class="sidebar">` / `<div class="main">` structure, replaced with `<MudLayout>` + `<MudAppBar>` + `<MudDrawer>` + `<MudMainContent>`
- AppBar shows "GIFBot" title with hamburger toggle for the drawer
- GIFBot logo moved from NavMenu bottom to MainLayout drawer bottom area
- Version check logic (`HttpClient` injection, `OnInitializedAsync`) preserved in NavMenu
- Conditional "Update Available" nav link uses `Color.Secondary` for visibility
- Theme `@code` block untouched
- Build: 0 errors, 32 pre-existing warnings

### Icon mapping applied
| Open Iconic | Material Design |
|---|---|
| oi-home | Icons.Material.Filled.Dashboard |
| oi-cog | Icons.Material.Filled.Settings |
| oi-play-circle | Icons.Material.Filled.PlayCircle |
| oi-box | Icons.Material.Filled.CardGiftcard |
| oi-target | Icons.Material.Filled.TrackChanges |
| oi-thumb-up | Icons.Material.Filled.WavingHand |
| oi-badge | Icons.Material.Filled.EmojiEmotions |
| oi-trash | Icons.Material.Filled.Recycling |
| oi-image | Icons.Material.Filled.Wallpaper |
| oi-timer | Icons.Material.Filled.Timer |
| oi-question-mark | Icons.Material.Filled.Help |
| oi-people | Icons.Material.Filled.Favorite |
| oi-tag | Icons.Material.Filled.Info |
| oi-bullhorn | Icons.Material.Filled.NewReleases |

### Learnings
- MudNavLink uses `Href` (not `href`) and `Icon` parameter — no child `<span>` needed for icons
- MudDrawer with `ClipMode="DrawerClipMode.Always"` clips under the AppBar for a clean Material look
- `MudSpacer` inside the drawer pushes the logo to the bottom — same flex trick as in the AppBar
- The old `<font color>` tag for "Update Available" replaced with `MudText Color="Color.Secondary"` + `IconColor="Color.Secondary"` on MudNavLink

— Helly

---

📌 **Completed: M4 Tasks 4.3 + 4.4 — MudGrid layouts + Typography/spacing pass**

### What was done
- **Typography**: Replaced all `<h1>` → `<MudText Typo="Typo.h4">`, `<h3>` → `Typo.h6`, `<h4>`/`<h5>` → `Typo.subtitle1` across 18 files
- **Font tags**: Replaced `<font class="text-white">` → `<MudText>`, `text-success` → `Color="Color.Success"`, `text-error` → `Color="Color.Error"`
- **Grid layouts**: Converted `<div class="row">` + `<div class="col-sm">` → `<MudGrid Spacing="3">` + `<MudItem>` in Index.razor (log + animations two-column layout)
- **Button bars**: Replaced ~10 files' `<div class="row"><div class="container-fluid text-right p-3">` with `<div class="d-flex justify-end pa-3">`
- **Bootstrap removal**: Removed `container` / `container-fluid` classes from all page/UI files, keeping only utility classes
- **Jumbotron headers**: Replaced `jumbotron jumbotron-fluid p-1 my-1 text-left` with `pa-1 my-1` across all pages
- **Column classes**: Replaced `col-sm text-right p-3` → `d-flex justify-end pa-3`, `container-fluid text-right` → `d-flex justify-end`
- **Spacing**: Replaced `style="margin-top: 2px"` with `class="mt-1"` in CategoryComponent
- **Help.razor**: Simplified Q&A row/col wrappers to plain `<div>` elements (single-column stacking)
- **Skipped**: BrowserSource pages (OBS overlays with BasicLayout), PlacementComponent row/col (complex interactive canvas with JS interop)
- Build: 0 errors, 32 pre-existing warnings
- 22 .razor files modified total

### Files modified
Pages: About, AddCategory, AnimationsEditor, AnimationTutorial, BackdropEditor, Charity, CountdownTimer, EditAnimation, Error, GiveawayEditor, GoalBarEditor, GreeterEditor, Help, Index, Regurgitator, Settings, Setup, StickersEditor
UI: AnimationComponent, CategoryComponent, GoalComponent, PlacementComponent (minimal)

### Learnings
- Regex with lazy `.+?` and `re.DOTALL` for HTML replacement is dangerous with nested tags — the lazy quantifier stops at the first `</div>` regardless of nesting depth. PlacementComponent was briefly mangled by this; had to `git checkout` and skip it.
- `container-fluid` removal left empty `class=""` and `<div >` artifacts — a cleanup pass for empty attributes is essential after bulk replacements.
- Button bar rows (`row` + `container-fluid text-right p-3`) are the most common grid pattern in GIFBot — converting to `d-flex justify-end pa-3` is cleaner than MudGrid for single-element right-aligned layouts.
- Single-col `<div class="row"><div class="col">` is presentational overhead in Bootstrap — safe to simplify to plain `<div>`.
- MudBlazor utility classes (pa-N, ma-N, mt-N, d-flex, justify-end) map well to Bootstrap spacing utilities but use a 4px scale.

— Helly
