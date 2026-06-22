using System;
using System.Collections.Generic;

namespace Sim.Items;

/// <summary>
/// Immutable, deterministic record of what a player holds (item id → count). All operations
/// are pure: they return a new <see cref="Inventory"/> and never mutate the receiver.
/// </summary>
/// <remarks>
/// Server-authoritative state: the server owns and mutates inventory via these operations
/// and sends snapshots; the client only displays. Zero-count stacks are never stored, counts
/// never go negative, and <see cref="Entries"/> is ordered by id (ordinal) for deterministic
/// enumeration. Two inventories are equal when they hold the same items in the same counts.
/// </remarks>
public sealed class Inventory : IEquatable<Inventory>
{
    private readonly Dictionary<string, int> _counts;

    private Inventory(Dictionary<string, int> counts) => _counts = counts;

    /// <summary>An inventory holding nothing.</summary>
    public static Inventory Empty { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>Number of distinct item stacks held.</summary>
    public int StackCount => _counts.Count;

    /// <summary>Count of <paramref name="itemId"/> held; 0 if none.</summary>
    public int CountOf(string itemId)
    {
        if (itemId is null) throw new ArgumentNullException(nameof(itemId));
        return _counts.TryGetValue(itemId, out int count) ? count : 0;
    }

    /// <summary>Whether at least one of <paramref name="itemId"/> is held.</summary>
    public bool Contains(string itemId) => CountOf(itemId) > 0;

    /// <summary>All held stacks as (id, count) pairs, ordered by id (ordinal).</summary>
    public IReadOnlyList<(string Id, int Count)> Entries
    {
        get
        {
            var list = new List<(string Id, int Count)>(_counts.Count);
            foreach (KeyValuePair<string, int> entry in _counts)
                list.Add((entry.Key, entry.Value));
            list.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }

    /// <summary>Returns a new inventory with <paramref name="count"/> more of <paramref name="itemId"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="count"/> is not positive.</exception>
    public Inventory Add(string itemId, int count)
    {
        if (itemId is null) throw new ArgumentNullException(nameof(itemId));
        if (count <= 0) throw new ArgumentException("Count to add must be > 0.", nameof(count));

        var next = new Dictionary<string, int>(_counts, StringComparer.Ordinal);
        next[itemId] = CountOf(itemId) + count;
        return new Inventory(next);
    }

    /// <summary>
    /// Returns a new inventory with up to <paramref name="count"/> of <paramref name="itemId"/>
    /// removed. Removing the whole stack drops the entry; removing an absent item is a no-op.
    /// Counts never go negative.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="count"/> is not positive.</exception>
    public Inventory Remove(string itemId, int count)
    {
        if (itemId is null) throw new ArgumentNullException(nameof(itemId));
        if (count <= 0) throw new ArgumentException("Count to remove must be > 0.", nameof(count));

        int have = CountOf(itemId);
        if (have == 0) return this;

        var next = new Dictionary<string, int>(_counts, StringComparer.Ordinal);
        int remaining = have - count;
        if (remaining > 0) next[itemId] = remaining;
        else next.Remove(itemId);
        return new Inventory(next);
    }

    /// <summary>
    /// Attempts to consume <paramref name="count"/> of <paramref name="itemId"/>. Succeeds only
    /// if at least that many are held; on failure the inventory is returned unchanged.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="count"/> is not positive.</exception>
    public ItemUseOutcome Consume(string itemId, int count = 1)
    {
        if (itemId is null) throw new ArgumentNullException(nameof(itemId));
        if (count <= 0) throw new ArgumentException("Count to consume must be > 0.", nameof(count));

        if (CountOf(itemId) < count) return new ItemUseOutcome(false, this);
        return new ItemUseOutcome(true, Remove(itemId, count));
    }

    /// <inheritdoc/>
    public bool Equals(Inventory? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_counts.Count != other._counts.Count) return false;

        foreach (KeyValuePair<string, int> entry in _counts)
            if (!other._counts.TryGetValue(entry.Key, out int otherCount) || otherCount != entry.Value)
                return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Inventory);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Order-independent combine so equal contents hash equally regardless of insertion order.
        int hash = 17;
        foreach (KeyValuePair<string, int> entry in _counts)
            hash += StringComparer.Ordinal.GetHashCode(entry.Key) ^ entry.Value;
        return hash;
    }
}
