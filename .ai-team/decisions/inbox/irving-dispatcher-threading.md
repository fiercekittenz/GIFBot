### 2026-02-13: Blazor Server Dispatcher threading pattern
**By:** Irving
**What:** All SignalR hub callbacks must use InvokeAsync(StateHasChanged) under Blazor Server
**Why:** Hub callbacks run on non-UI threads; StateHasChanged requires Dispatcher access
