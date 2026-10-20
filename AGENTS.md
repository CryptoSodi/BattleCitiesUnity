# BattleCities project instructions

For every game UI task, read and follow `Assets/BattleCities/UI/AGENTS.md` before creating or editing pages, screens, menus, HUDs, buttons, or controls. It contains the user's approved fonts, text treatments, shared title/footer sizes and action positions, artwork handling, focus/activation behavior, scrolling, and Web/Seeker/PSG1 layouts. Apply these rules to UI code outside that directory as well. Update that guide when the user approves a reusable UI change.

Use these approved rules and the current implemented menu as the design reference when older descriptive documents disagree.

Only update game source until the user explicitly requests a build. Do not build APK or Web versions, launch a new player build, upload, or deploy without that request. When a local Web preview is requested, build and launch on localhost.

Localhost and loopback WebGL previews must connect to the test API (`https://test.battlecities.com`) and use Solana devnet. Keep public live hosts on the live API.
