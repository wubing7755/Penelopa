using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;

namespace Penelopa.Core.Interaction;

/// <summary>
/// Drives canvas editing gestures: click-select, drag-move, and corner-handle
/// resize. The controller is pure model logic — it receives world- and
/// CSS-space pointer positions and a pre-computed <see cref="HitTestResult"/>,
/// and only mutates primitives through the world contract. World coordinates
/// drive geometry (drag/resize); CSS coordinates drive the view pan. The host
/// supplies both coordinate spaces and the hit test.
/// </summary>
/// <remarks>
/// Gesture invariants:
/// <list type="bullet">
/// <item>A press only becomes a drag after the pointer moves beyond the
/// threshold, so clicks never nudge the selection.</item>
/// <item>While dragging or resizing, the controller mutates geometry but
/// never notifies; <see cref="IEditorInteractionHost.NotifyPrimitivesChanged"/>
/// fires once on commit.</item>
/// <item>Cancel restores the pre-gesture geometry from snapshots.</item>
/// </list>
/// </remarks>
public sealed class EditorInteractionController
{
    /// <summary>Move threshold in world units (1:1 with CSS pixels) that turns a press into a drag.</summary>
    public const float DragThresholdWorld = 3f;

    private readonly IEditorInteractionHost _host;

    private ControllerState _state = ControllerState.Idle;

    // Press snapshot (click-vs-drag decision).
    private Point _pressStart;
    private Point _pressStartCss;
    private Primitive? _pressHit;
    private bool _pressDeferredCommit;

    // Drag snapshot.
    private List<Primitive> _dragItems = new();
    private Point _lastWorld;
    private Point _lastCss;
    private float _dragTotalDx;
    private float _dragTotalDy;
    private bool _isPanGesture;

    // Resize snapshot.
    private Primitive? _resizeTarget;
    private Box _resizeOriginalBounds;
    private ResizeHandle _resizeHandle;

    // Drill-down snapshot: repeated clicks at the same spot advance the
    // candidate index from the outermost container toward the deepest leaf.
    private Point? _lastClickPosition;
    private DateTime _lastClickTime;
    private IReadOnlyList<Primitive> _drillCandidates = Array.Empty<Primitive>();
    private int _drillIndex;

    // Empty-space press that may become a pan gesture.
    private bool _panPending;

    // Whether the host was already told this gesture is mutating (captured
    // once, at the first real geometry change).
    private bool _mutationCaptured;

    private const float DrillSlopWorld = 4f;
    private static readonly TimeSpan DrillInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the current controller state (for tests and diagnostics).</summary>
    public ControllerState State => _state;

    public EditorInteractionController(IEditorInteractionHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// Handles a pointer press at world/CSS positions with the layered hit
    /// result. World coordinates drive geometry gestures (drag/resize); CSS
    /// coordinates drive the view pan.
    /// </summary>
    public void PointerDown(Point world, Point css, HitTestResult hit, bool ctrl)
    {
        _pressStart = world;
        _pressStartCss = css;

        if (hit.Handle is ResizeHandle handle && hit.Primitive is not null)
        {
            _resizeTarget = hit.Primitive;
            _resizeOriginalBounds = _resizeTarget.GetWorldBoundingBox();
            _resizeHandle = handle;
            _state = ControllerState.Resizing;
            return;
        }

        if (hit.Primitive is not null)
        {
            var primitive = hit.Primitive;
            if (ctrl)
            {
                // Ctrl-click follows the drill direction too: the outermost
                // candidate is appended (or toggled off when already
                // selected), consistent with plain-click selection.
                var candidates = hit.Candidates is { Count: > 0 }
                    ? hit.Candidates
                    : new[] { primitive };
                var ctrlTarget = candidates[0];
                if (_host.IsSelected(ctrlTarget))
                {
                    _host.ToggleSelected(ctrlTarget);
                    _state = ControllerState.Idle;
                    return;
                }

                _host.AppendSelected(ctrlTarget);
                _pressHit = ctrlTarget;
                _pressDeferredCommit = false;
                _state = ControllerState.Pressed;
                return;
            }

            if (_host.IsSelected(primitive) && _host.GetSelection().Count > 1)
            {
                // Pressing an already-selected member of a multi-selection:
                // defer the click decision until pointer-up, so the gesture
                // can either collapse the selection (click) or drag the group.
                _pressHit = primitive;
                _pressDeferredCommit = true;
                _state = ControllerState.Pressed;
                return;
            }

            // Drill-down selection: the first click picks the outermost
            // candidate (the root container when nested), and repeated
            // clicks at the same spot descend toward the deepest leaf.
            var target = SelectDrillTarget(world, hit);
            _host.SetSelected(target);
            _pressHit = target;
            _pressDeferredCommit = false;
            _state = ControllerState.Pressed;
            return;
        }

        if (hit.InUnionBox)
        {
            // Click inside the multi-selection union box: group drag.
            _pressHit = null;
            _pressDeferredCommit = false;
            _state = ControllerState.Pressed;
            return;
        }

        // Pressing empty space clears the selection; a drag that crosses the
        // threshold pans the view instead.
        _host.ClearSelection();
        ClearDrillContext();
        _pressHit = null;
        _pressDeferredCommit = false;
        _panPending = true;
        _state = ControllerState.Pressed;
    }

    /// <summary>Handles a pointer move at world/CSS positions.</summary>
    public void PointerMove(Point world, Point css)
    {
        switch (_state)
        {
            case ControllerState.Pressed:
                if (MathF.Abs(world.X - _pressStart.X) > DragThresholdWorld
                    || MathF.Abs(world.Y - _pressStart.Y) > DragThresholdWorld)
                {
                    BeginDrag(world, css);
                }

                break;

            case ControllerState.Dragging:
                if (_isPanGesture)
                {
                    _host.PanByCss(css.X - _lastCss.X, css.Y - _lastCss.Y);
                    _lastCss = css;
                }
                else
                {
                    float dx = world.X - _lastWorld.X;
                    float dy = world.Y - _lastWorld.Y;
                    _dragTotalDx += dx;
                    _dragTotalDy += dy;
                    foreach (var item in _dragItems)
                    {
                        item.Translate(dx, dy);
                    }

                    _lastWorld = world;
                }

                break;

            case ControllerState.Resizing:
                CaptureMutation();
                var newBounds = ResizeMath.ComputeBounds(_resizeOriginalBounds, _resizeHandle, world);
                var anchor = ResizeMath.FixedCorner(_resizeOriginalBounds, _resizeHandle);
                _resizeTarget!.SetBounds(newBounds, anchor);
                break;
        }
    }

    /// <summary>Handles pointer release; commits the gesture.</summary>
    public void PointerUp(Point world)
    {
        switch (_state)
        {
            case ControllerState.Pressed:
                if (_pressDeferredCommit && _pressHit is not null)
                {
                    // Click on an already-selected member of a multi-selection
                    // collapses the selection to that member.
                    _host.SetSelected(_pressHit);
                }

                break;

            case ControllerState.Dragging:
                if (!_isPanGesture)
                {
                    _host.NotifyPrimitivesChanged(_dragItems);
                }

                break;

            case ControllerState.Resizing:
                if (_resizeTarget is not null)
                {
                    _host.NotifyPrimitivesChanged(new[] { _resizeTarget });
                }

                break;
        }

        ResetGesture();
    }

    /// <summary>
    /// Cancels the active gesture (ESC, pointercancel, lost capture, window
    /// blur) and restores the pre-gesture geometry from snapshots.
    /// </summary>
    public void Cancel()
    {
        switch (_state)
        {
            case ControllerState.Dragging:
                if (!_isPanGesture)
                {
                    foreach (var item in _dragItems)
                    {
                        item.Translate(-_dragTotalDx, -_dragTotalDy);
                    }
                }

                break;

            case ControllerState.Resizing:
                // SetBounds(original, anchor) restores exactly for the current
                // leaf shapes: rectangle passes through, circle and triangle
                // are reversible fits of the original bounds.
                _resizeTarget?.SetBounds(
                    _resizeOriginalBounds,
                    ResizeMath.FixedCorner(_resizeOriginalBounds, _resizeHandle));
                break;
        }

        ResetGesture();
    }

    /// <summary>
    /// Resolves the drill-down target: advances the candidate index when the
    /// click repeats at the same spot on the same candidate chain, otherwise
    /// restarts at the outermost candidate.
    /// </summary>
    private Primitive SelectDrillTarget(Point world, HitTestResult hit)
    {
        var candidates = hit.Candidates is { Count: > 0 } ? hit.Candidates : new[] { hit.Primitive! };
        var now = DateTime.UtcNow;

        bool sameChain = _drillCandidates.Count > 0
            && ReferenceEquals(candidates[^1], _drillCandidates[^1]);
        bool sameSpot = _lastClickPosition is not null
            && MathF.Abs(world.X - _lastClickPosition.Value.X) <= DrillSlopWorld
            && MathF.Abs(world.Y - _lastClickPosition.Value.Y) <= DrillSlopWorld;
        bool withinInterval = now - _lastClickTime <= DrillInterval;

        if (sameChain && sameSpot && withinInterval && _drillIndex < candidates.Count - 1)
        {
            _drillIndex++;
        }
        else if (!(sameChain && sameSpot && withinInterval))
        {
            _drillIndex = 0;
        }

        _lastClickPosition = world;
        _lastClickTime = now;
        _drillCandidates = candidates;
        return candidates[_drillIndex];
    }

    private void ClearDrillContext()
    {
        _lastClickPosition = null;
        _drillCandidates = Array.Empty<Primitive>();
        _drillIndex = 0;
    }

    private void CaptureMutation()
    {
        if (_mutationCaptured)
        {
            return;
        }

        _mutationCaptured = true;
        _host.BeginMutation();
    }

    private void BeginDrag(Point world, Point css)
    {
        if (_panPending)
        {
            // Empty-space drag pans the view in CSS space; apply the full
            // displacement from the press point so the content follows the
            // pointer.
            _isPanGesture = true;
            _host.PanByCss(css.X - _pressStartCss.X, css.Y - _pressStartCss.Y);
            _lastCss = css;
            _state = ControllerState.Dragging;
            return;
        }

        _dragItems = _host.GetSelection().ToList();
        CaptureMutation();

        // Apply the full displacement from the press point so the selection
        // follows the pointer from where it was pressed, not from where the
        // threshold was crossed.
        float dx = world.X - _pressStart.X;
        float dy = world.Y - _pressStart.Y;
        _dragTotalDx = dx;
        _dragTotalDy = dy;
        foreach (var item in _dragItems)
        {
            item.Translate(dx, dy);
        }

        _lastWorld = world;
        _state = ControllerState.Dragging;
    }

    private void ResetGesture()
    {
        _pressHit = null;
        _pressDeferredCommit = false;
        _dragItems = new List<Primitive>();
        _resizeTarget = null;
        _panPending = false;
        _isPanGesture = false;
        _mutationCaptured = false;
        _state = ControllerState.Idle;
    }
}

/// <summary>The interaction controller's gesture states.</summary>
public enum ControllerState
{
    Idle,
    Pressed,
    Dragging,
    Resizing,
}
