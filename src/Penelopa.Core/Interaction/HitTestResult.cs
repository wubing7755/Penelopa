using Penelopa.Core.Primitives;

namespace Penelopa.Core.Interaction;

/// <summary>
/// 指针命中测试的分层结果，由渲染层在交互控制器决策前计算：
/// 角柄（最高优先级）→ 图元本体 → 多选并集框（最低优先级，用作整组拖拽柄）。
/// </summary>
public readonly struct HitTestResult
{
    /// <summary>命中的角柄（如有）。</summary>
    public ResizeHandle? Handle { get; init; }

    /// <summary>通过颜色键缓冲区命中的图元本体（如有）。</summary>
    public Primitive? Primitive { get; init; }

    /// <summary>
    /// 钻取候选链：命中图元及其祖先链，按根 → 最深叶子排序（即确认的钻取方向）。
    /// 无命中时为 null。
    /// </summary>
    public IReadOnlyList<Primitive>? Candidates { get; init; }

    /// <summary>点是否在多选并集框内。</summary>
    public bool InUnionBox { get; init; }
}
