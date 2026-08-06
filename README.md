# Penelopa

**A primitive alignment editor for Blazor WebAssembly.**

Penelopa is an independent consumer of the
[Atlas.Blazor](https://www.nuget.org/packages/Atlas.Blazor) NuGet package. It
hosts a SkiaSharp drawing canvas, a primitive tree, alignment actions, and a
property editor inside an Atlas dockable workspace, and provides six-direction
alignment (left / horizontal-center / right / top / vertical-center / bottom)
for circle, rectangle, and triangle primitives.

## Features

- Atlas dockable workspace layout (tool panels, split views, drag-and-drop)
- SkiaSharp canvas with color-key hit testing
- Circle / Rectangle / Triangle primitives with an editable property system
- Six-direction alignment against the union bounding box, with idempotence
- Property editor driven by the primitive property model

## Project Structure

```text
Penelopa.sln
src/Penelopa/            Blazor WebAssembly app consuming Atlas.Blazor 0.2.0
src/Penelopa.Core/       Primitive model and alignment algorithms (no UI deps)
src/Penelopa.Rendering/  SkiaSharp canvas rendering and hit testing
docs/                    Documentation
tests/                   xUnit test projects (Core, Rendering)
```

## Development

Requirements:

- .NET 6 SDK (`global.json` pins 6.0.428)
- `wasm-tools` workload — required by the SkiaSharp WebAssembly native
  assets:
  ```sh
  dotnet workload install wasm-tools
  ```
- SkiaSharp is pinned to **2.88.9** (the last stable line that supports
  net6.0 WebAssembly; 4.x requires net8.0+).

```sh
dotnet restore Penelopa.sln
dotnet build Penelopa.sln --no-restore
dotnet test Penelopa.sln --no-build --no-restore
dotnet format Penelopa.sln --verify-no-changes --no-restore
```

## License

MIT — see [LICENSE](LICENSE).
