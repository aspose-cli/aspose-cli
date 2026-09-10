using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Errors;

public sealed class ErrorCodesTests
{
    private static IReadOnlyList<ErrorCode> DeclaredCodes { get; } =
        typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(ErrorCode))
            .Select(static field => (ErrorCode)field.GetValue(null)!)
            .ToArray();

    [Fact]
    public void All_ContainsEveryDeclaredCode()
    {
        // Guards the hand-maintained list against forgetting a new code.
        Assert.Equal(DeclaredCodes.Count, ErrorCodes.All.Count);
        foreach (ErrorCode code in DeclaredCodes)
        {
            Assert.Contains(code, ErrorCodes.All);
        }
    }

    [Fact]
    public void Names_AreUnique()
    {
        var names = ErrorCodes.All.Select(static code => code.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Names_FollowScreamingSnakeCase()
    {
        foreach (ErrorCode code in ErrorCodes.All)
        {
            Assert.Matches(new Regex("^[A-Z][A-Z0-9_]*$"), code.Name);
        }
    }

    [Fact]
    public void EveryCode_MapsToANonSuccessExitCode()
    {
        foreach (ErrorCode code in ErrorCodes.All)
        {
            Assert.NotEqual(ExitCode.Success, code.ExitCode);
        }
    }
}
