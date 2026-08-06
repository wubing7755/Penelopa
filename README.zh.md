# Penelopa

**面向 Blazor WebAssembly 的图元对齐编辑器。**

Penelopa 是 [Atlas.Blazor](https://www.nuget.org/packages/Atlas.Blazor) NuGet
包的独立消费者。它在 Atlas 可停靠工作区中承载 SkiaSharp 绘制画布、图元树、
对齐操作和属性编辑器，并为圆形、矩形、三角形图元提供六方向对齐（左 / 水平
居中 / 右 / 上 / 垂直居中 / 下）。

## 功能

- Atlas 可停靠工作区布局（工具面板、分屏、拖放）
- SkiaSharp 画布与颜色键命中检测
- 圆形 / 矩形 / 三角形图元与可编辑属性系统
- 基于整体包围盒的六方向对齐，支持幂等检查
- 由图元属性模型驱动的属性编辑器

## 项目结构

```text
Penelopa.sln
src/Penelopa/             Blazor WebAssembly 应用（消费 Atlas.Blazor 0.2.0）
src/Penelopa.Core/        图元模型与对齐算法（无 UI 依赖）
src/Penelopa.Rendering/   SkiaSharp 画布渲染与命中检测
docs/                     文档
tests/                    xUnit 测试项目（Core、Rendering）
```

## 开发

环境要求：

- .NET 6 SDK（`global.json` 固定 6.0.428）
- `wasm-tools` workload——SkiaSharp WebAssembly 原生资源需要：
  ```sh
  dotnet workload install wasm-tools
  ```
- SkiaSharp 固定 **2.88.9**（最后一个支持 net6.0 WebAssembly 的稳定版本
  线；4.x 要求 net8.0+）。

```sh
dotnet restore Penelopa.sln
dotnet build Penelopa.sln --no-restore
dotnet test Penelopa.sln --no-build --no-restore
dotnet format Penelopa.sln --verify-no-changes --no-restore
```

## 许可

MIT — 见 [LICENSE](LICENSE)。
