### 2026-02-13: Prerendering disabled for local desktop app
**By:** Irving
**What:** Set `prerender: false` on InteractiveServerRenderMode in App.razor
**Why:** GIFBot is a local desktop app — prerendering causes JS interop crashes during static render and opens duplicate browser tabs. No SEO or first-paint benefit.
