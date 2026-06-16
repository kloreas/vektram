using Xunit;

namespace Sim.Tests;

public class SimVersionTests
{
    [Fact]
    public void Current_IsNotEmpty()
    {
        Assert.NotEmpty(SimVersion.Current);
    }
}
