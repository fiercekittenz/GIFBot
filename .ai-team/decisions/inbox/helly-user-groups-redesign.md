### 2025-07-24: User Groups Tab — MudDialog Pattern for CRUD Actions
**By:** Helly
**What:** All CRUD actions on the User Groups tab (Add Group, Add User, Delete Group, Rename Group) now use inline `<MudDialog @bind-Visible>` with `<DialogActions>` instead of inline form fields or immediate execution.
**Why:** Inline text fields + buttons cluttered the layout and offered no validation before submission. Dialog pattern centralizes input, gives room for validation (disabled Add when duplicate/empty), and matches the confirmation UX users expect for destructive actions (Delete). This is now the standard pattern for any future modal interactions on Settings or similar pages.

**Also decided:**
- Action buttons (Rename, Clone, Delete) grouped in a `<MudStack Row>` below the DataGrid — keeps the "add" action visually separated from "manage existing" actions.
- Cancel/Save page-level buttons migrated from Bootstrap `<button>` to `<MudButton>` with `gap-4` spacing — last Bootstrap button instances on this page are now gone.
- MudSelect for User Groups now properly populates with `<MudSelectItem>` children — this was a bug from the original Telerik migration.
