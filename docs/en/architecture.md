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
  the render target reported by the host (`Render` receives the SKGLView
  `e.Info` size, so the canvas fills its host panel at any display DPI).
  Both bitmaps share one `ViewTransform` (y-flip world transform scaled by
  the device pixel ratio); `Render` clears both, paints every primitive
  (visible color on the draw bitmap, color key on the hit bitmap), then
  blits the result, draws the axis indicator, and draws the selection
  overlay (bounding box plus four corner handles at a fixed 8px screen
  size). The hit buffer is drawn without antialiasing so 1px edges stay
  hittable.
- `ViewTransform` maps world coordinates (model space, Y up) to view pixels
  (Y down) at physical resolution: one world unit equals one CSS pixel, so
  the render target is `devicePixelRatio` times the CSS size. It is the
  single extension point for future zoom/pan.
- `HitTest(cssX, cssY)` converts browser CSS coordinates through the shared
  `ViewTransform` and resolves the color-key pixel back to a primitive.

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
  physical-resolution rendering via the default `IgnorePixelScaling=false`)
  inside a scrollable content box (`.penelopa-canvas-scroll` + minimum
  content size), so shrinking the window shows scrollbars instead of
  clipping the drawing area. It wires `OnPaintSurface` →
  `CanvasRenderer.Render` with the event's `e.Info` size and the current
  `devicePixelRatio`, plus `mousedown` → `HitTest` → selection (Ctrl-click
  appends). A small JS helper (`wwwroot/js/penelopa.js`) watches
  `devicePixelRatio` changes (matchMedia) so moves across displays with
  different DPI stay in sync, and hosts the pointer layer (capture,
  canvas-relative CSS coordinates, synthesized-mouse suppression, rAF
  throttling) that reports semantic callbacks into the interaction
  controller. Browser `OffsetX/Y` are relative to the canvas element, so
  scrolling does not affect hit testing.

### Interaction

Editing gestures run through `EditorInteractionController` in Core — a
small state machine (Idle / Pressed / Dragging / Resizing) that receives
world-space pointer positions and a layered `HitTestResult`, snapshots
the selection and geometry at pointer-down, and notifies panels once on
commit. ESC, pointer-cancel, lost capture, or window blur restores the
snapshot and returns to Idle. Clicking an already-selected member of a
multi-selection defers the decision until pointer-up, so the same gesture
either collapses the selection (click) or drags the group; Ctrl-click
toggles membership. Resize keeps the corner opposite the dragged handle
fixed (`ResizeMath`, with a minimum size and mirror-flip when crossing
the fixed corner), and primitives fit themselves to the target bounds
through `SetBounds(bounds, anchor)`: circles keep their aspect with the
fixed corner on the boundary, triangles map their vertices by normalized
position. Future hit-through (drill-down) will collect candidates with
`ContainsWorldPoint` plus ancestor chains.
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
