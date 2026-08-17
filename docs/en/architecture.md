# Penelopa Architecture

## Overview

Penelopa is a Blazor WebAssembly primitive alignment editor that consumes the
Atlas.Blazor 0.2.0 NuGet package for its dockable workspace shell. The editor
logic is layered so each concern is independently testable.

## Layers

```text
src/Penelopa (WASM app)
  ├── Components/PenelopaWorkspace.razor   Atlas six-region dockable layout
  ├── Components/PenelopaTopToolbar.razor  Hosted in the Atlas top toolbar slot
  ├── Components/PenelopaStatusBar.razor   Hosted in the Atlas status bar slot
  ├── Components/Panels/                   Tools / PrimitiveTree / Canvas / Properties
  └── wwwroot/css/app.css                  Editor shell styles + bright studio theme
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

- `CanvasRenderer` owns a draw bitmap and an off-screen hit bitmap sized to
  the target canvas (`EnsureBuffersFor` rebuilds them when the canvas
  `DeviceClipBounds` changes, so the editor canvas fills its host panel).
  `Render` clears both, applies the y-flip world transform
  (`Translate(0, height)` + `Scale(1, -1)`), paints every primitive (visible
  color on the draw bitmap, color key on the hit bitmap), then blits the
  result and draws the axis indicator.
- `HitTest(screenX, screenY)` reads the hit bitmap pixel and resolves the
  color key back to a primitive. Screen coordinates are the browser's
  top-left origin; the y-flip means a world point `(x, y)` renders at screen
  `(x, height - y)`. Coordinates outside the bitmap return null.

### Penelopa (WASM app)

- `PenelopaWorkspace.razor` hosts the workspace through the programmatic
  API: it builds the `AtlasWorkspaceDefinition` (via
  `PenelopaWorkspaceDefinition`) and passes the resulting `IAtlasWorkspace`
  to `AtlasWorkspaceHost` with `WorkspaceOwnership.External`, so the
  component owns disposal. The layout is a six-region workspace: an
  inline-start dock (Tools + Primitives over an empty lower slot), a central
  document group (Diagram), an inline-end dock (Properties over an empty
  lower slot), and a block-end dock with two empty slots. The left and right
  docks start at 300px each so the side panels are symmetric; the four empty
  groups stay collapsed and only keep their toolbar slots.
  `PenelopaTopToolbar` and `PenelopaStatusBar` are supplied through the
  `TopToolbarContent` / `StatusBarContent` host slots, and the four panels
  are registered through `ContentRoutes` using the kind constants in
  `PenelopaContentKinds`.
- `PenelopaWorkspaceDefinition.cs` builds the layout tree programmatically
  (`SplitNode` / `GroupNode` / `DockItem` arrays plus toolbar states),
  mirroring the declarative defaults: the inner dock splits and the bottom
  dock use a 0.5 proportional basis, the document group uses Scroll overflow
  with Adjacent activation, and the empty collapsed groups are persistent
  with no selected item.
- `CanvasPanel.razor` hosts the `SKGLView` (`EnableRenderLoop=true`,
  `IgnorePixelScaling=true`, fills the panel) and wires `OnPaintSurface` →
  `CanvasRenderer.Render`, plus `mousedown` → `HitTest` → selection
  (Ctrl-click appends).
- `ToolPanel.razor` exposes Add (Circle/Rectangle/Triangle) and Align
  (six directions) actions.
- `PrimitiveTreePanel.razor` lists all primitives; clicking selects.
- `PropertyPanel.razor` renders the selected primitive's `PropValue` bag
  through the typed input components in `Panels/Props/`.

## Height Chain

Atlas workspaces are sized by their parent; the editor shell must chain
`html/body/#app/.penelopa-main/.penelopa-shell/.atlas-v2-workspace` to 100%
height (see `wwwroot/css/app.css`). The top toolbar and status bar render
inside the Atlas host, above and below the work area.

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
  `SkiaSharp.NativeAssets.WebAssembly.targets` requires net8.0+. The
  `SkiaSharp.NativeAssets.Linux` package is referenced explicitly because
  2.88.9 only pulls Win32/macOS native assets transitively; without it the
  Linux CI tests fail with `DllNotFoundException: libSkiaSharp`.
- The `wasm-tools` workload is required to link the SkiaSharp native assets
  into the WASM build; CI installs it before restore.
- `NuGet.Config` maps every package to nuget.org
  (`packageSourceMapping`) so central package management (CPM) does not trip
  NU1507 on GitHub Actions runners, which add a default `library-packs`
  source.
