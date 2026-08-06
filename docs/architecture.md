# Penelopa Architecture

## Overview

Penelopa is a Blazor WebAssembly primitive alignment editor that consumes the
Atlas.Blazor 0.2.0 NuGet package for its dockable workspace shell. The editor
logic is layered so each concern is independently testable.

## Layers

```text
src/Penelopa (WASM app)
  ├── Components/PenelopaWorkspace.razor   Atlas dockable four-pane layout
  ├── Components/Panels/                   Tools / PrimitiveTree / Canvas / Properties
  └── wwwroot/css/app.css                  Editor shell styles + height chain
src/Penelopa.Rendering                     SkiaSharp canvas + color-key hit testing
src/Penelopa.Core                          Primitive model + alignment algorithms
```

### Penelopa.Core

Pure domain logic with no UI or SkiaSharp dependency:

- `Primitives/` — `Primitive` base class and `Circle` / `Rectangle` / `Triangle`
  subclasses, each exposing an editable `PropValue` property bag (`Float`,
  `Double`, `Int`, `Uint`, `Bool`, `String`).
- `Primitives/ColorKeyManager` — maps a unique packed uint color key to each
  primitive for hit testing. Keys start at `0xFF000001` and wrap past
  `0xFF000000` (black is never a valid key).
- `Alignment/` — `AlignType` enum (Left/HCenter/Right/Top/VCenter/Bottom),
  `Box` (axis-aligned bounding box), `Transform`, and `AlignExtensions.Align`
  which moves all selected items so their alignment value matches the union
  bounding box. The operation is idempotent: already-aligned selections return
  false and nothing moves.
- `Services/IPrimitiveService` — collection + selection state shared across
  panels via DI (registered as a singleton).

### Penelopa.Rendering

SkiaSharp rendering extracted so hit-testing logic can be unit tested without
a browser:

- `CanvasRenderer` owns a 512x512 draw bitmap and an off-screen hit bitmap.
  `Render` clears both, applies the y-flip world transform
  (`Translate(0, 512)` + `Scale(1, -1)`), paints every primitive (visible
  color on the draw bitmap, color key on the hit bitmap), then blits the
  result and draws the axis indicator.
- `HitTest(screenX, screenY)` reads the hit bitmap pixel and resolves the
  color key back to a primitive. Screen coordinates are the browser's
  top-left origin; the y-flip means a world point `(x, y)` renders at screen
  `(x, 512 - y)`.

### Penelopa (WASM app)

- `PenelopaWorkspace.razor` declares the Atlas layout: a left tool strip
  (Tools + Primitives), a central document group (Diagram), and a right tool
  group (Properties). Panels receive the Atlas content context through the
  `AtlasContentComponent` base class.
- `CanvasPanel.razor` hosts the `SKGLView` (512x512, `EnableRenderLoop=true`)
  and wires `OnPaintSurface` → `CanvasRenderer.Render`, plus `mousedown` →
  `HitTest` → selection (Ctrl-click appends).
- `ToolPanel.razor` exposes Add (Circle/Rectangle/Triangle) and Align
  (six directions) actions.
- `PrimitiveTreePanel.razor` lists all primitives; clicking selects.
- `PropertyPanel.razor` renders the selected primitive's `PropValue` bag
  through the typed input components in `Panels/Props/`.

## Height Chain

Atlas workspaces are sized by their parent; the editor shell must chain
`html/body/#app/.penelopa-main/.penelopa-shell/.atlas-v2-workspace` to 100%
height (see `wwwroot/css/app.css`).

## Alignment Semantics

The six directions align against the **union** of the selected items'
bounding boxes:

| AlignType | Reference value | Translation |
|---|---|---|
| Left | `union.MinX` | `dx = ref.MinX - box.MinX` |
| HCenter | `union.CenterX` | `dx = ref.CenterX - box.CenterX` |
| Right | `union.MaxX` | `dx = ref.MaxX - box.MaxX` |
| Top | `union.MaxY` | `dy = ref.MaxY - box.MaxY` |
| VCenter | `union.CenterY` | `dy = ref.CenterY - box.CenterY` |
| Bottom | `union.MinY` | `dy = ref.MinY - box.MinY` |

Top/Bottom use MaxY/MinY because the canvas is y-flipped (screen y grows
down); the visible top edge is the larger world Y. Selection of fewer than
two items returns false without moving.

## Toolchain Notes

- `global.json` pins SDK 6.0.428. Building with SDK 10 triggers the
  RazorSourceGenerator incremental-input bug for net6.0 WASM, so the version
  is pinned.
- SkiaSharp is pinned to 2.88.9: the 4.x line's
  `SkiaSharp.NativeAssets.WebAssembly.targets` requires net8.0+.
- The `wasm-tools` workload is required to link the SkiaSharp native assets
  into the WASM build; CI installs it before restore.
