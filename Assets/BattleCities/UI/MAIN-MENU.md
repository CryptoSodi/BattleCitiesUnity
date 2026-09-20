# Main menu scene

Open `Assets/BattleCities/Scenes/MainMenu.unity`, or use **Battle Cities > UI > Open Main Menu**. The new scene is the first enabled build scene. START loads the existing `BattleCity` gameplay scene.

The scene uses real Canvas, Image, Text, Button, RectTransform, Mask, EventSystem and Input System components, all visible in the Hierarchy. It does not instantiate a flattened screenshot. The hierarchy and labels are editable in the Unity Editor.

## Layouts

`MainMenuScene.Platform` defaults to Auto and uses the existing `RuntimePlatformInfo`. Select Web, Psg1 or Android in the Inspector to preview a specific composition. Auto also uses the portrait composition on narrow web windows. Android landscape uses the compact horizontal-navigation composition without controller hints.

- Web: 1600 x 1067 design reference, left navigation, right leaderboard, lower explainer.
- PSG1: 1240 x 1080, bottom navigation, D-pad/stick navigation, A/select and B/back prompts.
- Android: 940 x 1672 portrait reference, bottom touch navigation, safe-area padding. Content adapts to aspect ratio.

The PlaySolana SDK docs and the installed `Psg1.cs` were used to verify that physical A is `buttonEast` and B is `buttonSouth`. PSG1 binds these explicitly. The regular UI map retains conventional gamepad South/submit, East/cancel. Both maps support keyboard navigation and pointer/touch events. No L2/R2 bindings are used.

## Reuse

- `Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset`: shared art, fonts and palette.
- `Assets/BattleCities/UI/Settings/MainMenuInput.asset`: UI and PSG1 action maps.
- `Assets/BattleCities/UI/Prefabs/Shared`: NavigationTile, PlayButton, StatusCard and RewardPodium.
- `Assets/BattleCities/UI/Prefabs/MainMenu`: BattlefieldHero, LeaderboardPanel and HowItWorks.
- Template button events are intentionally empty; connect them to the destination page's controller. Scene buttons have persistent controller listeners. Selected Play color and controller focus outline are separate states.
- All labels, player values, rank labels and reward amounts are native Text components. The decorative logo is raster art. Barlow Condensed is bundled with its SIL OFL license.

## Asset library

The master library is `C:/repos/Unity/ui assets/`:

```
shared/
  sources/          Original icon sheet and Play button generation
  icons/            Twelve normalized transparent icons
  buttons/          Gold Play button without baked text
  panels/           Eight authored 9-slice panel/focus skins
  backgrounds/      Soft menu backdrop
  fonts/            Barlow Condensed and OFL
main-menu/
  sources/          Original generated battlefield, logo and reward garden
  backgrounds/      Battlefield and reward garden
  branding/         Transparent logo
  previews/         Unity-rendered Web, PSG1 and Android previews
asset-manifest.json
generation-prompts.json
prepare_assets.py
```

The Unity import copies live under `Assets/BattleCities/UI/Art`. Originals stay in the master library so later pages can share assets. Texture settings: Sprite/Single, FullRect, 100 PPU, centered pivot, bilinear, clamp, no mipmaps, read/write disabled. Panel borders are 32 px. Android uses ASTC 6x6; desktop previews retain uncompressed sprites. Both generated output and deterministic normalization are recorded in the manifest. White-background removal preserves enclosed highlights; review source images before further extraction changes.

## Current behavior and integration boundaries

- START transitions to the existing Unity gameplay scene.
- Play focuses START; settings opens the controls dialog; Back closes a dialog and restores focus.
- Quarters, Shop and Socials show an explicit not-yet-converted dialog. Their destination scene names are serialized for future scene integration.
- Ranking and Retry currently explain that the Unity leaderboard service is not connected. No live ranking, payouts, wallet linkage or authentication has been implemented by this UI task.
- `SetPlayer` and `SetLeaderboard` provide view integration hooks. Player labels default to COMMANDER and zero scores unless the documented PlayerPrefs keys or live setters are populated. The existing gameplay does not yet persist these keys.
- Reward amounts and the 30-minute heading reproduce the reference design as editable UI copy; they are not connected to a round configuration service.

## Validation and previews

Use **Battle Cities > UI > Validate Main Menu** for scene, action, font, layout-bound and focus-link checks at Web 1600x1067/1920x1080, PSG1 1240x1080, Android 940x1672/390x844/844x390.

Use **Capture Platform Previews** to render all three platform compositions. Use **Test PSG1 Navigation In Play Mode** to queue gamepad events and check D-pad navigation, A/select, B/back and restored focus. This temporary test device is removed afterwards. Test the START transition separately so the input test does not scan scene objects while Unity is asynchronously loading the gameplay scene. Actual hardware testing remains necessary; editor simulation does not establish physical-device performance or driver behavior.

PSG1 docs: https://developers.playsolana.com/input-system and https://developers.playsolana.com/psg1-keys . Local skill search did not find a PSG1 SKILL.md; the installed PlaySolana developer tools and SDK supplied platform guidance instead. The game-assets, imagegen, Unity C# scripting and Unity Input System skills guided asset normalization, native UI composition and input handling.
