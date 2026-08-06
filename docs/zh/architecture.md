# Penelopa 架构

## 概述

Penelopa 是一个消费 Atlas.Blazor 0.2.0 NuGet 包的 Blazor WebAssembly 图元对齐编辑器，使用其可停靠工作区外壳。编辑器逻辑分层设计，每个关注点都可以独立测试。

## 分层

```text
src/Penelopa（WASM 应用）
  ├── Components/PenelopaWorkspace.razor   Atlas 六区域可停靠布局
  ├── Components/PenelopaTopToolbar.razor  通过 Atlas 顶栏插槽承载
  ├── Components/PenelopaStatusBar.razor   通过 Atlas 状态栏插槽承载
  ├── Components/Panels/                   Tools / PrimitiveTree / Canvas / Properties
  └── wwwroot/css/app.css                  编辑器外壳样式 + 高达主题
src/Penelopa.Rendering                     SkiaSharp 画布 + 颜色键命中检测
src/Penelopa.Core                          图元模型 + 对齐算法
```

### Penelopa.Core

纯领域逻辑，不依赖 UI 或 SkiaSharp：

- `Primitives/` — `Primitive` 基类与 `Circle` / `Rectangle` / `Triangle` 子类，各自暴露可编辑的 `PropValue` 属性包（`Float`、`Double`、`Int`、`Uint`、`Bool`、`String`）。
- `Primitives/ColorKeyManager` — 为每个图元映射唯一的打包 uint 颜色键，用于命中检测。键从 `0xFF000001` 开始，绕过 `0xFF000000`（黑色永远不是有效键）。
- `Alignment/` — `AlignType` 枚举（Left/HCenter/Right/Top/VCenter/Bottom）、`Box`（轴对齐包围盒）、`Transform` 与 `AlignExtensions.Align`——将选中项移动到其对齐值与整体包围盒匹配。操作幂等：已对齐的选择返回 false 且不移动。
- `Services/IPrimitiveService` — 通过 DI 在各面板间共享的集合 + 选择状态（注册为单例）。

### Penelopa.Rendering

抽取 SkiaSharp 渲染，使命中检测逻辑无需浏览器即可单元测试：

- `CanvasRenderer` 持有一张绘制位图和一张离屏命中位图，尺寸跟随目标画布（`EnsureBuffersFor` 在画布 `DeviceClipBounds` 变化时重建，因此编辑器画布填满宿主面板）。`Render` 清空两者，应用 y-flip 世界变换（`Translate(0, height)` + `Scale(1, -1)`），绘制每个图元（可见颜色画到绘制位图、颜色键画到命中位图），然后 blit 结果并绘制坐标轴指示。
- `HitTest(screenX, screenY)` 读取命中位图像素并把颜色键解析回图元。屏幕坐标是浏览器的左上原点；y-flip 意味着世界点 `(x, y)` 渲染在屏幕 `(x, height - y)`。位图外的坐标返回 null。

### Penelopa（WASM 应用）

- `PenelopaWorkspace.razor` 将 Atlas 布局声明为六区域工作区：inline-start 停靠区（Tools + Primitives，下方空槽位）、中央文档组（Diagram）、inline-end 停靠区（Properties，下方空槽位）、以及含两个空槽位的 block-end 停靠区。左右停靠区初始各 300px，侧面板对称；四个空组保持折叠，仅保留工具栏槽位。`PenelopaTopToolbar` 与 `PenelopaStatusBar` 通过 `TopToolbarContent` / `StatusBarContent` 宿主插槽提供。
- `CanvasPanel.razor` 承载 `SKGLView`（`EnableRenderLoop=true`、`IgnorePixelScaling=true`，填满面板），接线 `OnPaintSurface` → `CanvasRenderer.Render`，以及 `mousedown` → `HitTest` → 选择（Ctrl 点击追加）。
- `ToolPanel.razor` 提供 Add（Circle/Rectangle/Triangle）与 Align（六方向）操作。
- `PrimitiveTreePanel.razor` 列出全部图元，点击选择。
- `PropertyPanel.razor` 通过 `Panels/Props/` 中的类型化输入组件渲染选中图元的 `PropValue` 属性包。

## 高度链

Atlas 工作区由其父级确定尺寸；编辑器外壳必须将 `html/body/#app/.penelopa-main/.penelopa-shell/.atlas-v2-workspace` 链到 100% 高度（见 `wwwroot/css/app.css`）。顶栏与状态栏在 Atlas 宿主内部渲染，分别位于工作区上方与下方。

## 对齐语义

六方向对齐基于选中项包围盒的**整体**：

| AlignType | 参考值 | 位移 |
|---|---|---|
| Left | `union.MinX` | `dx = ref.MinX - box.MinX` |
| HCenter | `union.CenterX` | `dx = ref.CenterX - box.CenterX` |
| Right | `union.MaxX` | `dx = ref.MaxX - box.MaxX` |
| Top | `union.MaxY` | `dy = ref.MaxY - box.MaxY` |
| VCenter | `union.CenterY` | `dy = ref.CenterY - box.CenterY` |
| Bottom | `union.MinY` | `dy = ref.MinY - box.MinY` |

Top/Bottom 使用 MaxY/MinY 是因为画布做了 y-flip（屏幕 y 向下增长）；可见顶边是更大的世界 Y。少于两个选中项时返回 false 且不移动。

## 工具链说明

- `global.json` 固定 SDK 6.0.428。使用 SDK 10 构建会触发 net6.0 WASM 的 RazorSourceGenerator 增量输入 bug，因此锁定版本。
- SkiaSharp 固定 2.88.9：4.x 的 `SkiaSharp.NativeAssets.WebAssembly.targets` 要求 net8.0+。显式引用 `SkiaSharp.NativeAssets.Linux` 是因为 2.88.9 只传递拉取 Win32/macOS 原生资源；缺少时 Linux CI 测试会报 `DllNotFoundException: libSkiaSharp`。
- `wasm-tools` workload 用于把 SkiaSharp 原生资源链接进 WASM 构建；CI 在 restore 前安装。
- `NuGet.Config` 通过 `packageSourceMapping` 把所有包映射到 nuget.org，使中央包管理（CPM）不会在 GitHub Actions runner 上触发 NU1507（runner 默认带有 `library-packs` 源）。
