namespace Domain;

public class Collection
{
    private readonly List<Guid> _items = new();

    public string Name { get; private set; }
    public IReadOnlyList<Guid> Items => _items;

    public Collection(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be empty.");

        if (name.Length > 80)
            throw new ArgumentException("Collection name cannot exceed 80 characters.");

        Name = name;
    }

    public void AddItem(Guid quoteId)
    {
        if (_items.Count >= 50)
            throw new InvalidOperationException("Collection cannot have more than 50 items.");

        if (_items.Contains(quoteId))
            throw new InvalidOperationException("Quote already exists in this collection.");

        _items.Add(quoteId);
    }

    public void RemoveItem(Guid quoteId)
    {
        if (!_items.Contains(quoteId))
            throw new InvalidOperationException("Quote does not exist in this collection.");

        _items.Remove(quoteId);
    }
}