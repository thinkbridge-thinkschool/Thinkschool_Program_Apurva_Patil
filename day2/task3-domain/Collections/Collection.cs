using System;
using System.Collections.Generic;
using System.Linq;

namespace Task03.Domain.Collections;

public class Collection
{
    private readonly List<int> _items = new();
    public const int MaxItems = 50;

    public string Name { get; }

    public IReadOnlyList<int> Items => _items.AsReadOnly();

    public Collection(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty", nameof(name));
        if (name.Length > 80)
            throw new ArgumentException("Name must be at most 80 characters", nameof(name));

        Name = name;
    }

    public void AddItem(int quoteId)
    {
        if (_items.Count >= MaxItems)
            throw new InvalidOperationException("Collection has reached its maximum size");
        if (_items.Contains(quoteId))
            throw new InvalidOperationException("Duplicate quote id");

        _items.Add(quoteId);
    }

    public void RemoveItem(int quoteId)
    {
        if (!_items.Remove(quoteId))
            throw new InvalidOperationException("Item not found in collection");
    }
}
