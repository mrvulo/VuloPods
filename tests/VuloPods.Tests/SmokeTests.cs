namespace VuloPods.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var asm = typeof(VuloPods.Core.Marker).Assembly;
        Assert.Equal("VuloPods.Core", asm.GetName().Name);
    }
}
