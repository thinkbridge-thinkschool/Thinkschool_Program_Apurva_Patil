using Domain;
using FluentAssertions;

namespace Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void Creating_Collection_WithEmptyName_ShouldThrow()
    {
        Action act = () => new Collection("");

        act.Should()
           .Throw<ArgumentException>()
           .WithMessage("*empty*");
    }

    [Fact]
    public void Creating_Collection_WithNameOver80Chars_ShouldThrow()
    {
        var longName = new string('A', 81);

        Action act = () => new Collection(longName);

        act.Should()
           .Throw<ArgumentException>()
           .WithMessage("*80*");
    }

    [Fact]
    public void AddingItem_WhenCollectionHas50Items_ShouldThrow()
    {
        var collection = new Collection("My Quotes");

        for (int i = 0; i < 50; i++)
            collection.AddItem(Guid.NewGuid());

        Action act = () => collection.AddItem(Guid.NewGuid());

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*50*");
    }

    [Fact]
    public void AddingItem_WithDuplicateQuoteId_ShouldThrow()
    {
        var collection = new Collection("My Quotes");
        var sameQuoteId = Guid.NewGuid();

        collection.AddItem(sameQuoteId);

        Action act = () => collection.AddItem(sameQuoteId);

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*already exists*");
    }

    [Fact]
    public void RemovingItem_ThatDoesNotExist_ShouldThrow()
    {
        var collection = new Collection("My Quotes");
        var randomId = Guid.NewGuid();

        Action act = () => collection.RemoveItem(randomId);

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*does not exist*");
    }

    [Fact]
    public void AddingItem_ThenRemovingIt_ShouldLeaveZeroItems()
    {
        var collection = new Collection("My Quotes");
        var quoteId = Guid.NewGuid();

        collection.AddItem(quoteId);
        collection.RemoveItem(quoteId);

        collection.Items.Should().BeEmpty();
    }
}