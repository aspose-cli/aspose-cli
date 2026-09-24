using Xunit.v3;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The tests that scripts/test.ps1 leaves out of its Fast scope. Every other test is fast:
/// a unit, contract, architecture or short integration test.
/// </summary>
public enum TestCategory
{
    /// <summary>Runs the customer installer under Windows PowerShell against a package.</summary>
    Installer,

    /// <summary>Drives the App or a viewer page in the pinned Chromium.</summary>
    Browser,

    /// <summary>Takes several seconds by nature, such as full workflows or lock waits.</summary>
    Slow,
}

/// <summary>
/// Marks a test class or method with its <see cref="TestCategory"/>, reported as the xUnit
/// trait <c>Category</c> that <c>dotnet test --filter</c> selects on.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CategoryAttribute(TestCategory category) : Attribute, ITraitAttribute
{
    public const string TraitName = "Category";

    public TestCategory Category { get; } = category;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new(TraitName, Category.ToString())];
}
