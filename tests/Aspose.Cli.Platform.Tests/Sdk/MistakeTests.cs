using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class MistakeTests
{
    [Fact]
    public void Of_SuggestsTheClosestNamesAndKeepsWhatExists()
    {
        var mistake = Mistake.Of("Salez", ["Summary", "Sales", "Q4"]);

        Assert.Equal("Salez", mistake.Requested);
        Assert.Equal(["Summary", "Sales", "Q4"], mistake.Available);
        Assert.Equal(["Sales"], mistake.Suggestions);
        Assert.Equal("Did you mean 'Sales'?", mistake.Question);
    }

    [Fact]
    public void Of_PutsTheMeantNameFirst()
    {
        var mistake = Mistake.Of("colour", ["color", "colors", "font"], meant: "font");

        Assert.Equal(["font", "color", "colors"], mistake.Suggestions);
        Assert.Equal("Did you mean 'font', 'color' or 'colors'?", mistake.Question);
    }

    [Fact]
    public void Of_PutsEveryMeantNameFirstInTheirOrder()
    {
        var mistake = Mistake.Of("query", ["inspect", "read", "search", "edit"], meant: ["read", "search"]);

        Assert.Equal(["read", "search"], mistake.Suggestions.Take(2));
        Assert.StartsWith("Did you mean 'read'", mistake.Question, StringComparison.Ordinal);
    }

    [Fact]
    public void Of_ComparesByKeyAndSuggestsTheNamesAsPublished()
    {
        var options = Mistake.Of("--vresion", ["--help", "--version"], keyOf: static name => name.TrimStart('-'));
        var commands = Mistake.Of("sattus", ["app status", "app stop"], keyOf: static path => path[(path.LastIndexOf(' ') + 1)..]);

        Assert.Equal(["--version"], options.Suggestions);
        Assert.Equal(["app status"], commands.Suggestions);
    }

    /// <summary>An edit that turns one word of an operation's name into another names another operation, however few letters it takes.</summary>
    [Theory]
    [InlineData("replace_text", "redact_text")]
    [InlineData("insert_chart", "insert_image")]
    [InlineData("hide_slide", "move_slide")]
    [InlineData("add_notes", "set_notes")]
    [InlineData("replace-text", "redact_text")]
    [InlineData("replace text", "redact_text")]
    public void Of_DoesNotSuggestAnOperationThatDiffersByAWholeWord(string requested, string other) =>
        Assert.Empty(Mistake.Of(requested, [other]).Suggestions);

    /// <summary>A typo stays within its word, an abbreviation is the word it shortens, names of different word counts are compared whole, and a document's own text with spaces keeps its edit distance.</summary>
    [Theory]
    [InlineData("set_titel", "set_title")]
    [InlineData("replace_txt", "replace_text")]
    [InlineData("set_propertys", "set_properties")]
    [InlineData("add_pagenumbers", "add_page_numbers")]
    [InlineData("Sale 2025", "Sale 2026")]
    [InlineData("Northh", "North")]
    [InlineData("insert_img", "insert_image")]
    [InlineData("insert_tbl", "insert_table")]
    [InlineData("del_sheet", "delete_sheet")]
    [InlineData("Title & Content", "Title and Content")]
    [InlineData("第五条 假期类别", "第五条 假期种类")]
    public void Of_StillSuggestsATypoInAWord(string requested, string meant) =>
        Assert.Equal([meant], Mistake.Of(requested, [meant]).Suggestions);

    [Fact]
    public void Of_WithoutACloseNameAsksNothing()
    {
        var mistake = Mistake.Of("zzz", ["alpha", "beta"]);

        Assert.Empty(mistake.Suggestions);
        Assert.Null(mistake.Question);
        Assert.Equal(string.Empty, mistake.Aside());
        Assert.Equal("Use one of the names in details.available.", mistake.Hint("Use one of the names in details.available."));
    }

    [Fact]
    public void Hint_AndAside_StateTheSuggestionsTheSameWay()
    {
        var mistake = Mistake.Of("naem", ["name", "title"]);

        Assert.Equal("Did you mean 'name'? Check the field.", mistake.Hint("Check the field."));
        Assert.Equal(" (did you mean 'name'?)", mistake.Aside());
        Assert.Equal(" (did you mean its unused key 'name'?)", mistake.Aside("its unused key"));
    }

    [Fact]
    public void WriteTo_WritesAvailableNamesUpToTheLimitTheirCountAndSuggestionsAsAnArray()
    {
        var mistake = Mistake.Of("gamma", ["alpha", "beta", "gamma2"]);
        var bounded = new JsonObject();
        var none = new JsonObject();
        var nothingClose = new JsonObject();

        mistake.WriteTo(bounded, maximumAvailable: 2);
        mistake.WriteTo(none, maximumAvailable: 0);
        Mistake.Of("zzz", ["alpha"]).WriteTo(nothingClose);

        Assert.Equal("""{"available":["alpha","beta"],"availableCount":3,"suggestions":["gamma2"]}""", bounded.ToJsonString());
        Assert.Equal("""{"suggestions":["gamma2"]}""", none.ToJsonString());
        Assert.Equal("""{"available":["alpha"]}""", nothingClose.ToJsonString());
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
