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
