### NoNavMenuLayout now has its own MudTheme (duplicated from MainLayout)
**By:** Helly
**When:** 2025-07-18
**What:** NoNavMenuLayout.razor was updated to use MudBlazor components (MudThemeProvider, MudLayout, MudMainContent) with a duplicated dark theme definition matching MainLayout's PaletteDark.
**Why:** The theme is a `private static` field in MainLayout and can't be shared directly. The setup wizard layout now renders with the correct dark background and themed components.
**Trade-off:** Theme colors are duplicated in two files. If the palette changes, both must be updated. A future refactor could extract the theme to a shared static class (e.g., `GIFBotTheme.cs`).
**Also:** `html, body` background in app.css and App.razor now set to `#121218` to eliminate white gaps. Browser source pages are unaffected — they override backgrounds via inline styles.
