using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Maia.Presentation.Collections;

namespace Maia.Presentation.Tests;

public sealed class CollectionSyncTests
{
    private sealed record Row(string Id, string Text);

    /// <summary>A row the screen keeps by reference, as Your plan keeps its rows and open card.</summary>
    private sealed class Item(string id)
    {
        public string Id { get; } = id;
    }

    [Theory]
    [InlineData("a b c", "a b c")]
    [InlineData("a b c", "a c")]
    [InlineData("a c", "a b c")]
    [InlineData("a b c", "c b a")]
    [InlineData("a b c", "d e")]
    [InlineData("", "a b")]
    [InlineData("a b", "")]
    [InlineData("a b c d", "b d a")]
    public void Ends_up_matching_the_new_data(string before, string after)
    {
        var target = new ObservableCollection<Row>(Rows(before));
        var source = Rows(after);

        CollectionSync.Apply(target, source, (x, y) => x.Id == y.Id);

        Assert.Equal(source, target);
    }

    [Fact]
    public void A_changed_row_is_replaced_and_nothing_else_moves()
    {
        var target = new ObservableCollection<Row>([new("a", "1"), new("b", "1"), new("c", "1")]);
        var changes = Record(target);

        CollectionSync.Apply(target, [new("a", "1"), new("b", "2"), new("c", "1")], (x, y) => x.Id == y.Id);

        var change = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Replace, change.Action);
        Assert.Equal(1, change.NewStartingIndex);
    }

    [Fact]
    public void Unchanged_data_raises_no_changes()
    {
        var target = new ObservableCollection<Row>(Rows("a b c"));
        var changes = Record(target);

        CollectionSync.Apply(target, Rows("a b c"), (x, y) => x.Id == y.Id);

        Assert.Empty(changes);
    }

    [Fact]
    public void A_row_that_moves_is_still_the_same_row_so_its_view_stays()
    {
        var (a, b, c) = (new Item("a"), new Item("b"), new Item("c"));
        var target = new ObservableCollection<Item>([a, b, c]);

        CollectionSync.Apply(target, [c, a, b], (x, y) => x.Id == y.Id);

        Assert.Same(c, target[0]);
        Assert.Same(a, target[1]);
        Assert.Same(b, target[2]);
        Assert.Equal(3, target.Count);
    }

    private static List<Row> Rows(string ids) =>
        ids.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id => new Row(id, id)).ToList();

    private static List<NotifyCollectionChangedEventArgs> Record(ObservableCollection<Row> collection)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, e) => changes.Add(e);
        return changes;
    }
}
