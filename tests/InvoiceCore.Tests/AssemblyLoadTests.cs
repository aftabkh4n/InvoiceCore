using System.Reflection;
using Xunit;

namespace InvoiceCore.Tests;

public sealed class AssemblyLoadTests
{
    [Fact]
    public void InvoiceCore_assembly_loads()
    {
        var assembly = Assembly.Load("InvoiceCore");

        Assert.NotNull(assembly);
        Assert.Equal("InvoiceCore", assembly.GetName().Name);
    }
}
