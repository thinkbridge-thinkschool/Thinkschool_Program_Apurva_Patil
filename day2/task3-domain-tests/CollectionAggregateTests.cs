using FluentAssertions;
using System;
using Task03.Domain.Collections;
using Xunit;

namespace Task03.Domain.Tests;

public class CollectionAggregateTests
{
    [Fact]
    public void Empty_name_throws()
    {
        Action act = () => new Collection("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_longer_than_80_throws()
    {
        var longName = new string('x', 81);
        Action act = () => new Collection(longName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adding_51st_item_throws()
    {
        var c = new Collection("my collection");
        for (int i = 1; i <= Collection.MaxItems; i++) c.AddItem(i);
        Action act = () => c.AddItem(999);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Duplicate_quote_id_throws()
    {
        var c = new Collection("s");
        c.AddItem(1);
        Action act = () => c.AddItem(1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Removing_nonexistent_item_throws()
    {
        var c = new Collection("s");
        Action act = () => c.RemoveItem(42);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Adding_then_removing_leaves_zero_items()
    {
        var c = new Collection("s");
        c.AddItem(7);
        c.RemoveItem(7);
        c.Items.Should().BeEmpty();
    }
}
