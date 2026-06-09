namespace QuotesApi.Models;
//Domain-Driven-Design
//Has BL into model itself 
public class Collection
{
    // Private parameterless constructor for EF Core
    private Collection() { }

    public Collection(string name, int ownerId)
    {
        ValidateName(name);
        Name = name;
        OwnerId = ownerId;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int OwnerId { get; private set; }

    // EF Core uses the _items backing field directly (configured in DbContext)
    private List<CollectionItem> _items = new();
    public IReadOnlyList<CollectionItem> Items => _items;

    public void AddItem(int quoteId, DateTimeOffset addedAt)
    {
        if (_items.Count >= 50)
            throw new InvalidOperationException("Collection cannot have more than 50 items.");

        if (_items.Any(i => i.QuoteId == quoteId))
            throw new InvalidOperationException("This quote is already in the collection.");

        _items.Add(new CollectionItem(quoteId, addedAt));
    }

    public void RemoveItem(int quoteId)
    {
        var item = _items.FirstOrDefault(i => i.QuoteId == quoteId);

        if (item is null)
            throw new InvalidOperationException("This quote is not in the collection.");

        _items.Remove(item);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 3)
            throw new ArgumentException("Collection name must be at least 3 characters.");

        if (name.Length > 80)
            throw new ArgumentException("Collection name cannot exceed 80 characters.");
    }
}
