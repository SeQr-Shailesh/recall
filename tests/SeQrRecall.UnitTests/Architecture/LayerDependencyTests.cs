using System.Reflection;
using SeQrRecall.Domain.Entities;
using Xunit;

namespace SeQrRecall.UnitTests.Architecture;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_does_not_reference_other_SeQrRecall_assemblies()
    {
        IReadOnlyList<string> references = SeQrRecallReferences(typeof(User).Assembly);

        Assert.Empty(references);
    }

    [Fact]
    public void Application_references_only_Domain_among_SeQrRecall_assemblies()
    {
        IReadOnlyList<string> references = SeQrRecallReferences(typeof(Application.DependencyInjection).Assembly);

        Assert.Equal(["SeQrRecall.Domain"], references);
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Api()
    {
        IReadOnlyList<string> references = SeQrRecallReferences(typeof(Application.DependencyInjection).Assembly);

        Assert.DoesNotContain("SeQrRecall.Infrastructure", references);
        Assert.DoesNotContain("SeQrRecall.Api", references);
    }

    [Fact]
    public void Infrastructure_does_not_reference_Api()
    {
        IReadOnlyList<string> references = SeQrRecallReferences(typeof(SeQrRecall.Infrastructure.DependencyInjection).Assembly);

        Assert.DoesNotContain("SeQrRecall.Api", references);
        Assert.Contains("SeQrRecall.Application", references);
    }

    [Fact]
    public void Domain_does_not_reference_persistence_or_web_frameworks()
    {
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Serilog",
            "Swashbuckle"
        ];

        string[] referencedNames = typeof(User).Assembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .ToArray();

        foreach (string fragment in forbidden)
        {
            Assert.DoesNotContain(referencedNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IReadOnlyList<string> SeQrRecallReferences(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies()
            .Select(static name => name.Name)
            .Where(static name => name is not null && name.StartsWith("SeQrRecall.", StringComparison.Ordinal))
            .Cast<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
