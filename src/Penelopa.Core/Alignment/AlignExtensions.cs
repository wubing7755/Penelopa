namespace Penelopa.Core.Alignment;

/// <summary>
/// Aligns a set of <see cref="IAlignable"/> items against the union of their
/// bounding boxes, using the same alignment value (edge or center) as the
/// reference for every item.
/// </summary>
public static class AlignExtensions
{
    private const float Tolerance = 1e-6f;

    /// <summary>
    /// Aligns a set of alignable items to a common reference.
    /// </summary>
    /// <typeparam name="T">The alignable item type.</typeparam>
    /// <param name="items">The items to align.</param>
    /// <param name="type">The alignment direction.</param>
    /// <returns>True when an alignment was applied; false when the items were
    /// already aligned or there are fewer than two items.</returns>
    public static bool Align<T>(this IEnumerable<T> items, AlignType type) where T : IAlignable
    {
        var list = items.ToList();
        if (list.Count < 2)
        {
            return false;
        }

        // Capture the current state of every item.
        var boxes = list.Select(item => item.GetWorldBoundingBox()).ToList();
        var originalPositions = list.Select(item => item.GetWorldPosition()).ToList();

        // The union of all bounding boxes is the alignment reference.
        var unionBox = MergeBoxes(boxes);

        // Nothing to do when the items are already aligned.
        if (IsAlreadyAligned(boxes, type, unionBox))
        {
            return false;
        }

        // Translate each item by the offset to its target alignment value.
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var box = boxes[i];
            var originalPosition = originalPositions[i];

            var (dx, dy) = CalculateOffset(box, type, unionBox);

            item.SetWorldPosition(new Point(originalPosition.X + dx, originalPosition.Y + dy));
        }

        return true;
    }

    /// <summary>Merges bounding boxes into a single union box.</summary>
    private static Box MergeBoxes(IReadOnlyList<Box> boxes)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var box in boxes)
        {
            if (box.MinX < minX) minX = box.MinX;
            if (box.MinY < minY) minY = box.MinY;
            if (box.MaxX > maxX) maxX = box.MaxX;
            if (box.MaxY > maxY) maxY = box.MaxY;
        }

        return new Box(minX, minY, maxX, maxY);
    }

    /// <summary>Checks whether the boxes are already aligned by the given type.</summary>
    private static bool IsAlreadyAligned(IReadOnlyList<Box> boxes, AlignType type, Box referenceBox)
    {
        float referenceValue = GetAlignmentValue(referenceBox, type);

        return boxes.All(box => Math.Abs(GetAlignmentValue(box, type) - referenceValue) < Tolerance);
    }

    /// <summary>Computes the translation needed to align a box to the reference.</summary>
    private static (float dx, float dy) CalculateOffset(Box box, AlignType type, Box referenceBox)
    {
        return type switch
        {
            // Screen coordinates grow downward, so the visual top edge is MaxY
            // and the visual bottom edge is MinY (kept from the original demo).
            AlignType.Left => (referenceBox.MinX - box.MinX, 0),
            AlignType.HCenter => (referenceBox.CenterX - box.CenterX, 0),
            AlignType.Right => (referenceBox.MaxX - box.MaxX, 0),
            AlignType.Top => (0, referenceBox.MaxY - box.MaxY),
            AlignType.VCenter => (0, referenceBox.CenterY - box.CenterY),
            AlignType.Bottom => (0, referenceBox.MinY - box.MinY),
            _ => throw new ArgumentException("Invalid alignment type", nameof(type)),
        };
    }

    /// <summary>Gets the alignment value of a box for the given type.</summary>
    private static float GetAlignmentValue(Box box, AlignType type) => type switch
    {
        AlignType.Left => box.MinX,
        AlignType.HCenter => box.CenterX,
        AlignType.Right => box.MaxX,
        AlignType.Top => box.MaxY,
        AlignType.VCenter => box.CenterY,
        AlignType.Bottom => box.MinY,
        _ => throw new ArgumentException("Invalid alignment type", nameof(type)),
    };
}
