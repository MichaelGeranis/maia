namespace Maia.Presentation.Collections;

/// <summary>
/// Brings a bound list in line with fresh data using the fewest inserts, removes and replaces, so a list
/// view keeps its scroll position and only redraws the rows that changed. Clearing and refilling would
/// reset it to the top.
/// </summary>
internal static class CollectionSync
{
    /// <param name="target">The bound collection to update.</param>
    /// <param name="source">What it should contain, in order.</param>
    /// <param name="sameItem">Whether two values describe the same row (for example the same check id).</param>
    /// <param name="update">Updates a matching row; by default it is replaced when it is no longer equal.</param>
    public static void Apply<T>(
        IList<T> target,
        IReadOnlyList<T> source,
        Func<T, T, bool> sameItem,
        Action<IList<T>, int, T>? update = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sameItem);
        update ??= ReplaceIfChanged;

        var i = 0;
        while (i < source.Count)
        {
            if (i < target.Count && sameItem(target[i], source[i]))
            {
                update(target, i, source[i]);
                i++;
            }
            else if (i < target.Count && !ContainsFrom(source, i, target[i], sameItem))
            {
                // Gone from the new data (or moved earlier, where it was already inserted).
                target.RemoveAt(i);
            }
            else
            {
                target.Insert(i, source[i]);
                i++;
            }
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static void ReplaceIfChanged<T>(IList<T> target, int index, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(target[index], value))
        {
            target[index] = value;
        }
    }

    private static bool ContainsFrom<T>(IReadOnlyList<T> source, int start, T value, Func<T, T, bool> sameItem)
    {
        for (var j = start; j < source.Count; j++)
        {
            if (sameItem(source[j], value))
            {
                return true;
            }
        }

        return false;
    }
}
