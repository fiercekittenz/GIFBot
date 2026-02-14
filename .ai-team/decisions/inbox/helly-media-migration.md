### 2026-02-14: Media folder migrated to Server project
**By:** Helly
**What:** All media files and wwwroot references moved from Client/wwwroot to Server/wwwroot. Removed all `.Replace("Server", "Client")` path hacks from AnimationLibrary and GIFBotHub. The Client project is fully unused.
**Why:** Georgia confirmed the Client project is no longer used after the Blazor Server consolidation. All static content belongs in Server/wwwroot.
