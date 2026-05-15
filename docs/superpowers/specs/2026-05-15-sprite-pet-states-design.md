# Sprite Pet States Design

## Goal

HyperPet should support the Miku Kimono pet using its current folder shape: `pet.json` plus `spritesheet.webp`. The app should render real sprite animations instead of the current text-circle placeholder.

## Asset Location

The pet lives here:

```text
src/HyperPet.App/Assets/Pets/miku-kimono.codex-pet/
  pet.json
  spritesheet.webp
```

This folder is treated as a HyperPet pet package. It does not require Codex at runtime.

## Pet Metadata

Keep the existing `pet.json` fields and add HyperPet animation metadata:

- `frameWidth`: `128`
- `frameHeight`: `208`
- `states`: animation definitions keyed by state name

The detected spritesheet layout is:

- Width: `1536`
- Height: `1872`
- Columns: `12`
- Rows: `9`
- Frame size: `128 x 208`

State rows:

- `idle`: row `0`
- `runRight`: row `1`
- `runLeft`: row `2`
- `waving`: row `3`
- `jumping`: row `4`
- `failed`: row `5`
- `waiting`: row `6`
- `running`: row `7`
- `review`: row `8`

Each state has `12` frames. FPS and loop settings can differ by state.

## Runtime Format

HyperPet should read the pet package directly. It should not require a conversion step or external Codex format support. The `.codex-pet` folder name is allowed, but HyperPet interprets the metadata itself.

## Rendering

Add `SixLabors.ImageSharp` to decode WebP at runtime. A sprite loader crops the WebP spritesheet into WPF `BitmapSource` frames at startup.

The current `PetFace` text-circle UI is replaced by an `Image` control that displays the current animation frame.

## Animation Behavior

### Calm Mode

Calm mode keeps the pet at the user-chosen location. The pet loops `idle` and occasionally switches to `waiting`.

### Desktop Mode

Desktop mode moves the pet window around the screen. While moving right it uses `runRight`; while moving left it uses `runLeft`. Movement should remain simple for this iteration: screen wandering only, no physics or collision system.

### Notification Alert

Any notification interrupts the current animation and plays `waving` while the bubble is visible. When the alert ends, the pet returns to its previous behavior mode.

## Settings

Add `PetBehaviorMode` to settings:

- `Calm`
- `Desktop`

The settings window should allow switching between these modes. `Calm` is the default.

## Error Handling

- If the pet package cannot be read, fall back to the existing default placeholder pet.
- If WebP decode fails, fall back to the existing default placeholder pet.
- If an animation state is missing, use `idle`.
- If `idle` is missing, use the first available state.

## Testing

Add tests for:

- Pet metadata parsing.
- State lookup fallback.
- Settings default and persistence for `PetBehaviorMode`.

Manual checks:

- Miku Kimono renders instead of text-circle placeholder.
- Idle animates.
- Waiting appears sometimes in Calm mode.
- Desktop mode moves the pet window around the screen.
- Notification alert switches to waving and returns to previous mode after dismissal.

## Acceptance Criteria

- The Miku Kimono pet folder stays in `Assets/Pets`.
- App builds with WebP decode support.
- App loads and displays `spritesheet.webp`.
- Calm mode and Desktop mode are available in settings.
- Notification alert uses `waving`.
- App falls back cleanly if pet assets fail to load.
