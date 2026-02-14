# GIFBot UX Audit Report
**Date:** February 13, 2026  
**Auditor:** Burt (UX Expert)  
**Scope:** Full UI/UX audit of GIFBot Server web interface

---

## Executive Summary

GIFBot's UI has made significant progress in its migration to MudBlazor 8.5, with a cohesive dark purple theme identity and clean dialog implementations. The application serves streamers well as a desktop utility. However, several inconsistencies remain that impact polish and professional appearance.

### Overall Impressions
- **Theme:** Strong purple identity (#7e57c2 primary) with well-defined dark surface hierarchy
- **Component Adoption:** ~70% MudBlazor, ~30% legacy Bootstrap/HTML
- **Layout:** Good use of MudContainer and MudGrid on most pages
- **Typography:** Consistent header treatment with `gifbot-page-header` class
- **Pain Points:** Bootstrap button remnants, open-iconic icons, inconsistent form layouts

### Top 5 Priorities

| Priority | Issue | Impact | Effort |
|----------|-------|--------|--------|
| 1 | **Bootstrap buttons (btn, btn-secondary, btn-primary)** used on 12+ pages | Medium | Low |
| 2 | **Dashboard Quick Actions navbar** uses Bootstrap instead of MudBlazor | High | Medium |
| 3 | **open-iconic icons (oi-*)** scattered across pages (~42 instances) | Medium | Low |
| 4 | **Inconsistent button placement** - Save/Cancel buttons vary in position and style | Medium | Low |
| 5 | **Form field spacing** - Some pages use `form-group`, others use MudBlazor spacing | Medium | Medium |

---

## Site-Wide Findings

### Color & Theme Consistency ✅ GOOD
The color system is well-defined and consistently applied:

| Element | Color | Usage |
|---------|-------|-------|
| Primary (brand) | `#7e57c2` | MudBlazor primary, accent elements |
| Background | `#1a1a2e` | App background, drawer, appbar |
| Surface | `#22223a` | MudBlazor cards/surfaces |
| Page Header | `#2d2b52` | `.gifbot-page-header` class |
| Content Panel | `#1e1e38` | `.gifbot-content-panel` class |
| Section Panel | `#161630` | `.gifbot-section-panel` class |
| Form Panel | `#1c1c34` | `.gifbot-form-panel` class |
| Input Fill | `#2a2a48` | Text input backgrounds |
| Text Primary | `#ffffffde` | 87% opacity white |
| Text Secondary | `#ffffffa0` | 63% opacity white |

**Recommendation:** No changes needed. The panel hierarchy provides excellent visual depth.

### Component Consistency ⚠️ NEEDS WORK

#### Bootstrap Buttons Still in Use
Found in 12+ pages with ~85 total instances:

| Pattern | Count | Pages |
|---------|-------|-------|
| `btn btn-secondary` | ~50 | Index, Regurgitator, CountdownTimer, GoalBar, etc. |
| `btn btn-primary` | ~20 | Index, Regurgitator, Settings, etc. |
| Custom btn classes (`.gifbot-action-btn`, `.gifbot-secondary-btn`) | ~15 | Dashboard, feature pages |

**CSS Classes Being Used:**
```css
.gifbot-action-btn { background-color: #3d3566 !important; }
.gifbot-secondary-btn { background-color: #3a2d56 !important; }
.gifbot-primary-btn { background-color: #6b2fa0 !important; }
.gifbot-danger-btn { background-color: #4a2d3d !important; }
.gifbot-special-btn { background-color: #2d3566 !important; }
.gifbot-neutral-btn { background-color: #3a3a5c !important; }
```

**Recommendation:** Replace with MudButton using Color parameter and custom CSS classes for background overrides where needed. This eliminates Bootstrap dependency while maintaining the custom color palette.

**Replacement Mapping:**
| Legacy | MudBlazor Replacement |
|--------|----------------------|
| `btn btn-secondary gifbot-secondary-btn` | `<MudButton Variant="Variant.Filled" Class="gifbot-secondary-btn">` |
| `btn btn-primary gifbot-primary-btn` | `<MudButton Variant="Variant.Filled" Color="Color.Primary">` |
| `btn btn-secondary gifbot-action-btn` | `<MudButton Variant="Variant.Filled" Class="gifbot-action-btn">` |

#### open-iconic Icons Still in Use
Found ~42 instances of `oi-*` or `span class="oi oi-*"` icons:

| Icon | Usage | MudBlazor Replacement |
|------|-------|----------------------|
| `oi oi-cog` | Settings header | `@Icons.Material.Filled.Settings` |
| `oi oi-play-circle` | Animations header | `@Icons.Material.Filled.PlayCircle` |
| `oi oi-pencil` | Edit buttons | `@Icons.Material.Filled.Edit` |
| `oi oi-trash` | Delete buttons | `@Icons.Material.Filled.Delete` |
| `oi oi-plus` | Add buttons | `@Icons.Material.Filled.Add` |
| `oi oi-media-play` | Play buttons | `@Icons.Material.Filled.PlayArrow` |
| `oi oi-timer` | Countdown header | `@Icons.Material.Filled.Timer` |
| `oi oi-target` | Goal Bar header | `@Icons.Material.Filled.TrackChanges` |
| `oi oi-box` | Giveaway header | `@Icons.Material.Filled.CardGiftcard` |
| `oi oi-thumb-up` | Greeter header | `@Icons.Material.Filled.WavingHand` |
| `oi oi-image` | Backdrop header | `@Icons.Material.Filled.Wallpaper` |
| `oi oi-badge` | Stickers header | `@Icons.Material.Filled.EmojiEmotions` |
| `oi oi-question-mark` | Help header | `@Icons.Material.Filled.Help` |
| `oi oi-tag` | About header | `@Icons.Material.Filled.Info` |

**Recommendation:** Replace all `oi-*` icons with MudBlazor Material Icons for consistency with NavMenu.

### Typography ✅ GOOD
- GitHub font stack properly defined in `app.css` and `MainLayout.razor`
- Consistent heading hierarchy using `Typo.h4`, `Typo.h6`, `Typo.subtitle1`
- Text opacity follows Material Design patterns (87%/63% for primary/secondary)

### Dialog Styling ✅ GOOD
Dialog implementation is excellent:
- Title bar: `#2d2b52` with proper padding
- Content area: `#1c1c34` with consistent spacing
- Field descriptions use `MudText Typo.caption Class="text-white-50"`
- Min-width: 450px for comfortable reading

### Navigation ✅ GOOD
- MudNavMenu with Material Icons is clean and scannable
- Drawer with GIFBot logo at bottom is nice branding
- Update notification uses `Color.Secondary` appropriately

---

## Per-Page Findings

### Dashboard (Index.razor)

**Layout Issues:**
1. ⚠️ **Bootstrap navbar for Quick Actions** - Should use MudButtonGroup or MudStack
2. ⚠️ **HTML tables inside MudTreeView** - Awkward nesting for layout purposes
3. ⚠️ **Bootstrap buttons** (`btn btn-secondary`) for all actions

**Visual Hierarchy:**
- ✅ Page header uses `gifbot-page-header` class
- ⚠️ Quick Actions bar doesn't visually separate from content
- ✅ Log panel and Animations panel are well-balanced with MudGrid

**Specific Issues:**
```razor
<!-- Current: Bootstrap navbar -->
<nav class="navbar navbar-expand-sm gifbot-quick-actions">
   <ul class="navbar-nav">
      <li class="nav-item">
         <button class="btn btn-secondary nav-item p-1 mr-3 gifbot-action-btn">
```

**Recommendation:**
```razor
<!-- Replace with: MudBlazor stack -->
<MudPaper Class="pa-2 mb-3 gifbot-quick-actions">
   <MudStack Row="true" Spacing="2" AlignItems="AlignItems.Center">
      <MudText Typo="Typo.subtitle2"><strong>Quick Actions:</strong></MudText>
      <MudButton Variant="Variant.Filled" Class="gifbot-action-btn" OnClick="@ToggleStreamerOnlyMode">
         @mStreamerOnlyModeState Streamer-Only Mode
      </MudButton>
```

### Settings.razor ✅ MOSTLY GOOD

**Strengths:**
- User Groups tab redesigned with proper MudBlazor dialogs
- MudDataGrid for throttle list
- Nested MudTabs for Integrations section
- Good use of MudExpansionPanel for collapsible sections

**Issues:**
1. ⚠️ Lines 101, 408: Mix of Bootstrap button (`btn btn-secondary`) and MudButton
2. ⚠️ Browsersource URLs tab uses HTML tables for layout

**Recommendation:** Replace the few remaining Bootstrap buttons with MudButton.

### Regurgitator.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Bootstrap navbar for package selection
2. ⚠️ `btn btn-secondary` and `btn btn-primary` used throughout
3. ⚠️ HTML table for add entry section
4. ⚠️ HTML `<input type="range">` for volume slider - should be `MudSlider`
5. ⚠️ Save/Cancel buttons at bottom use Bootstrap

**Form Layout Issues:**
- Excessive use of `<div class="form-group">` instead of MudStack/MudGrid
- Radio groups have awkward whitespace

**Recommendation:**
- Replace volume slider: `<MudSlider T="int" @bind-Value="mFormVolume" Min="0" Max="100" />`
- Use `<MudStack Spacing="3">` instead of `form-group` divs
- Use `<MudRadioGroup>` with proper vertical layout

### CountdownTimer.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Timer control buttons (`Start`, `Pause`, `Reset`, `Hide`) use Bootstrap
2. ⚠️ Save/Cancel buttons duplicated at top and bottom of General tab
3. ⚠️ Font selection uses 24+ MudRadio items - should use MudSelect

**Font Selection Problem:**
```razor
<!-- Current: 24 radio buttons in a row -->
<MudRadioGroup @bind-Value="mCaptionFontSelection" T="int">
   <MudRadio T="int" Value="@(0)">Arial</MudRadio>
   <MudRadio T="int" Value="@(1)">Comic Sans</MudRadio>
   <!-- ... 22 more ... -->
</MudRadioGroup>
```

**Recommendation:**
```razor
<!-- Better: Use MudSelect dropdown -->
<MudSelect T="int" @bind-Value="mCaptionFontSelection" Label="Font" Variant="Variant.Filled">
   <MudSelectItem T="int" Value="0">Arial</MudSelectItem>
   <MudSelectItem T="int" Value="1">Comic Sans</MudSelectItem>
   <!-- ... -->
</MudSelect>
```

### GiveawayEditor.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Bootstrap buttons for giveaway actions (Accept Entries, Draw Winner, Reset)
2. ⚠️ Save/Cancel buttons use Bootstrap
3. ⚠️ `oi oi-trash` icon on delete buttons

**Positive:**
- MudDataGrid for entrants and banned users
- Clean dialog for adding banned users
- Good use of conditional rendering for entry behavior types

### GoalBarEditor.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ HTML tables for layout (URL copy section)
2. ⚠️ `btn btn-secondary` and `btn btn-primary` buttons
3. ⚠️ Same font radio button issue as CountdownTimer (24+ options)
4. ⚠️ Nested MudTabs inside Goals tab creates visual clutter

**Recommendation:** Extract font selection to reusable component `<FontSelectorComponent>`.

### GreeterEditor.razor ✅ MOSTLY GOOD

**Strengths:**
- Clean dialog implementation
- MudDataGrid with proper toolbar

**Issues:**
1. ⚠️ Legacy Telerik/Bootstrap CSS in embedded `<style>` block (`.k-listview-header`, `.k-card`)
2. ⚠️ Edit/Delete buttons in grid use `btn btn-secondary` with `oi` icons

### StickersEditor.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Edit dialog uses Bootstrap navbar for Cancel/Update buttons
2. ⚠️ Mix of MudDialog and Bootstrap elements
3. ⚠️ `form-group` divs throughout

**Edit Dialog Problem:**
```razor
<DialogContent>
   <nav class="navbar navbar-expand-sm gifbot-quick-actions">
      <ul class="navbar-nav">
         <li class="nav-item">
            <button type="button" class="btn btn-secondary nav-item p-1 mr-3 gifbot-action-btn">
               Cancel
            </button>
```

This is very strange UX - putting navbar inside a dialog. Should use `<DialogActions>`.

### BackdropEditor.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Three buttons on one line at top (`Take Down`, `Cancel`, `Save Changes`) - all Bootstrap
2. ⚠️ Duplicated button bar at top and bottom of General tab
3. ⚠️ HTML table for URL copy section

### AnimationsEditor.razor ⚠️ NEEDS WORK

**Issues:**
1. ⚠️ Action buttons in grid cells use `btn btn-secondary` with `oi` icons
2. ⚠️ Toolbar buttons (Expand All, Collapse All, Add Category, etc.) don't have labels - just empty buttons

**Toolbar Issue:**
```razor
<MudButton Variant="Variant.Text" Size="Size.Small" OnClick="@HandleExpandAllRequest"></MudButton>
<MudButton Variant="Variant.Text" Size="Size.Small" OnClick="@HandleCollapseAllRequest"></MudButton>
```
These buttons have no content - users can't see what they do.

**Recommendation:**
```razor
<MudButton Variant="Variant.Text" Size="Size.Small" StartIcon="@Icons.Material.Filled.UnfoldMore" OnClick="@HandleExpandAllRequest">Expand All</MudButton>
<MudButton Variant="Variant.Text" Size="Size.Small" StartIcon="@Icons.Material.Filled.UnfoldLess" OnClick="@HandleCollapseAllRequest">Collapse All</MudButton>
```

### EditAnimation.razor ✅ GOOD

Clean implementation delegating to `AnimationComponent.razor`.

### About.razor ✅ GOOD

Clean layout with MudExpansionPanels for version history.

### Help.razor ✅ GOOD

Clean layout with MudExpansionPanels.

### Charity.razor ✅ GOOD

Simple page with donor list.

### Setup.razor ✅ EXCELLENT

The setup wizard is the best-implemented page:
- Centered layout with `max-width: 700px`
- Consistent `gifbot-form-panel` styling
- MudButton throughout
- Clear step progression

---

## Accessibility Findings

### Contrast ✅ GOOD
- Text primary (`#ffffffde`) on background (`#1a1a2e`): Ratio ~12:1 ✅
- Text secondary (`#ffffffa0`) on background: Ratio ~8:1 ✅
- Purple primary (`#7e57c2`) on dark backgrounds: Ratio ~4.8:1 ✅

### Focus Indicators ⚠️ NEEDS REVIEW
- MudBlazor components have built-in focus indicators
- Bootstrap buttons may not have adequate focus rings in dark theme

### Screen Reader Hints ⚠️ MISSING
- Icon-only buttons lack `aria-label` attributes
- Some interactive elements missing `title` or `aria-describedby`

**Example Problem:**
```razor
<button type="button" class="btn btn-secondary p-0 m-0" style="width: 25px; height: 25px">
   <span class="oi oi-trash"></span>
</button>
```

**Recommendation:**
```razor
<MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" 
               aria-label="Delete item" Title="Delete" />
```

---

## Action Plan

### Phase 1: Quick Wins (Low Effort, High Impact)

**1.1 Replace open-iconic icons in page headers**
All page headers currently use `<span class="oi oi-*">` before the title. Replace with MudBlazor icons:

```razor
<!-- Before -->
<MudText Typo="Typo.h4"><span class="oi oi-cog" aria-hidden="true"></span> Bot Settings</MudText>

<!-- After -->
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
   <MudIcon Icon="@Icons.Material.Filled.Settings" />
   <MudText Typo="Typo.h4">Bot Settings</MudText>
</MudStack>
```

**Files:** Index.razor, Settings.razor, AnimationsEditor.razor, GiveawayEditor.razor, GoalBarEditor.razor, GreeterEditor.razor, Regurgitator.razor, StickersEditor.razor, BackdropEditor.razor, CountdownTimer.razor, Help.razor, About.razor, Charity.razor

**1.2 Replace icon-only buttons in MudDataGrid cells**
All feature pages use Bootstrap buttons with `oi` icons for Edit/Delete/Play actions.

```razor
<!-- Before -->
<button type="button" class="btn btn-secondary p-0 m-0 gifbot-secondary-btn" style="width: 25px; height: 25px" @onclick="...">
   <span class="oi oi-pencil"></span>
</button>

<!-- After -->
<MudIconButton Icon="@Icons.Material.Filled.Edit" Size="Size.Small" Class="gifbot-neutral-btn" OnClick="..." />
```

### Phase 2: Dashboard Overhaul (Medium Effort, High Impact)

**2.1 Replace Bootstrap Quick Actions navbar**
See mockup: `dashboard-quick-actions.html`

**2.2 Clean up MudTreeView layout**
Remove HTML `<table>` wrappers inside tree items. Use CSS for spacing.

### Phase 3: Form Standardization (Medium Effort, Medium Impact)

**3.1 Create FontSelectorComponent**
Extract the 24-radio-button font selection pattern into a reusable `<MudSelect>` component.

**3.2 Standardize button placement**
All pages should have Save/Cancel buttons at the bottom right using:
```razor
<div class="d-flex justify-end pa-3 gap-2">
   <MudButton Variant="Variant.Outlined" Color="Color.Default" OnClick="@OnCancel">Cancel</MudButton>
   <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="@OnSave">Save Changes</MudButton>
</div>
```

**3.3 Remove duplicate button bars**
CountdownTimer and BackdropEditor have buttons at top AND bottom. Keep only bottom.

### Phase 4: CSS Cleanup (Low Effort, Medium Impact)

**4.1 Remove Bootstrap button dependencies**
Once all `btn btn-*` classes are replaced, remove Bootstrap button CSS from app.css.

**4.2 Remove open-iconic CSS import**
Once all `oi-*` icons are replaced, remove from app.css:
```css
@import url('open-iconic/font/css/open-iconic-bootstrap.min.css');
```

**4.3 Clean up legacy styles**
Remove `.k-*` Telerik styles from GreeterEditor embedded style block.

---

## Summary Table: Issues by Page

| Page | Bootstrap Buttons | oi Icons | HTML Tables | Font Radios | Priority |
|------|------------------|----------|-------------|-------------|----------|
| Index.razor | ✗ High | ✗ | ✗ | - | HIGH |
| Settings.razor | ✗ Low | ✗ | ✗ | - | MEDIUM |
| Regurgitator.razor | ✗ High | ✗ | ✗ | - | HIGH |
| CountdownTimer.razor | ✗ High | ✗ | - | ✗ | HIGH |
| GiveawayEditor.razor | ✗ Medium | ✗ | - | - | MEDIUM |
| GoalBarEditor.razor | ✗ Medium | ✗ | ✗ | ✗ | HIGH |
| GreeterEditor.razor | ✗ Low | ✗ | - | - | LOW |
| StickersEditor.razor | ✗ High | ✗ | - | - | HIGH |
| BackdropEditor.razor | ✗ High | ✗ | ✗ | - | HIGH |
| AnimationsEditor.razor | ✗ Medium | ✗ | - | - | MEDIUM |
| EditAnimation.razor | ✓ | ✗ | - | - | LOW |
| About.razor | ✓ | ✗ | - | - | LOW |
| Help.razor | ✓ | ✗ | - | - | LOW |
| Charity.razor | ✓ | ✗ | - | - | LOW |
| Setup.razor | ✓ | ✗ | - | - | LOW |

---

## Appendix: CSS Recommendations

### Add to app.css for MudButton custom colors

```css
/* MudBlazor custom button overrides */
.gifbot-action-btn.mud-button-filled {
    background-color: #3d3566 !important;
}

.gifbot-secondary-btn.mud-button-filled {
    background-color: #3a2d56 !important;
}

.gifbot-primary-btn.mud-button-filled {
    background-color: #6b2fa0 !important;
}

.gifbot-danger-btn.mud-button-filled {
    background-color: #4a2d3d !important;
}

.gifbot-special-btn.mud-button-filled {
    background-color: #2d3566 !important;
}

.gifbot-neutral-btn.mud-button-filled,
.gifbot-neutral-btn.mud-icon-button {
    background-color: #3a3a5c !important;
}
```

### Standardized page header component (recommended)

```razor
<!-- Components/UI/PageHeader.razor -->
@code {
    [Parameter] public string Icon { get; set; }
    [Parameter] public string Title { get; set; }
}

<div class="pa-1 my-1 gifbot-page-header">
    <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
        @if (!string.IsNullOrEmpty(Icon))
        {
            <MudIcon Icon="@Icon" />
        }
        <MudText Typo="Typo.h4">@Title</MudText>
    </MudStack>
</div>
```

Usage:
```razor
<PageHeader Icon="@Icons.Material.Filled.Settings" Title="Bot Settings" />
```

---

*Report generated by Burt, UX Expert for GIFBot*
