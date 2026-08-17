using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Penelopa.Components;

/// <summary>
/// Central content kind constants shared between the workspace definition and
/// the <see cref="AtlasContentRoute"/> declarations in
/// <see cref="PenelopaWorkspace"/>. Keeping these strings in one place prevents
/// a rename on either side from silently producing a "Content is not
/// registered" runtime failure.
/// </summary>
public static class PenelopaContentKinds
{
    public const string Tools = "penelopa-tools";
    public const string Tree = "penelopa-tree";
    public const string Canvas = "penelopa-canvas";
    public const string Props = "penelopa-props";
}

/// <summary>
/// Builds the Penelopa workspace: an Atlas six-region dockable layout with
/// symmetric 300px side docks around a full-height document canvas, plus the
/// four empty collapsed groups that keep the toolbar slots available. The
/// layout mirrors the original declarative razor tree; both forms produce the
/// same <see cref="AtlasWorkspaceDefinition"/>.
/// </summary>
internal static class PenelopaWorkspaceDefinition
{
    internal static AtlasWorkspaceDefinition Create(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));
        }

        var tools = Tool("tools", "Tools", PenelopaContentKinds.Tools);
        var tree = Tool("tree", "Primitives", PenelopaContentKinds.Tree);
        var canvas = Document("canvas", "Diagram", PenelopaContentKinds.Canvas);
        var props = Tool("props", "Properties", PenelopaContentKinds.Props);

        var toolsGroup = ToolGroup(
            "tools-group",
            LogicalRegion.InlineStartUpper,
            GroupVisibility.Expanded,
            tools.Id,
            tree.Id);
        var leftLower = ToolGroup(
            "left-lower",
            LogicalRegion.InlineStartLower,
            GroupVisibility.Collapsed);
        var docsGroup = DocumentGroup("docs-group", canvas.Id);
        var propsGroup = ToolGroup(
            "props-group",
            LogicalRegion.InlineEndUpper,
            GroupVisibility.Expanded,
            props.Id);
        var rightLower = ToolGroup(
            "right-lower",
            LogicalRegion.InlineEndLower,
            GroupVisibility.Collapsed);
        var bottomLeft = ToolGroup(
            "bottom-left",
            LogicalRegion.BlockEndInlineStart,
            GroupVisibility.Collapsed);
        var bottomRight = ToolGroup(
            "bottom-right",
            LogicalRegion.BlockEndInlineEnd,
            GroupVisibility.Collapsed);

        // The left/right docks are symmetric at 300px; the inner split of each
        // dock and the bottom dock default to a 0.5 proportional basis, exactly
        // as the declarative form did when no Basis was specified.
        var leftDock = Split(
            "left-dock",
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.5d),
            toolsGroup.Id,
            leftLower.Id);
        var rightDock = Split(
            "right-dock",
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.5d),
            propsGroup.Id,
            rightLower.Id);
        var editorRight = Split(
            "editor-right",
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(300d, SplitAnchor.Second),
            docsGroup.Id,
            rightDock.Id);
        var mainArea = Split(
            "main-area",
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(300d, SplitAnchor.First),
            leftDock.Id,
            editorRight.Id);
        var bottomDock = Split(
            "bottom-dock",
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5d),
            bottomLeft.Id,
            bottomRight.Id);
        var root = Split(
            "root",
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.78d),
            mainArea.Id,
            bottomDock.Id);

        LayoutNode[] nodes =
        {
            root,
            mainArea,
            leftDock,
            editorRight,
            rightDock,
            bottomDock,
            toolsGroup,
            leftLower,
            docsGroup,
            propsGroup,
            rightLower,
            bottomLeft,
            bottomRight,
        };
        DockItem[] items =
        {
            tools,
            tree,
            canvas,
            props,
        };

        // The declarative collector derived these from the non-empty tool
        // groups; the programmatic definition supplies them explicitly.
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { tools.Id, tree.Id }),
            new ToolBarOrderState(LogicalRegion.InlineStartLower, Array.Empty<DockItemId>()),
            new ToolBarOrderState(LogicalRegion.InlineEndUpper, new[] { props.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndLower, Array.Empty<DockItemId>()),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineStart, Array.Empty<DockItemId>()),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineEnd, Array.Empty<DockItemId>()),
        };

        return new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceName),
            root.Id,
            nodes,
            items,
            toolBars,
            toolBarOrders);
    }

    private static GroupNode ToolGroup(
        string id,
        LogicalRegion logicalRegion,
        GroupVisibility visibility,
        params DockItemId[] itemIds)
    {
        // An empty group is only valid with a persistent retention policy and
        // no selected item (enforced by GroupNode's constructor).
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            visibility,
            itemIds,
            itemIds.Length == 0 ? null : itemIds[0],
            logicalRegion: logicalRegion);
    }

    private static GroupNode DocumentGroup(string id, params DockItemId[] itemIds)
    {
        // The declarative form defaulted to Scroll overflow and Adjacent
        // activation for the document group's editor state.
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            itemIds,
            itemIds[0],
            new EditorGroupState(
                EditorOverflowMode.Scroll,
                EditorActivationPolicy.Adjacent));
    }

    private static SplitNode Split(
        string id,
        SplitAxis axis,
        SplitBasis basis,
        LayoutNodeId firstId,
        LayoutNodeId secondId)
    {
        return new SplitNode(
            new LayoutNodeId(id),
            axis,
            basis,
            default,
            firstId,
            secondId);
    }

    private static DockItem Tool(string id, string title, string contentKind)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference(contentKind, id),
            DockItemCapabilityPresets.Tool,
            title,
            iconKey: null);
    }

    private static DockItem Document(string id, string title, string contentKind)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference(contentKind, id),
            DockItemCapabilityPresets.Document,
            title,
            iconKey: null,
            new DocumentState(isPinned: false, isPreview: false));
    }
}
