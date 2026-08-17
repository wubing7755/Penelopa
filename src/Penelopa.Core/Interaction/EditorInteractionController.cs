using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;

namespace Penelopa.Core.Interaction;

/// <summary>
/// Drives canvas editing gestures: click-select, drag-move, and corner-handle
/// resize. The controller is pure model logic — it receives world-space
/// pointer positions and a pre-computed <see cref="HitTestResult"/>, and only
/// mutates primitives through the world contract. The host translates CSS
/// coordinates and supplies the hit test.
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
    private Primitive? _pressHit;
    private bool _pressDeferredCommit;

    // Drag snapshot.
    private List<Primitive> _dragItems = new();
    private Point _lastWorld;
    private float _dragTotalDx;
    private float _dragTotalDy;

    // Resize snapshot.
    private Primitive? _resizeTarget;
    private Box _resizeOriginalBounds;
    private ResizeHandle _resizeHandle;

    /// <summary>Gets the current controller state (for tests and diagnostics).</summary>
    public ControllerState State => _state;

    public EditorInteractionController(IEditorInteractionHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// Handles a pointer press at a world position with the layered hit result.
    /// </summary>
    public void PointerDown(Point world, HitTestResult hit, bool ctrl)
    {
        _pressStart = world;

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
                if (_host.IsSelected(primitive))
                {
                    _host.ToggleSelected(primitive);
                    _state = ControllerState.Idle;
                    return;
                }

                _host.AppendSelected(primitive);
                _pressHit = primitive;
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

            _host.SetSelected(primitive);
            _pressHit = primitive;
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

        _host.ClearSelection();
        _state = ControllerState.Idle;
    }

    /// <summary>Handles a pointer move at a world position.</summary>
    public void PointerMove(Point world)
    {
        switch (_state)
        {
            case ControllerState.Pressed:
                if (MathF.Abs(world.X - _pressStart.X) > DragThresholdWorld
                    || MathF.Abs(world.Y - _pressStart.Y) > DragThresholdWorld)
                {
                    BeginDrag(world);
                }

                break;

            case ControllerState.Dragging:
                float dx = world.X - _lastWorld.X;
                float dy = world.Y - _lastWorld.Y;
                _dragTotalDx += dx;
                _dragTotalDy += dy;
                foreach (var item in _dragItems)
                {
                    item.Translate(dx, dy);
                }

                _lastWorld = world;
                break;

            case ControllerState.Resizing:
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
                _host.NotifyPrimitivesChanged(_dragItems);
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
                foreach (var item in _dragItems)
                {
                    item.Translate(-_dragTotalDx, -_dragTotalDy);
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

    private void BeginDrag(Point world)
    {
        _dragItems = _host.GetSelection().ToList();

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
