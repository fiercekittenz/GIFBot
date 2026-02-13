# GIFBot Smoke Test Checklist

> **Purpose:** Manual validation checklist for the GIFBot modernization migration.
> Run this checklist at each milestone gate to confirm no regressions.
>
> **Baseline recorded:** Pre-migration (main branch, .NET 8 / Blazor WASM / Telerik)

---

## 1. Build & Launch

- [ ] `dotnet build GIFBot.sln` completes with 0 errors (warnings are acceptable — see baseline notes)
- [ ] `dotnet run --project GIFBot/Server/GIFBot.Server.csproj` starts without crash
- [ ] Application opens browser to `http://localhost:5000` automatically (non-dev mode)
- [ ] Dashboard page (`/`) loads and renders without errors
- [ ] No unhandled exceptions in console output at startup

## 2. Navigation

All sidebar links should render their target page without errors.

- [ ] **Dashboard** (`/`) — loads, shows connection status
- [ ] **Bot Settings** (`/settings/`) — settings page renders
- [ ] **Animations** (`/animationseditor/`) — editor page loads
- [ ] **Giveaway** (`/giveawayeditor/`) — editor page loads
- [ ] **Goal Bar** (`/goalbareditor/`) — editor page loads
- [ ] **Greeter** (`/greetereditor/`) — editor page loads
- [ ] **Stickers** (`/stickerseditor/`) — editor page loads
- [ ] **Regurgitator** (`/regurgitator/`) — page loads
- [ ] **Backdrop** (`/backdropeditor/`) — editor page loads
- [ ] **Countdown** (`/countdowneditor/`) — editor page loads
- [ ] **Help** (`/help`) — page renders
- [ ] **Charity Thanks** (`/charity`) — page renders
- [ ] **About** (`/about`) — page renders

## 3. SignalR Connection

- [ ] Dashboard shows connected state (no "Disconnected from the bot!" warning)
- [ ] `HubConnection` to `/gifbothub` establishes successfully (check browser dev tools → Network → WebSocket/LongPolling)
- [ ] Hub connection survives a page navigation and reconnects on new pages
- [ ] No SignalR connection errors in browser console

## 4. Browser Sources (OBS Overlay Pages)

Each HTML landing page should poll `localhost:5000/ping/pong` and redirect to the Blazor route.

- [ ] `http://localhost:5000/animations.html` → redirects to `/animations`
- [ ] `http://localhost:5000/stickers.html` → redirects to `/stickers`
- [ ] `http://localhost:5000/secondarystickers.html` → redirects to `/secondarystickers`
- [ ] `http://localhost:5000/goalbar.html` → redirects to `/goalbar`
- [ ] `http://localhost:5000/countdowntimer.html` → redirects to `/countdowntimer`
- [ ] `http://localhost:5000/backdrop.html` → redirects to `/backdrop`
- [ ] Browser source pages use `BasicLayout` (no nav chrome, transparent background)
- [ ] `/ping/pong` endpoint returns 200 OK (PingController)

## 5. Core Features — Animations

- [ ] AnimationsEditor loads animation tree/list
- [ ] Can create a new animation category (`/animationseditor/addcategory`)
- [ ] Can add a new animation via the editor
- [ ] Can edit an existing animation
- [ ] Animation tutorial page (`/animationseditor/tutorial`) renders
- [ ] Animation plays in the browser source overlay (`/animations`)
- [ ] Animation component renders placement controls correctly

## 6. Core Features — Stickers

- [ ] StickersEditor page loads with sticker configuration
- [ ] Can add/remove stickers
- [ ] Stickers render in browser source overlay (`/stickers`)
- [ ] Secondary stickers render in browser source overlay (`/secondarystickers`)

## 7. Core Features — Goal Bar

- [ ] GoalBarEditor loads and displays goal configuration
- [ ] Can create/edit goal entries
- [ ] Goal bar renders in browser source overlay (`/goalbar`)
- [ ] Progress bar component updates correctly

## 8. Core Features — Countdown Timer

- [ ] CountdownTimer editor page loads
- [ ] Can configure countdown settings
- [ ] Countdown renders in browser source overlay (`/countdowntimer`)

## 9. Core Features — Giveaway

- [ ] GiveawayEditor page loads
- [ ] Can configure giveaway settings
- [ ] Giveaway functionality operates (start/stop/draw)

## 10. Core Features — Regurgitator

- [ ] Regurgitator page loads
- [ ] Can manage regurgitator packages
- [ ] Server-side data grid (DataSourceRequest) functions for entry listing
- [ ] Import file upload works via `/upload/regurgitator/{id}`

## 11. Core Features — Greeter

- [ ] GreeterEditor page loads
- [ ] Can configure greeter settings

## 12. Core Features — Backdrop

- [ ] BackdropEditor page loads
- [ ] Can configure backdrop settings
- [ ] Backdrop renders in browser source overlay (`/backdrop`)

## 13. File Operations

- [ ] Upload controller accepts file uploads (`/upload/` endpoints)
- [ ] Settings save (via SignalR hub methods)
- [ ] Settings load on startup (persisted data restores correctly)
- [ ] Media files (GIFs, images, sounds) load from `wwwroot/media/`

## 14. Editor Pages — Deep Validation

- [ ] **AnimationsEditor** — MudBlazor components render animation hierarchy
- [ ] **StickersEditor** — MudBlazor components render all sticker configuration
- [ ] **Settings** — MudBlazor components render bot settings
- [ ] **GoalBarEditor** — MudBlazor components render goal configuration

## 15. Setup & Authentication

- [ ] Setup page (`/setup`) loads
- [ ] Twitch OAuth flow (`/twitchoauth`) initiates correctly
- [ ] Bot authentication state persists across restarts

## 16. Auxiliary Features

- [ ] **Channel Points** integration functions
- [ ] **Tiltify** integration loads
- [ ] **Stream Elements** integration loads
- [ ] **Version check** in NavMenu detects updates from GitHub

## 17. UI Polish (M4)

- [ ] **Dark theme** renders correctly — deep purple palette, no white/light flashes
- [ ] **MudThemeProvider** with `IsDarkMode="true"` is present in MainLayout
- [ ] **Navigation drawer** opens and closes via hamburger icon (MudDrawer)
- [ ] **Material Design icons** visible on all nav links (Dashboard, Settings, Animations, etc.)
- [ ] **MudNavMenu** renders all 13 navigation links
- [ ] **MudText** used for page headings (no raw `<h1>`–`<h3>` in page files)
- [ ] **MudGrid/MudItem** used for layout on Dashboard (no Bootstrap `row`/`col-` in Pages)
- [ ] **No `<font>` tags** remain in any Razor component
- [ ] **No `text-light`** CSS class references in Pages
- [ ] **No GIFBotPurple22** CSS references in Server project (App.razor clean)
- [ ] **Typography** uses Roboto font family via MudBlazor theme
- [ ] **gifbot-page-header** class used consistently for page title sections
- [ ] **Browser source overlays** (animations.html, stickers.html, etc.) still reference jQuery and are unaffected by UI cleanup

---

## Validation Gate

> **Run this full checklist at the end of each migration milestone.**
>
> **Milestone gates:**
> - **M0 (Prep):** Baseline passes — confirms pre-migration state is sound
> - **M1 (.NET 10 + Minimal Hosting):** Sections 1–4 must pass; Sections 5–16 unchanged
> - **M2 (Blazor Server Consolidation):** All sections must pass; SignalR behavior may change (Section 3 critical)
> - **M3 (MudBlazor Replacement):** All sections must pass; Section 14 is the critical regression zone
> - **M4 (Polish):** Full regression — every checkbox green
>
> **Blocking rule:** If any checkbox in Sections 1–4 fails, the milestone does NOT pass.
> Feature-area failures (Sections 5–16) should be logged as issues but do not necessarily block.
>
> **How to run:** Open `http://localhost:5000` with browser dev tools open. Work through each section.
> Record pass/fail inline. Commit the filled checklist as evidence of the gate review.
