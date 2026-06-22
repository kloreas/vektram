using System;
using Sim.Items;
using Xunit;

namespace Sim.Tests.Items;

public class InventoryTests
{
    [Fact]
    public void Empty_HoldsNothing()
    {
        Assert.Equal(0, Inventory.Empty.StackCount);
        Assert.Equal(0, Inventory.Empty.CountOf("anything"));
        Assert.False(Inventory.Empty.Contains("anything"));
    }

    [Fact]
    public void Add_AccumulatesIntoOneStack()
    {
        Inventory inv = Inventory.Empty.Add("heal", 3).Add("heal", 2);

        Assert.Equal(5, inv.CountOf("heal"));
        Assert.Equal(1, inv.StackCount);
        Assert.True(inv.Contains("heal"));
    }

    [Fact]
    public void Add_DoesNotMutateReceiver()
    {
        Inventory original = Inventory.Empty.Add("heal", 1);

        original.Add("heal", 5);   // result discarded

        Assert.Equal(1, original.CountOf("heal"));
    }

    [Fact]
    public void Add_NonPositiveCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => Inventory.Empty.Add("heal", 0));
        Assert.Throws<ArgumentException>(() => Inventory.Empty.Add("heal", -1));
    }

    [Fact]
    public void Remove_PartialStack_LeavesRemainder()
    {
        Inventory inv = Inventory.Empty.Add("heal", 5).Remove("heal", 2);

        Assert.Equal(3, inv.CountOf("heal"));
    }

    [Fact]
    public void Remove_WholeStack_DropsEntry()
    {
        Inventory inv = Inventory.Empty.Add("heal", 3).Remove("heal", 3);

        Assert.Equal(0, inv.CountOf("heal"));
        Assert.Equal(0, inv.StackCount);
    }

    [Fact]
    public void Remove_MoreThanHeld_ClampsToZero_NeverNegative()
    {
        Inventory inv = Inventory.Empty.Add("heal", 2).Remove("heal", 10);

        Assert.Equal(0, inv.CountOf("heal"));
        Assert.Equal(0, inv.StackCount);
    }

    [Fact]
    public void Remove_AbsentItem_IsUnchanged()
    {
        Inventory start = Inventory.Empty.Add("heal", 2);

        Inventory after = start.Remove("missing", 1);

        Assert.Equal(start, after);
    }

    [Fact]
    public void Consume_Enough_SucceedsAndReduces()
    {
        Inventory start = Inventory.Empty.Add("heal", 2);

        ItemUseOutcome outcome = start.Consume("heal");

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.Inventory.CountOf("heal"));
    }

    [Fact]
    public void Consume_ExactToZero_DropsEntry()
    {
        Inventory start = Inventory.Empty.Add("heal", 1);

        ItemUseOutcome outcome = start.Consume("heal");

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.Inventory.StackCount);
    }

    [Fact]
    public void Consume_Missing_FailsAndLeavesUnchanged()
    {
        Inventory start = Inventory.Empty.Add("heal", 1);

        ItemUseOutcome outcome = start.Consume("other");

        Assert.False(outcome.Success);
        Assert.Equal(start, outcome.Inventory);
    }

    [Fact]
    public void Consume_Insufficient_FailsAndLeavesUnchanged()
    {
        Inventory start = Inventory.Empty.Add("heal", 1);

        ItemUseOutcome outcome = start.Consume("heal", 2);

        Assert.False(outcome.Success);
        Assert.Equal(start, outcome.Inventory);
    }

    [Fact]
    public void Entries_AreOrderedByIdOrdinal()
    {
        Inventory inv = Inventory.Empty.Add("zeta", 1).Add("alpha", 1).Add("mike", 1);

        var entries = inv.Entries;

        Assert.Equal(new[] { "alpha", "mike", "zeta" },
            new[] { entries[0].Id, entries[1].Id, entries[2].Id });
    }

    [Fact]
    public void Equality_SameContentsRegardlessOfOrder_AreEqual()
    {
        Inventory a = Inventory.Empty.Add("x", 2).Add("y", 3);
        Inventory b = Inventory.Empty.Add("y", 3).Add("x", 2);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCounts_AreNotEqual()
    {
        Inventory a = Inventory.Empty.Add("x", 2);
        Inventory b = Inventory.Empty.Add("x", 3);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Operations_AreDeterministic()
    {
        Inventory a = Inventory.Empty.Add("x", 5).Remove("x", 2).Add("y", 1).Consume("x").Inventory;
        Inventory b = Inventory.Empty.Add("x", 5).Remove("x", 2).Add("y", 1).Consume("x").Inventory;

        Assert.Equal(a, b);
    }
}
