using System.Reflection;

namespace SupportDesk.ArchitectureTests;

/// <summary>
/// The dependency rule of the clean architecture - dependencies point inwards, towards the
/// domain - checked against the compiled assemblies, so breaking it fails the test run rather
/// than waiting for a code review.
/// </summary>
/// <remarks>
/// The compiler only records a reference to an assembly whose types are actually used, so
/// these tests catch real dependencies, not unused project references.
/// </remarks>
public class LayerDependencyTests
{
    private const string Domain = "SupportDesk.Domain";
    private const string Application = "SupportDesk.Application";
    private const string Infrastructure = "SupportDesk.Infrastructure";
    private const string Presentation = "SupportDesk.Presentation";

    [Fact]
    public void Domain_DependsOnNoOtherLayerAndNoFramework() =>
        AssertDoesNotReference(Domain, Application, Infrastructure, Presentation, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Application_DependsOnlyOnTheDomain() =>
        AssertDoesNotReference(Application, Infrastructure, Presentation, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_DoesNotDependOnPresentation() =>
        AssertDoesNotReference(Infrastructure, Presentation, "Microsoft.AspNetCore");

    private static void AssertDoesNotReference(string assemblyName, params string[] forbiddenPrefixes)
    {
        var offending = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offending);
    }
}
