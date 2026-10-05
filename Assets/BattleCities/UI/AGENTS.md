# Approved game UI rules

These are the user's approved typography and UI design rules as of 2026-10-04. Apply them when creating or updating game screens on Web, Seeker, and PSG1. Reuse the current menu and tank selector as visual references. Later explicit user instructions take precedence.

## Fonts and text treatments

- Use the bundled Barlow Condensed family. Golden yellow text, white text, and button captions use `BarlowCondensed-Bold.ttf` with the bold setting. Reuse `MenuTheme.HeadingFont`, `ArcadeTypography`, and the shared `ArcadeHeadingFont` TMP atlas.
- Golden yellow lettering uses the approved `ArcadeTextStyles` Gold treatment: a warm yellow-to-gold gradient, crisp dark brown outline, and short navy shadow. Apply this to golden titles, score values, fuel amounts, deployment cost, instruction titles (PLAY, REACH TOP 10, EARN SKR), and prize ranks (1ST, 2ND, 3RD, 4TH-10TH).
- All white UI text follows the fuel-button lettering rule everywhere: a solid white face, crisp navy outline, and short dark shadow. This includes HUD labels and values, player names, ranking columns, live/season subtitles, controls, and button captions.
- Button captions over blue or dark artwork use `ArcadeTextTreatment.WhiteButton`. Captions over gold or cream artwork use the Navy treatment.
- Legacy uGUI uses `ArcadeTextStyles.ApplyGold` / `ArcadeGoldText`, `ApplyWhite` / `ArcadeWhiteText`, or `ApplyNavy`. TMP uses `ArcadeTextStyles.Apply`. Reuse the shared treatments and materials across platforms and interaction states.
- Main navigation labels (PLAY, SHOP, RANKING, QUARTERS, SOCIALS) use the clean prize amount lettering (15 SKR EACH): BarlowCondensed-Bold with FontStyle.Normal, theme.Navy `(6,29,54)`, and no added outline or shadow. Preserve the larger caption sizes and raised placement below the icons, keep full labels on one line clear of the bottom rim, and reapply this treatment after restoring a platform layout.
- Use available space for readable text. Preserve alignment and responsive fitting; avoid wrapping or clipping compact headings and captions. TOP 10 EVERY 30 MINUTES has an enlarged text area and a maximum size of 28, with best fit on smaller layouts.
- Keep labels and related values close together. In particular, the fuel count belongs immediately after FUEL AVAILABLE.
- Keep the approved color values in `ArcadeTextStyles`: gold top `(255,235,112)`, gold bottom `(255,183,12)`, gold outline `(52,31,9)`, white outline `(6,33,64)`, navy face `(7,43,94)`, and shadow `(5,18,36,220)`.

## Buttons and artwork

- Reuse the established blue metal frames, gold selected states, cream panels, and supplied icons. Maintain the current illustrated game style across new screens.
- Buttons with lettering baked into artwork must use aspect fit. For uGUI, use `Image.Type.Simple` with `preserveAspect = true`. Never stretch, squash, crop, or nine-slice artwork containing lettering.
- Separately rendered captions may sit over a sliced background when that artwork contains no text. Preserve the proportions of icons and text independently of the background.
- Apply the appropriate text and artwork treatment in normal, highlighted, selected, pressed, and disabled states. Extra layout space should become padding around artwork that must retain its proportions.
- Select Tank fuel and locked buttons span 80% of the card width, centered between the lower inside corners. Keep their existing height, calculated from 72% of card width and the original active-blue sprite aspect ratio with a 1.25 height multiplier. Leave a bottom inset of 2.3% of card width so the button sits inside the rim.
- Play tank cards use a width-to-height ratio of 0.85. Preserve the established tank artwork and title sizes relative to card width, and remove spare height below the four stat rows. Keep a small clear gap above the fuel button; PSG1 uses tighter stat-row spacing with the approved readable lettering.
- Fuel-button skins contain no text and use slicing to preserve their rounded corners at the original width-based scale. The three supplied skins have 128-pixel borders. Keep the fuel icon and caption grouped and centered, with clearance from the stat rows and card rim.
- Continue uses the same empty blue skin as the fuel buttons, with a gold skin for focus, hover, and press. Render CONTINUE as a centered caption using WhiteButton lettering on blue and Navy lettering on gold. Keep its established width and rounded corners; its height is 20% greater than the original Continue artwork's aspect-ratio height, within the footer row.
- Tank selector illustrations fit within a centered area 72% of the card width and 42.75% of its width in height, keeping their original proportions and a little space below the name and above the stats. Position them slightly lower in that area of the card and use a soft oval contact shadow below their tracks, rendered behind the artwork.
- Leaderboard trophies use matching soft oval contact shadows behind their bases, scaled to the fitted artwork. Hide the empty-state trophy's shadow whenever that trophy is hidden.
- Future cards with featured object illustrations must include the same soft oval contact shadows beneath the objects. Reuse the shared `TankGroundShadow` graphic with the approved warm dark color `(35,25,13,112)` and soft fading edges. Center each shadow under the object's base and size it to the fitted artwork's footprint. Render it behind the artwork, keep it clear of labels and the card rim, disable raycasts, and show or hide it together with the object. Preserve this treatment on Web, Seeker, and PSG1.

## TV layout and spacing

- Web and Seeker landscape use navigation on the left, the TV in the middle, and the leaderboard on the right. Landscape detection must work in both the Unity Editor and runtime.
- Preserve PSG1's approved platform composition and controller navigation. Its controller key details disappear, after which HOW IT WORKS appears inside the TV.
- On Seeker landscape and PSG1, place the HUD inside the TV. Keep Commander and Hi-score cards compact, with icons, progress, and the level pill aligned as in the web reference. Avoid excessive blank width and oversized score readouts.
- TV content on Web, Seeker landscape, and PSG1 fills `TV Background Viewport` with a modest 12-unit clearance from its rim. Use small fixed inner margins and let the roster or loadout fill the space between the header and footer. Do not add the TV sprite's full sliced border or percentage-based outer padding to the visible opening.
- Use available TV space while retaining a little breathing room around content. Avoid both unused outer space and a crowded arrangement.

## Shop inside the TV

- Open Shop inside the existing TV with the same 12-unit rim clearance, title plaque, BACK artwork, cream card frames, blue price buttons, and gold selection states as the tank selector. Use SHOP as the heading and SKR, SOLANA, and SWAP as its tabs.
- Keep the compact SHOP title at the left of a single header row, followed by SKR, SOLANA, SWAP, and BACK. Start inventory and products directly below that row so the removed tab row becomes usable content space.
- Keep an inventory sidebar using the approved power-up icons, ALL/FUEL/POWER/PACKS filters, a vertically scrolling product grid, and a fixed balance strip. Use four product columns on Web and three on Seeker landscape and PSG1, reducing further only when needed to keep text readable.
- Shop inventory uses eight stacked rows: a cream icon tile, navy item name at a maximum size of 20, and a separate blue count badge with white lettering. Reuse the How It Works metal container and its frosted arena background. Only the INVENTORY title container has a white background, with navy lettering at a maximum size of 24. Keep thin row dividers, use the matching power-up illustrations from ShopArt with an atlas fallback, and keep every row visible with padding inside the frame.
- Shop category filters form one joined rounded deep-blue bar with four equal segments and thin navy/cyan separators. Use a gold inset for the active category, centered navy text on gold and white text on blue at a maximum size of 28, and a subtle focus marker for keyboard/controller navigation. Keep the live item count at a maximum size of 22 to the right on the cream panel; do not render four separate blue button frames.
- Reuse `ShopArt` product illustrations and the shared fonts/shadows. Position each shadow at the visible artwork's base, accounting for transparent image margins.
- Shop cards use a width-to-height ratio of 1.0. Keep the enlarged titles and price-button width and height unchanged. Put reward and owned count on one line (for example, `+1 FUEL • OWNED 0`) with larger navy text in a single flat cream detail band matching the Play cards. Do not use a separate owned row or an oval background.
- Use the supplied Solana coin (`solana-coin.png`) and matching SKR coin (`skr-coin.png`) for Shop tabs, prices, and balances through `ShopArt.solana` and `ShopArt.skr`. Both have a silver rim and dark teal face; the SKR coin preserves the supplied white emblem. Their source and generation prompt are recorded beside the artwork. Keep the star coin for its existing non-Shop uses.
- Token coins use Simple images with aspect fit plus compensation for unequal ancestor scaling, so their rendered circles stay round. Keep mipmaps and trilinear filtering enabled on both coin textures for clean rendering in small price buttons.
- Treat SKR as its own currency. Never label the legacy BATC `tokenBalance` field as SKR. Show unavailable balances or swap quotes as a dash; purchase and swap UI must not claim success without a real completed transaction.

## Instructions and prize distribution

- On Seeker and PSG1, HOW IT WORKS and PRIZE DISTRIBUTION sit directly inside the TV at the bottom, without their own frame or background. Retain the yellow heading bars on both platforms.
- Use the exact headings `HOW IT WORKS ?` and `PRIZE DISTRIBUTION !`, including the space before punctuation.
- Keep each icon and its text together as a group, with clear space between groups on every platform. Reduce the gap between a text heading and its description.
- Place enlarged 1/2/3 step badges before the icon-and-text groups. Do not place the step number between its icon and text.
- Use thin vertical separation lines between instruction groups and between prize groups. Place the horizontal divider below the yellow heading.
- Instruction action headings and prize ranks use the Gold text rule. How It Works supporting descriptions use the same clean font, weight, navy color, and absence of outline/shadow as prize amounts (15 SKR EACH), through the shared `ApplyPrizeAmountTextStyle` helper.
- EARN SKR uses the game's golden prize crate (`MenuTheme.PrizeCrates[0]`, `prize-crate-gold.png`), matching the first-place prize icon.

## Leaderboard panels

- PLAYER RANKINGS joins the top of its parent panel: square at the top seam, rounded only at the bottom.
- SEASON STANDINGS joins the bottom of its parent panel: rounded only at the top, square at the bottom seam.
- Both leaderboard mini panels use the same frosted TV glass background inside their joined shapes, with a thin blue edge. Use the approved Gold treatment for their headings. Supporting text must match the clean prize amount lettering (15 SKR EACH): BarlowCondensed-Bold with FontStyle.Normal, theme.Navy `(6,29,54)`, and no extra bolding, outline, or shadow. Position the heading trophy 18 units from the left edge and keep its contact shadow aligned beneath it.
- Align the season heading and subtitle to the left, immediately after the star/coin icon. Keep the original outer frame intact.
- Keep the removed Retry button out of the leaderboard and controller focus order unless the user requests it again.

## Verify new screens

- Check readable text, intact artwork proportions, modest padding, and alignment at the relevant Web, Seeker, and PSG1 sizes.
- Confirm compact labels remain complete and do not overlap icons, neighboring content, or frame edges. Reuse the existing layout and style helpers so platform changes preserve these rules.
