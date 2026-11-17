using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class MistakeTests
{
    [Fact]
    public void Of_KeepsWhatExists()
    {
        var mistake = Mistake.Of("Salez", ["Summary", "Sales", "Q4"]);

        Assert.Equal("Salez", mistake.Requested);
        Assert.Equal(["Summary", "Sales", "Q4"], mistake.Available);
    }

    /// <summary>A usage error lists as many existing names as a not-found error, and no more.</summary>
    [Fact]
    public void WriteTo_ListsAtMostOneCapOfAvailableNamesForEveryError()
    {
        string[] names = [.. Enumerable.Range(1, CliErrors.MaximumAvailableNames + 10).Select(static number => $"name{number}")];
        var details = new JsonObject();

        Mistake.Of("zzz", names).WriteTo(details);
        CliException usage = CliErrors.Usage(["'zzz' is not a name."], Mistake.Of("zzz", names));
        CliException notFound = CliErrors.NotFound(ErrorCode.NotFound("SHEET_NOT_FOUND"), "sheet", "zzz", names);

        Assert.Equal(names[..CliErrors.MaximumAvailableNames], details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal(names.Length, details["availableCount"]!.GetValue<int>());
        Assert.Equal(CliErrors.MaximumAvailableNames, usage.Details!["available"]!.AsArray().Count);
        Assert.Equal(names.Length, usage.Details!["availableCount"]!.GetValue<int>());
        Assert.Equal(CliErrors.MaximumAvailableNames, notFound.Details!["available"]!.AsArray().Count);
    }
}
