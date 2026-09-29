# UI artwork rules

- Buttons with text baked into their artwork must always use aspect-fit. Never stretch, squash, crop, or nine-slice artwork containing lettering.
- For Unity uGUI, use `Image.Type.Simple` with `preserveAspect = true`. Size the RectTransform proportionally to the source sprite, or fit the sprite inside its allocated bounds.
- Apply this rule to every button state, including normal, highlighted, selected, pressed, and disabled. Extra layout space must remain padding rather than distort the button image.
- Separately rendered text may sit over a sliced background only when the background artwork contains no text.
